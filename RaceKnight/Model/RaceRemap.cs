using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Enums;
using RaceFilter.Model;

namespace RaceFilter.Model;

/// <summary>
/// 把「角色外观字节（Customize）」就地改写成目标种族所需的<b>合法</b>值。
///
/// 算法与数据取自可运行的开源插件 <b>OopsAllRace</b>（基于 Penumbra IPC 的种族替换）。
/// 其结构体布局 Race=0 / ModelType=2 / Tribe=4 / FaceType=5 / HairStyle=6
/// 与 Dalamud 的 <see cref="CustomizeIndex"/> 枚举完全一致，故可直接套用。
///
/// 只改种族字节是不够的：每个种族的<b>发型/脸型/体型数量不同</b>，
/// 若不把其它字段包进目标种族的合法范围，替换后的模型会显示异常（穿模/空缺）。
/// 这正是本插件早期 Replace 一直发虚的根因。
/// </summary>
public static class RaceRemap
{
    /// <summary>
    /// 每种族「合法发型数量」——用于把原发型包进目标种族可选范围。来自 OopsAllRace。
    /// 键为 1 起始的 <see cref="RaceId"/>（与游戏字节一致）。
    /// </summary>
    private static readonly Dictionary<RaceId, int> RaceHairs = new()
    {
        { RaceId.Hyur, 13 },
        { RaceId.Elezen, 12 },
        { RaceId.Lalafell, 13 },
        { RaceId.Miqote, 12 },
        { RaceId.Roegadyn, 13 },
        { RaceId.AuRa, 12 },
        { RaceId.Hrothgar, 8 },
        { RaceId.Viera, 17 },
    };

    /// <summary>
    /// 就地改写 <paramref name="cz"/>（角色 Customize 字节）为 <paramref name="targetRace"/> 的合法外观。
    /// 会同步修正 Tribe（按公式重算）、FaceType、ModelType、HairStyle，使替换模型正常显示。
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

        // 部族公式：保留原部族奇偶（映射到目标种族对应的两个部族之一）。
        cz[(int)CustomizeIndex.Tribe] = overrideTribe
            ?? (byte)(tr * 2 - (origTribe % 2));

        if (targetGender.HasValue)
            cz[(int)CustomizeIndex.Gender] = (byte)targetGender.Value;

        // 封顶到目标种族合法范围，避免越界导致模型异常：
        cz[(int)CustomizeIndex.FaceType] = (byte)(face % 4);
        cz[(int)CustomizeIndex.ModelType] = (byte)(model % 2);
        cz[(int)CustomizeIndex.HairStyle] = (byte)(hair % RaceHairs[targetRace] + 1);
    }
}
