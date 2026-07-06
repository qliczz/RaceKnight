using System;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using RaceFilter.Model;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace RaceFilter.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    public ConfigWindow(Plugin plugin) : base("RaceKnight 设置###config")
    {
        Size = new System.Numerics.Vector2(460, 560);
        SizeCondition = ImGuiCond.Always;
        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var cfg = plugin.Configuration;

        if (ImGui.CollapsingHeader($"{FontAwesomeIcon.EyeSlash.ToIconString()} 一、按种族规则", ImGuiTreeNodeFlags.DefaultOpen))
            DrawRules(cfg);

        ImGui.Separator();

        if (ImGui.CollapsingHeader($"{FontAwesomeIcon.UserSecret.ToIconString()} 二、手动目标（按名字）", ImGuiTreeNodeFlags.DefaultOpen))
            DrawManualTargets(cfg);

        ImGui.Separator();

        var preferPenumbra = cfg.PreferPenumbraRedraw;
        ImGui.Checkbox("若已安装 Penumbra，Replace 优先用其 IPC 重绘（更稳）", ref preferPenumbra);
        cfg.PreferPenumbraRedraw = preferPenumbra;

        ImGui.TextWrapped("插件完全不依赖任何外部插件：Hide 通过改写渲染标志位实现（无需签名）；" +
                          "Replace 在装有 Penumbra 时借其 IPC 重绘，否则自动改用插件自带的原生可见性切换重绘（同样零配置、无需任何签名）。");

        ImGui.Separator();
        if (ImGui.Button($"{FontAwesomeIcon.Save.ToIconString()} 保存配置"))
        {
            cfg.Save();
            Plugin.Log.Information("[RaceKnight] 配置已保存");
        }
    }

    // ===== 种族规则 =====
    private void DrawRules(Configuration cfg)
    {
        for (var i = 0; i < cfg.Rules.Count; i++)
        {
            var rule = cfg.Rules[i];
            ImGui.PushID("rule" + i);

            if (ImGui.CollapsingHeader($"规则 {i + 1}: {rule.Name}"))
            {
                var enabled = rule.Enabled;
                ImGui.Checkbox("启用", ref enabled);
                rule.Enabled = enabled;

                var name = rule.Name ?? "";
                if (InputText("名称", ref name, 64))
                    rule.Name = name;

                var targetRace = rule.TargetRace;
                EnumCombo("目标种族", ref targetRace);
                rule.TargetRace = targetRace;

                var targetGender = rule.TargetGender;
                NullableGenderCombo("目标性别 (空=不限)", ref targetGender);
                rule.TargetGender = targetGender;

                var targetTribe = rule.TargetTribe;
                NullableTribeInput("目标部族 (-1=不限)", ref targetTribe);
                rule.TargetTribe = targetTribe;

                var scope = rule.Scope;
                EnumCombo("作用范围", ref scope);
                rule.Scope = scope;

                var action = rule.Action;
                EnumCombo("动作", ref action);
                rule.Action = action;

                if (rule.Action == ActionKind.Replace)
                {
                    var repRace = rule.ReplacementRace;
                    EnumCombo("替换种族", ref repRace);
                    rule.ReplacementRace = repRace;

                    var repGender = rule.ReplacementGender;
                    NullableGenderCombo("替换性别 (空=保持)", ref repGender);
                    rule.ReplacementGender = repGender;

                    var repTribe = rule.ReplacementTribe;
                    NullableTribeInput("替换部族 (-1=保持原部族)", ref repTribe);
                    rule.ReplacementTribe = repTribe;
                }

                if (ImGui.Button($"{FontAwesomeIcon.TrashAlt.ToIconString()} 删除该规则"))
                {
                    cfg.Rules.RemoveAt(i);
                    cfg.Save();
                    ImGui.PopID();
                    break;
                }
            }

            ImGui.PopID();
        }

        if (ImGui.Button($"{FontAwesomeIcon.Plus.ToIconString()} 新增种族规则"))
            cfg.Rules.Add(new RaceRule());
    }

    // ===== 手动目标 =====
    private void DrawManualTargets(Configuration cfg)
    {
        ImGui.TextWrapped("按显示名匹配（玩家为 \"名 姓\"，NPC 为角色名，大小写不敏感）。命中后对其施加动作。");

        for (var i = 0; i < cfg.ManualTargets.Count; i++)
        {
            var m = cfg.ManualTargets[i];
            ImGui.PushID("manual" + i);

            if (ImGui.CollapsingHeader($"目标 {i + 1}: {m.Name}"))
            {
                var enabled = m.Enabled;
                ImGui.Checkbox("启用", ref enabled);
                m.Enabled = enabled;

                var name = m.Name ?? "";
                if (InputText("角色名", ref name, 64))
                    m.Name = name;

                var scope = m.Scope;
                EnumCombo("作用范围", ref scope);
                m.Scope = scope;

                var action = m.Action;
                EnumCombo("动作", ref action);
                m.Action = action;

                if (m.Action == ActionKind.Replace)
                {
                    var repRace = m.ReplacementRace;
                    EnumCombo("替换种族", ref repRace);
                    m.ReplacementRace = repRace;

                    var repGender = m.ReplacementGender;
                    NullableGenderCombo("替换性别 (空=保持)", ref repGender);
                    m.ReplacementGender = repGender;

                    var repTribe = m.ReplacementTribe;
                    NullableTribeInput("替换部族 (-1=保持原部族)", ref repTribe);
                    m.ReplacementTribe = repTribe;
                }

                if (ImGui.Button($"{FontAwesomeIcon.TrashAlt.ToIconString()} 删除该目标"))
                {
                    cfg.ManualTargets.RemoveAt(i);
                    cfg.Save();
                    ImGui.PopID();
                    break;
                }
            }

            ImGui.PopID();
        }

        if (ImGui.Button("+ 新增手动目标"))
            cfg.ManualTargets.Add(new ManualTarget());

        ImGui.Spacing();
        ImGui.TextWrapped($"提示：{FontAwesomeIcon.InfoCircle.ToIconString()} 插件完全不依赖任何外部插件……");
    }

    // ===== 辅助：文本/枚举/可空下拉 =====

    private static bool InputText(string label, ref string value, uint max)
    {
        var buf = new byte[(int)max + 1];
        Encoding.UTF8.GetBytes(value ?? "", 0, Math.Min(value?.Length ?? 0, (int)max), buf, 0);
        if (ImGui.InputText(label, buf, ImGuiInputTextFlags.None))
        {
            value = Encoding.UTF8.GetString(buf).TrimEnd('\0');
            return true;
        }

        return false;
    }

    private static bool EnumCombo<T>(string label, ref T value) where T : struct, Enum
    {
        var names = Enum.GetNames<T>();
        var idx = Array.IndexOf(names, value.ToString());
        if (idx < 0) idx = 0;
        if (ImGui.Combo(label, ref idx, names, names.Length))
        {
            value = Enum.Parse<T>(names[idx]);
            return true;
        }

        return false;
    }

    private static void NullableGenderCombo(string label, ref Gender? value)
    {
        var options = new[] { "不限", "Male", "Female" };
        var idx = value == null ? 0 : (value == Gender.Male ? 1 : 2);
        if (ImGui.Combo(label, ref idx, options, options.Length))
            value = idx == 0 ? (Gender?)null : (idx == 1 ? Gender.Male : Gender.Female);
    }

    private static void NullableTribeInput(string label, ref byte? value)
    {
        var v = value.HasValue ? value.Value : -1;
        if (ImGui.InputInt(label, ref v))
            value = v < 0 ? (byte?)null : (byte)Math.Max(0, Math.Min(13, v));
    }

    private static void DrawIcon(FontAwesomeIcon icon)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.Text(icon.ToIconString());
        ImGui.PopFont();
    }
}
