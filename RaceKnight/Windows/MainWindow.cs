using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using RaceFilter.Services;

namespace RaceFilter.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    public MainWindow(Plugin plugin) : base("RaceKnight##main",
        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(300, 160),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.Text("种族过滤器已运行（仅影响你自己的客户端画面，永不影响本人）。");
        ImGui.Text($"种族规则数：{plugin.Configuration.Rules.Count}");
        ImGui.Text($"手动目标数：{plugin.Configuration.ManualTargets.Count}");
        ImGui.Spacing();

        // 当前生效的 Replace 重绘后端（两套方案并存时可见）
        var mode = plugin.ActiveRedraw switch
        {
            RedrawMode.Penumbra => "Penumbra IPC（已装 Penumbra，零配置）",
            RedrawMode.Native   => "原生可见性切换（未装 Penumbra，零配置、无需签名）",
            RedrawMode.None     => "无（仅字节改写，外观可能不刷新）",
            _                   => "检测中…",
        };
        ImGui.Text($"Replace 重绘后端：{mode}");
        ImGui.Spacing();

        if (ImGui.Button("打开设置"))
            plugin.ToggleConfigUi();

        ImGui.Separator();
        ImGui.TextWrapped("设置里可：① 按种族增删规则（隐藏/替换为其他种族）；② 手动添加指定角色（按名字）进行改动。");
        ImGui.TextWrapped("/xllog 可查看 Hide 钩子是否成功启用、Replace 重绘是否可用。");
    }
}
