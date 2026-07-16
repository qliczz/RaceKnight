namespace RaceFilter.Model;

/// <summary>
/// 一次匹配最终要执行的指令：动作 + 替换参数。
/// 由 RaceRule（按种族）或 ManualTarget（按名字）统一产出，供 Intervention 消费。
/// </summary>
public readonly record struct MatchSpec
{
    public ActionKind Action { get; }
    public RaceId? ReplacementRace { get; }   // 仅 Replace 用
    public Gender? ReplacementGender { get; } // null = 保持原性别
    public byte? ReplacementTribe { get; }    // null = 保持原部族

    public MatchSpec(ActionKind action, RaceId? replacementRace = null,
                     Gender? replacementGender = null, byte? replacementTribe = null)
    {
        Action = action;
        ReplacementRace = replacementRace;
        ReplacementGender = replacementGender;
        ReplacementTribe = replacementTribe;
    }
}
