# 程序集拆分 — Agent 工作记录（Asmdef_agent）

> 人读版：同目录 `Asmdef.md`。输入模块：`../input/Input_agent.md`；工具链：`../tooling/Tooling_agent.md`。

## 目录

- [0. 本次改动](#0-本次改动)
- [1. 程序集清单](#1-程序集清单)
- [2. 解耦改动](#2-解耦改动)
- [3. 依赖与循环](#3-依赖与循环)
- [4. 编译与测试](#4-编译与测试)
- [5. 已知限制与后续](#5-已知限制与后续)
- [6. 接手检查清单](#6-接手检查清单)

---

## 0. 本次改动

目标：把单一运行时程序集拆成可编译的模块边界，最初采用 7 模块方案，后续新增 `NineDays.Flow`：

- 保留 `NineDays.Game` 作为 Character / Bullet / Burst / Scene / Config / Events / test 聚合层；
- 拆出 `NineDays.Input`、`NineDays.Settings`、`NineDays.UI`、`NineDays.Battle`、`NineDays.Camera`、`NineDays.Debug`；
- 现有 `NineDays.Editor` 引用所有运行时程序集；
- EditMode / PlayMode 测试程序集引用所有被测运行时程序集；
- 后续新增 `NineDays.Flow`（战斗流程编排，选弹门），依赖 `Battle + Game + UI + Input`，Battle 不反向引用 Flow/UI。

本次**不改玩法逻辑**，只改编译边界和两个必要的类型归属。

## 1. 程序集清单

| 程序集 | asmdef 路径 | 职责 |
|---|---|---|
| `NineDays.Input` | `Assets/Scripts/Input/NineDays.Input.asmdef` | 输入快照、门面、Unity 输入源、瞄准接收接口 |
| `NineDays.Settings` | `Assets/Scripts/Settings/NineDays.Settings.asmdef` | 玩家设置、存档、Applier |
| `NineDays.UI` | `Assets/Scripts/UI/NineDays.UI.asmdef` | UI 框架 |
| `NineDays.Game` | `Assets/Scripts/NineDays.Game.asmdef` | 玩法聚合层 |
| `NineDays.Battle` | `Assets/Scripts/Battle/NineDays.Battle.asmdef` | 回合/对局/战斗输入路由 |
| `NineDays.Flow` | `Assets/Scripts/Flow/NineDays.Flow.asmdef` | 选弹等高层流程编排 |
| `NineDays.Camera` | `Assets/Scripts/Camera/NineDays.Camera.asmdef` | 镜头控制 |
| `NineDays.Debug` | `Assets/Scripts/Debug/NineDays.Debug.asmdef` | 调试 HUD |
| `NineDays.Editor` | `Assets/Scripts/Editor/NineDays.Editor.asmdef` | 生成器、Bake、BuildCheck、菜单 |
| `NineDays.Tests` | `Assets/Tests/NineDays.Tests.asmdef` | EditMode 测试 |
| `NineDays.Tests.PlayMode` | `Assets/Tests/PlayMode/NineDays.Tests.PlayMode.asmdef` | PlayMode 测试 |

`Library/ScriptAssemblies` 中实际生成：

```text
NineDays.Game.dll
NineDays.Input.dll
NineDays.Settings.dll
NineDays.UI.dll
NineDays.Battle.dll
NineDays.Flow.dll
NineDays.Camera.dll
NineDays.Debug.dll
NineDays.Editor.dll
NineDays.Tests.dll
NineDays.Tests.PlayMode.dll
```

## 2. 解耦改动

### 2.1 移动 BattleInputController

- 从 `Assets/Scripts/Input/BattleInputController.cs` 移到 `Assets/Scripts/Battle/BattleInputController.cs`；
- `.meta` 一起移动，脚本 GUID 不变，场景/预制体引用不需要重建；
- 移动后 `NineDays.Input` 不再引用 `TurnController` / `CharacterLauncher`；
- `NineDays.Battle` 引用 `NineDays.Input` 和 `NineDays.Game`。

### 2.2 抽取 IBattleParticipant

- 从 `Battle/BattleSession.cs` 移到 `Character/IBattleParticipant.cs`；
- `Character` 不再为了接口反向依赖 `NineDays.Battle`；
- `BattleSession` 位于 `NineDays.Battle`，从 `NineDays.Game` 引用该接口；
- `ITurnActionRunner` 仍留在 Battle 程序集，因为它只由战斗驱动方实现/使用。

### 2.3 Game 聚合层

`NineDays.Game` 根 asmdef 仍覆盖未单独声明 asmdef 的目录：

- Character / Bullet / Burst / Scene / Config / Events / test；
- 引用 `NineDays.Input`（`CharacterLauncher : IAimInputReceiver`）；
- 保留 2D Animation / Common / VisualScripting 依赖；
- 不再直接引用 `Unity.InputSystem`（输入实现已在 `NineDays.Input`）。

## 3. 依赖与循环

### 3.1 已解决的依赖

| 原依赖 | 处理 |
|---|---|
| `Character -> Battle`（`IBattleParticipant`） | 接口移到 Character 程序集 |
| `Input -> Battle/Character`（`BattleInputController`） | 适配文件移到 Battle |
| `UI -> Game`（`UIRoot` 用 `GameInput`） | UI 只引用 Input |
| `Editor -> 单一 Game` | Editor 引用全部运行时程序集 |

### 3.2 保留在 Game 内的循环

这些是当前不细拆的原因：

| 循环 | 说明 |
|---|---|
| `GameEvents ↔ Character/Projectile/Burst/Arena` | 全局事件签名直接引用玩法类型 |
| `Projectile ↔ BurstRuntime` | 子弹触发爆发，爆发重新处理子弹命中 |
| `Character ↔ Projectile` | `CharacterLauncher` 发射子弹，子弹命中 Character |
| `Config ↔ Bullet/Burst/Scene` | 配置直接引用玩法类型 |

### 3.3 依赖规则

- `Input` / `Settings` 不引用任何玩法程序集；
- `UI` 只引用 `Input`；
- `Game` 只引用 `Input` 和 Unity 包，不引用 Battle/Camera/Debug/UI；
- `Battle` 单向引用 `Game` + `Input`；
- `Camera` / `Debug` 可以引用 `Game` + `Battle`；
- `Editor` 和 Tests 是最高层，引用所有需要的运行时程序集；
- 禁止新增反向引用，例如 `Game -> Camera`、`Input -> Battle`。

## 4. 编译与测试

### 4.1 编辑后检查

```bat
Unity.exe -batchmode -nographics -projectPath <项目> ^
  -runTests -testPlatform EditMode -testResults Temp\editmode.xml -logFile Temp\editmode.log
```

### 4.2 物理/场景测试

```bat
Unity.exe -batchmode -projectPath <项目> ^
  -runTests -testPlatform PlayMode -testResults Temp\playmode.xml -logFile Temp\playmode.log
```

### 4.3 结构校验

```bat
Unity.exe -batchmode -quit -projectPath <项目> ^
  -executeMethod NineDays.EditorTools.BuildCheck.ValidateAll ^
  -logFile Temp\check.log
```

本次实测：

| 项 | 结果 |
|---|---|
| EditMode | **90/90 Passed** |
| PlayMode | **44/44 Passed** |
| `BuildCheck` | 通过（历史记录；本次新增功能未重跑 BuildAllBatch） |
| `Library/ScriptAssemblies` | 生成 11 个 `NineDays.*.dll`（含 Flow） |

> 本次没有重新跑 `BuildAllBatch`，因为 asmdef 拆分不改资源；`BuildCheck` 已验证 Editor 程序集和场景结构。之前的内容配置由 `BuildAll` 重写后已恢复，见 `../input/Input_agent.md` 第 8 节。

## 5. 已知限制与后续

- `NineDays.Game` 仍是玩法大程序集，Character/Bullet/Burst/Scene 没有独立编译；
- 要拆 Character/Bullet/Burst，必须先重构 `GameEvents` / `Projectile ↔ Burst` / `CharacterLauncher` / Config 归属；
- `NineDays.Game` 根 asmdef 会包含所有未单独声明 asmdef 的子目录；新增模块目录时如果不想进 Game，必须在该目录放自己的 asmdef；
- asmdef 只建立编译边界，不自动建立命名空间边界；当前所有运行时仍使用 `namespace NineDays`；
- 没有新增 `asmref`，没有改变程序集 GUID 或脚本 GUID；
- `BattleInputController.cs` 的 `.meta` 随文件移动，TestArena 中的组件引用保持有效。

## 6. 接手检查清单

- [ ] 新增代码是否放在正确的 asmdef 目录；
- [ ] 是否引入了反向依赖（尤其 Input -> Battle、Game -> Camera）；
- [ ] 新程序集是否同步加入 Editor / Tests 引用；
- [ ] `Library/ScriptAssemblies` 是否生成对应 DLL；
- [ ] EditMode / PlayMode / `BuildCheck` 是否通过；
- [ ] 是否同步 `README.md`、`Input.md`、`Tooling.md` 的路径和程序集说明；
- [ ] 如要拆 Gameplay，是否先完成事件/接口契约重构。
