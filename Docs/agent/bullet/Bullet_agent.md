# 子弹模块（Agent 记录）

> 面向后续 agent：记录本次改了什么、为什么、涉及文件与函数、坑与待办、验证方式。
> 人读版见 `Bullet.md`；设计原则见 `Docs/program/summary.md`（`Docs/program/bullet.md` 当前为空，未改动）。

## 1. 本次改动清单

| 文件 | 类型 | 内容 |
|---|---|---|
| `Scripts/Bullet/ProjectileStats.cs` | 新增 | 每发子弹运行时数值副本；`From(config)`、`Validate()`、`Clone()` |
| `Scripts/Bullet/ProjectileHit.cs` | 新增 | `ProjectileHit`、`HitDisposition`、`ExpireReason`、`ProjectileHitResolver` |
| `Scripts/Bullet/ProjectileTrait.cs` | 新增 | 特性基类：`ApplyStats/OnLaunch/OnTick/OnHit/OnExpire` |
| `Scripts/Bullet/ProjectileModifier.cs` | 新增 | 发射修改器基类 + `IProjectileModifierSource` |
| `Scripts/Bullet/ProjectileModifierSource.cs` | 新增 | 角色侧修改器容器（`Add/Remove/CollectModifiers`） |
| `Scripts/Bullet/ProjectileBuild.cs` | 新增 | `ProjectileBuildContext`、`ProjectileLoadout`、纯函数 `Build` |
| `Scripts/Bullet/ProjectileInfluence.cs` | 新增 | `ProjectileInfluence` + `ProjectileInfluenceSolver.Integrate` |
| `Scripts/Bullet/ProjectileField.cs` | 新增 | 区域场基类，`OnTriggerStay2D` 内累加影响 |
| `Scripts/Bullet/RadialGravityField.cs` | 新增 | 点状引力/斥力场 + 纯函数 `ComputeAcceleration` |
| `Scripts/Bullet/Traits/PierceTrait.cs` | 新增 | 穿透特性（演示用） |
| `Scripts/Bullet/ProjectilePool.cs` | 新增 | 场景级对象池：`Rent/Return/Prewarm/CountInactive/CountActive/Clear` |
| `Scripts/Bullet/Projectile.cs` | 重写 | 改用 `Stats`/`Traits`/`ProjectileInfluence`；统一 `Despawn`；新增事件；池化 `ResetForPool` + 忽略碰撞全量恢复 |
| `Scripts/Bullet/ShotSpawner.cs` | 重写 | 收集 `IProjectileModifierSource` → `ProjectileBuild.Build` → `Configure` → `Launch` |
| `Scripts/Config/ProjectileConfig.cs` | 重写 | 新增 `gravityScale`、`linearDamping`、`traits`、`Validate()` |
| `Scripts/Events/GameEvents.cs` | 修改 | 新增 `ProjectileHitLanded`、`ProjectileExpired` 及 Raise/ClearAll |
| `Scripts/Editor/PrefabFactory.cs` | 修改 | 写入新配置字段；`TestBall` 刚体使用配置重力/阻力；`Character_Base` 挂 `ProjectileModifierSource` |
| `Scripts/Editor/BuildCheck.cs` | 修改 | 断言修改器组件、子弹配置非空、刚体重力与配置一致、traits 列表非空、场景含 `ProjectilePool` |
| `Assets/Tests/EditMode/ProjectileBuildTests.cs` | 新增 | 9 个用例：构建顺序、修改器、穿透、命中决策 |
| `Assets/Tests/EditMode/ProjectileInfluenceTests.cs` | 新增 | 7 个用例：积分、累加、引力衰减/钳制 |
| `Assets/Tests/PlayMode/ProjectileTraitTests.cs` | 新增 | 5 个用例：穿透、引力、Trigger 路径、撞墙 |
| `Assets/Tests/PlayMode/ProjectilePoolTests.cs` | 新增 | 5 个用例：复用、事件、预热、inactive 判定、拒绝外部对象 |
| `Docs/agent/bullet/Bullet.md` / `Bullet_agent.md` | 新增 | 模块文档 |
| `Docs/agent/README.md` | 修改 | 索引新增「子弹」一行 |

