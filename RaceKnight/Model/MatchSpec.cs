namespace RaceFilter.Model;

/// <summary>
/// 一次匹配最终要执行的指令：动作 + 替换参数。
/// 由 RaceRule（按种族）或 ManualTarget（按名字）统一产出，供 Intervention 消费。
/// </summary>
public readonly struct MatchSpec
{
    public readonly ActionKind Action;
    public readonly RaceId? ReplacementRace;   // 仅 Replace 用
    public readonly Gender? ReplacementGender; // null = 保持原性别
    public readonly byte? ReplacementTribe;     // null = 保持原部族

    public MatchSpec(ActionKind action, RaceId? replacementRace = null,
                     Gender? replacementGender = null, byte? replacementTribe = null)
    {
        Action = action;
        ReplacementRace = replacementRace;
        ReplacementGender = replacementGender;
        ReplacementTribe = replacementTribe;
    }
}
