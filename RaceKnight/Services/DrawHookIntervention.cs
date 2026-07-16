using System;
using System.Collections.Generic;
using System.Linq;
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
    Native,   // 走原生可见性切换（不装 Penumbra 也零配置，无需任何签名）
    None,     // 都无法重绘（仅字节改写保活，外观可能不刷新）
}

/// <summary>
/// 单一后端，<b>不依赖任何外部插件</b>：
///  - <b>Hide</b>   ：改写命中 actor 的 <c>GameObject.RenderFlags</c> 的 <see cref="CSVisibility.Model"/> 位。
///            置位 = 隐藏/拆掉模型，清位 = 显示/重建模型。无需绘制钩子、无需任何版本签名、无需配置其它插件。
///  - <Replace>：改写 actor 的 Customize 字节为替换目标种族的<b>合法</b>外观，并触发重绘。
///            改写算法（含 Tribe 公式重算、Face/Model/Hair 合法化、RaceHairs 表）取自可运行插件 OopsAllRace，
///            见 <see cref="RaceRemap"/>。重绘优先用 Penumbra IPC（若用户已装 Penumbra，零配置即可）；
///            否则走<b>原生可见性切换</b>（同样零配置，无需签名）。
///
/// ⚠️ 重绘机制说明（已对照 Penumbra 1.6.1.10 源码确认，<b>不需要任何签名</b>）：
///    原生重绘 = 一帧把目标 actor 的 <c>RenderFlags.Model</c> 位置位（让游戏拆掉并暂停渲染该模型），
///    下一帧清位（游戏据此重建 DrawObject 并重新渲染，外观随之刷新）。这是 Penumbra 内部 RedrawService
///    实际使用的同一手法，版本无关、稳定，且不会因为“签名随版本失效”而降级。
///    Hide 已不再依赖任何签名。
/// </summary>
public sealed class DrawHookIntervention : IRaceIntervention, IDisposable
{
    private readonly IFramework framework;
    private readonly Configuration config;

    // Hide 用：记录每个被隐藏 actor 的原始 RenderFlags，用于失配时还原
    private readonly Dictionary<ulong, CSVisibility> hidden = new();

    // Replace 保活：objectId -> (指令, 原始 Customize 快照)
    private readonly Dictionary<ulong, (MatchSpec spec, byte[] orig)> replaced = new();
    // Penumbra 重绘 IPC（Action<int>，按 ObjectIndex 重绘模型）。装了 Penumbra 才会连上。
    private ICallGateSubscriber<int, object>? penumbraRedraw;

    // 原生重绘队列：分两帧完成“置位→清位”的切换。
    // hideQueue 中的 actor 本帧置位（拆模型），随后转入 showQueue；
    // showQueue 中的 actor 下一帧清位（重建/显示）。两帧分离才能保证游戏真正重建模型。
    private readonly Queue<ulong> nativeHideQueue = new();
    private readonly Queue<ulong> nativeShowQueue = new();

    // 延后重绘队列：race 替换这类“整模型重建”在首帧重绘后，游戏内部刷新往往还差一口气，
    // 故在若干帧后再补一次原生重绘（参考 Glamourer PenumbraAutoRedraw 的 WaitFrames=5 思路）。
    // 每条只触发一次，避免无限循环。
    private readonly Queue<(ulong Id, int Frames)> redrawLater = new();

    /// <summary>当前实际生效的重绘方式（两套方案并存时用于 UI 展示）。</summary>
    public RedrawMode ActiveRedraw { get; private set; } = RedrawMode.Unknown;

    public DrawHookIntervention(IFramework framework, Configuration config)
    {
        this.framework = framework;
        this.config = config;
    }

    public void Enable()
    {
        // 检测 Penumbra 是否可用：尝试其稳定的 ApiVersion IPC（Func<int>）。
        // 若 Penumbra 未加载，InvokeFunc 会抛，捕获后降级到原生可见性切换。
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
            Plugin.Log.Information("[RaceKnight] 未检测到 Penumbra；Replace 将改用原生可见性切换重绘（零配置、无需签名）。");
        }

