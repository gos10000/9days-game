# 程序集拆分（人读版）

> 一句话：**保留 `NineDays.Game` 作为玩法聚合程序集，拆出 Input / Settings / UI / Battle / Camera / Debug / Editor；先解决依赖方向，不强行细分 Character/Bullet/Burst。**
> Agent 记录见同目录 `Asmdef_agent.md`。

## 目录

1. [为什么运行时是 7 个程序集](#1-为什么运行时是-7-个程序集)
2. [程序集清单](#2-程序集清单)
3. [依赖方向](#3-依赖方向)
4. [Game 里为什么还有 Character/Bullet/Burst](#4-game-里为什么还有-characterbulletburst)
5. [新代码放哪里](#5-新代码放哪里)
6. [Editor 与 Tests](#6-editor-与-tests)
7. [验收](#7-验收)
8. [已知限制与后续](#8-已知限制与后续)

---

## 1. 为什么运行时是 7 个程序集

当前代码不是“每个目录都独立”就能编译，存在硬循环：

- `GameEvents` 引用 `Character` / `Projectile` / `Burst*` / `ArenaController`，这些模块又引用 `GameEvents`；
- `Character` 实现 `IBattleParticipant`，该接口原先在 `BattleSession.cs`，形成 `Character ↔ Battle`；
- `Projectile` 引用 `BurstDefinition/BurstRuntime`，`BurstRuntime` 又引用 `Projectile/ProjectileHit`；
- `ProjectileConfig` / `TerrainBreakProfile` 反向引用 Bullet / Burst / Scene 类型。

因此先把**能单向依赖的部分**拆出去，把仍强耦合的 Character/Bullet/Burst/Scene/Config/Events 留在一个 `NineDays.Game` 聚合程序集里。这样能建立真实编译边界，又不需要重写事件系统。

## 2. 程序集清单

| 程序集 | 目录 | 内容 | 主要依赖 |
|---|---|---|---|
| `NineDays.Input` | `Scripts/Input` | `InputSnapshot`、`GameInput`、`UnityInputSource`、`IAimInputReceiver` | `Unity.InputSystem` |
| `NineDays.Settings` | `Scripts/Settings` | 玩家设置、存档、Applier | 无 |
| `NineDays.UI` | `Scripts/UI` | UIRoot、页面栈、过渡、占位页 | `NineDays.Input`、`UnityEngine.UI` |
| `NineDays.Game` | `Scripts/` 根 | Character / Bullet / Burst / Scene / Config / Events / test | `NineDays.Input`、2D Animation |
| `NineDays.Battle` | `Scripts/Battle` | BattleController、TurnController、BattleSession、TurnQueue、BattleInputController | `NineDays.Game`、`NineDays.Input` |
| `NineDays.Camera` | `Scripts/Camera` | CameraRig、CameraDirector、BattleCameraController | `NineDays.Game`、`NineDays.Battle` |
| `NineDays.Debug` | `Scripts/Debug` | CombatDebugHud | `NineDays.Game`、`NineDays.Battle`、`NineDays.Input` |
| `NineDays.Editor` | `Scripts/Editor` | 生成器、Bake、BuildCheck、菜单 | 所有运行时程序集 |
| `NineDays.Tests` / `NineDays.Tests.PlayMode` | `Tests` | EditMode / PlayMode 测试 | 所有被测程序集 |

> `BattleInputController.cs` 已从 `Input/` 移到 `Battle/`，所以 `NineDays.Input` 不再反向依赖战斗或角色。

## 3. 依赖方向

```text
NineDays.Input        NineDays.Settings
      ↑                     ↑
      ├── NineDays.UI       │
      └── NineDays.Game ────┘
               ↑
          NineDays.Battle
             ↑       ↑
      NineDays.Camera  NineDays.Debug

NineDays.Editor → 所有运行时程序集
Tests → 被测运行时程序集
```

关键约束：

- `Input` 不引用 Battle / Character / UI；
- `Game` 可以引用 Input（`CharacterLauncher` 实现 `IAimInputReceiver`）；
- `Battle` 单向依赖 `Game` 和 `Input`；
- `Camera`、`Debug` 是高层的，可以引用 Battle / Game；
- `Editor` 只被 Editor 程序集使用，运行时不反向引用它。

## 4. Game 里为什么还有 Character/Bullet/Burst

`NineDays.Game` 仍是**玩法聚合层**，不是“一个目录一个程序集”。原因：

- `GameEvents` 是全局事件总线，事件签名直接引用 `Character`、`Projectile`、`BurstDefinition`、`ArenaController`；
- `Projectile` 和 `BurstRuntime` 互相引用；
- `CharacterLauncher` 同时依赖 Character 和 Projectile；
- `Config` 里 `ProjectileConfig` / `TerrainBreakProfile` 直接引用玩法类型。

在这些契约不改的情况下，强行把 Character/Bullet/Burst 拆成独立 asmdef 会产生编译循环。后续要拆，需要先做事件/接口重构，见第 8 节。

## 5. 新代码放哪里

| 新增内容 | 放哪里 |
|---|---|
| 输入设备、按键映射、输入快照 | `Scripts/Input` → `NineDays.Input` |
| 玩家设置、存档、设置 Applier | `Scripts/Settings` → `NineDays.Settings` |
| UI 页面、Canvas、页面路由 | `Scripts/UI` → `NineDays.UI` |
| 角色、子弹、爆发、地形、内容配置 | `Scripts/` 对应目录 → `NineDays.Game` |
| 回合、战斗会话、战斗输入路由 | `Scripts/Battle` → `NineDays.Battle` |
| 镜头行为 | `Scripts/Camera` → `NineDays.Camera` |
| 测试 HUD / 调试面板 | `Scripts/Debug` → `NineDays.Debug` |
| 资源生成、校验 | `Scripts/Editor` → `NineDays.Editor` |

如果新代码需要跨模块类型，优先：

1. 把稳定契约放在 `NineDays.Game` 或 `NineDays.Input`；
2. 让高层程序集引用低层程序集；
3. 不要新增反向引用（例如 Input 引用 Battle、Game 引用 Camera）。

## 6. Editor 与 Tests

- `NineDays.Editor` 已引用所有运行时程序集；
- EditMode / PlayMode 测试引用所有被测运行时程序集，不再只引用 `NineDays.Game`；
- 新增程序集后，必须同步更新这两个测试 asmdef，否则测试程序集会找不到类型；
- `BuildCheck` 仍然负责场景/资源结构校验，和 asmdef 编译边界是两件事。

## 7. 验收

```bat
Unity.exe -batchmode -nographics -projectPath <项目> ^
  -runTests -testPlatform EditMode -testResults Temp\editmode.xml -logFile Temp\editmode.log

Unity.exe -batchmode -projectPath <项目> ^
  -runTests -testPlatform PlayMode -testResults Temp\playmode.xml -logFile Temp\playmode.log

Unity.exe -batchmode -quit -projectPath <项目> ^
  -executeMethod NineDays.EditorTools.BuildCheck.ValidateAll ^
  -logFile Temp\check.log
```

当前结果：**EditMode 81/81、PlayMode 34/34、`BuildCheck` 通过**。

## 8. 已知限制与后续

当前 `NineDays.Game` 仍是玩法大程序集，完整细分需要：

- 拆分 `GameEvents`，按 Character / Bullet / Burst / Terrain / Battle 分事件，或把事件载荷改成接口/ID；
- 打破 `Projectile ↔ BurstRuntime`：合并为 `Combat`，或抽出接口；
- 打破 `Character ↔ Projectile`：把 `CharacterLauncher` 提到更高层，或引入投射物生成接口；
- 把配置按归属拆分：角色配置跟 Character、子弹配置跟 Bullet/Combat、地形配置跟 Scene；
- 之后再考虑 Character / Combat / Scene 各自 asmdef。

在这些契约重构完成前，不建议继续增加更细的 asmdef。
