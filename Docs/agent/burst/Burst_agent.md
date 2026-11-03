# 子弹爆发（Agent 记录）

> 面向后续 agent：记录本次改了什么、为什么、涉及文件与函数、坑与待办、验证方式。
> 人读版见 `Burst.md`。

## 1. 本次改动清单

| 文件 | 类型 | 内容 |
|---|---|---|
| `Scripts/Burst/BurstTypes.cs` | 新增 | 触发类型、碰撞过滤、`BurstContext` |
| `Scripts/Burst/BurstDefinition.cs` | 新增 | 触发/冷却/次数/效果/可选 viewPrefab |
| `Scripts/Burst/BurstRuntime.cs` | 新增 | 每发状态、计时、冷却、执行、destroyOnTrigger |
| `Scripts/Burst/BurstEffect.cs` | 新增 | 效果基类 |
| `Scripts/Burst/BurstTargetUtility.cs` | 新增 | 范围角色收集、阵营/发射者过滤 |
| `Scripts/Burst/Effects/BurstDamageEffect.cs` | 新增 | 范围伤害 |
| `Scripts/Burst/Effects/BurstForceEffect.cs` | 新增 | Push/Pull |
| `Scripts/Burst/Effects/BurstTerrainEffect.cs` | 新增 | 调场景地形破坏 |
| `Scripts/Config/ProjectileConfig.cs` | 修改 | 增加 `bursts` 列表 + Validate |
| `Scripts/Bullet/ProjectileBuild.cs` | 修改 | Context/Loadout 增加 Bursts；复制配置/支持修改器追加 |
| `Scripts/Bullet/Projectile.cs` | 修改 | BurstRuntime 列表；Configure 重载；Launch/Update/Impact/Despawn/Reset 接入 |
| `Scripts/Bullet/ShotSpawner.cs` | 修改 | 传 bursts |
| `Scripts/Bullet/ProjectileHit.cs` | 修改 | `ExpireReason.Burst` |
| `Scripts/Events/GameEvents.cs` | 修改 | `HitKind.Burst`、`BurstTriggered`、`BurstEffectExecuted` |
| `Scripts/Editor/PrefabFactory.cs` | 修改 | 新配置初始化 bursts |
| `Scripts/Editor/BuildCheck.cs` | 修改 | 校验 bursts/effects 非空 |
| `Assets/Tests/EditMode/BurstRuntimeTests.cs` | 新增 | 计时/冷却/过滤/重置 |
| `Assets/Tests/EditMode/BurstBuildTests.cs` | 新增 | 构建复制/追加、destroy 校正 |
| `Assets/Tests/PlayMode/BurstPlayModeTests.cs` | 新增 | 伤害/牵引/地形/定时/命中顺序 |

**未改动**场景模块接口；`BurstTerrainEffect` 只调用现有 `ArenaController.ApplyTerrainBreak`。

---

## 2. 设计决策与理由

1. **爆发是独立层**：不塞进 `ProjectileTrait`。Trait 负责命中投票；爆发负责计时/条件/效果。
2. **每发子弹一份 `BurstRuntime`**：`BurstDefinition` 是共享 SO，触发次数/冷却不能写回资产。
3. **直接命中先结算**：`Projectile.OnCollisionEnter2D` 先执行 `Character.ReceiveHit`，再 `TryImpact`。
4. **重复 + 冷却**：`maxTriggers = 0` 表示无限；`cooldown` 控制频率；`OnLaunch/OnExpire` 只触发一次。
5. **效果无状态**：伤害/力场/地形只读 `BurstContext`，不做表现。
6. **动画只留接口**：`viewPrefab` 字段 + `BurstTriggered` / `BurstEffectExecuted`；本次不实现 Presenter/Animator。
7. **伤害复用现有链路**：`BurstDamageEffect` 调 `Character.ReceiveHit`，自动进入血量/受击/回合统计；`impulse` 可选触发 Airborne/Hit。
8. **牵引直接作用刚体**：`BurstForceEffect` 不伪造 `HitInfo`，避免伤害统计被污染。

---

## 3. 关键实现细节

### 3.1 触发

`BurstRuntime`：

- `Tick(dt, projectile)`：推进 `Elapsed` 与 `CooldownRemaining`；仅 `OnTimer` 检查 `NextTimerTime`
- `TryImpact(projectile, hit, hitTerrain)`：检查 `trigger == OnImpact`、`MatchesImpact`、冷却、次数
- `TryTrigger(...)`：处理 OnLaunch/OnExpire/Manual
- `Trigger(...)`：
  1. `TriggerCount++`
  2. 设置冷却 / 下一次 Timer
  3. 构造 `BurstContext`
  4. 发 `BurstTriggered`
  5. 按顺序执行 effects，每个发 `BurstEffectExecuted`
  6. `destroyOnTrigger` → `Despawn(ExpireReason.Burst)`

### 3.2 坑

