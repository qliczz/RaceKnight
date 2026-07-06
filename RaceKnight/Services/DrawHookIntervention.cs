using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using RaceFilter.Model;

// 避免与 Dalamud 托管 GameObject 重名
using CSGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using CSVisibility = FFXIVClientStructs.FFXIV.Client.Game.Object.VisibilityFlags;

namespace RaceFilter.Services;

/// <summary>Replace 重绘当前实际使用的后端（用于 UI 展示两套方案并存状态）。</summary>
public enum RedrawMode
{
    Unknown,  // 尚未确定
    Penumbra, // 走 Penumbra IPC（已装 Penumbra，零配置）
    Native,   // 走原生重载函数（未装 Penumbra，需在设置填写 RedrawSignature）
    None,     // 都无法重绘（仅字节改写保活，外观可能不刷新）
}

/// <summary>
/// 单一后端，<b>不依赖任何外部插件</b>：
///  - <b>Hide</b>   ：每帧把命中 actor 的 <c>GameObject.RenderFlags</c> 的 <see cref="CSVisibility.Model"/> 位清掉，使其模型不渲染。
///             无需绘制钩子、无需任何版本签名、无需配置其它插件，比“钩绘制函数”稳定得多。
///  - <b>Replace</b>：改写 actor 的 Customize 字节为替换目标种族的<b>合法</b>外观，并触发重绘。
///             改写算法（含 Tribe 公式重算、Face/Model/Hair 合法化、RaceHairs 表）取自可运行插件 OopsAllRace，
///             见 <see cref="RaceRemap"/>。重绘优先用 Penumbra IPC（若用户已装 Penumbra，零配置即可）；
///             否则走<b>原生重载函数</b>（见 <see cref="Configuration.RedrawSignature"/>，在插件设置里粘贴签名后无需 Penumbra 也能刷新外观）。
///
/// ⚠️ 版本相关：原生 Replace 重载签名 <see cref="Configuration.RedrawSignature"/> 需随游戏版本/客户端更新确认。
///    签名留空或填错时 Replace（未装 Penumbra）会降级为“仅字节改写、外观可能不刷新”，但不会导致崩溃。
///    Hide 已不再依赖任何签名。
/// </summary>
public sealed class DrawHookIntervention : IRaceIntervention, IDisposable
{
    private readonly IFramework framework;
    private readonly ISigScanner sigScanner;
    private readonly Configuration config;

    // Hide 用：记录每个被隐藏 actor 的原始 RenderFlags，用于失配时还原
    private readonly Dictionary<ulong, CSVisibility> hidden = new();

    // Replace 保活：objectId -> (指令, 原始 Customize 快照)
    private readonly Dictionary<ulong, (MatchSpec spec, byte[] orig)> replaced = new();
    // Penumbra 重绘 IPC（Action<int>，按 ObjectIndex 重绘模型）。装了 Penumbra 才会连上。
    private ICallGateSubscriber<int, object>? penumbraRedraw;
    private RedrawDelegate? redrawFn;
    private int frame;

    /// <summary>当前实际生效的重绘方式（两套方案并存时用于 UI 展示）。</summary>
    public RedrawMode ActiveRedraw { get; private set; } = RedrawMode.Unknown;

    public DrawHookIntervention(IFramework framework, ISigScanner sigScanner, Configuration config)
    {
        this.framework = framework;
        this.sigScanner = sigScanner;
        this.config = config;
    }

    public void Enable()
    {
        // 检测 Penumbra 是否可用：尝试其稳定的 ApiVersion IPC（Func<int>）。
        // 若 Penumbra 未加载，InvokeFunc 会抛，捕获后降级到原生重载。
        var penumbraAvailable = false;
        try
        {
            var ver = Plugin.PluginInterface.GetIpcSubscriber<int>("Penumbra.ApiVersion");
            ver.InvokeFunc();
            penumbraAvailable = true;
        }
        catch
        {
            penumbraAvailable = false;
        }

        if (penumbraAvailable)
        {
            // RedrawObject.V1 = Action<int>，接收“对象索引(ObjectIndex)”（与 Brio 等一致），
            // 而非 GameObjectId。Penumbra 长期保留 V1 以兼容旧插件。
            try
            {
                penumbraRedraw = Plugin.PluginInterface.GetIpcSubscriber<int, object>("Penumbra.RedrawObject.V1");
                Plugin.Log.Information("[RaceKnight] 已连接 Penumbra 重绘 IPC：Replace 将用 Penumbra 重绘（无需任何签名）。");
            }
            catch
            {
                penumbraRedraw = null;
            }
        }
        else
        {
            penumbraRedraw = null;
            Plugin.Log.Warning("[RaceKnight] 未检测到 Penumbra；Replace 将改用原生重载函数（需在设置里填写当前版本的 RedrawSignature）。");
        }

        // 原生重载函数（不装 Penumbra 时 Replace 刷新用）：从配置读取签名并尝试定位
        RefreshNativeRedraw();

        framework.Update += OnUpdate;
    }

