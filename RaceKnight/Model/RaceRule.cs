namespace RaceFilter.Model;

/// <summary>
/// FFXIV 种族 ID。<b>1 起始</b>，与游戏 Customize 字节完全一致：
/// Hyur=1, Roegadyn=2, Lalafell=3, Miqo'te=4, Elezen=5, Au Ra=6, Hrothgar=7, Viera=8。
/// （注意：这是从可运行插件 OopsAllRace 反编译确认的正确编号，早期 0 起始的写法会导致替换异常。）
/// </summary>
public enum RaceId : byte
{
    Hyur = 1,
    Roegadyn = 2,
    Lalafell = 3,
    Miqote = 4,
    Elezen = 5,
    AuRa = 6,
    Hrothgar = 7,
    Viera = 8,
}

/// <summary>性别（0=男, 1=女，与游戏一致）。</summary>
public enum Gender : byte
{
    Male = 0,
    Female = 1,
}

/// <summary>对命中 actor 执行的动作。</summary>
public enum ActionKind
{
    Hide,    // 完全不渲染（重定向到空白合集）
    Replace, // 改写为其他种族的外观（改写 Customize 字节 + 重绘）
}

/// <summary>规则作用范围。</summary>
public enum TargetScope
{
    PlayersAndNpcs,
    PlayersOnly,
    NpcsOnly,
}

/// <summary>一条过滤规则。</summary>
public class RaceRule
{
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "新规则";

    // 匹配条件
    public RaceId TargetRace { get; set; } = RaceId.Lalafell;
    public Gender? TargetGender { get; set; } = null; // null = 不限
    public byte? TargetTribe { get; set; } = null;    // null = 不限（部族 1-16，每种族两个）

    public TargetScope Scope { get; set; } = TargetScope.PlayersAndNpcs;

    // 动作与替换目标
    public ActionKind Action { get; set; } = ActionKind.Hide;
    public RaceId ReplacementRace { get; set; } = RaceId.Elezen;
    public Gender? ReplacementGender { get; set; } = null; // null = 保持原性别
    public byte? ReplacementTribe { get; set; } = null;    // null = 按公式重算（targetRace*2 - 原部族奇偶）；填值则强制覆盖
}