资源侧由 `BuildAllBatch` 重生成：`TestBall.prefab` 的 `Rigidbody2D.gravityScale/linearDamping` 来自配置；`TestBallConfig.asset` 写入 `gravityScale: 1`、`linearDamping: 0`、`traits: []`；角色 prefab 含 `ProjectileModifierSource`。

---

## 2. 设计决策与理由

1. **发射构建与飞行影响分离**：道具/装备影响“这一发子弹的最终数值”，技能/场景影响“已经在飞的子弹”。两者共用 `Projectile`，但扩展点不同（`ProjectileModifier` vs `ProjectileField`）。
2. **运行时 `ProjectileStats` 副本**：`ProjectileConfig` 是共享 SO，直接改会污染所有子弹与编辑器资产；每发 `new ProjectileStats` 后只改副本。
3. **特性必须无状态**：`ProjectileTrait` 是可共享的 SO，参数通过 `ApplyStats` 写进 `stats`，每发状态（如 `pierceRemaining`）放 `ProjectileStats`。
4. **命中投票用可空枚举**：`OnHit` 返回 `HitDisposition?`，null = 不参与决策；避免“一个特性不想消耗”被另一个默认值覆盖。合并优先级 `Consume > Pass > Ignore`。
5. **构建顺序固定**：配置 → 基础特性 `ApplyStats` → 修改器 `Modify` → 修改器追加的特性补算 → `Validate`。这样“道具加特性”也能生效。
6. **穿透要恢复速度 + 忽略碰撞**：物理碰撞会先产生反弹，若不恢复 `lastVelocity`，穿透弹会被第一个目标带偏。
7. **场用加速度而非力**：`ProjectileInfluence.Acceleration` 与质量无关，引力井对轻/重弹轨迹一致；需要质量差异时在 `Accumulate` 里乘/除 `stats.mass`。
8. **保持旧接口**：`Projectile.Configure(ProjectileConfig)` 与 `Projectile.Config` 保留，避免改 HUD 与已有 PlayMode 测试。

---

## 3. 关键实现细节

### 3.1 Projectile 生命周期

| 阶段 | 行为 |
|---|---|
| `Awake` | 惰性取 `Rigidbody2D`/`Collider2D`；`stats = ProjectileStats.From(config)`；`ApplyStats()` |
| `Configure(config)` | 转成 `From(config)` + 配置里的 traits，走下面的重载 |
| `Configure(stats, traits)` | `Validate()`、钳制 `pierceRemaining`、复制 traits 列表、`ApplyStats()`；**不调用 `ApplyStats` 钩子**（由 `ProjectileBuild` 负责） |
| `Launch` | 记录 owner、重置寿命/影响、清 trail、设初速、忽略 owner 碰撞、依次 `OnLaunch` |
| `Update` | 寿命、忽略 owner 倒计时、`OnTick`、超时 `Despawn(Lifetime)` |
| `FixedUpdate` | `ProjectileInfluenceSolver.Integrate` → `body.linearVelocity`；记录 `lastVelocity`；清空 `pendingInfluence` |
| `OnCollisionEnter2D` | 过滤 owner/友军 → `traits.OnHit` 投票 → `Resolve` → 穿透则忽略碰撞并恢复速度 → `ReceiveHit` + `ProjectileHitLanded` → `Consume` 时 `Despawn(Hit)`。注意 `target != null && target == owner`，否则无 owner 时 `null == null` 会把撞墙误判成打到自己 |
| `Despawn` | `despawning` 防重入、`IsLaunched=false`、`OnExpire`、`ProjectileExpired`、`Destroy`（编辑模式 `DestroyImmediate`） |

### 3.2 命中决策

```csharp
HitDisposition result = destroyOnHit ? Consume : Pass;
bool voted = false;
foreach trait:
    vote = trait.OnHit(projectile, hit);
    if (vote == null) continue;
    result = voted ? Combine(result, vote) : vote;
    voted = true;
```

`Combine`：Consume 优先，其次 Pass，最后 Ignore。

### 3.3 飞行影响

- `ProjectileField.OnTriggerStay2D` 按 `targetLayers` 与 `affectPlayer1/2` 过滤后，调用子类 `Accumulate`，再 `projectile.AddInfluence`。该方法声明为 `protected virtual`，避免基类 `private` 导致 Unity 消息不派发到派生类。
- `ProjectileInfluence.Add`：加速度相加、`VelocityScale` 相乘、`VelocityOverride` 后者覆盖前者。
- `Projectile.FixedUpdate` 每物理帧积分一次并清空，避免跨帧重复叠加。

