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
    /// 若用户已安装 Penumbra，Replace 动作优先用其 IPC 触发重绘（零配置、效果更稳）；
    /// 关闭或没装则自动走插件自带的<b>原生可见性切换</b>重绘（同样零配置、无需任何签名）。
    /// </summary>
    public bool PreferPenumbraRedraw { get; set; } = true;

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
