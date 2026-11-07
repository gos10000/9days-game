# 用框架跑通游戏：方案 + 教程（人读版）

> 一句话：**先借现成的 `TestArena` 在 10 分钟内跑通「开始页 → 完整一局」；再把框架搬进美术战斗场景；最后按「抽选 → 搭建 → 发射 → 结算」补齐设计文档里的最小循环。**
> 本文件是方案与操作教程；实现记录、证据与风险见同目录 `GameFlow_agent.md`。
> 当前状态：**只有方案，尚未改任何代码/资产**。开工前请先看第 13 节「待确认问题」。

## 目录

1. [目标与验收](#1-目标与验收)
2. [现状盘点](#2-现状盘点)
3. [总体方案](#3-总体方案)
4. [回合相位设计](#4-回合相位设计)
5. [分阶段计划](#5-分阶段计划)
6. [教程 A：10 分钟跑通现有框架](#6-教程-a10-分钟跑通现有框架)
7. [教程 B：把框架接进美术战斗场景](#7-教程-b把框架接进美术战斗场景)
8. [教程 C：新增一个 UI 页面](#8-教程-c新增一个-ui-页面)
9. [教程 D：新增一种子弹](#9-教程-d新增一种子弹)
10. [教程 E：新增一种道具](#10-教程-e新增一种道具)
11. [参数与接口速查](#11-参数与接口速查)
12. [排错](#12-排错)
13. [待确认问题](#13-待确认问题)
14. [最终验收清单](#14-最终验收清单)

---

## 1. 目标与验收

“跑通”定义为一条能从头玩到尾的最小闭环：

```text
开始页投球
  → 加载战斗场景
  → 每回合抽 4 选 1（子弹 / 道具）
  → 选道具：网格摆放 + 完成
  → 瞄准发射（受场上的引力 / 斥力 / 重力影响）
  → 命中：扣血、击退、镜头反馈
  → 9 轮打满或一方血量归零
  → 结算页：胜者 / 平局 → 重开 或 返回开始页
```

MVP 验收标准：

| 项 | 标准 |
|---|---|
| 开始 | 开始页投球命中开始盒，能进入战斗场景；命中退出盒能退出 |
| 回合 | 每回合都能抽 4 选 1；未选择前不能发射 |
| 搭建 | 选道具后能放置、撤销、完成；放置时能看到作用范围 |
| 发射 | 拖拽手感与现在一致；不同子弹有可见差异 |
| 结算 | 血量归零或 9 轮结束能出胜负；能重开 |
| 稳定 | EditMode / PlayMode 测试全绿，`BuildCheck` 通过 |

---

## 2. 现状盘点

### 2.1 已经能用的（框架资产）

| 能力 | 状态 | 位置 |
|---|---|---|
| 角色状态机、拖拽瞄准、发射、受击/击退 | ✅ | `Scripts/Character/*` |
| 子弹原型、特性、爆发、对象池 | ✅ | `Scripts/Bullet/*`、`Scripts/Burst/*` |
| 回合、血量、9 轮、先手、额外行动 | ✅ | `Scripts/Battle/BattleSession.cs`（纯逻辑）+ `BattleController.cs` |
| 镜头跟随 / 取景 / 震动 | ✅ | `Scripts/Camera/*` |
| UI 页面栈 / 弹窗 / 过渡 / Canvas 五层 | ✅ | `Scripts/UI/Framework/*` |
| 分层遮罩地形、破坏、追加 | ✅ | `Scripts/Scene/*` |
| 输入门面与战斗输入路由 | ✅ | `Scripts/Input/*`、`Scripts/Battle/BattleInputController.cs` |
| 一套完整接好框架的测试场景 | ✅ | `Assets/Scenes/TestArena.unity`（由 `ArenaBuilder` 生成） |

`TestArena` 里已经有：`BattleController` + `TurnController` + `ArenaController` + `CameraRig/CameraDirector/BattleCameraController` + `BattleInputController` + `ProjectilePool` + `UIRoot` + `CombatDebugHud`。这是目前唯一能直接打出「9 轮 + 血量 + 胜负」的场景。

### 2.2 会卡住“跑通”的问题

| 问题 | 现象 | 证据 |
|---|---|---|
| 开始页命中开始盒后打开的是占位页 | 投球进 `Box_Start` 后弹出 `Placeholder` 页面，而不是进战斗 | `Playmode Scene` 的 `StartScene.nextPageId = Placeholder`；`UIRoot` 用 `UIPageCatalog_Default`，其中 `Placeholder` 有 prefab，所以页面切换成功，不会走场景回退 |
| 美术战斗场景没有对局驱动 | `Test_BattleScene` 打完不会结算，回合会一直轮流 | 场景里只有 `TurnController` + 两个角色；没有 `BattleController` / `ArenaController` / 镜头 / 池 / UI |
| 场景没有加入 Build Settings | 打包后进不去真正想用的场景 | Build Settings 只启用了 `Test_StartScene`、`Test_BattleScene`；`Playmode Scene` 与 `TestArena` 都没启用 |
| 旧开始页脚本挂反、缺控制器 | 编辑器里投球进盒没反应 / 退出盒反而开始 | `Test_StartScene`：`Box_Exit` 挂的是 `StartBoxTrigger`，`Box_Start` 挂的是 `ExitBoxTrigger`，且场景没有 `StartScene` |
| 子弹不能按回合切换 | 抽到不同子弹也打同一种 | `CharacterLauncher.projectilePrefab` 固定；`ShotSpawner.Spawn` 只读 `prefab.Config` |
| 抽选 / 搭建不存在 | 没有抽卡页、没有网格放置、没有道具数据 | `Scripts/` 下无对应脚本 |
| 结算只有文字 | 没有结算页、没有重开按钮 | 只有 `TurnBanner` 的 `OnGUI` 文字 |
| 音频未接 | 进度表里仍是未完成 | `Docs/program/summary.md` |

### 2.3 场景关系（重要）

```text
Test_StartScene（Build 第 0 个）
  └─ 旧开始页：盒子脚本挂反、没有 StartScene → 目前不能正确开始

Playmode Scene（未加入 Build Settings）
  └─ 新开始页：StartScene + UIRoot + Box_Start/Box_Exit + 未激活的 BattleController
       └─ 命中开始盒 → 目前打开 Placeholder 页（不是战斗）

Test_BattleScene（Build 第 1 个）
  └─ 美术战斗场景：只有 TurnController + 两个角色 → 能互相打，但不会结算

TestArena（未加入 Build Settings，生成物）
  └─ 框架验收场景：全模块接好 → 能打完整一局
```

**结论：先用 `TestArena` 验证框架，再把框架搬进 `Test_BattleScene`，最后接 `Playmode Scene` 作为正式入口。**

---

## 3. 总体方案

### 3.1 分层

```text
Flow（新增，最高层）
  对局相位：抽选 → 搭建 → 发射 → 结算
  依赖 Battle / Game / UI / Input
        ↓
Battle（已有）
  BattleSession（纯逻辑）+ BattleController + TurnController + BattleInputController
        ↓
Game（已有）
  Character / Projectile / Burst / Prop / Events / Config / Scene
        ↓
Input / Settings / UI（已有基础层）
```

> 依赖只能向下。`Flow` 可以引用 `Battle`、`Game`、`UI`；反过来不行。
> 如果不想新增 asmdef，也可以把相位代码放进 `Scripts/Battle`，但页面交互会变绕；推荐新增 `NineDays.Flow`。

### 3.2 新增模块清单

| 模块 | 位置（建议） | 职责 |
|---|---|---|
| `ITurnPhaseGate` | `Scripts/Battle/` | “回合前相位”的钩子接口 |
| `TurnFlowController` | `Scripts/Flow/` | 驱动 `抽选 → 搭建 → 发射`，实现 `ITurnPhaseGate` |
| `TurnLoadout` | `Scripts/Flow/` | 本回合选中的子弹 / 道具 |
| `DrawService` | `Scripts/Flow/` | 从混合池抽 4 张、去重 |
| `PropPlacementController` | `Scripts/Flow/` | 网格吸附、合法性校验、幽灵预览、范围预览 |
| `MatchFlowBootstrap` | `Scripts/Flow/` | 场景入口：注册 phase gate、订阅 `BattleEnded`、开结算页 |
| `PropDefinition` / `PropCatalog` | `Scripts/Props/` | 道具数据（展示名、图标、prefab、放置规则） |
| `GravityZoneField` | `Scripts/Bullet/` | 区域重力（反转 / 加重），`ProjectileField` 子类 |
| `DrawPage` / `PlacementPage` / `BattleHudPage` / `ResultPage` | `Scripts/UI/Pages/` | 正式页面 |
| `TrajectoryPreview` / `ShotHistoryTracker` | `Scripts/Bullet/` | 预计弹道 / 上一发完整轨迹 |

### 3.3 三个关键设计决定

**决定 1：不改 `BattleSession`，在 `BattleController.BeginTurn` 前插“相位门”。**
`BattleSession` 是纯逻辑，现有 EditMode / PlayMode 测试直接测它。抽选和搭建属于表现层相位，塞进 `BattleSession` 会破坏测试与职责边界。

**决定 2：子弹“单 prefab + 运行时配置注入”。**
`Projectile` 已经是“原型脚本 + 配置差异”的结构，但 `ShotSpawner` 只读 `prefab.Config`，`CharacterLauncher` 只认 prefab。加一个带 `ProjectileConfig` 参数的重载即可，不需要为每种子弹复制一套逻辑。

**决定 3：页面只收纯数据 + 回调。**
`NineDays.UI` 不引用 `Game` / `Battle`，所以页面参数里不能出现 `Team`、`ProjectileConfig` 等类型。用 `UIPageArgs` 子类传字符串 / 图标 / `Action<int>`，由 `Flow` 负责把选择结果翻译成玩法数据。

### 3.4 Flow 接口草案

| 类 | 接口 | 入参 / 返回 | 说明 |
|---|---|---|---|
| `TurnFlowController` | `Run(Team actor, Action onReady)` | 行动方、就绪回调 | 实现 `ITurnPhaseGate`；内部跑抽选/搭建，完成后调 `onReady` |
| | `OnTurnFinished(in TurnResult result)` | 结算结果 | 清幽灵、关页面、重置 loadout |
| | `Cancel()` | 无 | 场景卸载 / 重开时调用，保证 `onReady` 不会悬挂 |
| `DrawService` | `Draw(ItemPoolConfig pool, int count, Random rng)` | 池、数量、随机源 → `IReadOnlyList<ItemDefinition>` | 纯逻辑；数量不足时按可用数量返回 |
| `TurnLoadout` | `Bullet` / `Prop` / `Kind` | `ProjectileConfig` / `PropDefinition` / 枚举 | 本回合选择结果 |
| `PropPlacementController` | `Begin(PropDefinition prop, Action onComplete)` | 道具、完成回调 | 进入网格放置；完成后调 `onComplete` |
| | `Undo()` / `Cancel()` | 无 | 撤销 / 退出搭建 |
| `MatchFlowBootstrap` | `BattleController battle` / `TurnFlowController flow` / `UIRoot uiRoot` | Inspector 引用 | `Start` 时 `battle.SetPhaseGate(flow)`；订阅 `BattleEnded` |

> 这些是接口草案，落地时可以按实际需要增删；关键是保持“Flow 依赖下层，下层不依赖 Flow”。

---

## 4. 回合相位设计

### 4.1 时序

```text
BattleSession.NextTurn()
  → BattleController.BeginTurn(actor, onResolved)
      → phaseGate.Run(actor, onReady)
          → DrawPhase    抽 4 选 1（页面）
          → BuildPhase   选到道具才进入（网格摆放 + 完成）
          → onReady
              → TurnController.BeginTurn(actor)   // 角色 CanAct=true
                  → 玩家瞄准 / 发射
                      → TurnController.TurnResolved
                          → BattleController.HandleTurnControllerResolved
                              → phaseGate.OnTurnFinished(result)（清临时状态）
                              → onResolved
                                  → BattleSession 继续下一动 / 下一轮
```

### 4.2 相位状态

| 相位 | 进入条件 | 玩家能做什么 | 退出条件 |
|---|---|---|---|
| `Draw` | 每个行动者回合开始 | 点 4 张卡之一 | 选中一张 |
| `Build` | 选中的是道具 | 左键放置、右键撤销、点“完成” | 点“完成”或道具用完 |
| `Aim` | 抽选 / 搭建结束 | 拖拽瞄准、松开发射、右键取消 | 发射并结算 |
| `Resolved` | `TurnResolved` | 无 | 下一动 / 下一轮 / 战斗结束 |

约束：

- `Build` 与 `Draw` 期间把 `BattleInputController.InputEnabled` 设为 `false`，避免误触发射；
- `Aim` 开始时才给角色 `CanAct`，保证“没选完不能打”；
- 场景卸载 / 重开时 `phaseGate` 必须能取消，否则 `BattleController.pendingCallback` 会悬挂。

### 4.3 最小接口

```csharp
// Scripts/Battle/ITurnPhaseGate.cs
using System;

namespace NineDays
{
    /// <summary>回合前相位门：抽选/搭建完成后才真正开始可操作回合。</summary>
    public interface ITurnPhaseGate
    {
        /// <summary>run 内部完成后必须调用 onReady，否则本回合会停住。</summary>
        void Run(Team actor, Action onReady);

        /// <summary>一次行动结算完成，清理本回合临时状态。</summary>
        void OnTurnFinished(in TurnResult result);
    }
}
```

`BattleController` 只加三处：

```csharp
private ITurnPhaseGate phaseGate;

public void SetPhaseGate(ITurnPhaseGate gate) => phaseGate = gate;

public void BeginTurn(Team actor, Action<TurnResult> onResolved)
{
    pendingCallback = onResolved;

    if (phaseGate != null) phaseGate.Run(actor, () => turnController.BeginTurn(actor));
    else turnController.BeginTurn(actor);
}

private void HandleTurnControllerResolved(TurnResult result)
{
    Action<TurnResult> callback = pendingCallback;
    pendingCallback = null;
    phaseGate?.OnTurnFinished(result);
    callback?.Invoke(result);
}
```

> 现有 `BattleController.BeginTurn` / `HandleTurnControllerResolved` 就在这两个位置，改动很小；`TurnController`、`BattleSession` 不动。

---

## 5. 分阶段计划

| 阶段 | 目标 | 预估 | 主要改动 |
|---|---|---|---|
| P0 | 基线可复现 | 0.5h | 无代码 |
| P1 | 用 `TestArena` 跑通「开始页 → 完整一局」 | 0.5h | 场景字段、Build Settings |
| P2 | 把框架接进 `Test_BattleScene` | 0.5–1d | 场景组件 |
| P3 | 回合前抽 4 选 1 | 1–2d | `ITurnPhaseGate`、`Flow`、`DrawPage` |
| P4 | 道具网格搭建 | 2–3d | `PropDefinition`、`PropPlacementController`、道具 prefab |
| P5 | 子弹差异化 + 弹道预览 | 2–3d | `ShotSpawner` 重载、特性、预览/历史 |
| P6 | HUD / 结算 / 入口 / 打磨 | 2–3d | `BattleHudPage`、`ResultPage`、场景与 Build Settings |

> 总预估按 1 人全职约 8–12 天。P0/P1 当天就能看到完整一局；P2 之后才动正式资产。

### P0 基线（0.5h）

1. 关闭 Unity Editor（batchmode 需要，`Temp/UnityLockfile` 存在时 Unity 会直接退出）。
2. 跑全量生成与结构校验（命令见第 6.2 节），确认日志末尾 `BUILD_OK`。
3. 跑 EditMode / PlayMode，确认基线仍是 **81/81、34/34**。
4. Play `Assets/Scenes/TestArena.unity`，关掉 `Placeholder` 页，打一发球、按 `T` 看自动对射。

验收：`BUILD_OK`；测试全绿；TestArena 能发球、扣血、出胜负。

### P1 端到端（0.5h）

1. 先把 `TestArena` 加进 Build Settings 并勾选（`SceneManager.LoadScene` 要求）。
2. 打开 `Assets/Scenes/Playmode Scene.unity`。
3. 选中 `StartPage` → `StartScene`：`nextPageId` 清空，`battleSceneName` 改为 `TestArena`。
4. Play，投球进 `Box_Start`，确认淡出后进入 `TestArena`。
5. 打完整一局，确认 HUD、回合横幅、胜负。
6. 把 `Playmode Scene` 加入 Build Settings 第 0 位（这一步是临时验证；P6 再定正式入口）。

验收：开始页 → 战斗 → 9 轮 → 胜负，全程不需要手动切场景。

> `TestArena` 是生成物，`9Days/Build/Test Arena` 会覆盖它。P1 只用来证明“框架链路是通的”，不要在这上面做美术。

### P2 战斗场景接入（0.5–1d）

1. 按第 7 节把 `Test_BattleScene` 补齐组件。
2. 保留现有美术墙（Layer = `Obstacle`），`ArenaBounds.createInvisibleWalls = false`。
3. 不挂 `ArenaController`（静态美术地形），`BattleController.arena` 留空。
4. Play 验证：回合、血量、镜头、发射、结算。
5. 建议加一个幂等的编辑器菜单 `9Days/Setup/Battle Scene Framework`，以后新战斗场景一键接线（可选）。

验收：`Test_BattleScene` 与 `TestArena` 行为一致，但使用美术场景。

### P3 回合前抽选（1–2d）

数据：

- `ItemDefinition`（子弹 / 道具两种）：展示名、图标、描述、对应 `ProjectileConfig` 或 `PropDefinition`；
- `ItemPoolConfig`：子弹列表 + 道具列表、每次抽几张（默认 4）、是否允许重复。

代码：

- `ITurnPhaseGate` + `BattleController` 三处改动（第 4.3 节）；
- `NineDays.Flow` 程序集 + `TurnFlowController`；
- `DrawService`（抽 4、去重，纯逻辑，可 EditMode 测试）；
- `DrawPage`（UIPage）+ `DrawPageArgs`（纯数据 + `Action<int>`）。

验收：

- 每个行动者回合开始弹 4 张卡；
- 未选择前角色 `CanAct = false`、不能发射；
- 选择子弹后直接进入瞄准；选择道具后进入搭建；
- EditMode 覆盖抽卡去重、数量不足、空池。

### P4 道具网格搭建（2–3d）

数据与资产：

- `PropDefinition`（id、展示名、图标、prefab、放置规则、最大数量、作用范围）；
- `PropCatalog`；
- 黑洞 / 白洞 prefab 加 `CircleCollider2D (Is Trigger)` + `RadialGravityField`；
- 重力反转 / 加重用新的 `GravityZoneField`；
- 反重力障碍用实体 `Collider2D`，Layer = `Obstacle`。

代码：

- `PropPlacementController`：网格吸附、越界/重叠校验、地面检测、幽灵预览、范围预览、左键放置、右键撤销；
- `PlacementPage`：剩余数量、撤销、完成；
- 战斗开始 / 重开时清空本局放置的道具。

验收：

- 放置时能看到作用范围；
- 不允许放到角色身上、场外、非法格；
- 完成后才能瞄准；
- 子弹经过力场会明显改道；
- `RestartBattle` 后场上道具清空。

### P5 子弹差异化 + 弹道（2–3d）

- `ShotSpawner` 增加带 `ProjectileConfig` 的重载；
- `CharacterLauncher` 增加 `SetAmmo(ProjectileConfig)`；
- 6 种子弹按第 9 节建配置 / prefab；
- `Projectile.ignoreFields` + `ProjectileField` 跳过（引力子弹）；
- `DemolishTrait`（拆除沿途道具 / 地形，无伤害）；
- `TracerTrait`（记录完整轨迹）；
- `TrajectoryPreview`（预计弹道前一小段）；
- `ShotHistoryTracker`（上一发完整轨迹）。

验收：6 种子弹各有可见差异；预览与历史轨迹可开关；专项测试覆盖构建管线与特性。

### P6 HUD / 结算 / 入口 / 打磨（2–3d）

- `BattleHudPage`：回合、行动方、双方血条、轮次；
- `ResultPage`：胜者 / 平局、重开、返回开始页；
- `MatchFlowBootstrap`：`BattleEnded` → 开结算页；`BattleStarted` → 清道具、重置 HUD；
- 镜头死区、命中顿帧、震屏参数调到 Inspector；
- 开始页正式化：`StartPage` 作为 `UIPage` 注册，或保留场景回退；修正 `Test_StartScene` 盒子脚本；
- Build Settings：入口场景第 0 位、战斗场景第 1 位；
- 音频（可选）、地形破坏（可选）。

验收：第 14 节清单全部通过。

---

## 6. 教程 A：10 分钟跑通现有框架

### 6.1 准备

| 项 | 值 |
|---|---|
| Unity 版本 | `6000.0.83f1`（`ProjectSettings/ProjectVersion.txt`） |
| Unity 路径 | `C:\Program Files\Unity\Hub\Editor\6000.0.83f1\Editor\Unity.exe` |
| 工程路径 | `C:\Users\19314\Desktop\此电脑\program\Project\9days-game` |
| 入口场景（临时） | `Assets/Scenes/Playmode Scene.unity` |
| 战斗场景（临时） | `Assets/Scenes/TestArena.unity` |

> 跑 batchmode 前必须先关掉 Unity；工程被 `Temp/UnityLockfile` 锁住时 Unity 会直接退出，日志里看不出原因。

### 6.2 命令行基线（可选，但建议每次大改前后都跑）

```powershell
$unity   = 'C:\Program Files\Unity\Hub\Editor\6000.0.83f1\Editor\Unity.exe'
$project = 'C:\Users\19314\Desktop\此电脑\program\Project\9days-game'

# 全量生成：Layer、prefab、配置、测试场景、结构校验
& $unity -batchmode -quit -projectPath $project `
  -executeMethod NineDays.EditorTools.BuildPipeline.BuildAllBatch `
  -logFile "$project\Temp\build.log"

# EditMode（纯逻辑，秒级）
& $unity -batchmode -nographics -projectPath $project `
  -runTests -testPlatform EditMode `
  -testResults "$project\Temp\editmode.xml" -logFile "$project\Temp\editmode.log"

# PlayMode（物理集成，分钟级）
& $unity -batchmode -projectPath $project `
  -runTests -testPlatform PlayMode `
  -testResults "$project\Temp\playmode.xml" -logFile "$project\Temp\playmode.log"
```

成功标志：

- `Temp/build.log` 末尾 `BUILD_OK`（失败是 `BUILD_FAILED` + 堆栈）；
- 测试结果文件里 `failed="0"`，当前基线 81 / 34。

> 只改数值（SO）时不要跑 `Build/Prefabs`，生成器会重写部分配置字段。

### 6.3 打开工程

1. Unity Hub → Add → 选择工程目录 → 用 `6000.0.83f1` 打开。
2. 首次打开会导入资源；等 Console 不再刷导入日志。
3. 打开 `Assets/Scenes/TestArena.unity`，点 Play。

### 6.4 在 TestArena 里打一局

1. 屏幕上会出现 `Placeholder` 页，点 `CLOSE` 关掉（`UIRoot.startupPageId = Placeholder` 是生成器设的）。
2. 鼠标左键拖拽 → 松开，发射。
3. 观察：TurnBanner 显示轮次/行动方；HUD 显示 HP、回合、日志。
4. 调试键：

| 键 | 作用 |
|---|---|
| 左键拖拽 / 右键 / Esc | 瞄准 / 取消瞄准 |
| 空格 | 当前角色直接朝对手发球 |
| `1` / `2` | 强制切到 P1 / P2 回合 |
| `R` | 重开（角色回出生点、清子弹、地形重建） |
| `F1` | 冲量详细日志 |
| `T` / `F2` | 自动对射 20 次 + 统计 |
| `G` / `F3` | 慢动作 0.2x / 1x |
| `E` | 给当前方追加一次立即行动（验证额外行动接口） |

> F 键会被 Scene / Hierarchy 视图吞掉；先点一下 Game 视图，或用 `T` / `G` 别名。

5. 打满 9 轮或把一方打到 0 血，确认出现 `WINNER` / `DRAW`。

### 6.5 把开始页接到战斗

> `SceneManager.LoadScene` 的目标场景**必须在 Build Settings 里**；只在编辑器打开过不算。先把 `TestArena` 加进去（勾选 enabled），再做下面的步骤。

1. 打开 `File → Build Profiles`（或 `Build Settings`），把 `Assets/Scenes/TestArena.unity` 加进去并勾选。
2. 打开 `Assets/Scenes/Playmode Scene.unity`。
3. Hierarchy 选中 `StartPage`。
4. Inspector → `StartScene`：
   - `Next Page Id` **清空**（否则会打开 `Placeholder` 页，不会加载场景）；
   - `Battle Scene Name` 改为 `TestArena`（临时；P2 之后改回 `Test_BattleScene`）。
5. Play。
6. 拖拽角色投球，命中 `Box_Start`：
   - 期望：页面淡出 → 加载 `TestArena`；
   - 如果弹出 `Placeholder`：`nextPageId` 没清空，或 `UIRoot` 的 catalog 里还有同名页面；
   - 如果 Console 报 `Scene 'TestArena' couldn't be loaded`：场景没加进 Build Settings。
7. 命中 `Box_Exit`：期望退出 Play（编辑器内停止）。

### 6.6 加入 Build Settings（打包验证）

1. `File → Build Profiles`（或 `Build Settings`）。
2. 确认 `TestArena` 已启用。
3. 把 `Playmode Scene` 加为第 0 个（编辑器里直接 Play 它不需要在 Build Settings，但打包入口需要）。
4. 取消勾选 `Test_StartScene`（旧开始页，盒子脚本挂反且缺 `StartScene`；P6 再修）。
5. Build 一次，确认打包后能从开始页进战斗。

> P6 会把入口换成正式场景；现在只是验证链路。

---

## 7. 教程 B：把框架接进美术战斗场景

目标：让 `Test_BattleScene` 具备和 `TestArena` 一样的对局能力，但保留现有美术墙与角色摆位。

### 7.1 先备份

- 建议先提交一次 git 或复制一份 `Test_BattleScene.unity`；
- 下面只加组件，不改美术墙、不改角色 prefab。

### 7.2 场景现状（已核对）

| 对象 | 现状 | 处理 |
|---|---|---|
| `Main Camera` | 有 Camera，无镜头组件 | 加 `CameraRig` / `CameraDirector` / `BattleCameraController` |
| `Character_P1` / `P2` | 预制体实例，位置约 `(-5.66, -1.26)` / `(6.70, -1.18)` | 不动 |
| `WallLeft/Right/Up/Up(1)` | Layer = `Obstacle`（10） | 不动 |
| `Square` / `Environment` / 其他美术物体 | 美术/碰撞 | 不动 |
| `TurnController` | 只有它，`autoStart=true`、`autoAdvance=true` | 改字段，交给 `BattleController` 驱动 |
| `BattleController` / `ArenaController` / `ProjectilePool` / `UIRoot` | 没有 | 新建 |

### 7.3 接线步骤

**第 1 步：场地边界**

1. 新建空物体 `ArenaBounds`。
2. 挂 `ArenaBounds`，按现有墙的内沿填 `Play Area`（先用 Gizmos 对着墙量）。
   - 参考：左墙 x≈-9.09、右墙 x≈9.06、上墙 y≈5.21、下墙 y≈-3.52；
   - 建议先填 `Rect(-8.5, -3.4, 17.0, 8.2)`，再按 Gizmos 微调。
3. `Create Invisible Walls` **取消勾选**（美术墙已经有 Collider）。
4. `Obstacle Layer Name = Obstacle`、`Recover Out Of Bounds = true`、`Check Interval = 0.2`。

> 如果不取消勾选，会在边界外再生成四堵空气墙；不致命，但会和美术墙重复。

**第 2 步：镜头**

选中 `Main Camera`，加三个组件：

| 组件 | 字段 | 值 |
|---|---|---|
| `CameraRig` | `Target Camera` | 自身 Camera |
| | `Position Smooth Time` / `Size Smooth Time` | 0.25 / 0.35 |
| `CameraDirector` | `Rig` | 上面的 `CameraRig` |
| | `Bounds` | 第 1 步的 `ArenaBounds` |
| | `Min/Max Orthographic Size` | 4.5 / 7 |
| | `Framing Padding` / `Overview Padding` | 1 / 0.5 |
| `BattleCameraController` | `Director` | 上面的 `CameraDirector` |
| | `Turn Controller` | 场景里的 `TurnController` |
| | `Characters` | `Character_P1`、`Character_P2` |
| | `Follow Projectiles` | 勾选 |

> `CameraDirector` 没有 `Bounds` 时不会更新姿态；`Main Camera` 的 Tag 必须是 `MainCamera`。

**第 3 步：回合控制器**

选中 `TurnController`：

| 字段 | 值 |
|---|---|
| `Characters` | `Character_P1`、`Character_P2` |
| `First Team` | `Player1` |
| `Auto Start` | **取消勾选** |
| `Auto Advance` | **取消勾选** |
| `Resolve Timeout` | 3 |
| `Repeat When Opponent Missing` | 取消勾选 |

> `BattleController.Awake` 会调用 `UseExternalDriver()` 强制关掉自动开局/自动换人；但场景里先把字段设对，避免 Start 顺序问题。

**第 4 步：对局控制器**

新建空物体 `BattleController`，挂 `BattleController`：

| 字段 | 值 |
|---|---|
| `Config` | `Assets/Data/Battle/BattleConfig_Default.asset` |
| `Turn Controller` | 第 3 步的 `TurnController` |
| `Arena` | **留空**（当前是静态美术地形） |
| `Characters` | `Character_P1`、`Character_P2` |
| `Auto Start` | 勾选 |
| `Show Turn Banner` | 勾选 |

> 如果要改回程序地形：加 `ArenaController` 并赋 `ArenaConfig`，同时移除静态地形；两者不要同时存在。
> `BattleController` 至少需要两名角色，否则 `BeginBattle` 会报错并停止。

**第 5 步：输入**

新建空物体 `BattleInputController`，挂 `BattleInputController`：

| 字段 | 值 |
|---|---|
| `Turn Controller` | 第 3 步的 `TurnController` |
| `Enable Gameplay Input` | 勾选 |

**第 6 步：子弹池**

新建空物体 `ProjectilePool`，挂 `ProjectilePool`：

| 字段 | 值 |
|---|---|
| `Prewarm Prefab` | `Assets/Prefabs/Projectiles/Bullet/TestBall.prefab`（角色实际引用的那个） |
| `Prewarm Count` | 8 |
| `Default Capacity` | 8 |
| `Max Size Per Prefab` | 64 |
| `Dont Destroy On Load` | 取消勾选 |

> 场景没有池也能跑（`ProjectilePool.Instance` 会自动建），但每发都会实例化，第一发会卡。

**第 7 步：UI**

1. 把 `Assets/Prefabs/UI/UIRoot.prefab` 拖进场景。
2. `Startup Page Id` **留空**（战斗场景不要自动弹占位页）。
3. `Catalog` 用 `Assets/Data/UI/UIPageCatalog_Default.asset`，先把正式 HUD 页加进去（P6）。

**第 8 步：调试 HUD（临时）**

新建空物体 `DebugHUD`，挂 `CombatDebugHud`：

| 字段 | 值 |
|---|---|
| `Turn Controller` | 第 3 步的 `TurnController` |
| `Battle Controller` | 第 4 步的 `BattleController` |
| `Test Projectile Prefab` | `TestBall.prefab` 的 `Projectile` 组件 |
| `Show Hud` | 勾选 |
| `Verbose Impact Log` | 取消勾选 |

> 正式 HUD 做好后删掉它；它只是 `OnGUI` 调试面板。

### 7.4 验收

Play 后按顺序检查：

1. 屏幕中央出现 `BATTLE START`，随后 `第 1 / 9 轮`；
2. 只有当前行动方可以拖拽，另一方拖拽无反应；
3. 发射后镜头跟随子弹；命中后闪白、击退、震屏；
4. HUD 的 HP 下降；
5. 打满 9 轮或一方 0 血，出现 `WINNER` / `DRAW`；
6. 按 `R` 能重开，角色回出生点、HP 回满。

如果 1 不出现：检查 `BattleController` 是否激活、`Config` 是否赋值、`TurnController` 是否在 `Characters` 里。
如果 3 不出现：检查 `CameraDirector.Bounds` 是否赋值、`BattleCameraController.Turn Controller` 是否赋值。

---

## 8. 教程 C：新增一个 UI 页面

以“抽选页”为例。页面只负责显示与回调，不引用 `Team`、`ProjectileConfig` 等玩法类型。

### 8.1 页面 prefab 结构

```text
DrawPage (RectTransform)
 ├─ CanvasGroup
 ├─ DrawPage (UIPage 子类)
 ├─ Background
 ├─ Title (Text)
 └─ Cards
      ├─ Card_0 (Button + Image + Text)
      ├─ Card_1
      ├─ Card_2
      └─ Card_3
```

要求（`BuildCheck` / `UIPageRouter` 会检查）：

- 根节点是 `RectTransform`；
- 根节点有 `CanvasGroup`；
- 根节点有 `UIPage` 子类；
- 不要加 `Canvas` / `GraphicRaycaster`，用 `UIRoot` 的根 Canvas。

### 8.2 页面脚本

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NineDays
{
    /// <summary>抽选页参数：只带展示数据与回调，不带玩法类型。</summary>
    public sealed class DrawPageArgs : UIPageArgs
    {
        public string Title;
        public IReadOnlyList<DrawCardView> Cards;
        public Action<int> OnPick;
    }

    /// <summary>一张卡的纯展示数据。</summary>
    public readonly struct DrawCardView
    {
        public readonly string Title;
        public readonly string Description;
        public readonly Sprite Icon;

        public DrawCardView(string title, string description, Sprite icon)
        {
            Title = title;
            Description = description;
            Icon = icon;
        }
    }

    /// <summary>抽选页：显示 4 张卡，点击后用下标回调。</summary>
    public sealed class DrawPage : UIPage
    {
        [SerializeField] private Text titleText;
        [SerializeField] private Button[] cardButtons;
        [SerializeField] private Image[] cardIcons;
        [SerializeField] private Text[] cardTitles;

        private Action<int> onPick;

        public override void OnShow(UIPageArgs args)
        {
            if (args is not DrawPageArgs drawArgs) return;

            onPick = drawArgs.OnPick;
            if (titleText != null) titleText.text = drawArgs.Title;

            for (int i = 0; i < cardButtons.Length; i++)
            {
                bool hasCard = drawArgs.Cards != null && i < drawArgs.Cards.Count;
                cardButtons[i].gameObject.SetActive(hasCard);
                if (!hasCard) continue;

                DrawCardView card = drawArgs.Cards[i];
                if (cardIcons[i] != null) cardIcons[i].sprite = card.Icon;
                if (cardTitles[i] != null) cardTitles[i].text = card.Title;

                int index = i;
                cardButtons[i].onClick.RemoveAllListeners();
                cardButtons[i].onClick.AddListener(() => Pick(index));
            }
        }

        private void Pick(int index)
        {
            Action<int> callback = onPick;
            onPick = null;
            Close();
            callback?.Invoke(index);
        }

        public override void OnHide()
        {
            onPick = null;
        }
    }
}
```

### 8.3 注册页面

1. `Assets/Prefabs/UI/Pages/` 下建 `DrawPage.prefab`，挂好 `CanvasGroup` + `DrawPage`，把 4 个 Button/Image/Text 拖进字段。
2. 菜单 `Create > 9Days > UI > Page Config`，生成 `Assets/Data/UI/Pages/DrawPage.asset`：
   - `pageId = Draw`
   - `prefab = DrawPage.prefab`
   - `layer = Screen`
   - `cachePolicy = KeepAlive`
   - `closeOnBack = false`（抽选阶段不允许 Esc 跳过）
   - `transition` 可留空
3. 把 `DrawPage.asset` 加进 `UIPageCatalog_Default.assets` 的 `pages` 列表。
4. 确认场景里的 `UIRoot.catalog` 指向它。

### 8.4 打开与关闭

```csharp
UIPageHandle handle = UIRoot.Instance.Router.ShowScreen(
    "Draw",
    new DrawPageArgs
    {
        Title = "选择本回合的物品",
        Cards = views,
        OnPick = index => OnCardPicked(index),
    });
```

- 页面内部用 `Close()` 关闭自己；
- `ShowScreen` 会隐藏当前页并压栈，`ReplaceScreen` 不保留历史；
- 过渡进行中新的切换会被拒绝（`Router.IsTransitioning`）。

> UI 程序集不引用 Game/Battle，所以页面里不能写 `Team` / `ProjectileConfig`；这些数据留在 Flow 层。

---

## 9. 教程 D：新增一种子弹

### 9.1 最快路径（只换数值，不改代码）

1. `Create > 9Days > 配置 > 子弹配置`，生成 `Assets/Data/Projectiles/Bullet_Blast.asset`。
2. 改字段：
   - `mass` / `radius` / `initialSpeed` / `lifeTime`；
   - `damage`（强化子弹填 20）；
   - `gravityScale`（0 = 直线弹）；
   - `destroyOnHit` / `friendlyFire`；
   - `traits` / `bursts`。
3. 复制角色实际引用的 `Assets/Prefabs/Projectiles/Bullet/TestBall.prefab`，命名 `Bullet_Blast.prefab`。
4. 把新 prefab 上 `Projectile.config` 指向第 1 步的资产，替换 Sprite。
5. 要让角色发射它：把 `CharacterLauncher.projectilePrefab` 换成新 prefab（只能全局换，不能按回合换）。

> 不要直接改 `Assets/Prefabs/Projectiles/TestBall.prefab` 后再跑 `Build/Prefabs`，生成器会覆盖。

### 9.2 按回合切换（抽选需要）

`CharacterLauncher.projectilePrefab` 是固定的，`ShotSpawner` 只读 `prefab.Config`。加一个重载即可：

```csharp
// ShotSpawner.cs
public static Projectile Spawn(Projectile prefab, ProjectileConfig config,
                               Character shooter, Vector2 direction, float speed)
{
    if (prefab == null || shooter == null) return null;

    List<ProjectileModifier> modifiers = CollectModifiers(shooter);
    ProjectileLoadout loadout = ProjectileBuild.Build(config, modifiers, shooter, speed);
    ProjectileStats stats = loadout.Stats;

    CharacterConfig characterConfig = shooter.Config;
    Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
    int obstacleMask = characterConfig != null ? characterConfig.obstacleMask.value : 0;
    int attempts = characterConfig != null ? characterConfig.spawnNudgeAttempts : 1;
    float step = characterConfig != null ? characterConfig.spawnNudgeStep : 0.3f;

    Vector2 origin = ResolveFreePoint(GetOrigin(shooter), dir, stats.radius, obstacleMask, attempts, step);
    Projectile projectile = ProjectilePool.Instance.Rent(prefab, origin, Quaternion.identity);
    if (projectile == null) return null;

    projectile.Configure(stats, loadout.Traits, loadout.Bursts);
    projectile.Launch(dir * stats.speed, shooter);
    return projectile;
}
```

原来的 `Spawn(prefab, shooter, direction, speed)` 改为转调：

```csharp
return Spawn(prefab, prefab.Config, shooter, direction, speed);
```

`CharacterLauncher` 增加：

```csharp
[Header("弹药")]
[SerializeField] private ProjectileConfig ammo;

public ProjectileConfig Ammo => ammo;

public void SetAmmo(ProjectileConfig config) => ammo = config;

// Fire() 里把原来的 Spawn 调用改成：
Projectile projectile = ShotSpawner.Spawn(
    projectilePrefab,
    ammo != null ? ammo : projectilePrefab.Config,
    character, direction, speed);
```

Flow 在进入 `Aim` 相位时调用：

```csharp
Character actor = turnController.GetCharacter(team);
actor.GetComponent<CharacterLauncher>().SetAmmo(loadout.Bullet);
```

### 9.3 六种子弹的落点

| 子弹 | 做法 | 备注 |
|---|---|---|
| 普通 | `damage=10`，默认重力 | 现有 `TestBall` |
| 爆破 | 加一个 `BurstDefinition`，把爆炸范围调到 2 倍 | `BurstDefinition.effects` 里调范围/力度 |
| 拆除 | `damage=0` + `DemolishTrait`（沿途删道具/地形） | 需要新特性；不能对角色造成伤害 |
| 强化 | `damage=20` | 只改配置 |
| 引力 | `ignoreFields=true`（新字段）+ `ProjectileField` 跳过 | 直线/纯物理，无视所有道具 |
| 曳光 | `TracerTrait` + 记录上一发轨迹 | 与 `ShotHistoryTracker` 配合 |

`ignoreFields` 最小实现：

```csharp
// Projectile.cs
public bool IgnoreFields { get; set; }

// ProjectileField.cs 的 OnTriggerStay2D 里
if (projectile.IgnoreFields) return;
```

`DemolishTrait` 建议把“道具注册表”放在 Game 层（例如 `PlacedProp` 组件 + `PropRegistry`），特性只调用注册表，不反向引用 Flow。

---

## 10. 教程 E：新增一种道具

### 10.1 数据

```csharp
// Scripts/Props/PropDefinition.cs
using UnityEngine;

namespace NineDays
{
    public enum PropPlacementRule { Anywhere, GroundOnly }

    /// <summary>道具定义：展示信息 + 放置规则 + 作用范围。</summary>
    [CreateAssetMenu(menuName = "9Days/配置/道具定义", fileName = "PropDefinition")]
    public sealed class PropDefinition : ScriptableObject
    {
        public string propId = "Prop";
        public string displayName = "道具";
        public Sprite icon;
        public GameObject prefab;

        public PropPlacementRule placementRule = PropPlacementRule.Anywhere;
        public Vector2 footprint = Vector2.one;
        [Min(0f)] public float range = 4f;
        [Min(1)] public int maxPerTurn = 1;
    }
}
```

### 10.2 黑洞 / 白洞

现有 prefab：`Assets/Prefabs/Projectiles/Prop/BlackHole.prefab`、`WhiteHole.prefab`（目前只有 Sprite + 粒子）。

1. 根节点加 `CircleCollider2D`，勾 `Is Trigger`，半径 = 作用范围。
2. 加 `RadialGravityField`：
   - 黑洞：`strength = 30`（正数吸引）；
   - 白洞：`strength = -30`（负数排斥）；
   - `radius = 4`、`maxAcceleration = 60`、`minDistance = 0.5`。
3. 范围预览：加一个子物体 `RangePreview`，用半透明圆 Sprite 缩放到 `radius * 2`，只在放置阶段显示。

> `RadialGravityField` 是 `ProjectileField` 子类，只影响已发射（`IsLaunched`）的子弹；角色不会受它影响。

### 10.3 重力反转 / 加重

```csharp
using UnityEngine;

namespace NineDays
{
    /// <summary>区域重力场：附加重力倍率。-2 = 净反转，+0.5 = 净加重 50%。</summary>
    public sealed class GravityZoneField : ProjectileField
    {
        [Header("重力")]
        [Tooltip("附加重力倍率；子弹本身仍受 gravityScale 影响")]
        [SerializeField] private float gravityMultiplier = -2f;

        public override void Accumulate(Projectile projectile, ref ProjectileInfluence influence, float deltaTime)
        {
            influence.Acceleration += Physics2D.gravity * gravityMultiplier;
        }
    }
}
```

- 重力反转：`gravityMultiplier = -2`（子弹自身 +1g，附加 -2g，净 -1g）；
- 重力加重：`gravityMultiplier = 0.5`（净 1.5g）；
- prefab 用 `BoxCollider2D (Is Trigger)` 覆盖“其上方区域”。

### 10.4 反重力障碍

- 实体 `BoxCollider2D` / `PolygonCollider2D`，Layer = `Obstacle`；
- 不加 `ProjectileField`，它只是挡路；
- 角色撞上会走 `CharacterCombat` 的撞击上报。

### 10.5 网格放置

`PropPlacementController` 的最小逻辑：

```csharp
private Vector2 Snap(Vector2 world)
{
    Rect area = bounds.PlayArea;
    float x = area.xMin + Mathf.Round((world.x - area.xMin) / cellSize) * cellSize;
    float y = area.yMin + Mathf.Round((world.y - area.yMin) / cellSize) * cellSize;
    return new Vector2(x, y);
}

private bool IsValid(Vector2 point, PropDefinition prop)
{
    if (!bounds.Contains(point)) return false;
    if (prop.placementRule == PropPlacementRule.GroundOnly &&
        Physics2D.Raycast(point, Vector2.down, 0.6f, obstacleMask).collider == null) return false;

    return Physics2D.OverlapBox(point, prop.footprint, 0f, blockMask) == null;
}
```

交互：

| 输入 | 行为 |
|---|---|
| 鼠标移动 | 幽灵跟随吸附后的格子，显示范围 |
| 左键 | 放置；数量 -1 |
| 右键 | 撤销最后一个；数量 +1 |
| 完成按钮 | 结束搭建，进入 `Aim` |
| Esc | 取消当前幽灵（不退出搭建） |

放置阶段：

- `BattleInputController.InputEnabled = false`；
- `PlacementPage` 显示剩余数量、撤销、完成；
- 完成后才调用 `TurnController.BeginTurn(actor)`。

清理：

- 订阅 `GameEvents.BattleStarted` 和 `RestartBattle`，销毁本局放置的道具；
- 道具常驻，跨回合保留，直到战斗结束 / 重开。

---

## 11. 参数与接口速查

### 11.1 BattleController（`Scripts/Battle/BattleController.cs`）

| 成员 | 说明 |
|---|---|
| `BattleConfig config` | 轮次 / 先手 / 节奏 |
| `TurnController turnController` | 回合驱动 |
| `ArenaController arena` | 可空；程序地形重建 |
| `List<Character> characters` | 至少 2 人 |
| `bool autoStart` | `Start()` 时自动开战 |
| `bool showTurnBanner` | 自动补 `TurnBanner` |
| `Session` / `Round` / `TotalRounds` / `CurrentActor` / `IsRunning` | 只读状态 |
| `BeginBattle()` | 回血、回出生点、重建地形、进入第 1 轮 |
| `RestartBattle()` | 清子弹、中止旧会话、重新开战 |
| `Abort()` | 中止当前行动 |
| `RequestExtraTurn(team, immediate=false, count=1)` | 追加行动 |
| `SetPhaseGate(gate)`（计划） | 接入抽选 / 搭建相位 |

### 11.2 TurnController

| 成员 | 说明 |
|---|---|
| `BeginTurn(Team)` | 开始某队回合；给该队 `CanAct=true` |
| `ForceSwitch(Team)` | 强制切回合（调试） |
| `GetCharacter(Team)` / `GetOpponent(Team)` | 查角色 |
| `ResetMatch()` | 清子弹、角色回出生点、重开 |
| `UseExternalDriver()` | 关掉自动开局 / 自动换人（`BattleController` 调用） |
| `AbortTurn()` | 强制结束当前行动 |
| `CurrentTeam` / `IsResolving` / `TurnIndex` | 只读状态 |
| `event Action<TurnResult> TurnResolved` | 一次行动结算 |

### 11.3 CharacterLauncher

| 成员 | 说明 |
|---|---|
| `BeginAim(Vector2 pointerWorld)` | 开始拖拽；只允许 `CanAct` |
| `UpdateAim(Vector2 pointerWorld)` | 更新方向 / 力度 / 瞄准线 |
| `ReleaseAim(Vector2 pointerWorld)` | 松手发射 |
| `CancelAim()` | 取消 |
| `IsAiming` / `AimPower` / `TargetCamera` | 只读 |
| `SetAmmo(ProjectileConfig)`（计划） | 本回合子弹配置 |

### 11.4 ShotSpawner / ProjectilePool

| 成员 | 说明 |
|---|---|
| `ShotSpawner.GetOrigin(Character)` | 出膛点 = 刚体中心 |
| `ShotSpawner.ResolveFreePoint(...)` | 原点被墙占住时找空位 |
| `ShotSpawner.Spawn(prefab, shooter, dir, speed)` | 实例化 + 构建 + 发射 |
| `ProjectilePool.Instance` | 场景级单例（没有就自动建） |
| `Rent(prefab, pos, rot)` / `Return(projectile)` | 取 / 还 |
| `Prewarm(prefab, count)` | 预热 |
| `CountInactive` / `CountActive` / `Clear` | 池状态 / 清空 |

### 11.5 UIPageRouter（`UIRoot.Instance.Router`）

| 方法 | 说明 |
|---|---|
| `ShowScreen(pageId, args)` | 打开主页面，当前页压栈 |
| `ReplaceScreen(pageId, args)` | 替换当前页，不保留历史 |
| `PushPopup(pageId, args)` | 打开弹窗 |
| `Back()` | 先关弹窗，再回退主页面 |
| `Close(page)` / `CloseAll()` | 关闭指定页 / 全部 |
| `CurrentScreen` / `CurrentPopup` / `CanGoBack` / `IsTransitioning` | 只读状态 |

### 11.6 镜头

| 组件 | 关键字段 / 方法 |
|---|---|
| `CameraRig` | `positionSmoothTime`、`sizeSmoothTime`、`shakeAmplitude`、`shakeFrequency`、`shakeDecay`、`SetPose`、`AddShake`、`SetShakeScale` |
| `CameraDirector` | `rig`、`bounds`、`min/maxOrthographicSize`、`framingPadding`、`overviewPadding`、`ShowOverview`、`SetTargets`、`AddTarget`、`RemoveTarget` |
| `BattleCameraController` | `director`、`turnController`、`characters`、`followProjectiles`、各类震动参数 |

### 11.7 关键配置资产

| 资产 | 路径 | 关键字段 |
|---|---|---|
| `BattleConfig_Default` | `Assets/Data/Battle/` | `roundsToPlay=9`、`randomSeed`、`maxTurnsPerRound=6`、`turnResolveDelay=0.6`、`roundResolveDelay=1` |
| `CharacterConfig_Default` | `Assets/Data/Characters/` | `maxHealth=100`、`hitThreshold`、`knockback*`、`controlLockTime`、`skins` |
| `TestBallConfig` | `Assets/Data/Projectiles/` | `mass`、`radius`、`initialSpeed`、`lifeTime`、`damage=10`、`gravityScale`、`traits`、`bursts` |
| `ArenaConfig_Default` | `Assets/Data/Arena/` | `worldRect`、`pixelsPerUnit`、`layers` |
| `PlayerSettingsDefaults` | `Assets/Data/Settings/` | 玩家默认音量 / 分辨率 |

### 11.8 事件（`Scripts/Events/GameEvents.cs`）

| 事件 | 用途 |
|---|---|
| `BattleStarted(int rounds)` | 清道具、重置 HUD |
| `RoundStarted(int round, Team first)` | 轮次 UI |
| `TurnStarted(Team)` / `TurnEnded(Team)` | 行动方 UI |
| `TurnResolved(TurnResult)` | 结算 / 日志 |
| `ShotFired(Character, Projectile)` | 镜头跟踪、轨迹记录 |
| `CharacterHit(Character, HitInfo)` | 伤害 / 闪白 / 震动 |
| `CharacterHealthChanged` | 血条 |
| `CharacterDefeated` | 战败表现 |
| `BattleEnded(BattleResult)` | 结算页 |
| `BurstTriggered` | 爆发表现 |

---

## 12. 排错

| 现象 | 原因 | 处理 |
|---|---|---|
| 开始页投球进 `Box_Start` 弹出 `PLACEHOLDER` | `StartScene.nextPageId` 不为空，且 catalog 里有该页 | 清空 `nextPageId`，或把 `nextPageId` 指向真正的战斗页 |
| 投球进盒子完全没反应 | 盒子没有 `Is Trigger` 的 Collider，或脚本不在 Collider 同节点 | 勾 `Is Trigger`，脚本挂根节点 |
| 开始页没有 `StartScene` 警告 | 场景里没有 `StartScene` 组件 | 用 `Playmode Scene`，或给场景根加 `StartScene` + `CanvasGroup` |
| `Test_StartScene` 开始/退出反了 | `Box_Exit` 挂 `StartBoxTrigger`、`Box_Start` 挂 `ExitBoxTrigger` | 对调两个脚本 |
| `Test_BattleScene` 打完不结束 | 没有 `BattleController`，只有 `TurnController` 自动轮流 | 按第 7 节接 `BattleController` |
| 战斗里不能拖拽 | `TurnController` 不是 `BattleController` 驱动；角色 `CanAct=false`；结算中 | 检查 `BattleController` 激活、`TurnController.autoAdvance=false` |
| 镜头不跟随 / 不取景 | `CameraDirector.bounds` 为空 | 赋 `ArenaBounds`；主相机 Tag = `MainCamera` |
| 角色掉出地面 | 地面 Layer 不是 `Obstacle`，或 `groundMask` 没包含 | 墙 / 地面统一 `Obstacle`（10） |
| 第一发卡顿 | 场景没有 `ProjectilePool` 或没预热 | 放池并 `Prewarm = 8` |
| 子弹不受力场影响 | 力场 Collider 不是 Trigger，或子弹不在 `targetLayers` | 勾 `Is Trigger`；检查 `ProjectileField.targetLayers` |
| 道具放不下去 | 越界 / 与角色或障碍重叠 / `GroundOnly` 没检测到地面 | 调大 `PlayArea` 或放宽检测距离；打开 Gizmos |
| 改完 prefab 跑 `Build/Prefabs` 后改动消失 | 生成器覆盖了同名 prefab / 配置字段 | 只改数值时不要跑生成；要改结构就改 `PrefabFactory` |
| batchmode 一秒退出、没有日志 | Unity Editor 还开着（`Temp/UnityLockfile`） | 关闭 Unity 再跑 |
| `F` 键没反应 | Scene / Hierarchy 视图吞键 | 点一下 Game 视图，或用 `T` / `G` |
| 打包后开始页进不去战斗 | Build Settings 没有入口场景，或 `battleSceneName` 拼错 / 未启用 | 检查 Build Settings 与 `StartScene.battleSceneName` |

---

## 13. 待确认问题

开工前需要你拍板；下面给了推荐默认值，确认后我会按默认值实现。

| # | 问题 | 推荐默认 | 影响 |
|---|---|---|---|
| 1 | 选到**道具**后，本回合发射什么子弹？ | 附赠“普通子弹”，即道具回合也能打一发 | 决定抽选语义与 `TurnLoadout` 结构 |
| 2 | 先手规则：设计文档写“随机先手”，框架是“每轮血量低者先” | **保留框架规则**（落后方先手，体验更好）；或改成“首轮随机、之后血少先” | 决定是否改 `BattleRules` |
| 3 | 抽选是“每个行动者回合”还是“每轮一次”？ | 每个行动者回合抽一次 | 决定相位门的调用频率 |
| 4 | 道具每回合能放几个？场上是否有上限？ | 每回合 1 个；场上最多 3 个 | 决定 `PropDefinition.maxPerTurn` 与校验 |
| 5 | 网格大小 / 可放置区域 | 1×1 格；`ArenaBounds.PlayArea` 内、地面以上 | 决定 `Snap` 与 `IsValid` |
| 6 | 道具是否跨回合保留？能否被拆除子弹拆掉？ | 常驻，跨回合保留；可被拆除子弹拆掉 | 决定清理时机与 `DemolishTrait` |
| 7 | 9 轮打满 vs 血量归零 | 两者都保留（框架已实现） | 无 |
| 8 | 是否需要“双方各自选道具”还是“只当前行动方选”？ | 只当前行动方选 | 决定 UI 与回合语义 |

> 规则：方案没写清楚的地方不臆测；确认后再改代码。

---

## 14. 最终验收清单

- [ ] `9Days/Build/All` 或 `BuildAllBatch` 输出 `BUILD_OK`
- [ ] EditMode 全绿（基线 81）
- [ ] PlayMode 全绿（基线 34）
- [ ] 开始页 → 战斗场景，Build 后也能跑
- [ ] 每回合 4 选 1，未选不能发射
- [ ] 选道具能放置 / 撤销 / 完成，范围可见
- [ ] 子弹经过黑洞 / 白洞 / 重力区会改道
- [ ] 6 种子弹行为符合设计
- [ ] 命中扣血、击退、闪白、镜头反馈
- [ ] 9 轮 / 血量归零能出胜负
- [ ] 结算页能重开、能回开始页
- [ ] 重开后道具、子弹、地形、血量都恢复
- [ ] 没有硬编码的玩法数值（都在 SO / Inspector）
- [ ] `Docs/agent` 文档与代码同步

---

> 下一步：确认第 13 节的 1–6 项后，从 **P0 + P1** 开始；P1 当天就能看到完整一局。