### 3.4 穿透

`PierceTrait.ApplyStats` 把 `extraPierce` 加到 `stats.pierceCount`；`OnHit` 在 `pierceRemaining > 0` 时自减并返回 `Pass`，否则 `Consume`。`Projectile` 在 `Pass` 且目标是角色时 `IgnorePair` 并恢复 `lastVelocity`。

### 3.5 对象池

- `ProjectilePool` 按 prefab 建 `ObjectPool<Projectile>`；`Rent` 设置 `Pool`/`SourcePrefab` 并 `MarkRented`，`Return` 先捕获 prefab 再 `Release`。
- `actionOnRelease` 调 `Projectile.ResetForPool`：恢复所有 `IgnorePair` 记录、清 owner/stats/traits/寿命/影响/速度/Trail，并置 `despawning = true` 防止重复回收。
- `Prewarm` 必须**先全部 `Get` 再统一 `Release`**；若边 Get 边 Release，会反复拿到同一个实例，预热数量永远为 1（本次踩过）。
- 清场路径 `TurnController.ResetMatch` / `BattleController.RestartBattle` 都改为 `Despawn(Aborted)`，池内对象 inactive，`FindObjectsByType` 默认不计入。

---

## 4. 兼容性与约束

| 约束 | 说明 |
|---|---|
| 旧接口保留 | `Projectile.Configure(ProjectileConfig)`、`Projectile.Config` 仍可用；HUD 继续读 `testProjectilePrefab.Config.initialSpeed` |
| 发射必须走 `ShotSpawner` | `Projectile.Awake` 只做 `From(config)`，**不会**执行 traits 的 `ApplyStats`；直接 `Instantiate` 会丢失特性数值 |
| `ProjectileStats` 每发一份 | 不要把同一个 `ProjectileStats` 实例复用到多颗子弹（穿透次数等会被共享） |
| `ProjectileTrait` 无状态 | 不要在 trait 里存 `pierceRemaining` 之类的每发状态 |
| `ProjectileModifierSource` 位置 | `ShotSpawner` 用 `GetComponentsInChildren` 收集，挂在角色根或子物体都可以 |
| `ProjectileField` 需要 Trigger | 组件本身不加 Collider；`Reset()` 只会在编辑器里把已有 Collider 设为 Trigger，运行时 `AddComponent` 需手动设 `isTrigger` |
| `lastVelocity` 时机 | 只在 `Launch` 与 `FixedUpdate` 更新；同一物理帧内多次碰撞时以最近一次为准 |
| 池化对象必须重新 `Configure` | `ResetForPool` 会清空 `stats/traits`；`Rent` 后直接 `Launch` 只能得到无特性、默认数值的子弹 |
| 池内对象是 inactive | `FindObjectsByType<Projectile>` 默认不统计，因此不会阻塞 `TurnController` 的回合结束判定 |
| 事件不要缓存引用 | `ProjectileExpired` 在回收前触发，之后对象会被复用；订阅者只应读取当场数据 |

---

## 5. 已知限制与待办

- [ ] 未实现爆炸、弹跳、追踪、分裂、持续伤害；`ProjectileTrait` 已留好钩子
- [ ] `ProjectilePool` 默认场景级（`dontDestroyOnLoad = false`）；跨场景常驻/Addressables 预加载未做
- [ ] `ProjectilePool.Clear()` 只清理池内 inactive 对象；已租出的活跃子弹不处理
- [ ] `ProjectilePool` 的池 root 是子弹的父节点；销毁池会连带销毁仍在飞的子弹（场景卸载时符合预期）
- [ ] 池化对象 `Rent` 后必须 `Configure`；若直接 `Launch`，`stats/traits` 为空，只有默认刚体行为
- [ ] `ProjectileExpired` 订阅者若缓存 `Projectile` 引用，复用后会指向新子弹；事件只保证触发瞬间的数据有效
- [ ] 没有集中式 `ProjectileInfluenceManager`；`OnTriggerStay2D` 与 `Projectile.FixedUpdate` 的调用顺序无保证，极端情况会有一物理帧延迟
- [ ] `ProjectileField` 不会自动补 Collider2D；场景里需要手动加 Trigger
- [ ] 穿透只忽略“命中的那个 Collider”；目标有多个 Collider 时，其余 Collider 仍可能挡住子弹
- [ ] `ProjectileHitLanded` 只在命中角色时触发；撞墙/障碍不触发（只有 `ProjectileExpired`）
- [ ] `ProjectileBuildContext.Traits` 允许 modifier 追加特性，但追加特性的 `ApplyStats` 在 modifier 之后执行；若某 modifier 依赖该特性带来的数值，顺序不满足
- [ ] `Projectile.Config` 与 `Projectile.Stats` 可能不一致（运行时被 modifier 改过），HUD 读 `Config` 只用于初始速度显示

