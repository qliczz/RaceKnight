# RaceKnight —— 按种族 / 指定角色 隐藏或替换外观的 Dalamud 插件

> 作用范围：其他玩家 + NPC（**永不影响你自己**）。
> - **按种族规则**：对指定种族（可加性别/部族/范围筛选）执行「隐藏」或「替换为其他种族外观」。
> - **手动目标**：按显示名手动添加指定角色，对其单独施加「隐藏 / 替换」。
>
> **不依赖任何外部插件**：Hide 由插件改写 `GameObject.RenderFlags` 渲染标志位实现（清掉 `Model` 位即不渲染模型，**无需绘制钩子、无需任何版本签名、无需配置其它插件**）；Replace 改写外观字节 + 重绘（已装 Penumbra 时借其 IPC 重绘，否则在设置里填好 `RedrawSignature` 签名即可，无需 Penumbra）。

## 目录结构
```
RaceKnight/
├── RaceKnight.csproj        # Dalamud.NET.Sdk/15.0.0（已开 AllowUnsafeBlocks）
├── RaceKnight.json          # 插件清单
├── Plugin.cs                # 入口：服务注入 + 扫描器/干预器装配 + 命令/UI
├── Configuration.cs         # 配置（种族规则列表 + 手动目标列表）
├── Model/
│   ├── RaceRule.cs          # 种族规则模型与枚举（RaceId/Gender/ActionKind/TargetScope）
│   ├── ManualTarget.cs      # 手动目标模型（按名字）
│   └── MatchSpec.cs         # 统一匹配指令（动作 + 替换参数）
├── Services/
│   ├── IRaceIntervention.cs # 干预适配器接口
│   ├── ActorScanner.cs      # 每帧遍历 ObjectTable，双匹配（种族规则 + 手动目标）
│   └── DrawHookIntervention.cs # 渲染标志位 Hide + 字节改写/重绘 Replace（无绘制钩子、无 DrawSig）
└── Windows/
    ├── MainWindow.cs        # 状态窗
    └── ConfigWindow.cs      # 规则编辑器 + 手动目标编辑器
```

## 核心数据流
```
IFramework.Update
  → ActorScanner 遍历 IObjectTable
      → 对每个 ICharacter 先按 RaceRule 匹配（读 Customize 种族/性别/部族）
      → 否则按 ManualTarget 匹配（按显示名，大小写不敏感）
      → 产出统一 MatchSpec
  → 命中：Intervention.OnActorMatched(actor, spec)
  → 失配：Intervention.OnActorUnmatched(actor)
```

种族 ID 对照（**1 起始**，与游戏 Customize 字节完全一致；这是从可运行插件 OopsAllRace 反编译确认的正确编号）：
`1 Hyur | 2 Roegadyn(鲁加) | 3 Lalafell(拉拉肥) | 4 Miqo'te(猫魅) | 5 Elezen | 6 Au Ra(敖龙) | 7 Hrothgar(赫斯卡) | 8 Viera(维埃拉)`
（早期 0 起始的写法是错的，会导致 Replace 写入错误种族字节、模型显示异常。）

## Replace 正确性的关键：种族专属外观合法化（参考 OopsAllRace）
只改写「种族」字节是不够的——每个种族的**发型/脸型/体型数量不同**，若不把其它字段包进目标种族的合法范围，替换后的模型会穿模/空缺。
本插件从可运行的开源插件 **OopsAllRace**（基于 Penumbra IPC 的种族替换，API 15 / net10 / 依赖 Penumbra.Api 5.13.1）反编译学习了其 `ChangeRace` 算法，落地在 `Model/RaceRemap.cs`：

- **种族字节**：1 起始（`RaceId` 枚举）。
- **部族重算**：`Tribe = targetRace*2 - (原Tribe % 2)`（保留原部族奇偶，映射到目标种族对应的两个部族之一）。
- **种族发型表 RaceHairs**（每种族合法发型数，用于包住原发型）：
  `Hyur13 / Elezen12 / Lalafell13 / Miqo'te12 / Roegadyn13 / AuRa12 / Hrothgar8 / Viera17`。
