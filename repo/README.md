# RaceKnight 自定义插件仓库（Plugin Repository）

本目录教你怎么把 RaceKnight 做成一个**第三方插件仓库**，让别人（以及你自己换机/重装后）在游戏内通过一条 URL 就能一键安装，而不用每次都走 Dev Plugin Locations 手动指目录。

> 适用对象：XIVLauncher / Soil（XIVLauncherCN）等所有使用 Dalamud 的启动器。Soil 用的也是同一套 Dalamud，自定义仓库的添加方式完全一致。

---

## 1. 一个"仓库"到底是什么

一个 Dalamud 自定义插件仓库 = **一个可由 HTTP GET 直接访问的 JSON 文件**，里面是一个插件清单数组。用户把该 JSON 的 URL 加到游戏设置里，Dalamud 就会去拉这个清单，把里面的插件显示在 `/xlplugins` 安装器里。

所以你不需要搭服务器，只要有一个能公网访问的 JSON 文件即可。常见的托管方式：
- **GitHub Releases + 仓库里的 JSON**（推荐，最简单）
- GitHub Pages / Vercel / Netlify 等静态托管
- Gitee、自建静态站点，任何返回 JSON 的 URL 都行（不支持鉴权）

本仓库已经准备好了两个文件：
- `repo/pluginmaster.json` —— 插件清单（改掉里面的占位符即可用）
- `RaceKnight/pack.cmd` —— 一键编译并产出发布包 `dist/RaceKnight.zip`

---

## 2. 清单字段说明（pluginmaster.json）

每个数组元素是一个插件条目，关键字段：

| 字段 | 含义 |
|------|------|
| `Name` / `InternalName` | 显示名 / 内部名（= DLL 名，必须和 `RaceKnight.json` 里的 `InternalName` 一致） |
| `Author` | 作者 |
| `Description` / `Punchline` | 详情 / 一句话简介 |
| `AssemblyVersion` | **稳定版**版本号，需和 csproj 的 `<Version>` 保持一致 |
| `TestingAssemblyVersion` | 测试版版本号（可选，大于稳定版时才作为测试版暴露） |
| `DalamudApiLevel` | 目标 API 级别。**你的 Soil 是 15，这里必须写 15**，否则客户端不显示 |
| `ApplicableVersion` | 一般填 `"any"` |
| `RepoUrl` | 你的源码仓库地址 |
| `DownloadLinkInstall` / `DownloadLinkUpdate` | **稳定版 zip 的下载直链** |
| `DownloadLinkTesting` | 测试版 zip 直链（可选） |
| `IsHide` / `IsTestingExclusive` | 是否隐藏 / 是否仅测试用户可见 |
| `LastUpdate` | Unix 时间戳，用于排序展示（填 `0` 也行，发布前改成当前时间更规范） |

> 把文件里 `你的用户名` 等占位符替换成你自己的 GitHub 账号与仓库名。

---

## 3. 打包插件

在 `RaceKnight/` 目录下双击 `pack.cmd`（或在 Git Bash 里 `cmd //c pack.cmd`）：

1. 它会用 `DALAMUD_HOME` 指向你机器上的 Soil dev 目录编译 Release；
2. Dalamud.NET.Sdk 15 会在 `bin/Release/RaceKnight/latest.zip` 自动生成**标准插件包**（含 `RaceKnight.dll` / `RaceKnight.json` / `RaceKnight.deps.json`）；
3. 脚本把它复制为 `dist/RaceKnight.zip` —— 这就是要发布的包。

> 想手动验证包内容：`unzip -l dist/RaceKnight.zip`，根目录应有上述三个文件。

---

## 4. 发布（让 URL 可用）

最简单可靠的路子 —— **GitHub Release**：

1. 在 GitHub 建仓库（如 `你的用户名/RaceKnight`），把 `RaceKnight/` 源码和 `repo/` 都推上去。
2. 打一个 Release（如 `v0.0.0.1`），把 `dist/RaceKnight.zip` 作为**附件**上传。
3. 该附件的**直链**形如：
   ```
   https://github.com/你的用户名/RaceKnight/releases/download/v0.0.0.1/RaceKnight.zip
   ```
4. 把这个直链填进 `repo/pluginmaster.json` 的 `DownloadLinkInstall` / `DownloadLinkUpdate`（测试版同理填 `DownloadLinkTesting`）。
5. 再把 `repo/pluginmaster.json` 本身推到仓库（比如放在 `main` 分支根目录或 `repo/` 目录）。

然后这个 JSON 的**可访问 URL** 就是你的仓库地址，例如：
```
https://raw.githubusercontent.com/你的用户名/RaceKnight/main/repo/pluginmaster.json
```
（GitHub Pages 用户则是 `https://你的用户名.github.io/RaceKnight/pluginmaster.json` 之类。）

> 想自助托管：把 `pluginmaster.json` 和 `RaceKnight.zip` 放到任意静态站点，URL 指向 JSON 即可。

---

## 5. 在游戏内添加仓库并安装

1. 游戏内回车输入 `/xlsettings` → 切到 **Experimental（实验性）** 标签页；
2. 找到 **Custom Plugin Repositories（自定义插件仓库）**，点 **Add**；
3. 粘贴上面的 `pluginmaster.json` 的 URL，确认；
4. 关掉设置，输入 `/xlplugins` → 切到 **Available / 可用** 或 **Custom** 分类，就能看到 **RaceKnight**，勾选安装即可。

Soil（XIVLauncherCN）界面与官方一致，步骤相同。

---

## 6. 维护要点

- **版本号要同步**：每次发新版本，记得改 csproj 的 `<Version>` 和 `pluginmaster.json` 的 `AssemblyVersion`，否则 Dalamud 认为"已是最新"不会更新。
- **API 级别匹配**：Soil 是 15；若将来 Soil 升级到更高 API，需要同步改 `DalamudApiLevel` 并重新针对新 dev 目录编译（参考 `build.cmd` / `pack.cmd` 里的 `DALAMUD_HOME`）。
- **测试版**：想先给小范围用户测，把 `DownloadLinkTesting` + `TestingAssemblyVersion` 填好，并在 `/xlsettings` 勾选"显示测试插件"。
- **完全不依赖 Penumbra**：Hide 改写 `RenderFlags` 的 `Model` 位隐藏模型；Replace 优先借 Penumbra IPC 重绘，未装 Penumbra 时自动改用插件自带的原生可见性切换重绘（两者均零配置、无需任何签名），仓库里所有用户都能用 Hide 与 Replace。

---

## 7. 目录结构总览

```
plugin-scaffold/
├── RaceKnight/            # 插件源码
│   ├── build.cmd          # 一键 Debug 编译（Dev Plugin Locations 用）
│   ├── pack.cmd           # 一键 Release 打包 -> dist/RaceKnight.zip
│   └── dist/              # 发布包输出
└── repo/
    ├── pluginmaster.json  # 自定义插件仓库清单（改占位符即用）
    └── README.md          # 本文件
```
