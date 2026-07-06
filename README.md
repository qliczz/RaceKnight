# 插件脚手架总览

本目录包含：

## `RaceKnight/`（要做的插件）⭐
按「种族规则」或「手动指定角色（按名字）」执行「隐藏 / 替换为其他种族外观」的 Dalamud 插件。
作用范围=其他玩家+NPC，可配置；**不依赖任何外部插件**（Hide 用渲染标志位 `GameObject.RenderFlags`，无需绘制钩子/签名；
Replace 用字节改写+重绘，重绘优先借 Penumbra IPC，未装时走插件自带的原生重载函数（设置里的 `RedrawSignature` 签名））。
- 入口与逻辑见 `RaceKnight/Plugin.cs`、`Services/`（ActorScanner + DrawHookIntervention）、`Windows/`、`Model/`。
- 详细说明与“最后一步”补全项见 `RaceKnight/README.md`。

---

## 构建/运行
1. 安装 XIVLauncher + Dalamud，至少启动游戏一次；安装 .NET 8 SDK + VS2022/Rider。
2. `dotnet build`（在 `RaceKnight/` 目录），产物在 `bin/Debug/RaceKnight.dll`。
3. 游戏内 `/xlsettings` → Experimental → 把 DLL 完整路径加入 Dev Plugin Locations。
4. `/xlplugins` → Dev Tools → 启用 RaceKnight；输入 `/raceknight` 打开界面。

> 提交官方仓库前请阅读 AI 使用政策：https://dalamud.dev/plugin-publishing/ai-policy

---

## 自动发布（GitHub Actions）

仓库已内置 `.github/workflows/release.yml`：打 tag 并推送即自动编译、打包、发 Release，免去手动 `pack.cmd` 与传 zip。

用法：
```bash
# 1) 先把版本号同步到本次要发的 tag：
#    - RaceKnight/RaceKnight.csproj 的 <Version>
#    - repo/pluginmaster.json 的 AssemblyVersion / TestingAssemblyVersion
# 2) 打 tag 并推送：
git tag v0.0.0.1
git push --tags
```
推送后 GitHub 会自动：下载官方 Dalamud api15 程序集充当 `DALAMUD_HOME` → `dotnet build -c Release` →
取出 `DalamudPackager` 生成的 `latest.zip` 改名 `RaceKnight.zip` → 以该 tag 创建 Release 并上传。

要点：
- 工作流跑在 `windows-latest`（MSBuild 的 `DalamudLibPath` 校验只认原生 Windows 路径，Linux/macOS runner 会失败）。
- **无需手填用户名**：发版时工作流会自动读取 `repo/pluginmaster.json` 模板，把 `你的用户名`/`你的名字` 换成你的真实 GitHub 用户名、把下载直链里的 `v0.0.0.1` 换成本次 tag，生成一份**发布用 `pluginmaster.json`** 一并上传到 Release。
  你只要把这个生成的 `pluginmaster.json` 拿去提交到插件仓库 / 自定义插件源即可，模板文件里的占位符保持原样就行。
- 发版前请同步升级版本号：把 `RaceKnight/RaceKnight.csproj` 的 `<Version>` 与 `repo/pluginmaster.json` 的 `AssemblyVersion`/`TestingAssemblyVersion` 改成与本次 tag 一致（下载直链由工作流自动跟随 tag，无需手改）。
- `DalamudApiLevel: 15` 已就位（对 Soil / XIVLauncherCN 用户可见）。
- 若要调整插件在仓库里的位置，记得同步修改工作流里的 `RaceKnight/RaceKnight.csproj` 编译路径。