        framework.Update += OnUpdate;
    }

    public void Disable()
    {
        framework.Update -= OnUpdate;
        penumbraRedraw = null;

        foreach (var kv in hidden.ToList())
            RestoreVisibility(kv.Key, kv.Value);
        hidden.Clear();

        foreach (var kv in replaced.ToList())
            Restore(kv.Key, kv.Value.orig);
        replaced.Clear();

        nativeHideQueue.Clear();
        nativeShowQueue.Clear();
        redrawLater.Clear();
    }

    private unsafe void OnUpdate(IFramework _)
    {
        // Hide：每帧保活（置位 Model 位，保持模型不渲染）
        foreach (var id in hidden.Keys)
        {
            if (Find(id) is { } obj)
                SetModelHidden(obj.Address, true);
        }

        // Replace：每帧保活字节，仅在游戏把字节覆盖回去时重绘。
        foreach (var kv in replaced)
        {
            if (Find(kv.Key) is not ICharacter c)
                continue;
            var changed = ApplyReplaceBytes(c, kv.Value.spec, kv.Value.orig);
            if (changed)
                Redraw(kv.Key);
        }

        // 延后重绘：倒计时归零时补一次原生重绘（race 替换首帧后的二次刷新）。
        var delayedCount = redrawLater.Count;
        for (var i = 0; i < delayedCount; i++)
        {
            var (id, f) = redrawLater.Dequeue();
            if (f <= 1)
                nativeHideQueue.Enqueue(id); // 触发一次完整的“置位→清位”重绘
            else
                redrawLater.Enqueue((id, f - 1));
        }

        // 原生重绘：先处理上一帧入队的“清位”（重建/显示），再处理本帧的“置位”（拆模型）。
        // 两帧分离是重绘生效的关键。
        while (nativeShowQueue.Count > 0)
        {
            var id = nativeShowQueue.Dequeue();
            if (Find(id) is { } showObj)
                SetModelHidden(showObj.Address, false);
        }

        while (nativeHideQueue.Count > 0)
        {
            var id = nativeHideQueue.Dequeue();
            if (Find(id) is { } hideObj)
                SetModelHidden(hideObj.Address, true);
            nativeShowQueue.Enqueue(id);
        }
    }

    public void OnActorMatched(IGameObject actor, MatchSpec spec)
    {
        if (spec.Action == ActionKind.Hide)
        {
            if (!hidden.ContainsKey(actor.GameObjectId))
            {
                hidden[actor.GameObjectId] = GetRenderFlags(actor.Address);
                SetModelHidden(actor.Address, true);
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
            ScheduleRedraw(actor.GameObjectId, 5); // race 替换首帧后补一次，确保整模型重建生效
        }
    }

    public void OnActorUnmatched(IGameObject actor)
    {
        RemoveQueuedRedraws(actor.GameObjectId);

        if (hidden.Remove(actor.GameObjectId, out var orig))
            RestoreVisibility(actor.GameObjectId, orig);

        if (replaced.Remove(actor.GameObjectId, out var entry))
            Restore(actor.GameObjectId, entry.orig);
    }

    public void OnActorGone(ulong gameObjectId)
    {
        // 对象已不存在，原始字节/可见性也不再有恢复目标。只清理状态，防止 ID 复用污染新对象。
        hidden.Remove(gameObjectId);
        replaced.Remove(gameObjectId);
        RemoveQueuedRedraws(gameObjectId);
    }

    // ===== Hide 实现（渲染标志位）=====
    // RenderFlags.Model 位（值 2）：置位 = 隐藏/拆模型，清位 = 显示/重建。
    // 该语义与 Penumbra RedrawService.WriteInvisible / WriteVisible 完全一致（已对照其 1.6.1.10 反编译确认）。

    private unsafe CSVisibility GetRenderFlags(nint addr)
    {
        if (addr == IntPtr.Zero)
            return CSVisibility.None;
        var go = (CSGameObject*)addr;
        return go->RenderFlags;
    }

    private unsafe void SetModelHidden(nint addr, bool hide)
    {
        if (addr == IntPtr.Zero)
            return;
        var go = (CSGameObject*)addr;
        var flags = (ulong)go->RenderFlags;
        if (hide)
            flags |= (ulong)CSVisibility.Model;       // 置位 = 隐藏/拆模型
        else
            flags &= ~(ulong)CSVisibility.Model;      // 清位 = 显示/重建
        go->RenderFlags = (CSVisibility)flags;
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
        ScheduleRedraw(id, 5);
    }

    /// <summary>安排一次延后重绘（仅触发一次），用于 race 替换/还原后的二次刷新。</summary>
    private void ScheduleRedraw(ulong id, int frames) => redrawLater.Enqueue((id, frames));

    private void RemoveQueuedRedraws(ulong id)
    {
        FilterQueue(nativeHideQueue, item => item != id);
        FilterQueue(nativeShowQueue, item => item != id);
        FilterQueue(redrawLater, item => item.Id != id);
    }

    private static void FilterQueue<T>(Queue<T> queue, Func<T, bool> keep)
    {
        var count = queue.Count;
        for (var i = 0; i < count; i++)
        {
            var item = queue.Dequeue();
            if (keep(item))
                queue.Enqueue(item);
        }
    }

    private void Redraw(ulong id)
    {
        var obj = Find(id);
        if (obj == null || obj.Address == IntPtr.Zero)
            return;
        var index = obj.ObjectIndex; // Penumbra 按“对象索引”重绘，不是 GameObjectId

        // 优先 Penumbra IPC（零配置，若用户已装且未在设置里关闭）
        if (config.PreferPenumbraRedraw && penumbraRedraw != null)
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

        // 原生重绘：两帧切换 RenderFlags.Model 位，无需任何签名。
        nativeHideQueue.Enqueue(id);
        ActiveRedraw = RedrawMode.Native;
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

    public void Dispose() => Disable();
}
