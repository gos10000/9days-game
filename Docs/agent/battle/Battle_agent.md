# 战斗轮 — Agent 工作记录（Battle_agent）

> 人读版：同目录 `Battle.md`；角色模块：`Docs/agent/character/Character_agent.md`。

## 目录

- [0. 文件清单](#0-文件清单)
- [1. 数据契约](#1-数据契约)
- [2. BattleSession 实现](#2-battlesession-实现)
- [3. 血量与角色相关改动](#3-血量与角色相关改动)
- [4. BattleController 场景接入](#4-battlecontroller-场景接入)
- [5. 生成器与校验](#5-生成器与校验)
- [6. 自动验收 asmdef 规则](#6-自动验收-asmdef-规则)
- [7. 踩坑](#7-踩坑)
- [8. 已知限制与待办](#8-已知限制与待办)
- [9. 接手检查清单](#9-接手检查清单)

---

## 0. 文件清单

| 文件 | 职责 |
|---|---|
| `Scripts/Battle/BattleRules.cs` | 纯静态：先手判定（血少优先/同血随机）、轮数用尽胜负 |
| `Scripts/Battle/TurnQueue.cs` | 轮内行动队列：默认 2 动、追加队尾/插队、`maxTurnsPerRound` 上限（含已消耗） |
| `Scripts/Battle/BattleSession.cs` | **纯逻辑**对局：轮循环、开局抽先手后整场沿用、事件、额外行动、胜负；含 `IBattleParticipant` / `ITurnActionRunner` |
| `Scripts/Battle/BattleController.cs` | MonoBehaviour 接入：连 `TurnController`、转发 `GameEvents`、行动/轮间停顿、`RestartBattle` |
| `Scripts/UI/TurnBanner.cs` | 回合信息**常驻 HUD**（OnGUI 占位）：顶部居中固定面板，轮次 + 行动方 + 结算/胜负，只订阅事件 |
| `Scripts/Character/CharacterHealth.cs` | HP、扣血、战败事件 |
| `Scripts/Config/BattleConfig.cs` | 轮数 / 种子 / 行动上限 / 节奏 |
| `Assets/Tests/EditMode/BattleRulesTests.cs` | 先手与胜负 |
| `Assets/Tests/EditMode/TurnQueueTests.cs` | 队列语义 |
| `Assets/Tests/EditMode/BattleSessionTests.cs` | 9 轮循环、击倒结束、事件一次、额外行动、轮间停顿、Abort |
| `Assets/Tests/PlayMode/ProjectileHitTests.cs` | 真实子弹命中 → 扣血 → `CharacterHit` |

## 1. 数据契约

`TurnResult`（`Events/GameEvents.cs`）：

| 字段 | 意义 |
|---|---|
| `Actor` | 本次行动方 |
| `ShotFired` | 是否开火 |
| `HitLanded` | 是否命中对方 |
| `DamageDealt` | 本次造成的伤害总和 |
| `TargetDefeated` | 是否把对方打至战败 |

`BattleResult`：`Winner`（`Team.None` = 平局）、`RoundsPlayed`、`HealthA`、`HealthB`。

新增事件：`BattleStarted(int)`、`RoundStarted(int, Team)`、`RoundEnded(int)`、`TurnResolved(TurnResult)`、`ExtraTurnGranted(Team, bool)`、`CharacterHealthChanged(Character, float, float)`、`CharacterDefeated(Character)`、`BattleEnded(BattleResult)`；均在 `ClearAll()` 中清空。

## 2. BattleSession 实现

构造：`BattleSession(BattleConfig, IReadOnlyList<IBattleParticipant>, ITurnActionRunner, Random rng = null)`
- `rng` 缺省：`config.randomSeed != 0 ? new Random(seed) : new Random()`
- 校验：配置非空、参战者 ≥ 2、runner 非空

状态推进：

```
Start()             round = 0；battleFirstActor = None → BeginRound()
BeginRound()        round++；仅 battleFirstActor==None 时抽（DecideFirstActor(hpA,hpB,rng)），
                    之后每轮沿用同一值
                    queue.Reset([first, opponent])；RoundStarted
NextTurn()          queue.TryDequeue → runner.BeginTurn(actor, OnTurnResolved)
                    队列空 → EndRound()
OnTurnResolved()    TurnResolved；统计战败人数：
                    1 → EndBattle(对方)；≥2 → EndBattle(None)；否则 NextTurn()
EndRound()          RoundEnded；round >= roundsToPlay → EndBattle(比血量)
                    AutoAdvanceRounds ? BeginRound() : awaitingRoundAdvance = true
ContinueAfterRound() awaiting → BeginRound()
ContinueAfterTurn()  等待行动后间隔 → NextTurn()
```

- **事件顺序**：`OnTurnResolved` 先计算战败 / 是否需要等待（`awaitingTurnAdvance`），**再**发 `TurnResolved`；
  这样 `BattleController` 在事件回调里能正确读到 `IsAwaitingTurnAdvance` 并启动延迟协程。
- 最后一动（队列空）不进入 `awaitingTurnAdvance`，直接 `NextTurn()` → `EndRound()`。

- **递归深度**：测试用 FakeRunner 同步回调，9 轮 × 2 动 = 18 层，安全；真实 `TurnController` 是异步（等结算）。
- `RequestExtraTurn`：`running && !awaiting && queue != null` 才生效；`immediate` → `TryInsertNext`，否则 `TryAppend`；超过 `maxTurnsPerRound` 截断；每次成功发 `ExtraTurnGranted`。
- `Abort`：置 `running=false` 并调用 `runner.Abort()`；不会发 `BattleEnded`。
- 双败/同时归零按平局处理。
- **先手只抽一次**：新增字段 `battleFirstActor`，`Start()` 里清空，`BeginRound()` 仅在它为 `Team.None` 时调 `BattleRules.DecideFirstActor`，之后每轮直接复用。
  理由：`Docs/plan/需求与玩法最小循环.md` 写的是「**随机决定先手**」；旧实现每轮按血量重算，导致同一方可能连续两轮先动（实测日志 `P2,P1,P1,P2`），玩家会读成「顺序错乱」，也让「谁的回合」难以预期。
  现在开局满血 → 等价于随机；整场恒为 `A,B,A,B…`。`BattleRules` 未改动，`BattleRulesTests` 仍覆盖静态判定。

## 3. 血量与角色相关改动

### CharacterHealth

- `Max = CharacterConfig.maxHealth`（默认 100）；`Awake` 时回满并广播一次
- `TakeDamage`：先扣血并广播 `CharacterHealthChanged`；归零 → `character.EnterDefeated()` + `CharacterDefeated`
- `ResetHealth`：战斗开始时回满（由 `Character.ResetForBattle` 调用）

### Character

- 实现 `IBattleParticipant`（`Team` / `CurrentHealth` / `IsDefeated`）
- `CharacterState.Defeated = 5`（追加在末尾，不改变 0~4）
- `Health` 属性**惰性解析** + `ReceiveHit` 惰性取 `CharacterCombat`（兼容运行时动态加组件 / 测试）
- `EnterDefeated()`：清控制锁、切状态
- `ResetForBattle()`：回血、清速度、回出生点、回 Idle、收回操作权

### CharacterCombat

- `ReceiveHit` 顺序：事件 → 闪白 → **扣血** → 若已战败直接返回（不再击飞）→ 阈值判断 → 击退
- `Health` 惰性解析

### TurnController

| 新增 | 说明 |
|---|---|
| `autoAdvance` | 默认 true（旧行为）；被 `BattleController` 接管时 false |
| `TurnResolved` 事件 | 结算完成时带 `TurnResult` |
| 统计字段 | `shotFiredThisTurn / hitLandedThisTurn / damageThisTurn / targetDefeatedThisTurn`（订阅 `GameEvents.CharacterHit`，只统计对方） |
| `AbortTurn()` | 强制结束当前行动（走 `EndTurn`，`autoAdvance` 为真时会换人） |
| `UseExternalDriver()` | `autoStart=false; autoAdvance=false`（`BattleController.Awake` 调用，早于 `TurnController.Start`） |
| `CancelShot()` | 取消本回合的这次发射：停结算计时、清四个战果标志、把发射权还给当前队并 `CancelAim`；**不换人、不推进 `TurnIndex`、不动角色 Transform**（开始界面 `RockReset` 用） |
| `logTurns`（序列化，默认 true） | `BeginTurn` 末尾打印 `[Turn] 第 N 次 BeginTurn → 行动方 X（firstTeam=…，autoAdvance=…） \| 角色=队伍 …`；排查先手/队伍用，发布前可关 |

`EndTurn` 现在会先把 `CurrentTeam = Team.None`，再发 `TurnResolved` / `TurnEnded`，最后按 `autoAdvance` 决定是否自己开下一回合。

输入已从本文件抽离：`TurnController` 不再包含 `InputRouter`，改由 `Battle/BattleInputController` 把鼠标/键盘转发给当前回合的 `CharacterLauncher`。

## 4. BattleController 场景接入

- `Awake`：解析 `TurnController` 并调用 `UseExternalDriver()`
- `Start`：`autoStart` 时 `BeginBattle()`
- `BeginBattle`：
  1. 校验配置 / 引用
  2. 收集 `Character` 为 `IBattleParticipant`
  3. 对每个角色 `ResetForBattle()`
  4. 创建 `BattleSession`，`AutoAdvanceRounds = false`
  5. 转发事件（`RoundStarted` / `RoundEnded` / `TurnResolved` / `ExtraTurnGranted` / `BattleEnded`）
  6. `GameEvents.RaiseBattleStarted(roundsToPlay)` → `session.Start()`
- `ITurnActionRunner`：`BeginTurn` 存回调并调 `turnController.BeginTurn(actor)`；`TurnController.TurnResolved` 回传
- `RoundEnded` → `roundResolveDelay` 秒后 `ContinueAfterRound()`（0 秒直接继续）
- `TurnResolved` → 若 `IsAwaitingTurnAdvance`，等 `turnResolveDelay` 秒后 `ContinueAfterTurn()`
- `Awake`：`EnsureTurnBanner()`，场景里没有 `TurnBanner` 时自动补一个（旧场景也能看到提示）
- `RestartBattle()`：Abort 会话、清场上子弹、重新 `BeginBattle()`；同时停止行动/轮间延迟协程

## 5. 生成器与校验

| 位置 | 改动 |
|---|---|
| `Editor/EditorAssets.cs` | 新增 `BattleConfigPath = Assets/Data/Battle/BattleConfig_Default.asset` |
| `Editor/PrefabFactory.cs` | `CreateBattleConfig`（9 轮 / 种子 0 / 上限 6 / 行动后 0.6s / 轮间 1s / 超时 3s）；`Character_Base` 增加 `CharacterHealth`，并连 `Character.health`、`CharacterCombat.health`、`CharacterHealth.character/config` |
| `Editor/ArenaBuilder.cs` | `TurnController` 设 `autoStart=false`、`autoAdvance=false`；新增 `BattleController`（`config` / `turnController` / 两个角色 / `autoStart`）；HUD 连 `battleController`；HUD 物体上挂 `TurnBanner` 并设 `showBanner` / `topMargin` / `panelWidth` / `panelHeight` |
| `Editor/BuildCheck.cs` | 角色必须含 `CharacterHealth` 且引用非空；`BattleConfig` 必须存在、`roundsToPlay > 0`、`maxTurnsPerRound >= 2` |

生成顺序（`PrefabFactory.BuildAll`）新增一步：皮肤 → 角色配置 → **战斗配置** → 子弹配置 → 子弹 → 材质 → 角色 prefab → 变体。

## 6. 自动验收 asmdef 规则

### 程序集分工

战斗相关代码在 `NineDays.Battle`；`Game` 是玩法聚合层，`Input` 是独立设备层。完整清单和依赖图见 `Docs/agent/asmdef/Asmdef.md`。

| 程序集 | 关键设置 | 作用 |
|---|---|---|
| `NineDays.Game` | 引用 `NineDays.Input` / 2D Animation；`autoReferenced: true` | Character / Bullet / Burst / Scene / Config / Events |
| `NineDays.Battle` | 引用 `NineDays.Game` + `NineDays.Input` | BattleController / TurnController / BattleInputController |
| `NineDays.Input` | 引用 `Unity.InputSystem` | 输入设备层 |
| `NineDays.Editor` | `includePlatforms: ["Editor"]`；引用所有运行时程序集 | 编辑器工具 |
| `NineDays.Tests` | `includePlatforms: ["Editor"]` + `optionalUnityReferences: ["TestAssemblies"]` + 引用被测模块 | EditMode 测试 |
| `NineDays.Tests.PlayMode` | `includePlatforms: []`（全平台）+ 同样的 marker | PlayMode 测试 |

**判定规则（Test Framework 源码 `EditorLoadedTestAssemblyProvider`）**：
先按是否引用 `nunit.framework` / `UnityEngine.TestRunner` 过滤，再按 **程序集是否带 `EditorOnly` 标记** 分类：
带 → EditMode；不带 → PlayMode。所以「EditMode 测试 asmdef 必须 Editor-only」是硬性要求。

### 命令

```bat
Unity.exe -batchmode -nographics -projectPath <项目> ^
  -runTests -testPlatform EditMode -testResults Temp\editmode.xml -logFile Temp\editmode.log

Unity.exe -batchmode -projectPath <项目> ^
  -runTests -testPlatform PlayMode -testResults Temp\playmode.xml -logFile Temp\playmode.log
```

结果（全量基线）：**EditMode 81/81、PlayMode 34/34**（2026-11-05 实测）。

### PlayMode 用例内容

`ProjectileHitTests`：
1. 建目标（`Rigidbody2D` Kinematic + `CapsuleCollider2D` + `Character` + `CharacterCombat` + `CharacterHealth`）
2. 建子弹（`Rigidbody2D` + `CircleCollider2D` + `Projectile`），`Configure(config)` 注入 `damage=25`
3. `Launch(right * 12, null)`，等待命中
4. 断言：命中 1 次、`HitInfo.Damage == 25`、目标血量 -25、事件只发一次

## 7. 踩坑

| 坑 | 现象 | 修法 |
|---|---|---|
| 组件 `Awake` 顺序 | PlayMode 测试命中 0 次；`Character.combat/health` 在 `Awake` 缓存成 null（测试逐个 `AddComponent`，`Awake` 早于后续组件） | `Character.Health`、`Character.ReceiveHit`、`CharacterCombat.Health` 改惰性解析 |
| 测试目标缺 `CharacterCombat` | 命中后不扣血、不发事件 | 测试里必须加 `CharacterCombat`（受击链路在它身上） |
| `TestAssemblies` + 手写 TestRunner 引用 | `Assembly has duplicate references`，编译失败 | 只保留 marker，不写 `UnityEngine.TestRunner` / `UnityEditor.TestRunner` |
| 测试 asmdef 全平台 | EditMode 跑出 0 个用例 | `includePlatforms: ["Editor"]` |
| `UNITY_INCLUDE_TESTS` 约束 | batchmode 下用例不被发现 | 去掉 `defineConstraints`（Editor-only 测试程序集不会进包） |
| 测试目录放在运行时 asmdef 子树里 | 排查困难 | 移到 `Assets/Tests`，与 `Scripts` 平级 |
| 子弹配置是私有序列化 | 测试无法注入伤害 | 增加 `Projectile.Configure(ProjectileConfig)`（对象池也会用到） |
| `BattleController` 与 `TurnController.Start` 顺序 | 可能自动开一回合 | `BattleController.Awake` 调 `UseExternalDriver()`（Awake 早于所有 Start） |
| `TurnResolved` 早于等待标志 | 控制器读到 `IsAwaitingTurnAdvance=false`，不启动行动间隔 | 先置 `awaitingTurnAdvance`，再发 `TurnResolved` |
| 改了 `TurnController.firstTeam` 却不生效 | 场景里加了 `BattleController` 后，先手始终「随机」，`First Team` 怎么改都没反应 | `BattleController.Awake` → `UseExternalDriver()` 关掉自动开局，先手改由 `BattleSession` 决定；`firstTeam` 只在无 `BattleController` 的场景（`StartScene`）生效 |
| 按 guid 搜场景找不到某组件 | 误判「场景里没有 `BattleController`」 | 组件可能挂在后来新增的物体上；遍历场景所有 `!u!114` 反查脚本名更可靠，不要只 `grep guid` |
| 移除/改名序列化字段后生成器空引用 | 跑 `9Days/Build/Scenes` 报 NRE | `EditorAssets.SetBool/SetFloat/...` 内部直接 `FindProperty(...)` 无空判断，改字段要同步改 `ArenaBuilder` |

## 8. 已知限制与待办

- [ ] `BattleConfig.turnResolveTimeout` 尚未接入 `TurnController.resolveTimeout`（场景里仍用后者 3s），后续统一
- [ ] PlayMode 只覆盖「命中→扣血」；完整 9 轮 PlayMode 冒烟可后续补
- [ ] `Defeated` 的美术表现未实现（皮肤 Animator State=5 缺省，无动画时停在当前姿态）
- [ ] 额外行动接口尚无玩法调用（等技能/道具）
- [ ] 没有正式 UI，信息只在 Debug HUD；`TurnBanner` 已改为**常驻顶部 HUD**（OnGUI 占位，无淡入淡出），后续替换为 Canvas/TMP
- [ ] `TurnBanner` 仍有每帧 `new GUIStyle` 的分配，正式 UI 落地时一并去掉
- [ ] `RestartBattle` 不重建障碍物（当前场景是静态障碍）
- [ ] 同步 FakeRunner 会产生递归（最多 6 动/轮），真实 runner 是异步，无风险

## 9. 接手检查清单

1. 先读 `BattleSession.cs`（规则全在这里），再看 `BattleController.cs`（Unity 接入）
2. 改规则：优先改 `BattleRules` / `TurnQueue` 并补 EditMode 测试
3. 改节奏：改 `BattleConfig`（轮间停顿、上限、种子）
4. 改场景：改 `ArenaBuilder` 后跑 `9Days/Build/All`
5. 改 prefab：改 `PrefabFactory` 后跑 `9Days/Build/Prefabs`（会重写角色与配置）
6. 提交前跑两套测试：

```bat
Unity.exe -batchmode -nographics -projectPath <项目> -runTests -testPlatform EditMode ...
Unity.exe -batchmode -projectPath <项目> -runTests -testPlatform PlayMode ...
```

> `Assets/Tests` 不属于运行时程序集；改测试时确认两个 asmdef 的 `includePlatforms` 没被改错。

