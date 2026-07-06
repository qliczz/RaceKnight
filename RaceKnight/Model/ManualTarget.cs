using System;

namespace RaceFilter.Model;

/// <summary>
/// 手动指定的目标角色：按显示名匹配，对其施加动作。
/// 完全不依赖任何外部插件。
/// 注意：玩家显示名为 "FirstName LastName"（大小写不敏感），NPC 为角色名。
///       跨服同名无法区分，个人自用足够。
/// </summary>
[Serializable]
public class ManualTarget
{
    public bool Enabled { get; set; } = true;

    /// <summary>显示名（玩家为 "名 姓"，NPC 为角色名）。匹配时大小写不敏感、去首尾空格。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>作用范围。</summary>
    public TargetScope Scope { get; set; } = TargetScope.PlayersAndNpcs;

    /// <summary>命中后执行的动作。</summary>
    public ActionKind Action { get; set; } = ActionKind.Hide;

    /// <summary>替换目标种族（仅 Action=Replace 时有效）。</summary>
    public RaceId ReplacementRace { get; set; } = RaceId.Elezen;

    /// <summary>替换性别（null=保持原性别）。</summary>
    public Gender? ReplacementGender { get; set; } = null;

    /// <summary>替换部族（null=按公式重算，字节 1-16）。</summary>
    public byte? ReplacementTribe { get; set; } = null;
}
