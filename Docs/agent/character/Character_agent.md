# 角色模块 — Agent 工作记录（Character_agent）

> 面向后续接手的 agent：决策、实现细节、验证证据、已知坑、未完成项。
> 人读版：同目录 `Character.md`；方案：`Docs/program/`（**人写，勿改**）。

## 目录

- [文档地图](#文档地图)
- [0. 当前状态与任务背景](#0-当前状态与任务背景)
- [1. 最终文件清单](#1-最终文件清单)
- [2. 关键设计决策与理由](#2-关键设计决策与理由)
- [3. 实现细节 接手必读](#3-实现细节-接手必读)
- [4. 资源引用关系](#4-资源引用关系)
- [5. 生成与校验](#5-生成与校验)
- [6. 验证记录](#6-验证记录)
- [7. 踩过的坑与修法](#7-踩过的坑与修法)
- [8. 当前状态、风险与下一步](#8-当前状态风险与下一步)
- [9. 接手检查清单](#9-接手检查清单)

---

## 文档地图

| 文档 | 内容 |
|---|---|
| `Docs/agent/character/Character.md` | 人读版：脚本功能与出入参、参数表、用法、FAQ |
| `Docs/agent/character/Character_agent.md`（本文） | 决策、实现链路、验证记录、踩坑、待办 |
| `Docs/agent/battle/Battle.md` / `Battle_agent.md` | 战斗轮：血量制、先手、额外行动、验收 |
| `Docs/agent/skin/Skin.md` / `Skin_agent.md` | 皮肤与动画体系（配置、切换、Animator 契约） |
| `Docs/agent/tooling/Tooling.md` / `Tooling_agent.md` | 资源生成、层配置、测试场景、HUD、结构校验 |
| `Docs/agent/input/Input.md` / `Input_agent.md` | 输入设备层、`BattleInputController`、调试动作 |
| `Docs/agent/README.md` | 索引 |
| `Docs/program/**` | **人写**，agent 不修改 |

## 0. 当前状态与任务背景

- **玩法**：双人对战，两人轮流用鼠标
- 角色模块最初只实现预制体、受击击退、撞击事件；后续已接入血量、战斗轮、皮肤/动画、对象池和测试框架。
- **当前程序集**：运行时已拆为 `NineDays.Game` / `Input` / `Settings` / `UI` / `Battle` / `Camera` / `Debug`；Editor 工具在 `Assets/Scripts/Editor/NineDays.Editor.asmdef`；测试在 `Assets/Tests`。详见 `Docs/agent/asmdef/Asmdef.md`。
- 当前自动化基线：EditMode `81/81`、PlayMode `34/34`，`BuildAllBatch` = `BUILD_OK`。
- prefab、内容配置、测试场景仍以代码生成为主；手工改数值只改 SO，不要随手重跑生成器。

## 1. 最终文件清单

### 角色相关运行时（节选）

| 路径 | 职责 | 关键点 |
|---|---|---|
| `Scripts/Events/GameEvents.cs` | 枚举、`HitInfo`/`ImpactInfo`、`AnimParams`、静态事件总线 | 含 `ClearAll()` + `[RuntimeInitializeOnLoadMethod]` |
| `Scripts/Character/Character.cs` | 状态机、队伍、接地、对外 API | 接地用 `ContactFilter2D + OverlapCircle`（无 GC） |
| `Scripts/Character/CharacterCombat.cs` | 受击、击退、撞障碍冲量上报 | 去重按 `Collider2D + cooldown` |
| `Scripts/Character/CharacterHealth.cs` | 血量、扣血、战败 | 战斗轮血量制使用 |
| `Scripts/Character/CharacterVisual.cs` | 皮肤实例化/切换、翻面、闪白、Animator 桥 | 翻容器镜像朝向；无 Animator 时用代码倾斜兜底 |
| `Scripts/Character/SkinAnimator.cs` | 皮肤动画阶段机（状态名驱动 + 轻受击 / 死亡表现） | 阈值读 `Config.hitThreshold`；详见 `Skin_agent.md` |
| `Scripts/Character/CharacterLauncher.cs` | 拖拽瞄准 + 发射 | 实现 `IAimInputReceiver`；受 `CanAct` 门控 |
| `Scripts/Input/GameInput.cs` | 输入门面 | 快照、取消消费、测试注入 |
| `Scripts/Battle/BattleInputController.cs` | 战斗输入路由 | 只转发给当前回合角色；归 `NineDays.Battle` |
| `Scripts/Bullet/Projectile.cs` | 子弹原型 | 出膛忽略 owner，命中构造 `HitInfo` |
| `Scripts/Bullet/ShotSpawner.cs` | 出膛点解析 | 身体中心 + 避障平移 |
| `Scripts/Battle/TurnController.cs` | 回合流程 | 不读取输入；结算超时兜底 |
| `Scripts/Scene/ArenaBounds.cs` | 边界 / 空气墙 / 出界兜底 | 完全不依赖场景里的墙 |
| `Scripts/Config/CharacterConfig.cs` | 角色数值 SO（含 `Skin` 嵌套结构） | 皮肤表 + 出膛避障参数 |
| `Scripts/Config/ProjectileConfig.cs` | 子弹数值 SO | — |

### 角色相关编辑器（`Scripts/Editor/`）

| 路径 | 职责 |
|---|---|
| `ProjectSetup.cs` | 写 `TagManager`：Layer / Sorting Layer（幂等） |
| `EditorAssets.cs` | 路径常量、内置精灵、材质、序列化字段读写 |
| `PrefabFactory.cs` | 生成皮肤 / 配置 / 子弹 / 角色 / 变体 |
| `ArenaBuilder.cs` | 生成 `TestArena` 场景 |
| `BuildCheck.cs` | 结构自检（失败即抛异常） |
| `BuildPipeline.cs` | 菜单入口 + batchmode 入口 |

### 资源（代码生成，勿手改）

```
Assets/Prefabs/Characters/Character_Base.prefab, Character_P1.prefab, Character_P2.prefab
Assets/Prefabs/Skins/Skin_Default.prefab, Skin_Alt.prefab
Assets/Prefabs/Projectiles/TestBall.prefab
Assets/Data/Characters/CharacterConfig_Default.asset
Assets/Data/Projectiles/TestBallConfig.asset
Assets/Materials/AimLine.mat
Assets/Scenes/TestArena.unity
```

## 2. 关键设计决策与理由

### 2.1 不做 Hook 刚体

原 demo 用 `SpringJoint2D.connectedBody` 指向一个 Kinematic `Hook` 子物体。
本模块不用弹簧（发射 = 直接给速度），因此**不建 Hook**，只保留空 `AimPivot`：

- 少一个刚体、少一处物理状态
- 避开 demo 的坑：`GetComponentsInChildren<Collider2D>(true)` 会把锚点碰撞体算进"发射者碰撞集合"
- 以后要做"拉弓回弹"，给 `AimPivot` 补 Kinematic 刚体 + `SpringJoint2D` 即可，层级不用改

### 2.2 物理体禁止旋转（`freezeRotation = true`）

摔倒 / 翻滚只做在 `Visual` 上（暂用代码倾斜，接美术后换成动画）。
理由：起身零物理对齐，不会出现"半截埋地里""贴墙卡住"。

### 2.3 事件用静态总线，不用 ScriptableObject EventChannel

- 双人对战、模块少，静态事件最轻
- `GameEvents.ClearAll()` 在 `SubsystemRegistration` 阶段自动调用，避免关闭 Domain Reload 时订阅翻倍
- 组件级还保留 `Character.StateChanged`，给表现层用（不经过全局总线）

### 2.4 撞击力度算法

**实测** Unity 6000.0.83f1 的 `UnityEngine.Physics2DModule.dll`：`Collision2D` **没有** `impulse` 属性，
但 `ContactPoint2D` 有 `normalImpulse` / `tangentImpulse`。因此：

```csharp
impulse = Σ(|normalImpulse| + |tangentImpulse|)   // N·s
force   = impulse / Time.fixedDeltaTime           // 近似力度 N
```

阈值判 `impulse`（质量相关），`speed` 只作为附带信息。
⚠️ Trigger 碰撞不产生接触冲量（恒为 0），障碍必须是实体碰撞体。

### 2.5 出膛点 = 身体中心（用户实测反馈后修正）

初版用 `character.AimPivot.position`（本地 `+0.55, +0.1`，已在 1×2 胶囊外面）。
角色贴墙时该点落进墙里 → 球出膛即撞墙 → 表现为"永远发不出球"。
改为 `ShotSpawner.GetOrigin()` = `Body.position`，并加避障平移（次数/步长走 `CharacterConfig`）。

### 2.6 prefab 全部代码生成

`PrefabFactory` 用 `PrefabUtility.SaveAsPrefabAsset` + `SerializedObject` 写私有字段，
`BuildCheck` 断言结构。好处：可重复执行、可 review、无手拖冲突。

### 2.7 asmdef 与测试

当前运行时已拆为 `NineDays.Game`、`NineDays.Input`、`NineDays.Settings`、`NineDays.UI`、`NineDays.Battle`、`NineDays.Camera`、`NineDays.Debug`；Editor 工具和 `Assets/Tests` 下的 EditMode / PlayMode 测试程序集分别引用所需模块。详见 `Docs/agent/asmdef/Asmdef.md`。
角色相关测试由 `BattleSessionTests`、`ProjectileHitTests`、`TerrainTests` 等覆盖；不要再按"无 asmdef、测试延后"的旧假设开发。

## 3. 实现细节 接手必读

### 3.1 状态机

| 状态 | 进入条件 | 离开条件 |
|---|---|---|
| `Idle` | 初始 / 起身结束 | 按住左键 → `Aiming`；受击 → `Airborne` |
| `Aiming` | `CanAct == true` 且按下左键 | 松开发射 / 右键取消 / 回合结束 → `Idle` |
| `Airborne` | 受击冲量 ≥ `hitThreshold` | 接地 且 `|vy| ≤ landedSpeed` → `Downed` |
| `Downed` | 落地 / 出界回位 / 重开 | `getUpDelay` 后 → `GettingUp` |
| `GettingUp` | `Downed` 计时结束 | `getUpDuration` 后 → `Idle` + `CharacterRecovered` |

- `CanAct = 回合许可 && controlLockTimer <= 0 && State ∈ {Idle, Aiming}`
- `IsSettled = IsGrounded && settleTimer >= settleTime`（`settleTimer` 在速度 ≤ `settleSpeed` 时累加）

### 3.2 受击链路（顺序不能乱）

```
Projectile.OnCollisionEnter2D
  → 过滤：自身 / 同队（friendlyFire=false）
  → new HitInfo(source, owner, team, Projectile, point, normal, impulse, speed, damage)
  → target.ReceiveHit(info)
      → CharacterCombat.ReceiveHit
          1) GameEvents.RaiseCharacterHit
          2) CharacterVisual.Flash()
          3) info.Impulse < hitThreshold → return（只闪白）
          4) direction = (Body.position - info.Point).normalized（退化用 -normal / up）
             impulse  = clamp(info.Impulse * knockbackScale + minKnockback, 0, maxKnockback)
             rb.linearVelocity = 0; rb.AddForce(direction * impulse, Impulse)
             LockControl(controlLockTime)
          5) Character.EnterAirborne()
          6) GameEvents.RaiseCharacterKnockedBack
  → destroyOnHit → Destroy(projectile)
```

注意：先清速度再加冲量，否则"迎面被打"时旧速度会抵消击退方向。

### 3.3 撞障碍上报

`CharacterCombat.OnCollisionEnter2D`：

1. `obstacleMask` 过滤（只认 `Obstacle` 层）
2. `SumImpulse` 累加接触点冲量
3. `impact = impulse`，`force = impulse / fixedDeltaTime`
4. **每次都发 `ImpactDetected`**（带 `AboveThreshold` 标志，供 F1 调试）
5. 达阈值 → `StrongImpact`，且同一 `Collider2D` 在 `impactCooldown` 内去重
6. 接触点为 0 时用刚体位置兜底

### 3.4 发射链路

```
CharacterLauncher.Update
  → 不在自己回合 / 有控制锁 → 取消瞄准
  → 左键按下 → BeginAim（记录 dragOrigin）
  → 拖动 → aimPower = clamp01(距离 / maxDragDistance)，FaceTo(反方向)
  → 松开 → Fire()
      direction = -pull.normalized
      speed     = lerp(minLaunchSpeed, maxLaunchSpeed, aimPower)
      ShotSpawner.Spawn(prefab, shooter, direction, speed)
        origin = Body.position → 被障碍占住则沿方向平移（spawnNudgeAttempts/Step）
        Instantiate → Projectile.Launch(dir * speed, shooter)
      GameEvents.RaiseShotFired → TurnController 进入 Resolving 并 SetCanAct(false)
```

调试面板空格发球走同一个 `ShotSpawner.Spawn`，保证行为一致。

### 3.5 回合结算

`TurnController.Update` 在 `IsResolving` 时：

```
HasSettled() = 场上没有 Projectile && 所有角色 IsSettled
resetTimer 累计超过 resolveTimeout(3s) 也强制结算
EndTurn → RaiseTurnEnded → BeginTurn(OpponentOf)
```

`Resolving` 期间双方 `SetCanAct(false)`，避免"球还在飞又补一枪"。

### 3.6 皮肤与 Animator 契约

- 逻辑 prefab 不感知美术；`CharacterVisual.ApplySkin` 销毁旧实例 → 实例化 `skin.visualPrefab` → 重绑 Animator → 重写当前状态
- 动画实际由 `SkinAnimator` 阶段机按**状态名**驱动：`Idle/PreAiming/Aiming/Fire/LightHIt/HeaveHit/Death/Died`，代码只写 float `AimPower`
- `AnimParams`（`State/Grounded/Hit/GetUp`）是预留软契约，当前未驱动
- 轻受击（冲量 < `hitThreshold`，阈值与 `CharacterCombat` 同源）只播 `LightHIt`，不动状态机
- 死亡链路：`Defeated → Death（0.5s）→ Died（循环保持）→ ResetForBattle 退出`
- 朝向翻转翻容器 `visualRoot.localScale.x`（逐部件 `flipX` 会让拼图皮肤破碎）
- 排序：`sortingOrder = skin.sortingOrder + 美术原 order`；挂错 Sorting Layer 的部件归并到多数部件所在层
- 骨骼皮肤额外支持 `CharacterConfig.Skin.spriteLibrary → SpriteLibrary.spriteLibraryAsset`

### 3.7 出界兜底

`ArenaBounds.Update` 每 `checkInterval` 检查一次：不在 `playArea` 内 → `RaiseCharacterOutOfBounds` → `ResetToSpawn()`（回出生点并进入 `Downed`）。
空气墙由 `ArenaBounds` 自己在 `Awake` 生成（`createInvisibleWalls`），不依赖场景里的任何墙物体。

## 4. 资源引用关系

```
Character_P1.prefab (Variant, team=1, initialSkinIndex=0)
Character_P2.prefab (Variant, team=2, initialSkinIndex=1)
        └─ 基础：Character_Base.prefab
                ├─ Character.config      → CharacterConfig_Default.asset
                │        └─ skins[0]     → Skin_Default.prefab
                │           skins[1]     → Skin_Alt.prefab
                ├─ CharacterVisual.config → 同一个 CharacterConfig
                └─ CharacterLauncher.projectilePrefab → TestBall.prefab
                        └─ Projectile.config → TestBallConfig.asset

TestArena.unity
├─ Character_P1 / Character_P2（预制体实例）
├─ TurnController.characters[0..1] → 两个 Character 组件
├─ DebugHUD / TurnBanner
├─ ArenaBounds(playArea = -8,-5,16,10)
├─ ArenaController（运行时重建地形，不保留静态 Ground）
├─ UIRoot / ProjectilePool
└─ Main Camera（CameraRig / CameraDirector / BattleCameraController）
```

## 5. 生成与校验

### 菜单

| 菜单 | 作用 |
|---|---|
| `9Days/Setup/Layers & Sorting Layers` | 写 `TagManager.asset`（幂等：已存在则跳过） |
| `9Days/Build/Prefabs (角色 + 皮肤 + 子弹)` | 生成 prefab + SO，并跑 `BuildCheck` |
| `9Days/Build/Test Arena` | 重建 `TestArena.unity` |
| `9Days/Build/All (Setup + Prefabs + Arena)` | 全量 |

### batchmode

```
Unity.exe -batchmode -quit -projectPath <项目> ^
  -executeMethod NineDays.EditorTools.BuildPipeline.BuildAllBatch ^
  -logFile <项目>\Temp\build.log
```

成功日志末尾 `BUILD_OK`；异常时打印 `BUILD_FAILED` 并 `EditorApplication.Exit(1)`。
**执行前必须关闭 Unity Editor**（否则工程被 `Temp/UnityLockfile` 占用，启动失败）。

### BuildCheck 断言（结构契约）

- 角色 prefab：有 `Rigidbody2D` / `CapsuleCollider2D` / `Character` / `CharacterCombat` / `CharacterLauncher`
- 角色根节点**无** `SpriteRenderer`
- `Visual` 有 `SortingGroup` + `CharacterVisual`，且其下**无** `Collider2D` / `Rigidbody2D`
- `AimPivot` **无**刚体 / 碰撞体；`GroundCheck`、`HitPoint` 存在
- `Character.config`、`body`、`combat.character`、`visual.config`、`launcher.projectilePrefab` 非空
- 皮肤 prefab：无刚体 / 碰撞体、根节点在原点、至少有 `SpriteRenderer` 或 `Animator`
- 子弹：`Rigidbody2D` + `CircleCollider2D` + `Projectile.config` 非空
- 配置：`skins.Count >= 2` 且每套 `visualPrefab` 非空；子弹 `radius/initialSpeed > 0`

### 幂等性

- 目录用 `EnsureFolder` 递归创建
- 配置资源用 `LoadAssetAtPath` 复用，字段整体重写（不是每次都 `CreateAsset`）
- prefab 用 `SaveAsPrefabAsset` 覆盖保存
→ 可以反复执行，不会产生重名资源

## 6. 验证记录

当前全量基线（2026-11-05）：

| 项 | 方式 | 结果 |
|---|---|---|
| 资源生成 | batchmode `BuildAllBatch` | `BUILD_OK`，`BuildCheck` 通过 |
| EditMode | 全量测试 | **81/81 Passed** |
| PlayMode | 全量测试 | **34/34 Passed** |
| 场景结构 | 读 YAML / `BuildCheck` | P1/P2、BattleController、BattleInputController、UIRoot、ProjectilePool、相机组件存在；地形由 ArenaController 重建 |
| 变体覆盖 | 读 `Character_P2.prefab` 的 `m_Modifications` | `team: 2`、`initialSkinIndex: 1` |

> 具体测试用例和数字会随全项目变化；不要在本文件里写死旧的单模块数字，统一使用全量基线。

## 7. 踩过的坑与修法

| # | 现象 | 根因 | 修法 |
|---|---|---|---|
| 7.1 | 角色贴墙时球永远发不出 | 初版出膛点用 `AimPivot`（本地 `+0.55,+0.1`，在 1×2 胶囊外 0.05），贴墙时落在墙体内，球出膛即撞墙销毁 | 改用 `Body.position` + `ShotSpawner` 避障平移；次数/步长提到 `CharacterConfig` |
| 7.2 | 拿不到撞击冲量 | Unity 6 的 `Collision2D` 没有 `impulse`（`Physics2DModule.dll` 实测无 `get_impulse`） | 累加 `ContactPoint2D.normalImpulse + tangentImpulse`，`force = impulse / fixedDeltaTime` |
| 7.3 | F2 / F3 像没实现 | Unity Editor 在 Scene / Hierarchy 聚焦时吞掉 F 键；且原实现只是开关，没有"20 次统计" | 增加 `T` / `G` 别名；F2 改为 20 次自动对射 + `shots/hits/knockback/strong/getup` 统计；F3 在 HUD 显示 `TimeScale` |
| 7.4 | 不能复用 demo 的 `Idle.anim` | 该 clip 是空 path 的 `m_LocalPosition` / `m_LocalScale` 绝对曲线，绑到 prefab 会把所有实例拉到同一坐标 | 不复用；无美术时用 `CharacterVisual` 代码倾斜兜底 |
| 7.5 | 写不进 prefab 的私有字段 | 没有 `AddComponent` 的 public setter | 统一用 `SerializedObject.FindProperty(...)` 写（`EditorAssets.SetRef/SetInt/...`） |
| 7.6 | 皮肤 prefab 若带碰撞体会污染物理 | `GetComponentsInChildren<Collider2D>` 会把它算进去 | `BuildCheck` 强制皮肤无刚体/碰撞体 |
| 7.7 | batchmode 启动失败 | Editor 打开时工程被锁 | 执行前关闭 Editor，检查 `Temp/UnityLockfile` 是否为 False |
| 7.9 | `SortingGroup` 编译找不到 | 它在 `UnityEngine.Rendering` 命名空间 | Editor 脚本加 `using UnityEngine.Rendering;` |
| 7.10 | 拼图皮肤瞄准转身时整体破碎 | `flipX` 只绕各部件自身 pivot 镜像、不动位置，16 个部件各自翻转 | `ApplyFacing` 改为翻 `visualRoot.localScale.x`；兜底倾斜角同步去掉 `* facing`（容器已镜像，再乘会往错方向倒） |
| 7.11 | 皮肤图层顺序错乱（头被身体盖住） | ① `ApplySkin` 用遍历索引 `i` 覆盖美术 order ② `head angry_0` / `head hurt_0` 挂在 `Background` 层（Layer 优先级高于 order） | 改为 `skin.sortingOrder + 美术原 order`；实例化时归并到多数部件所在层（病根仍建议在 prefab 里改） |

## 8. 当前状态、风险与下一步

### 已完成

- 角色状态机、受击/击退、撞障碍事件；
- 血量、战斗轮、重开；
- 皮肤与动画（`CharacterVisual` / `SkinAnimator`）；
- 子弹出膛、对象池、命中链路；
- `NineDays.Game` / `NineDays.Input` / `NineDays.Battle` 等运行时 asmdef、`NineDays.Editor` 与 EditMode / PlayMode 测试程序集；详见 `../asmdef/Asmdef.md`。

### 仍待办

- [ ] 美术资源：精灵 / 骨骼、动画片段、脚底 pivot、`visualOffset` 微调；
- [ ] 正式 UI / 音频：当前 `TurnBanner` 和 `CombatDebugHud` 仍是 OnGUI 占位；
- [ ] 更丰富的地形命中表现（碎屑、断面、音效）；
- [ ] `CharacterConfig` 的生成器覆盖策略：当前仍会重写部分字段，调数值时不要跑 `Build/Prefabs`。

### 风险

1. 静态事件若忘记取消订阅，会持有已销毁对象 → 订阅方必须在 `OnDisable` 取消（HUD 已示范）；
2. `TurnController.HasSettled()` 每帧 `FindObjectsByType<Projectile>`，子弹多时会分配内存；
3. 内容配置由生成器生成，手工修改后若重跑生成会被覆盖；
4. 角色视觉取景/碰撞依赖 `CharacterConfig` 与 prefab 结构，改字段时同步跑 `BuildCheck`。

### 下一步建议

1. 美术接入皮肤与动画；
2. 正式 UI / 音频；
3. 角色数值与对象池性能优化；
4. 需要新玩法时优先通过 `GameEvents` 扩展，不直接耦合模块。

## 9. 接手检查清单

1. 先读 `Assets/Scripts/Events/GameEvents.cs`：所有数据契约在这里；
2. 再读 `Character.cs`（状态机）→ `CharacterCombat.cs`（数值链路）→ `CharacterVisual.cs`（表现）；
3. 改**结构**：改 `Editor/PrefabFactory.cs` + `Editor/BuildCheck.cs`，然后跑 `9Days/Build/All`；
4. 改**数值**：只改 `Assets/Data/**` 下的 SO，不碰代码，也不要跑 `Build/Prefabs`；
5. 改**玩法**：优先在 `GameEvents` 加事件，避免模块间直接引用；
6. 每次改完：关闭 Editor → batchmode 生成/校验 → 看 `Temp/build.log` 是否有 `BUILD_OK`；
7. 跑 EditMode / PlayMode 全量测试，当前基线 `81/81`、`34/34`。

