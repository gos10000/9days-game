# 子弹爆发（人读版）

> 一句话：**爆发 = 子弹在条件满足时执行一组效果**；效果可以伤害角色、破坏地形、牵引角色，动画以后通过事件接入。

## 目录

1. [模块职责](#1-模块职责)
2. [数据流](#2-数据流)
3. [BurstDefinition](#3-burstdefinition)
4. [触发与重复/冷却](#4-触发与重复冷却)
5. [效果](#5-效果)
6. [Projectile 接入](#6-projectile-接入)
7. [动画接口](#7-动画接口)
8. [配置与使用](#8-配置与使用)
9. [测试与验收](#9-测试与验收)
10. [已知限制](#10-已知限制)

---

## 1. 模块职责

| 文件 | 职责 |
|---|---|
| `Burst/BurstTypes.cs` | 触发类型、碰撞过滤、`BurstContext` |
| `Burst/BurstDefinition.cs` | 爆发配置资产 |
| `Burst/BurstRuntime.cs` | 每发子弹的计时、冷却、次数、执行 |
| `Burst/BurstEffect.cs` | 效果基类 |
| `Burst/BurstTargetUtility.cs` | 范围目标收集与阵营/发射者过滤 |
| `Burst/Effects/BurstDamageEffect.cs` | 范围伤害 |
| `Burst/Effects/BurstForceEffect.cs` | 牵引/击退 |
| `Burst/Effects/BurstTerrainEffect.cs` | 调用场景地形破坏 |

爆发不直接操作 Animator、粒子或音效；表现通过事件接入。

---

## 2. 数据流

```text
ProjectileConfig.bursts
        ↓
ProjectileBuild（修改器可追加 BurstDefinition）
        ↓
Projectile.BurstRuntimes
        ↓ 满足 Trigger
BurstContext
        ↓
BurstDefinition.effects（按顺序）
        ├─ BurstDamageEffect
        ├─ BurstForceEffect
        └─ BurstTerrainEffect
```

---

## 3. BurstDefinition

路径：`Assets/Data/Bursts/`（资产由 `CreateAssetMenu` 创建）

| 字段 | 含义 |
|---|---|
| `burstId` | 调试/识别用 |
| `trigger` | `OnLaunch / OnTimer / OnImpact / OnExpire / Manual` |
| `delay` | `OnTimer` 首次触发延迟 |
| `cooldown` | 两次成功触发的最小间隔 |
| `maxTriggers` | 最大次数；`0 = 无限` |
| `impactFilter` | `OnImpact` 可响应的目标 |
| `minImpactSpeed` | 碰撞速度低于此值不触发 |
| `destroyOnTrigger` | 效果执行后回收子弹 |
| `viewPrefab` | 以后动画层使用的表现 prefab，当前逻辑不使用 |
| `effects` | 效果列表，按顺序执行 |

`destroyOnTrigger = true` 时 `maxTriggers` 会被校正为 1。

---

## 4. 触发与重复/冷却

### 触发类型

| 类型 | 首次 | 可重复 |
|---|---|---|
| `OnLaunch` | 出膛立即 | 否 |
| `OnTimer` | `delay` 秒后 | 是，按 `cooldown` |
| `OnImpact` | 第一次满足过滤的碰撞 | 是，按 `cooldown` |
| `OnExpire` | 子弹消失前 | 否，只一次 |
| `Manual` | `Projectile.TriggerManualBursts()` | 是，按 `cooldown` |

### OnTimer 时间轴

```text
Launch
  → delay
  → 第 1 次
  → cooldown
  → 第 2 次
  → ... 直到 maxTriggers
```

- `cooldown = 0` 时重复触发会很快，`maxTriggers = 0` 时由 Validate 使用最小冷却保护
- `OnLaunch` / `OnExpire` 忽略冷却与重复

### OnImpact

- 先结算直接命中，再执行爆发
- `impactFilter` 区分 `Character / Terrain / Other`
- `minImpactSpeed` 过滤轻碰
- 冷却从上一次成功触发开始计算，未命中不重置冷却

---

## 5. 效果

### BurstDamageEffect

| 字段 | 含义 |
|---|---|
| `radius` | 范围 |
| `targetMask` | 目标层 |
| `damage` | 伤害 |
| `impulse` | 0 = 不击退；大于角色阈值会进入 Airborne/Hit |
| `useFalloff` / `falloff` | 可选距离衰减 |
| `hitAllies` / `hitOwner` | 目标过滤 |

执行链路：

```text
BurstDamageEffect
→ Character.ReceiveHit(HitInfo)
→ CharacterHealth
→ CharacterHit / CharacterVisual 闪白
```

`HitKind` 为 `Burst`。

### BurstForceEffect

| 字段 | 含义 |
|---|---|
| `radius` | 范围 |
| `force` | 冲量 |
| `mode` | Push / Pull |
| `lockControlTime` | 可选操作锁定 |
| `enterAirborne` | 是否进入 Airborne（触发已有 Hit 表现） |
| `hitAllies` / `hitOwner` | 目标过滤 |

直接作用于 `Character.Body`，不产生伤害。

### BurstTerrainEffect

| 字段 | 含义 |
|---|---|
| `breakPower` | 破坏力 |
| `radius` | 范围 |
| `shape` / `size` | Circle / Box |
| `useProfile` / `profile` | 可选破坏力→范围联动 |

调用：

```csharp
ArenaController.Instance.ApplyTerrainBreak(...)
```

---

## 6. Projectile 接入

### 配置链

```text
ProjectileConfig.bursts
→ ProjectileBuildContext.Bursts（修改器可追加）
→ ProjectileLoadout.Bursts
→ Projectile.Configure(stats, traits, bursts)
```

旧的 `Configure(stats, traits)` 保留，自动读取当前 Config 的 bursts。

### 生命周期

| 时点 | 行为 |
|---|---|
| `Awake` | 从 Config 建立 burst 列表 |
| `Configure` | 建立 `BurstRuntime` |
| `Launch` | 重置计时，执行 `OnLaunch` |
| `Update` | 推进 `OnTimer` |
| `OnCollisionEnter2D` | 直接命中 → `OnImpact` |
| `Despawn` | `OnExpire` → 特性 OnExpire → 事件 → 入池 |
| `ResetForPool` | 清空运行时状态 |

### 手动触发

```csharp
projectile.TriggerManualBursts();
```

只触发 `trigger = Manual` 的定义。

---

## 7. 动画接口

**本次不实现动画**，只保留：

1. `BurstDefinition.viewPrefab`：以后挂表现 prefab
2. `GameEvents.BurstTriggered`：每次成功触发
3. `GameEvents.BurstEffectExecuted`：每个效果执行完成

`BurstContext` 提供：

- `Projectile` / `Owner` / `Team`
- `Definition` / `Trigger` / `TriggerCount`
- `Point` / `Normal`
- `Hit` / `HitTerrain`

以后新增表现层时，只需订阅事件，不需要改 `BurstRuntime`、`Projectile` 或效果类。示例：

```csharp
private void OnBurstTriggered(Projectile projectile, BurstDefinition definition, BurstContext context)
{
    if (definition.viewPrefab == null) return;
    Instantiate(definition.viewPrefab, context.Point, Quaternion.identity);
}
```

角色受击动画本身已经由现有 `Character.ReceiveHit` / `CharacterVisual` 处理：

- `BurstDamageEffect.impulse > 阈值` → 走已有 Airborne/Hit
- `BurstForceEffect.enterAirborne = true` → 同样复用已有 Hit 表现

地形破坏表现可订阅已有的 `GameEvents.TerrainBroken`。

---

## 8. 配置与使用

### 造一个定时范围伤害

1. 创建 `BurstDamageEffect`，设 `radius` / `damage`。
2. 创建 `BurstDefinition`：
   - `trigger = OnTimer`
   - `delay = 1.5`
   - `cooldown = 0.5`
   - `maxTriggers = 1`
   - `destroyOnTrigger = true`
   - 把伤害效果放进 `effects`
3. 把 `BurstDefinition` 加进子弹的 `ProjectileConfig.bursts`。
4. 子弹配置 `destroyOnHit = false`，让它飞到定时点再爆发。

### 造一个碰撞爆炸

- `trigger = OnImpact`
- `impactFilter = Any`
- `maxTriggers = 1`
- 效果放 `BurstDamageEffect` + `BurstTerrainEffect`
- 需要立即消失：`destroyOnTrigger = true`

### 造一个持续牵引区

- `trigger = OnImpact` 或 `OnTimer`
- `maxTriggers = 0`
- `cooldown = 0.2`
- 效果放 `BurstForceEffect`（Pull）
- 子弹 `destroyOnHit = false`

### 道具追加爆发

```csharp
public sealed class AddBurstModifier : ProjectileModifier
{
    public BurstDefinition burst;

    public override void Modify(ProjectileStats stats, ProjectileBuildContext context)
    {
        context.Bursts.Add(burst);
    }
}
```

---

## 9. 测试与验收

当前结果：

- EditMode **70/70**
- PlayMode **30/30**
- `BuildAllBatch`：`BUILD_OK`

覆盖：

- Timer delay/cooldown/次数
- Impact 冷却、过滤、最低速度
- OnExpire 只触发一次
- Damage 范围伤害
- Force Pull 方向
- Terrain 爆发真的抹除地形
- Projectile Timer 重复触发
- Impact 直接命中先结算
- Build 管线复制/追加爆发
- `destroyOnTrigger` 次数校正

---

## 10. 已知限制

- `OnLaunch` / `OnExpire` 不支持重复
- 效果范围 P0 为圆形（地形支持 Circle/Box）
- 未实现表现 prefab 实例化、动画、粒子、音效
- 未实现爆发连锁
- 未实现对象池化表现
- `BurstDamageEffect` 的击退依赖现有 `CharacterCombat` 阈值
- `BurstDefinition` 资产需要手动创建；没有默认演示资产
