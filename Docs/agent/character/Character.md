# 角色模块使用手册（人读版）

> 面向使用者的简洁文档：怎么用、每个脚本干什么、参数在哪、出入参是什么。
> 详细工作记录见同目录 `Character_agent.md`；
> 战斗轮（血量/轮次/额外行动）见 `Docs/agent/battle/Battle.md`；
> 皮肤/动画见 `Docs/agent/skin/Skin.md`；资源生成与测试场景见 `Docs/agent/tooling/Tooling.md`。

## 目录

1. [一句话说明](#1-一句话说明)
2. [目录结构](#2-目录结构)
3. [每个脚本的职责与出入参](#3-每个脚本的职责与出入参)
4. [事件 GameEvents](#4-事件-gameevents)
5. [三步上手](#5-三步上手)
6. [Inspector 可调参数](#6-inspector-可调参数)
7. [常见问题](#7-常见问题)

---

## 1. 一句话说明

角色是一个 2D 物理预制体：**拖拽瞄准 → 发射子弹 → 被打中按冲量飞出 → 落地起身 → 轮流回合**。
逻辑与美术分离，换皮肤只加资源，不改代码。

## 2. 目录结构

```
Assets/Scripts/
├─ Events/GameEvents.cs            事件与数据载荷（模块间唯一通信出口）
├─ Character/Character.cs          状态机 / 队伍 / 接地检测
├─ Character/CharacterCombat.cs    受击、击退、撞障碍冲量上报
├─ Character/CharacterVisual.cs    皮肤实例化、翻面、闪白、Animator 参数
├─ Character/CharacterLauncher.cs  拖拽瞄准与发射
├─ Bullet/Projectile.cs            子弹原型（命中上报、忽略发射者）
├─ Bullet/ShotSpawner.cs           出膛点计算（身体中心 + 避障）
├─ Battle/TurnController.cs        回合流程 + InputRouter
├─ Scene/ArenaBounds.cs            场地边界、空气墙、出界兜底
├─ Config/CharacterConfig.cs       角色数值（ScriptableObject）
├─ Config/ProjectileConfig.cs      子弹数值（ScriptableObject）
├─ Debug/CombatDebugHud.cs         测试 HUD 与热键
└─ Editor/                         资源生成工具（菜单 9Days/*）
```

## 3. 每个脚本的职责与出入参

### Character（根节点）

状态机：`Idle / Aiming / Airborne / Downed / GettingUp`

| 成员 | 说明 |
|---|---|
| `CharacterConfig Config` | 只读，当前配置 |
| `Team Team` | 只读，阵营 |
| `CharacterState State` | 只读，当前状态 |
| `bool CanAct` | 只读，是否可操作（回合 + 控制锁 + 状态） |
| `bool IsGrounded` / `bool IsSettled` | 只读，接地 / 停稳 |
| `Vector2 Velocity` / `Rigidbody2D Body` | 只读 |
| `event Action<CharacterState> StateChanged` | 状态变化回调 |
| `void SetCanAct(bool value)` | 回合切换时由 TurnController 调用 |
| `void BeginAim()` / `void CancelAim()` | 进入 / 退出瞄准 |
| `void EnterAirborne()` / `void EnterDowned()` | 状态切换 |
| `void ReceiveHit(in HitInfo info)` | 受击入口（转发给 CharacterCombat） |
| `void FaceTo(Vector2 worldPoint)` | 面向某点（只翻表现） |
| `void LockControl(float seconds)` | 控制锁 |
| `void ResetToSpawn()` | 回出生点并进入倒地→起身 |

### CharacterCombat

| 成员 | 说明 |
|---|---|
| `void ReceiveHit(in HitInfo info)` | 报事件 → 闪白 → 按冲量击退 |
| `static float SumImpulse(Collision2D)` | 累加接触点冲量（N·s） |

### CharacterVisual

| 成员 | 说明 |
|---|---|
| `void ApplySkin(int index)` | 按配置下标换皮肤 |
| `void ApplySkin(CharacterConfig.Skin skin)` | 直接指定皮肤 |
| `void FaceTo(Vector2 direction)` | 朝向（翻转 `visualRoot.localScale.x`，整副皮肤一起镜像） |
| `void Flash()` / `Flash(float duration)` | 受击闪白 |
| `void SetAimPower(float power)` | 兼容保留接口（瞄准力度已由 SkinAnimator 直读 Launcher） |

### SkinAnimator（挂皮肤 prefab 根，随皮肤实例化）

| 成员 | 说明 |
|---|---|
| ※ 无 public 方法 | 订阅 `StateChanged` / `ShotFired` / `CharacterHit`，按阶段机 CrossFade 同名状态 |
| 状态名契约 | `Idle/PreAiming/Aiming/Fire/LightHIt/HeaveHit/Death/Died`，详见 `Docs/agent/skin/Skin.md` 第 4 节 |

### CharacterLauncher

| 成员 | 说明 |
|---|---|
| `bool IsAiming` / `float AimPower` | 只读，调试用 |
| ※ 无 public 方法，靠鼠标输入驱动 | 左键拖拽 → 松开发射，右键 / Esc 取消 |

### Projectile

| 成员 | 说明 |
|---|---|
| `void Launch(Vector2 velocity, Character shooter)` | 出膛：设速度、忽略发射者一段时间 |
| `ProjectileConfig Config` / `Character Owner` / `Team Team` / `bool IsLaunched` | 只读 |

### ShotSpawner（静态工具）

| 成员 | 说明 |
|---|---|
| `static Vector2 GetOrigin(Character shooter)` | 出膛原点 = 刚体中心 |
| `static Vector2 ResolveFreePoint(origin, direction, radius, obstacleMask, attempts, step)` | 被障碍占住时找空位 |
| `static Projectile Spawn(prefab, shooter, direction, speed)` | 实例化 + 发射，失败返回 null |

### TurnController

| 成员 | 说明 |
|---|---|
| `void BeginTurn(Team team)` | 开始某队回合 |
| `void ForceSwitch(Team team)` | 强制切回合（调试） |
| `Character GetCharacter(Team)` / `GetOpponent(Team)` | 查角色 |
| `void ResetMatch()` | 清子弹、回出生点、重开 |
| `Team CurrentTeam` / `bool IsResolving` / `int TurnIndex` | 只读状态 |

### ArenaBounds

| 成员 | 说明 |
|---|---|
| `Rect PlayArea` | 只读，逻辑边界 |
| `bool Contains(Vector2 worldPosition)` | 点是否在边界内 |

### InputRouter（静态类，鼠标 / 键盘统一入口）

| 成员 | 说明 |
|---|---|
| `bool LeftPressed / LeftReleased / LeftHeld / RightPressed / EscapePressed` | 鼠标按键状态 |
| `Vector2 PointerScreen` / `PointerWorld(Camera cam)` | 屏幕 / 世界坐标（假设玩法平面 z = 0） |
| `bool KeyPressed(Key key)` | 键盘，`Key` 来自 `UnityEngine.InputSystem` |
| `bool HasPointer` | 是否有鼠标设备 |

### CombatDebugHud（仅测试场景）

| 成员 | 说明 |
|---|---|
| `void FireTestShot()` | 当前回合角色朝对手直接发一颗球（走同一个 `ShotSpawner`） |
| 热键 | 空格 / `1` `2` / `R` / `F1` / `T`(F2) / `G`(F3)，详见 `Docs/agent/tooling/Tooling.md` |

### CharacterConfig / ProjectileConfig（ScriptableObject）

数值字段见第 6 节；皮肤相关字段见 `Docs/agent/skin/Skin.md` 第 2 节。

## 4. 事件 GameEvents

| 事件 | 参数 | 何时 |
|---|---|---|
| `CharacterHit` | `Character, HitInfo` | 被任何东西命中 |
| `CharacterKnockedBack` | `Character, HitInfo` | 冲量达阈值被击飞 |
| `ImpactDetected` | `Character, ImpactInfo` | 与障碍碰撞（含未过阈值） |
| `StrongImpact` | `Character, ImpactInfo` | 与障碍碰撞且过阈值 |
| `CharacterDowned` / `CharacterRecovered` | `Character` | 倒地 / 起身完成 |
| `CharacterOutOfBounds` | `Character` | 出界 |
| `ShotFired` | `Character, Projectile` | 发射 |
| `TurnStarted` / `TurnEnded` | `Team` | 回合开始 / 结束 |

订阅示例（UI 或音效）：

```csharp
void OnEnable()  => GameEvents.CharacterHit += OnHit;
void OnDisable() => GameEvents.CharacterHit -= OnHit;

void OnHit(Character c, HitInfo info)
{
    Debug.Log($"{c.Team} 被 {info.SourceName} 打中，冲量 {info.Impulse:0.00}");
}
```

## 5. 三步上手

1. **场景里放**：直接拖 `Assets/Prefabs/Characters/Character_P1.prefab`（或 `_P2`）进场景
2. **配回合**：场景中放一个空物体挂 `TurnController`，把两个角色拖进 `characters` 列表
3. **调数值**：改 `Assets/Data/Characters/CharacterConfig_Default.asset`；只改 SO 时不要跑 `Build/Prefabs`，内容生成器会重写部分配置字段

测试场景 `Assets/Scenes/TestArena.unity` 已经按上面配好，可直接 Play。

## 6. Inspector 可调参数

| 位置 | 参数 | 作用 |
|---|---|---|
| `CharacterConfig` | `hitThreshold` | 多小的冲量算"被打飞" |
| | `maxHealth` | 战斗轮血量上限（默认 100） |
| | `knockbackScale` / `minKnockback` / `maxKnockback` | 飞多远 |
| | `controlLockTime` | 飞出去后多久不能操作 |
| | `impactThreshold` | 撞障碍多大力度才报事件 |
| | `landedSpeed` / `getUpDelay` / `getUpDuration` | 落地 / 倒地 / 起身节奏 |
| | `spawnNudgeAttempts` / `spawnNudgeStep` | 出膛点被墙占住时的避让 |
| `CharacterLauncher` | `maxDragDistance` / `min-maxLaunchSpeed` | 拖拽距离与出膛速度 |
| `CharacterVisual` | `flashDuration` / `fallbackTiltSpeed` | 受击闪白时长 / 未接动画时的倾斜速度 |
| `ProjectileConfig` | `mass` / `radius` / `initialSpeed` / `ignoreOwnerTime` | 子弹物理与手感 |
| `TurnController` | `resolveTimeout` | 结算超时（防卡回合） |
| `ArenaBounds` | `playArea` / `wallThickness` / `recoverOutOfBounds` | 场地大小与出界兜底 |

## 7. 常见问题

| 现象 | 处理 |
|---|---|
| 拖拽没反应 | 看 HUD 的 `canAct`；结算中或非当前回合都是 N |
| 球从奇怪的位置出来 | 出膛点是**身体中心**；若被墙占住会沿发射方向平移（`spawnNudge*` 控制） |
| 撞墙没报 `StrongImpact` | 用 `F1` 看 `impulse` 是否小于 `impactThreshold`；障碍必须是实体碰撞体 |
| 角色掉出地面 | `Ground` 的 Layer 必须是 `Obstacle` |
| 热键没反应 | 先点一下 Game 视图（Scene 视图会吞掉 F 键），或用 `T` / `G` 别名 |
| 换皮肤没动画 | Controller 缺同名状态（注意 `LightHIt` 是大写 I）或缺 `AimPower` 参数，见 `Skin.md` 第 8 节 |
