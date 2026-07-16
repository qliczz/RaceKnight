using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;
using RaceFilter.Model;
using RaceFilter.Services;
using RaceFilter.Windows;

namespace RaceFilter;

public sealed class Plugin : IDalamudPlugin
{
    // ===== 服务注入 =====
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/raceknight";

    public Configuration Configuration { get; init; }
    public readonly WindowSystem WindowSystem = new("RaceKnight");

    /// <summary>当前生效的 Replace 重绘后端（供 UI 展示两套方案并存状态）。</summary>
    public RedrawMode ActiveRedraw => Intervention.ActiveRedraw;

    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }
    private ActorScanner Scanner { get; init; }
    private IRaceIntervention Intervention { get; init; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        if (Configuration.Rules.Count == 0 && Configuration.ManualTargets.Count == 0)
            SeedDefaults();

        // 单一后端：自带渲染标志位（Hide）+ 字节改写/重绘（Replace），不依赖任何外部插件
        Intervention = new DrawHookIntervention(Framework, Configuration);
        Scanner = new ActorScanner(ObjectTable, ClientState, Framework, Intervention, Configuration);
        Intervention.Enable();

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);
        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "打开 RaceKnight 设置 / 主界面"
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Log.Information("=== RaceKnight 已加载 ===");
    }

    /// <summary>首次启动写入默认规则：男性拉拉肥、鲁加族、赫斯卡/Hrothgar（已确认种族正确）。</summary>
    private void SeedDefaults()
    {
        Configuration.Rules.Add(new RaceRule
        {
            Name = "男性拉拉肥",
            TargetRace = RaceId.Lalafell,
            TargetGender = Gender.Male,
            Action = ActionKind.Hide,
        });
        Configuration.Rules.Add(new RaceRule
        {
            Name = "鲁加族",
            TargetRace = RaceId.Roegadyn,
            Action = ActionKind.Hide,
        });
        Configuration.Rules.Add(new RaceRule
        {
            Name = "赫斯卡/Hrothgar",
            TargetRace = RaceId.Hrothgar,
            Action = ActionKind.Hide,
        });
        Configuration.Save();
    }

    public void Dispose()
    {
        CommandManager.RemoveHandler(CommandName);
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();
        MainWindow.Dispose();
        Scanner.Dispose();
        Intervention.Disable();
    }

    private void OnCommand(string command, string args) => MainWindow.Toggle();
    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();
}
