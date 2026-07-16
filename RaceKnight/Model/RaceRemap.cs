using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Enums;
using RaceFilter.Model;

namespace RaceFilter.Model;

/// <summary>
/// 把「角色外观字节（Customize）」就地改写成目标种族所需的<b>合法</b>值。
///
/// 算法与数据参考两个已运行的开源插件：
///  - <b>OopsAllRace</b>（基于 Penumbra IPC 的种族替换）：提供种族/性别/部族的改写骨架与发型数量表。
///  - <b>Glamourer</b>（1.6.1.8，反编译 <c>CustomizeSet.Count</c> / <c>CustomizeSet.HrothgarFaceHack</c> 学习）：
///    提供<b>按种族合法化</b>的权威规则——每个种族的脸型/发量数量不同，且 Hrothgar 的脸型 5–8 只是 1–4 的别名。
///
/// CustomizeIndex 负责映射真实布局（Race=0 / Gender=1 / ModelType=2 / Height=3 / Tribe=4 / FaceType=5 / HairStyle=6 …），
/// 与 Dalamud 的 <see cref="CustomizeIndex"/> 枚举（编译期绑定到真实字节偏移）完全一致，故可直接套用。
///
/// 只改种族字节是不够的：每个种族的<b>脸型/发型/体型数量不同</b>，
/// 若不把其它字段包进目标种族的合法范围，替换后的模型会显示异常（穿模/空缺/脸型为 0 崩坏）。
/// 本类保证改写后每个字段都落在目标种族的<b>合法非空</b>区间（最小值为 1，绝不为 0）。
/// </summary>
public static class RaceRemap
{
    /// <summary>每种族「合法脸型数量」(FaceType 1..N)。取自 Glamourer <c>CustomizeSet.Count(Face)</c> 的语义：
    /// 多数种族 8 张脸；Lalafell 与 Hrothgar 仅 4 张（Hrothgar 的 5–8 是 1–4 的别名）。
    /// 键为 1 起始的 <see cref="RaceId"/>（与游戏字节一致）。</summary>
    private static readonly Dictionary<RaceId, int> RaceFaceCounts = new()
    {
        { RaceId.Hyur, 8 },
        { RaceId.Roegadyn, 8 },
        { RaceId.Lalafell, 4 },
        { RaceId.Miqote, 8 },
        { RaceId.Elezen, 8 },
        { RaceId.AuRa, 8 },
        { RaceId.Hrothgar, 4 },   // HrothgarFaceHack：5–8 实为 1–4 的别名
        { RaceId.Viera, 8 },
    };

    /// <summary>每种族「合法发型数量」(HairStyle 1..N)。沿用 OopsAllRace 的发型表（其替换功能可运行验证）。</summary>
    private static readonly Dictionary<RaceId, int> RaceHairs = new()
    {
        { RaceId.Hyur, 13 },
        { RaceId.Roegadyn, 13 },
        { RaceId.Lalafell, 13 },
        { RaceId.Miqote, 12 },
        { RaceId.Elezen, 12 },
        { RaceId.AuRa, 12 },
        { RaceId.Hrothgar, 8 },
        { RaceId.Viera, 17 },
    };

    /// <summary>体型(ModelType)合法值数量：绝大多数种族只有 2 种（Type I / Type II）。</summary>
    private const int ModelTypeCount = 2;

    /// <summary>
    /// 就地改写 <paramref name="cz"/>（角色 Customize 字节）为 <paramref name="targetRace"/> 的合法外观。
    /// 会同步修正 Tribe（按公式重算）、FaceType、ModelType、HairStyle，使替换模型正常显示，且每个值都 ≥ 1。
    /// </summary>
    /// <param name="targetGender">null = 保持原性别。</param>
    /// <param name="overrideTribe">非 null 则强制使用该部族字节（高级覆盖），否则按公式重算。</param>
    public static void Apply(Span<byte> cz, RaceId targetRace, Gender? targetGender, byte? overrideTribe = null)
    {
        var tr = (byte)targetRace;

        var origTribe = cz[(int)CustomizeIndex.Tribe];
        var face = cz[(int)CustomizeIndex.FaceType];
        var model = cz[(int)CustomizeIndex.ModelType];
        var hair = cz[(int)CustomizeIndex.HairStyle];

        cz[(int)CustomizeIndex.Race] = tr;

        // 部族公式：每种族拥有 2*race-1 与 2*race 两个部族，保留原部族奇偶映射到目标种族对应部族之一。
        cz[(int)CustomizeIndex.Tribe] = overrideTribe
            ?? (byte)(tr * 2 - (origTribe % 2));

        if (targetGender.HasValue)
            cz[(int)CustomizeIndex.Gender] = (byte)targetGender.Value;

        // —— 合法化：每个字段都落到 [1, 上限]，绝不为 0（FFXIV 的 Customize 字段均为 1 起始）——
        // FaceType：按目标种族脸型数量取整，Hrothgar(4 张) 自然把 5–8 折回 1–4（对应 Glamourer 的 HrothgarFaceHack）。
        var faceMax = RaceFaceCounts.GetValueOrDefault(targetRace, 8);
        cz[(int)CustomizeIndex.FaceType] = (byte)(1 + (((Math.Max((byte)1, face) - 1) % faceMax + faceMax) % faceMax));

        // ModelType（体型）：绝大多数种族合法值为 1–2。
        cz[(int)CustomizeIndex.ModelType] = (byte)(1 + ((Math.Max((byte)1, model) - 1) % ModelTypeCount));

        // HairStyle：按目标种族发量取整，1 起始（原值为 0 时也安全映射到 1）。
        var hairMax = RaceHairs.GetValueOrDefault(targetRace, 12);
        cz[(int)CustomizeIndex.HairStyle] = (byte)(1 + ((Math.Max((byte)1, hair) - 1) % hairMax));
    }
}
