# UI 工作记录（Agent 版）

## 目标与范围

- 负责人只做 **除主菜单（StartScene）以外** 的 UI。
- 交付：`Assets/Scenes/UIScene.unity` + `Assets/Prefabs/UI/*`，之后的 UI 工作在 UIScene 上直接改预制体。
- 生成入口 `Editor/UIBuilder.cs`，菜单 `9Days/Build/UI (Prefabs + UIScene)`，batch `BuildPipeline.BuildUIBatch`。

## 关键决策与理由

1. **为什么用生成器而不是手拖**
   项目既有约定（`Docs/agent/tooling/Tooling.md` §1）是 prefab/配置/场景由代码生成，避免 YAML 冲突与漏引用。UI 按同一模式扩展，不与既有约定打架。

2. **生成器幂等，只补缺不覆盖**
   `UIBuilder.Exists(path)` 命中就跳过并打印日志。原因：UI 是美术/策划持续手改的资产，若每次生成都覆盖会丢工作。重建流程 = 手动删文件 + 跑菜单。

3. **UIScene 只做 UI，不带玩法**
   没有 `TurnController`、没有角色。面板默认 inactive，方便在 Scene 视图逐个调；`Overlay` 空容器留给受击闪白 / 屏幕震动 / 过场。

4. **Canvas 用 ScreenSpaceOverlay + 1920×1080 / Match 0.5**
   和截图对齐、和后续切分辨率都稳定；所有角元素用 `Margin = 24` 常量统一。

5. **EventSystem 用 `InputSystemUIInputModule`**
   工程启用了新输入系统（`InputRouter` 读 `Mouse.current`）。模块用 `AssignDefaultActions()` 显式赋默认 UI Actions，避免代码创建的模块 `actionsAsset` 为空。

6. **按钮只有 `UI_Button` 一处**
   三态（待机/悬停/选中）写在 `ColorBlock`，改预制体即可全局生效；面板里的按钮都是它的嵌套实例，文案通过 `PrefabUtility.RecordPrefabInstancePropertyModifications` 记录为实例覆盖。

## 生成顺序（依赖不能乱）

```
UI_Button → UI_HealthBar / UI_TurnBanner / UI_BulletType → UI_HUD
→ UI_PausePanel / UI_ItemSelectPanel / UI_PlacementPanel → UI_Screen → UIScene
```

`UI_HUD` 与各面板通过 `PrefabUtility.InstantiatePrefab` 嵌套引用前序预制体；`UI_Screen` 再嵌套 HUD 与三个面板。

## 布局参数（写死在生成器里，改完需重建）

- HUD：血条 P1 (24,-24) 左上 / P2 (-24,-24) 右上，横幅顶部居中 (0,-24)，子弹类型条底部居中 (0,24) 360×72，暂停按钮右下 (-24,24) 96×96。
- 暂停面板：遮罩 `Dim` alpha 0.65，面板 640×520，标题 y=-32，四个按钮 y=-140 / -232 / -324 / -416，280×72。
- 选取面板：面板 1400×800；道具位 220×260（y=-190，x 从 -525 步进 350）；子弹位 160×140（y=-540，x 从 -420 步进 280）；确认 y=-700。
- 摆放面板：整条底部锚点，高 200；道具位 96×96 y=-32；取消/确认在底部 200×64。

## 与现有代码的接口现状

| 需求 UI | 可用数据 | 缺口 |
|---|---|---|
| 回合横幅 | `GameEvents.TurnStarted/TurnEnded`、`TurnController.CurrentTeam/IsResolving` | 无 |
| 血条 | `HitInfo.Damage`（只透传） | `Character` / `CharacterConfig` 无 HP；无 `HealthChanged` |
| 子弹类型 | `ProjectileConfig.icon / displayName`（已加字段） | niexa 分支无 `Projectile` / `Character` 脚本，图标与文字为静态占位 |
| 暂停面板 | `TurnController.ResetMatch()` | 无 PauseController；`InputRouter` 未屏蔽 UI 点击 |
| 选取 / 摆放 | 无 | 无道具 SO、无阶段事件、无抽取逻辑 |
| 胜利嘲讽 | 无 | 无胜负判定 |

## 已知风险 / 待办

1. **输入穿透**：`CharacterLauncher.Update()` 直接读 `InputRouter.LeftPressed`，点暂停/卡牌按钮会触发一次瞄准发射。需要程序在 `InputRouter` 或 Launcher 里加 `EventSystem.current.IsPointerOverGameObject()` 判断，这是 UI 能真正可用的前置条件。
2. **中文字体**：当前用 `LiberationSans SDF`，无中文字形，正式 UI 前要换中文 TMP 字体资产。
3. **安全区**：未做刘海/安全区适配（当前按 1920×1080 固定边距）。
4. **面板开关脚本**：目前纯视觉占位，没有任何 `UIPanelController`；开关逻辑待程序/UI 分工确认后再写。
5. **未加入 Build Settings**：`UIScene` 只是工作场景，没有写 `EditorBuildSettings.asset`（该文件当前有未提交改动，避免混入无关变更）。

## 验证记录

- 编译：`BuildPipeline.cs` 已移除对 niexa 不存在的 `PrefabFactory / ArenaBuilder / BuildCheck / ProjectSetup` 的引用，只保留 UI 菜单与 `BuildUIBatch`；需在 Unity 重新编译后确认无 `error CS`。
- 结构校验：`UIBuilder.Validate()` 断言 6 个预制体存在、`UI_Screen` 是 Overlay Canvas、参考分辨率 1920×1080、Match=0.5、含 `HUD` 与 `Panels/PausePanel`、`UIScene.unity` 存在。
- 运行态：未在 Play Mode 验证（UIScene 暂无逻辑脚本，只验证层级与 Canvas 参数）。
