using Dalamud.Configuration;
using System.Collections.Generic;
using RaceFilter.Model;

namespace RaceFilter;

public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>按种族自动匹配的规则。</summary>
    public List<RaceRule> Rules { get; set; } = new();

    /// <summary>手动指定的目标角色（按名字匹配）。</summary>
    public List<ManualTarget> ManualTargets { get; set; } = new();

    /// <summary>
    /// 若用户已安装 Penumbra，Replace 动作会用其 IPC 触发重绘（零配置可用，效果更稳）；
    /// 关闭或没装则走原生重载（需在下方填写当前游戏版本的“模型重载函数”签名）。
    /// </summary>
    public bool PreferPenumbraRedraw { get; set; } = true;

    /// <summary>
    /// 是否启用“原生重载”作为 Penumbra 不可用时的 Replace 重绘后端。
    /// 关闭后，未装 Penumbra 时 Replace 仅改写字节、外观可能不刷新（不会崩溃）。
    /// </summary>
    public bool EnableNativeRedraw { get; set; } = true;

    /// <summary>
    /// “模型重载函数”的字节特征码（SigScanner 格式，例如 <c>E8 ?? ?? ?? ?? 48 8B D3 ...</c>）。
    /// 用于不装 Penumbra 时直接调用游戏内部函数刷新角色外观。
    /// 该签名<b>随游戏版本/客户端变化</b>，留空或填错则原生重绘不生效（自动降级，安全）。
    /// 获取方式：从同样需要重绘的开源插件（如 Penumbra / Glamourer 当前源码）复制其对应版本的 redraw 签名，
    /// 或自行在 IDA/Ghidra 中对 ffxiv_dx11.exe 定位“角色模型重载”函数生成特征码。
    /// 粘入此处并保存即可生效，无需重编译插件。
    /// </summary>
    public string RedrawSignature { get; set; } = string.Empty;

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
