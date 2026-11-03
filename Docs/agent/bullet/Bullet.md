# 子弹模块（人读版）

> 一句话：**一个 `Projectile` 原型脚本 + 配置资产 + 可组合特性**；发射时构建数值，飞行中可被区域场/技能实时影响，命中统一走 `Character.ReceiveHit(HitInfo)`。
> 爆发（定时/碰撞后的多效果）见 `Docs/agent/burst/Burst.md`。
> 详细实现见同目录 `Bullet_agent.md`；战斗流程见 `Docs/agent/battle/Battle.md`。

## 目录

1. [模块职责](#1-模块职责)
2. [两阶段数据流](#2-两阶段数据流)
3. [核心类型与接口](#3-核心类型与接口)
4. [配置项](#4-配置项)
5. [预制体原型](#5-预制体原型)
6. [特性：穿透](#6-特性穿透)
7. [飞行影响：引力场](#7-飞行影响引力场)
8. [道具与技能接入](#8-道具与技能接入)
9. [事件](#9-事件)
10. [对象池](#10-对象池)
11. [测试与验收](#11-测试与验收)
12. [常见问题](#12-常见问题)

---

## 1. 模块职责

| 脚本 | 职责 |
|---|---|
| `Bullet/Projectile.cs` | 唯一原型脚本：运动、寿命、碰撞、命中决策、销毁 |
| `Bullet/ProjectileStats.cs` | 每发子弹的**运行时数值副本**（配置资产只读） |
| `Config/ProjectileConfig.cs` | ScriptableObject 模板：基础数值 + 特性列表 |
| `Bullet/ProjectileTrait.cs` | 特性基类（SO，无状态），默认值 + 行为钩子 |
| `Bullet/Traits/PierceTrait.cs` | 示例特性：穿透 |
| `Bullet/ProjectileModifier.cs` | 发射时修改数值/追加特性的扩展点 |
| `Bullet/ProjectileModifierSource.cs` | 挂在角色上，收集装备/道具的修改器 |
| `Bullet/ProjectileBuild.cs` | 纯函数构建管线，可单测 |
| `Bullet/ProjectileInfluence.cs` | 飞行影响累加器 + 积分器 |
| `Bullet/ProjectileField.cs` | 区域场基类（Trigger 内每物理帧施力） |
| `Bullet/RadialGravityField.cs` | 示例场：点状引力 / 斥力 |
| `Bullet/ShotSpawner.cs` | 出膛点解析 + 收集修改器 + 构建 + 实例化 |
| `Bullet/ProjectileHit.cs` | 命中信息、处置枚举、消失原因、命中决策 |

---

## 2. 两阶段数据流

```
【发射时】
ProjectileConfig ──From()──► ProjectileStats（每发一份）
        │                         ▲
        └─ traits.ApplyStats ─────┤ 特性默认值
        └─ modifiers.Modify ──────┤ 道具/装备修改
                                  ▼
                    Projectile.Configure(stats, traits)

【飞行中】
ProjectileField.OnTriggerStay2D ──► ProjectileInfluence（每帧累加）
Projectile.AddAcceleration(...) ──►        │
                                           ▼
                   Projectile.FixedUpdate：velocity 积分
                                           ▼
                                Rigidbody2D.linearVelocity

【命中时】
OnCollisionEnter2D → 友军过滤 → traits.OnHit 投票 → ProjectileHitResolver
                   → Character.ReceiveHit(HitInfo) → Despawn(Consume)
```

**要点**：`Config` / `Trait` 资产在运行时只读；道具和技能只改 `ProjectileStats` 或 `ProjectileInfluence`。

---

## 3. 核心类型与接口

### 3.1 Projectile

| 成员 | 入参 | 返回 | 说明 |
|---|---|---|---|
| `Launch(velocity, shooter)` | 初速、发射者（可为 null） | — | 设置归属、重置寿命/影响、忽略发射者碰撞、触发特性 `OnLaunch` |
| `Configure(ProjectileStats, IReadOnlyList<ProjectileTrait>)` | 最终数值、特性列表 | — | 发射构建管线调用；同步刚体质量/重力/阻力与碰撞体半径 |
| `Configure(ProjectileConfig)` | 配置资产 | — | 兼容旧调用；内部转成 `From(config)` |
| `AddInfluence(in ProjectileInfluence)` | 一次影响 | — | 供区域场累加 |
| `AddAcceleration(Vector2)` | 加速度 m/s² | — | 技能直接操控单颗子弹 |
| `ScaleVelocity(float)` | 倍率 | — | 减速/加速区 |
| `OverrideVelocity(Vector2)` | 目标速度 | — | 反弹/传送 |
| `Despawn(ExpireReason)` | 消失原因 | — | **唯一销毁出口**，先跑特性 `OnExpire` 再发事件 |
| `Stats` | — | `ProjectileStats` | 运行时数值（特性可扣穿透次数） |
| `Team` | — | `Team` | 无发射者时为 `None` |

### 3.2 ProjectileTrait（SO，必须无状态）

| 钩子 | 入参 | 返回 | 说明 |
|---|---|---|---|
| `ApplyStats(stats)` | 运行时数值 | — | 把默认参数累加进去（如穿透 +1） |
| `OnLaunch(projectile)` | 子弹 | — | 出膛一次 |
| `OnTick(projectile, dt)` | 子弹、帧时长 | — | 飞行中每帧 |
| `OnHit(projectile, in hit)` | 子弹、命中信息 | `HitDisposition?` | 返回 null = 不投票；多特性按 `Consume > Pass > Ignore` 合并 |
| `OnExpire(projectile, reason)` | 子弹、原因 | — | 命中/寿命/中止 |

### 3.3 构建与修改器

| 接口 | 说明 |
|---|---|
| `ProjectileBuild.Build(config, modifiers, shooter, speedOverride = -1)` | 顺序：配置 → 特性 `ApplyStats` → 修改器 `Modify` → 修改器追加的特性补算 → `Validate`。返回 `ProjectileLoadout{ Stats, Traits }` |
| `ProjectileModifier.Modify(stats, context)` | 道具/装备/Buff 改数值，或 `context.Traits.Add(...)` 追加特性 |
| `IProjectileModifierSource.CollectModifiers(buffer)` | 角色侧来源；`ShotSpawner` 用 `GetComponentsInChildren` 收集 |

### 3.4 飞行影响

| 接口 | 说明 |
|---|---|
| `ProjectileInfluence` | `Acceleration`（m/s²，累加）、`VelocityScale`（累乘，1 = 不变）、`VelocityOverride`（非空直接改速度） |
| `ProjectileInfluenceSolver.Integrate(velocity, influence, dt)` | 纯函数：先加速度积分，再乘速度倍率；有 Override 时直接返回 |
| `ProjectileField.Accumulate(projectile, ref influence, dt)` | 子类实现“这颗子弹在这里受到什么影响” |
| `RadialGravityField.ComputeAcceleration(...)` | 纯函数：线性衰减 + 最大加速度钳制 |

---

## 4. 配置项

`ProjectileConfig`（默认资产 `Assets/Data/Projectiles/TestBallConfig.asset`）：

| 字段 | 默认 | 说明 |
|---|---|---|
| `mass` | 0.5 | 刚体质量 |
| `radius` | 0.25 | 圆形碰撞体半径 |
| `initialSpeed` | 15 | HUD/测试发射初速（实际发射可由调用方覆盖） |
| `lifeTime` | 5 | 存活时间（秒），超时自动销毁 |
| `ignoreOwnerTime` | 0.15 | 出膛后忽略发射者碰撞的时间 |
| `gravityScale` | 1 | 1 = 现有抛物弹，0 = 直线弹 |
| `linearDamping` | 0 | 空气阻力 |
| `damage` | 10 | 命中伤害 |
| `destroyOnHit` | true | 没有特性投票时的默认处置 |
| `friendlyFire` | false | 是否打自己人 |
| `traits` | 空 | 可组合特性列表，按顺序生效 |
| `bursts` | 空 | 爆发定义列表；见 `Docs/agent/burst/Burst.md` |

---

## 5. 预制体原型

`Assets/Prefabs/Projectiles/TestBall.prefab`：

```
TestBall (Layer = Projectile)
├─ Rigidbody2D        mass 0.5 / gravityScale 1 / Interpolate / Continuous
├─ CircleCollider2D   radius 0.25
├─ Projectile         config = TestBallConfig
└─ Visual
   └─ SpriteRenderer  Sorting Layer = Projectile, Order = 20
```

角色侧：`Character_Base.prefab` 挂有 `ProjectileModifierSource`（默认空列表），装备/道具系统以后往这里加修改器即可。

---

## 6. 特性：穿透

1. 菜单 `Assets > Create > 9Days > 子弹 > 特性 > 穿透` 创建 `PierceTrait` 资产。
2. 把资产拖进 `ProjectileConfig.traits`。
3. 调 `extraPierce`（默认 1）。

行为：命中第一个角色时 `pierceRemaining--` 并返回 `Pass`；`Projectile` 会忽略该目标的碰撞并恢复碰撞前速度，保证继续直线飞行；次数用完后返回 `Consume`。

---

## 7. 飞行影响：引力场

场景/技能里创建空物体，加一个 `Collider2D`（勾 `Is Trigger`）和 `RadialGravityField`：

| 参数 | 默认 | 说明 |
|---|---|---|
| `strength` | 30 | 正数吸引、负数排斥 |
| `radius` | 4 | 作用半径 |
| `maxAcceleration` | 60 | 单颗子弹加速度上限 |
| `minDistance` | 0.5 | 中心附近的最小计算距离，防数值爆炸 |
| `targetLayers` | 全部 | 只影响这些层的子弹 |
| `affectPlayer1/2` | true | 按子弹归属过滤 |

技能用法：在目标点 `Instantiate(gravityWellPrefab)`，持续 `duration` 后销毁；子弹经过 `OnTriggerStay2D` 自动受影响。

直接操控单颗子弹（反弹、弹弓）：

```csharp
projectile.OverrideVelocity(Vector2.Reflect(body.linearVelocity, normal));
projectile.AddAcceleration(Vector2.down * 20f);   // 持续施力
```

---

## 8. 道具与技能接入

| 需求 | 做法 | 改哪里 |
|---|---|---|
| 道具加伤害/穿透 | 新建 `ProjectileModifier` SO，改 `stats` | 不改子弹代码 |
| 道具追加特性 | `context.Traits.Add(trait)` | 不改子弹代码 |
| 技能放引力井/风区/减速区 | 新建 `ProjectileField` 子类，实例化到场景 | 不改子弹代码 |
| 技能反弹某颗子弹 | `projectile.OverrideVelocity(...)` | 不改子弹代码 |
| 角色装备提供修改器 | `ProjectileModifierSource.Add(modifier)` | 不改子弹代码 |

---

## 9. 事件

| 事件 | 参数 | 时机 |
|---|---|---|
| `ShotFired` | `Character, Projectile` | 发射成功后 |
| `ProjectileHitLanded` | `Character, Projectile, HitInfo` | 命中角色且伤害已结算 |
| `ProjectileExpired` | `Projectile, ExpireReason` | 命中 / 寿命 / 中止 |
| `BurstTriggered` | `Projectile, BurstDefinition, BurstContext` | 爆发成功触发（动画接口） |
| `BurstEffectExecuted` | `Projectile, BurstEffect, BurstContext` | 单个爆发效果执行后 |

`ExpireReason`：`Hit`、`Lifetime`、`Aborted`、`Burst`。特效、音效、统计只订阅这些事件，不写进 `Projectile`。

---

## 10. 对象池

`ProjectilePool` 是场景内的 MonoBehaviour，按 prefab 维护 `UnityEngine.Pool.ObjectPool<Projectile>`：

```
生成：ShotSpawner.Spawn → ProjectilePool.Instance.Rent(prefab, pos, rot)
回收：Projectile.Despawn → ProjectilePool.Return → ResetForPool → SetActive(false)
清场：TurnController.ResetMatch / BattleController.RestartBattle → Despawn(Aborted)
```

| 参数 | 默认 | 说明 |
|---|---|---|
| `prewarmPrefab` | TestBall | 场景启动预热的 prefab |
| `prewarmCount` | 8 | 预热数量 |
| `defaultCapacity` | 8 | 每个 prefab 的初始容量 |
| `maxSizePerPrefab` | 64 | 池上限，超出销毁 |
| `collectionCheck` | true | 重复归还会报错 |
| `dontDestroyOnLoad` | false | 单场景先关 |

规则：

- 池化对象回收时会恢复所有 `Physics2D.IgnoreCollision` 关系、清速度/角速度/Trail/owner/stats/traits
- 池内对象是 inactive，`FindObjectsByType<Projectile>` 默认不会统计，回合结束判定不受影响
- `ProjectileExpired` 在 `ResetForPool` **之前**触发，订阅者可以读 `Stats/Team`，但不要保存 `Projectile` 引用

---

## 11. 测试与验收

```bat
Unity.exe -batchmode -quit -projectPath <项目> -executeMethod NineDays.EditorTools.BuildPipeline.BuildAllBatch
Unity.exe -batchmode -nographics -projectPath <项目> -runTests -testPlatform EditMode
Unity.exe -batchmode -projectPath <项目> -runTests -testPlatform PlayMode
```

当前结果：**EditMode 59/59、PlayMode 30/30、`BUILD_OK` + 结构校验通过**。

关键用例：`ProjectileBuildTests`（构建顺序/修改器/穿透）、`ProjectileInfluenceTests`（积分/引力数学）、`ProjectileTraitTests`（穿透、引力 Trigger 路径）、`ProjectilePoolTests`（复用/预热/事件/回合判定）。

---

## 12. 常见问题

**Q：为什么不能直接改 `ProjectileConfig`？**
A：它是共享资产，改了会污染所有使用该配置的子弹和编辑器资源。请改 `ProjectileStats`。

**Q：为什么 `OnHit` 返回可空枚举？**
A：null 表示“这个特性不参与决策”，避免多个特性里只要有一个不想消耗就被强制消耗。

**Q：引力场为什么不用 `Rigidbody2D.AddForce`？**
A：用加速度语义可以做到与质量无关，轻弹/重弹在同一个引力井里轨迹一致；需要质量影响时在 `Accumulate` 里按 `stats.mass` 换算即可。

**Q：为什么穿透要恢复速度？**
A：物理碰撞已经产生反弹，若不恢复，穿透弹会被第一个目标带偏，打不到后面的人。

**Q：场上子弹一直不消失会怎样？**
A：`TurnController` 以“场上无子弹”结束行动；`lifeTime` 是兜底，默认 5 秒。