- **封顶**：`FaceType %= 4`、`ModelType %= 2`、`HairStyle = 原Hair % RaceHairs[目标] + 1`。

> OopsAllRace 的其它做法（仅供参考，本插件未采用）：它依赖 **Penumbra** 才能工作，并通过 Penumbra 的 `CreatingCharacterBase` 事件在建模时改写 Customize 指针，而非像我们这样每帧保活；它**只用原生重载签名**（RedrawAll IPC），且只作用于玩家（ObjectKind==1）。本插件刻意不依赖 Penumbra（Hide 用渲染标志位、Replace 用字节改写+重绘兜底），并额外支持 NPC 与手动目标。

## 手动目标怎么填
- 玩家：显示名为 `"名 姓"`（如 `Taro Yamada`），大小写不敏感。
- NPC：角色名即可。
- 跨服同名无法区分，个人自用足够；可配合「作用范围」缩小到 仅玩家 / 仅 NPC。

## 本地构建与运行
1. 装好 XIVLauncher + Dalamud，至少启动过一次游戏。
2. 安装 **.NET 10 SDK**（本插件用 `Dalamud.NET.Sdk/15.0.0`，默认目标 `net10.0`，与当前 Dalamud API 15 / 参考插件 OopsAllRace 一致）+ Visual Studio 2022 / Rider。
3. `dotnet build`（产物在 `bin/Debug/RaceKnight.dll`）。
4. 游戏内 `/xlsettings` → Experimental → 把该 DLL 完整路径加入 **Dev Plugin Locations**。
5. `/xlplugins` → Dev Tools → 启用 RaceKnight；输入 `/raceknight` 打开界面。

## ⚠️ 你需补全的“最后一步”（本环境无法运行游戏，留作 TODO）
1. **Hide 已无需任何签名**：Hide 现在通过清除 `GameObject.RenderFlags` 的 `Model` 位实现，不依赖绘制钩子、不涉及版本签名，开箱即用。
   补充：默认只隐藏**模型**，**名称牌(Nameplate)仍会显示**。若想连名牌一起藏，把 `DrawHookIntervention.SetHidden` 里注释掉的那行 `go->RenderFlags &= ~CSVisibility.Nameplate;` 取消注释即可（一行搞定）。
2. **Replace 原生重绘（可选，运行时可配置）**：若你**没装 Penumbra**，Replace 改写完字节后需要“角色模型重载”才能刷新外观。
   该签名已改为**设置项**，无需改代码或重编译：游戏内打开 RaceKnight 设置 → “三、原生重绘” → 在 `Redraw 签名` 框粘贴当前游戏版本的字节特征码（SigScanner 格式，例如 `E8 ?? ?? ?? ?? 48 8B D3 ...`），保存即生效。
   插件会把签名交给 `ISigScanner` 定位并重绘；签名随游戏版本/客户端变化，留空或填错时自动降级（仅改写字节、不刷新、不崩溃）。
   若已装 Penumbra，则自动优先走其 `RedrawObject` IPC，零配置即可，无需此步。
3. **替换的本质说明**：Replace 改写 Customize 字节（含种族/性别/部族，**并自动按目标种族合法化脸型/体型/发型**，见 `RaceRemap`），属于“外观层”改变，并非替换完整模型文件；部分装备/骨骼表现可能不完全贴合，属预期现象。

## 调试
- `/xldev` → Dalamud → Enable AntiDebug，再用 VS/Rider Attach 到 FFXIV 进程。
- `/xllog` 查看插件日志（Replace 重载函数是否定位、Penumbra 是否可用等；Hide 不再有相关日志）。

## 注意事项
- 本插件只改**你自己客户端**的画面，不影响其他玩家看到的你或其他人。
- FFXIV 的 ToS 原则上禁止 mod；提交官方仓库前请阅读
  https://dalamud.dev/plugin-publishing/ai-policy 与 restrictions。