    /// <summary>
    /// 依据 <see cref="Configuration.RedrawSignature"/> 重新定位“角色模型重载”函数。
    /// 在插件启用时、以及用户在设置界面修改并保存签名后调用。
    /// 签名留空 / 填错 / 关闭原生重绘时，<c>redrawFn</c> 置空，Replace 在未装 Penumbra 时自动降级（不崩溃）。
    /// </summary>
    public void RefreshNativeRedraw()
    {
        redrawFn = null;

        if (!config.EnableNativeRedraw)
        {
            Plugin.Log.Information("[RaceKnight] 原生重绘已关闭（EnableNativeRedraw=false）；未装 Penumbra 时 Replace 仅改写字节。");
            return;
        }

        var sig = (config.RedrawSignature ?? "").Trim();
        if (string.IsNullOrWhiteSpace(sig))
        {
            Plugin.Log.Warning("[RaceKnight] 未填写 RedrawSignature；未装 Penumbra 时 Replace 外观可能不刷新——请在设置里粘贴当前游戏版本的“模型重载”函数签名。");
            return;
        }

        if (sigScanner.TryScanText(sig, out var relAddr))
        {
            redrawFn = Marshal.GetDelegateForFunctionPointer<RedrawDelegate>(relAddr);
            Plugin.Log.Information("[RaceKnight] 角色重载函数已定位（Replace 不装 Penumbra 亦可刷新外观）。");
        }
        else
        {
            Plugin.Log.Warning("[RaceKnight] RedrawSignature 未能在游戏中扫描到匹配函数（可能版本/客户端不符）；未装 Penumbra 时 Replace 外观可能不刷新。请核对签名。");
        }
    }

    public void Disable()
    {
        framework.Update -= OnUpdate;
        redrawFn = null;

        foreach (var kv in hidden.ToList())
            RestoreVisibility(kv.Key, kv.Value);
        hidden.Clear();

        foreach (var kv in replaced.ToList())
            Restore(kv.Key, kv.Value.orig);
        replaced.Clear();
    }

    private unsafe void OnUpdate(IFramework _)
    {
        frame++;
        // Hide：每帧保活（清除 Model 位，防止游戏重新置位）
        foreach (var id in hidden.Keys)
        {
            if (Find(id) is { } obj)
                SetHidden(obj.Address, true);
        }

        // Replace：每帧保活字节，每 30 帧触发一次重绘以刷新外观
        foreach (var kv in replaced)
        {
            if (Find(kv.Key) is not ICharacter c)
                continue;
            var changed = ApplyReplaceBytes(c, kv.Value.spec, kv.Value.orig);
            if (changed || frame % 30 == 0)
                Redraw(kv.Key);
        }
    }

    public void OnActorMatched(IGameObject actor, MatchSpec spec)
    {
        if (spec.Action == ActionKind.Hide)
        {
            if (!hidden.ContainsKey(actor.GameObjectId))
            {
                hidden[actor.GameObjectId] = GetRenderFlags(actor.Address);
                SetHidden(actor.Address, true);
            }
        }
        else // Replace
        {
            if (actor is not ICharacter c)
                return;
            var orig = CaptureOriginal(c);
            replaced[actor.GameObjectId] = (spec, orig);
            ApplyReplaceBytes(c, spec, orig);
            Redraw(actor.GameObjectId);
        }
    }

    public void OnActorUnmatched(IGameObject actor)
    {
        if (hidden.Remove(actor.GameObjectId, out var orig))
            RestoreVisibility(actor.GameObjectId, orig);

        if (replaced.Remove(actor.GameObjectId, out var entry))
            Restore(actor.GameObjectId, entry.orig);
    }