---

## 6. 验证方式与结果

```bat
Unity.exe -batchmode -quit -projectPath <项目> -executeMethod NineDays.EditorTools.BuildPipeline.BuildAllBatch
Unity.exe -batchmode -nographics -projectPath <项目> -runTests -testPlatform EditMode
Unity.exe -batchmode -projectPath <项目> -runTests -testPlatform PlayMode
```

2026-11-02 实测：

| 项 | 结果 |
|---|---|
| `BuildAllBatch` | `BUILD_OK`，`[BuildCheck] 结构校验全部通过`（15s） |
| EditMode | **37/37 Passed**（原 21 + 新增 16） |
| PlayMode | **11/11 Passed**（原 1 + 新增 10） |

新增用例：

- `ProjectileBuildTests`：`From_CopiesConfigValues`、`Validate_ClampsNegativeValues`、`Build_AppliesSpeedOverride`、`Build_AppliesTraitStats`、`Build_AppliesModifierAfterTrait`、`Build_TraitAddedByModifier_StillContributes`、`Build_PierceTrait_SetsRemaining`、`HitResolver_NoVotes_UsesDestroyOnHit`、`HitResolver_ConsumeWinsOverPass`
- `ProjectileInfluenceTests`：`Solver_AppliesAcceleration`、`Solver_AppliesVelocityScale`、`Solver_OverrideWins`、`Influence_AddAccumulates`、`GravityField_PullsTowardCenter`、`GravityField_OutsideRadius_IsZero`、`GravityField_ClampsAcceleration`
- `ProjectileTraitTests`：`PierceTrait_HitsTwoTargets`、`GravityField_Accumulate_PullsTowardCenter`、`GravityField_TriggerAffectsFlyingProjectile`、`Influence_ChangesVelocity_OnFixedUpdate`、`Projectile_WithoutOwner_HitsWallAndDespawns`
- `ProjectilePoolTests`：`Rent_ReusesReturnedInstance`、`Despawn_DeactivatesAndFiresExpiredOnce`、`Prewarm_CreatesInactiveInstances`、`PooledProjectile_NotReturnedByFindObjects`、`Return_RejectsForeignProjectile`

`BuildCheck` 新增断言：

- 角色 prefab 必须有 `ProjectileModifierSource`
- `TestBall` 的 `Projectile.Config` 非空
- `TestBall` 刚体 `gravityScale` 与配置一致
- 子弹配置 `traits` 列表非空（允许空列表）
- `TestArena` 场景含 `ProjectilePool` 且 `prewarmPrefab` 非空

---

## 7. 文件与函数索引

| 关注点 | 位置 |
|---|---|
| 发射构建顺序 | `ProjectileBuild.Build` |
| 运行时数值 | `ProjectileStats.From / Validate` |
| 命中投票 | `ProjectileHitResolver.Resolve / Combine` |
| 穿透 | `PierceTrait.ApplyStats / OnHit` |
| 飞行积分 | `ProjectileInfluenceSolver.Integrate` |
| 区域场 | `ProjectileField.OnTriggerStay2D / Accumulate` |
| 引力数学 | `RadialGravityField.ComputeAcceleration` |
| 唯一销毁出口 | `Projectile.Despawn` |
| 对象池 | `ProjectilePool.Rent / Return / Prewarm` |
| 池化重置 | `Projectile.ResetForPool` |
| 发射入口 | `ShotSpawner.Spawn` |
| 道具修改器容器 | `ProjectileModifierSource.CollectModifiers` |

本次**未修改** `Docs/program/**`，也未改动战斗轮、角色状态机与场景生成以外的资源。

