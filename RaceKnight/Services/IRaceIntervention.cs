using Dalamud.Game.ClientState.Objects.Types;
using RaceFilter.Model;

namespace RaceFilter.Services;

/// <summary>
/// 干预适配器接口：把“匹配到的 actor + 指令(MatchSpec)”变成实际视觉效果。
/// 当前唯一实现 DrawHookIntervention —— 不依赖任何外部插件。
/// </summary>
public interface IRaceIntervention
{
    void Enable();
    void Disable();
    void OnActorMatched(IGameObject actor, MatchSpec spec);
    void OnActorUnmatched(IGameObject actor);

    /// <summary>当前实际生效的 Replace 重绘后端（Penumbra / 原生 / 无）。</summary>
    RedrawMode ActiveRedraw { get; }

    /// <summary>依据最新配置重新定位原生“模型重载”函数（用户在设置里修改 Redraw 签名后调用）。</summary>
    void RefreshNativeRedraw();
}