    // ===== Hide 实现（渲染标志位）=====

    private unsafe CSVisibility GetRenderFlags(nint addr)
    {
        if (addr == IntPtr.Zero)
            return CSVisibility.None;
        var go = (CSGameObject*)addr;
        return go->RenderFlags;
    }

    private unsafe void SetHidden(nint addr, bool hide)
    {
        if (addr == IntPtr.Zero)
            return;
        var go = (CSGameObject*)addr;
        if (hide)
        {
            // 清掉 Model 位 => 该 actor 模型不渲染（Hide 生效）
            // 用 ulong 中转以避免枚举位运算的结果类型歧义，确保稳定编译
            var flags = (ulong)go->RenderFlags;
            flags &= ~(ulong)CSVisibility.Model;
            go->RenderFlags = (CSVisibility)flags;

            // 若还想连名称牌一起藏，可同时清掉 Nameplate 位：
            //   flags = (ulong)go->RenderFlags;
            //   flags &= ~(ulong)CSVisibility.Nameplate;
            //   go->RenderFlags = (CSVisibility)flags;
        }
    }

    private unsafe void RestoreVisibility(ulong id, CSVisibility orig)
    {
        var obj = Find(id);
        if (obj == null || obj.Address == IntPtr.Zero)
            return;
        var go = (CSGameObject*)obj.Address;
        go->RenderFlags = orig;
    }

    // ===== Replace 实现（改字节 + 重绘）=====

    private static byte[] CaptureOriginal(ICharacter c) => c.Customize.ToArray();

    private static bool ApplyReplaceBytes(ICharacter c, MatchSpec spec, byte[] orig)
    {
        var cz = c.Customize;
        var before = cz.ToArray();

        // 用从 OopsAllRace 学到的正确算法改写（含 Tribe/Face/Model/Hair 合法化）
        var targetRace = spec.ReplacementRace ?? (RaceId)orig[(int)CustomizeIndex.Race];
        RaceRemap.Apply(cz, targetRace, spec.ReplacementGender, spec.ReplacementTribe);

        // 仅当确有字节变化时返回 true（保活时避免无谓重绘）
        for (var i = 0; i < cz.Length; i++)
            if (cz[i] != before[i])
                return true;
        return false;
    }

    private void Restore(ulong id, byte[] orig)
    {
        if (Find(id) is ICharacter c)
        {
            var cz = c.Customize;
            for (var i = 0; i < orig.Length && i < cz.Length; i++)
                cz[i] = orig[i];
        }

        Redraw(id);
    }

    private unsafe void Redraw(ulong id)
    {
        var obj = Find(id);
        if (obj == null || obj.Address == IntPtr.Zero)
            return;
        var index = obj.ObjectIndex; // Penumbra 按“对象索引”重绘，不是 GameObjectId

        // 优先 Penumbra IPC（零配置，若用户已装）
        if (penumbraRedraw != null)
        {
            try
            {
                penumbraRedraw.InvokeAction(index);
                ActiveRedraw = RedrawMode.Penumbra;
                return;
            }
            catch (Exception e)
            {
                Plugin.Log.Debug($"[RaceKnight] Penumbra 重绘失败，降级原生：{e.Message}");
                penumbraRedraw = null; // 连不上就不再尝试 Penumbra
            }
        }

        // 否则走原生重载函数（需在设置填写 RedrawSignature）
        var go = (CSGameObject*)obj.Address;
        var draw = go->DrawObject; // CharacterBase* / Human*
        if (draw != null && redrawFn != null)
        {
            try
            {
                redrawFn((IntPtr)draw);
                ActiveRedraw = RedrawMode.Native;
            }
            catch { }
        }
        else
        {
            ActiveRedraw = RedrawMode.None;
            Plugin.Log.Debug($"[RaceKnight] Replace 重绘：redrawFn 未就绪，actor {id} 外观可能不刷新（请在设置填写 RedrawSignature 或安装 Penumbra）。");
        }
    }

    private IGameObject? Find(ulong id)
    {
        var ot = Plugin.ObjectTable;
        for (var i = 0; i < ot.Length; i++)
        {
            var o = ot[i];
            if (o != null && o.GameObjectId == id)
                return o;
        }

        return null;
    }

    private delegate void RedrawDelegate(IntPtr self);

    public void Dispose() => Disable();
}