- **冷却必须对所有类型递减**：最初 `Tick` 在非 OnTimer 直接 return，导致 OnImpact 的冷却永远不减少。已改为先递减再判断触发类型。
- **OnExpire 不能被 `IsLaunched=false` 截断**：`Despawn` 先设置 `despawning/IsLaunched=false`，`TriggerBursts` 对 OnExpire 必须继续遍历所有 runtime。
- **`destroyOnTrigger` 触发 Despawn 后**，`TriggerBursts` 对非 OnExpire 立刻停止，避免已死子弹继续触发。
- **`BurstRuntime` 的 `maxTriggers` 0 + cooldown 0** 会每帧触发；Validate/Trigger 使用最小冷却 0.01s 保护。
- **`BurstDefinition.Validate` 会把 `destroyOnTrigger` 的 maxTriggers 校正为 1**。

### 3.3 Projectile 生命周期

- `Launch`：traits `OnLaunch` → 重置 runtime → `OnLaunch` 爆发
- `Update`：traits `OnTick` → `TickBursts` → 寿命检查；如果爆发回收了子弹，立即 return
- `OnCollision`：直接命中 → `OnImpact` 爆发 → Consume 回收
- `Despawn`：`OnExpire` 爆发 → traits `OnExpire` → `ProjectileExpired` → 入池
- `ResetForPool`：清空 `burstDefinitions` 与 `burstRuntimes`

### 3.4 效果

- `BurstTargetUtility` 用 `Physics2D.OverlapCircleAll` + `GetComponentInParent<Character>` + HashSet 去重
- 过滤：`hitOwner`、`hitAllies`、`IsDefeated`
- `BurstDamageEffect` 的 `HitInfo.Kind = HitKind.Burst`
- `BurstForceEffect` 的 `enterAirborne` 复用现有 Animator `Hit`
- `BurstTerrainEffect` 优先使用 `TerrainBreakProfile.CreateRequest`，否则显式请求

---

## 4. 兼容性与约束

| 约束 | 说明 |
|---|---|
| 旧接口 | `Projectile.Configure(config)` / `Configure(stats, traits)` 保留 |
| 资产只读 | `BurstDefinition` 不保存每发状态 |
| 表现 | `viewPrefab` 当前逻辑不读取 |
| 事件 | `BurstTriggered` 在 effects 执行前发出；`BurstEffectExecuted` 在每个 effect 后 |
| 目标层 | 效果 `targetMask` 默认 `~0`；角色组件过滤兜底 |
| 对象池 | `ResetForPool` 清空 runtime；下一次 `Configure/Launch` 重建 |
| 动画 | 本次不实现 `BurstPresenter/BurstView`，只约定接口 |
| 地形 | `BurstTerrainEffect` 依赖场景 `ArenaController.Instance` |

---

## 5. 已知限制与待办

- [ ] 未实现 `BurstPresenter` / `BurstView` / 动画 / 粒子 / 音效
- [ ] 未实现爆发连锁
- [ ] 未实现任意形状角色范围（当前圆形）
- [ ] 未实现表现对象池
- [ ] `OnLaunch/OnExpire` 不支持重复
- [ ] `BurstDamageEffect` 的击退强度依赖现有角色阈值
- [ ] 没有默认演示 burst 资产；需要手动创建
- [ ] `viewPrefab` 只是数据字段，未被任何运行时读取

---

## 6. 验证方式与结果

```bat
Unity.exe -batchmode -quit -projectPath <项目> -executeMethod NineDays.EditorTools.BuildPipeline.BuildPrefabsBatch
Unity.exe -batchmode -nographics -projectPath <项目> -runTests -testPlatform EditMode
Unity.exe -batchmode -projectPath <项目> -runTests -testPlatform PlayMode
```

2026-11-03 实测：

| 项 | 结果 |
|---|---|
| `BuildPrefabsBatch` / `BuildAllBatch` | `BUILD_OK`，结构校验通过 |
| EditMode | **54/54 Passed** |
| PlayMode | **25/25 Passed** |

新增关键用例：

- `BurstRuntimeTests`：timer 冷却/次数、impact 冷却/过滤/最低速度、OnExpire 一次、重置
- `BurstBuildTests`：配置复制、修改器追加、destroyOnTrigger 校正
- `BurstPlayModeTests`：范围伤害、Pull 方向、Timer 重复、Impact 直接命中先结算、地形抹除

---

## 7. 文件与函数索引

| 关注点 | 位置 |
|---|---|
| 触发类型/过滤/上下文 | `BurstTypes.cs` |
| 配置校验 | `BurstDefinition.Validate` |
| 计时/冷却/执行 | `BurstRuntime.Tick / TryImpact / Trigger` |
| 伤害效果 | `BurstDamageEffect.Execute` |
| 牵引效果 | `BurstForceEffect.Execute` |
| 地形效果 | `BurstTerrainEffect.Execute` |
| 目标收集 | `BurstTargetUtility.CollectCharacters` |
| 子弹接入 | `Projectile.Launch / Update / OnCollisionEnter2D / Despawn` |
| 构建链 | `ProjectileBuild.Build` |
| 发射 | `ShotSpawner.Spawn` |
| 动画接口 | `BurstDefinition.viewPrefab` + `GameEvents.BurstTriggered` |
