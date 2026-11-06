# 战斗轮（人读版）

> 一句话：**血量制、每轮双方各行动一次、开局随机先手并整场沿用、支持追加额外行动**；
> 逻辑在 `BattleSession`（纯 C#，可单测），场景接入在 `BattleController`。
> 详细实现见同目录 `Battle_agent.md`；角色模块见 `Docs/agent/character/Character.md`。

## 目录

1. [规则](#1-规则)
2. [流程](#2-流程)
3. [先手与行动顺序](#3-先手与行动顺序)
4. [额外行动](#4-额外行动)
5. [血量与胜负](#5-血量与胜负)
6. [事件 GameEvents](#6-事件-gameevents)
7. [配置](#7-配置)
8. [场景与调试](#8-场景与调试)
9. [自动验收](#9-自动验收)
10. [常见问题](#10-常见问题)

---

## 1. 规则

| 项 | 规则 |
|---|---|
| 胜负 | 血量制：任一方 HP 归零立即结束；打满 `roundsToPlay`（默认 9）轮后比剩余 HP；相同为平局 |
| 一轮 | 默认 **2 动**：双方各行动 1 次 |
| 先手 | **开局判定一次**：血量低者先，血量相同用随机（`randomSeed` 可复现）；之后每轮沿用同一顺序，不再重算 |
| 行动内容 | 只有发射（无移动、无时限） |
| 额外行动 | 可通过 API 在轮内追加（队尾 / 插队），受 `maxTurnsPerRound` 上限保护 |
| 行动后 | 每次行动结算后停 `turnResolveDelay`（0.6s）再轮到下一个行动者 |
| 轮间 | `roundResolveDelay`（默认 1s）给表现层停顿 |

## 2. 流程

```
BattleStarted(9)
 └─ BeginRound(n)
      first = 开局抽一次（血量低者 / 同血随机），之后每轮沿用
      queue = [first, 对方]
      RoundStarted(n, first)
      └─ NextTurn → TurnController.BeginTurn(actor)   // 一次「瞄准 → 发射 → 结算」
            TurnResolved(result) → 有人战败？ 是 → BattleEnded
                                  否 → 队列还有？ 有 → 等 0.6s → NextTurn
                                                  无 → EndRound
      EndRound(n) → 轮数到？是 → BattleEnded(比血量)
                    否 → 等 1s → BeginRound(n+1)
```

## 3. 先手与行动顺序

- 先手只影响「每轮第一个行动的人」，第二个固定是对方
- 先手**只在第 1 轮判定一次**（`BattleSession.battleFirstActor` 记住结果），后续轮次不再按血量重算
- 因此整场行动顺序恒为 `先手 → 对方 → 先手 → 对方 …`，不会出现同一方连续两轮先动
- 开局双方都是满血（`BattleController.BeginBattle` 里先 `ResetForBattle()`），所以「血量低者先」在开局等价于「血量相同 → 随机」
- 同血随机：`BattleConfig.randomSeed = 0` 为真随机；非 0 为固定种子（便于复现/验收）
- ⚠️ `TurnController.firstTeam` 只在**没有 `BattleController`** 的场景（如 `StartScene`）生效；被 `BattleController` 接管后它不参与判定

## 4. 额外行动

```csharp
// BattleController 上调用
battleController.RequestExtraTurn(Team.Player1);            // 排到本轮队尾
battleController.RequestExtraTurn(Team.Player2, true);      // 插到下一个（立即）
battleController.RequestExtraTurn(Team.Player1, false, 2);  // 一次追加 2 动
```

- 每次成功追加都会发 `ExtraTurnGranted(team, immediate)`
- 超过 `maxTurnsPerRound`（默认 6，含已行动次数）会被忽略
- 战斗结束或轮间停顿时调用无效
- 以后技能/连击系统直接调这个接口，不需要改轮循环

## 5. 血量与胜负

| 项 | 位置 |
|---|---|
| 最大血量 | `CharacterConfig.maxHealth`（默认 100，每个角色可不同） |
| 伤害 | `HitInfo.Damage` ← `ProjectileConfig.damage`（默认 10） |
| 扣血 | `CharacterHealth.TakeDamage(amount)`，只受一次伤害直到归零 |
| 归零 | 角色进入 `CharacterState.Defeated`，广播 `CharacterDefeated` |
| 结束 | `BattleEnded(BattleResult { Winner, RoundsPlayed, HealthA, HealthB })` |

> `Winner = Team.None` 表示平局（9 轮后血量相同，或双方同时战败）。

## 6. 事件 GameEvents

| 事件 | 载荷 | 时机 |
|---|---|---|
| `BattleStarted` | `int rounds` | 战斗开始 |
| `RoundStarted` | `int round, Team first` | 每轮开始 |
| `RoundEnded` | `int round` | 每轮结束 |
| `TurnResolved` | `TurnResult` | 一次行动结算完成 |
| `ExtraTurnGranted` | `Team, bool immediate` | 额外行动被接受 |
| `CharacterHealthChanged` | `Character, float current, float max` | 血量变化 |
| `CharacterDefeated` | `Character` | 某角色战败 |
| `BattleEnded` | `BattleResult` | 战斗结束 |

## 7. 配置

### BattleConfig — `Assets/Data/Battle/BattleConfig_Default.asset`

| 字段 | 默认 | 说明 |
|---|---|---|
| `roundsToPlay` | 9 | 打满多少轮 |
| `randomSeed` | 0 | 0 = 真随机；非 0 = 可复现 |
| `maxTurnsPerRound` | 6 | 每轮行动上限（含额外行动） |
| `turnResolveDelay` | 0.6 | 行动结算后 → 下一个行动者的停顿（秒） |
| `roundResolveDelay` | 1.0 | 轮间停顿（秒） |
| `turnResolveTimeout` | 3.0 | 单次行动结算超时（保留字段，当前由 `TurnController.resolveTimeout` 使用） |

### CharacterConfig

新增 `maxHealth`（默认 100）。

## 8. 场景与调试

- 场景里放：`TurnController`（`autoStart=false`、`autoAdvance=false`）+ `BattleController`（引用 `BattleConfig`、`TurnController`、两个 `Character`）
- `BattleInputController` 单独负责把输入转发给当前行动角色；`TurnController` 不读取输入
- `BattleController` 在 `Awake` 里调用 `TurnController.UseExternalDriver()`，由对局层决定行动者
- HUD 顶栏：`Round: x/9`、当前行动方、`HP 当前/最大`、结算/回合日志、胜负
- **回合信息常驻 HUD**（`Scripts/UI/TurnBanner.cs`）：屏幕**正上方居中**固定一块面板，常驻显示 `第 n / 9 轮` + `PlayerX 的回合 · 拖拽松开发射`，P1 白 / P2 蓝；结算期间显示 `结算中…`，结束显示 `WINNER: X` / `DRAW`；**不再每次行动弹一次、也没有淡入淡出**
- HUD 位置与尺寸可调：`topMargin`（默认 12）、`panelWidth`（380）、`panelHeight`（66）；颜色 `player1Color` / `player2Color` / `neutralColor` / `drawColor`
- HUD 只订阅 `GameEvents`；场景里没有时 `BattleController.Awake` 会自动补一个
- 热键：`E` 追加一次立即行动（验证接口）、`R` 重开整场战斗
- 开始界面（`StartScene`）的「无限重打」由 `Scripts/RockReset.cs` 提供，挂在 `Rock.prefab` 上：
  - 按 `R`（走 `InputRouter.KeyPressed(Key.R)`）→ 遍历场上 `Projectile` 调 `Despawn(Aborted)` → `TurnController.CancelShot()`
  - 只把这一枪作废：不换人、不推进 `TurnIndex`、不动角色位置
  - 依赖石头用的 `RockConfig`：`destroyOnHit = 0`、`lifeTime = 999`，保证撞完不消失、监听器一直存活
- `TurnController.logTurns`（默认开）：每次 `BeginTurn` 在 Console 打印 `[Turn] 第 N 次 BeginTurn → 行动方 X（firstTeam=…，autoAdvance=…） | 角色=队伍`，排查先手 / 队伍配置用；确认无误后可关掉

## 9. 自动验收

```bat
:: 逻辑（快，秒级）
Unity.exe -batchmode -nographics -projectPath <项目> ^
  -runTests -testPlatform EditMode -testResults Temp\editmode.xml -logFile Temp\editmode.log

:: 物理集成（真实子弹命中 → 扣血 → 事件）
Unity.exe -batchmode -projectPath <项目> ^
  -runTests -testPlatform PlayMode -testResults Temp\playmode.xml -logFile Temp\playmode.log
```

当前结果（全量基线）：**EditMode 81/81、PlayMode 34/34**。

## 10. 常见问题

| 现象 | 排查 |
|---|---|
| 一直不换人 | `BattleController` 是否在场景里；`TurnController.autoAdvance` 是否为 false（交给对局层） |
| 先手不符合预期 | 先看场景里有没有 `BattleController`：**有则 `TurnController.firstTeam` 无效**，先手由 `BattleSession` 开局随机；Console 的 `[Turn] 第 1 次 BeginTurn → 行动方 X` 可直接看到结果 |
| 每轮先手都在变 | 旧版「按血量重算」已移除；若仍在变，说明场景跑的是改动前的程序集，或另有代码在调 `BeginTurn` |
| HUD 不显示 | `TurnBanner.showBanner` 是否为 true；场景里是否真的挂了这个组件（`BattleController` 只在 `Awake` 时补一次） |
| 额外行动没生效 | 是否在轮间停顿/战斗结束；是否超过 `maxTurnsPerRound` |
| 打不死人 | `ProjectileConfig.damage` 是否为 0；`CharacterHealth` 是否挂在 prefab 上 |
| 第 9 轮没结束 | `roundsToPlay` 是否被改大；看 `ROUND END` 日志 |
