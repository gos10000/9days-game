# 镜头控制模块（人读版）

> 一句话：**CameraFramer 算取景，CameraRig 做平滑/震动，CameraDirector 管目标，BattleCameraController 把战斗事件翻译成镜头行为。**
> 自研、无 Cinemachine 依赖；本轮只做镜头框架，不做剧情演出。

## 目录

1. [模块定位](#1-模块定位)
2. [文件与职责](#2-文件与职责)
3. [场景结构](#3-场景结构)
4. [镜头行为](#4-镜头行为)
5. [CameraFramer](#5-cameraframer)
6. [CameraRig](#6-camerarig)
7. [CameraDirector](#7-cameradirector)
8. [BattleCameraController](#8-battlecameracontroller)
9. [与输入 / UI / 设置的关系](#9-与输入--ui--设置的关系)
10. [新增镜头行为](#10-新增镜头行为)
11. [验证与已知限制](#11-验证与已知限制)

---

## 1. 模块定位

负责：

- 2D 正交相机；
- 全景 / 双人取景；
- 飞行子弹跟踪；
- 场地边界限制；
- 相机位置和缩放的平滑；
- 命中、爆发、击败时的震动。

不负责：

- 3D 自由视角；
- 鼠标滚轮缩放；
- 剧情运镜；
- Cinemachine / Timeline 集成；
- 玩家设置页面。

镜头模块与内容配置无关：不新增 `Config` 资产，也不纳入子弹/角色 prefab 配置。

---

## 2. 文件与职责

| 文件 | 职责 |
|---|---|
| `Camera/CameraPose.cs` | 镜头姿态：中心点 + `orthographicSize` |
| `Camera/CameraFramer.cs` | 纯数学取景与边界夹取 |
| `Camera/CameraRig.cs` | 平滑移动/缩放、trauma 震动 |
| `Camera/CameraDirector.cs` | 全景/目标列表/边界管理 |
| `Camera/BattleCameraController.cs` | 订阅 `GameEvents`，驱动战斗镜头 |

---

## 3. 场景结构

`ArenaBuilder` 生成的 `TestArena` 主相机：

```text
Main Camera (Tag = MainCamera)
 ├─ Camera (Orthographic)
 ├─ AudioListener
 ├─ CameraRig
 │    targetCamera = 自身 Camera
 ├─ CameraDirector
 │    rig = CameraRig
 │    bounds = ArenaBounds
 └─ BattleCameraController
      director = CameraDirector
      turnController = TurnController
      characters = P1 / P2
```

相机保持原来的 `z = -10`。`CameraRig` 在 `Awake` 记录这个 z 值，之后只改 x/y。

---

## 4. 镜头行为

| 时机 | 行为 |
|---|---|
| 场景启动（可选） | **开场介绍**：先特写 `introFirstTeam`（默认 P1），停留后平滑移到对手，再交回常规逻辑 |
| `BattleStarted` | 立即回到整个场地全景 |
| `TurnStarted` | 平滑框住当前行动角色 + 对手 |
| `ShotFired` | 把飞行中的子弹加入取景 |
| `ProjectileExpired` | 移除子弹；最后一颗子弹结束后回到双方取景 |
| `CharacterHit` | 按命中冲量增加震动 |
| `StrongImpact` | 撞障碍时增加震动 |
| `CharacterDefeated` | 强震动 |
| `BurstTriggered` | 爆发震动 |
| `BattleEnded` | 清空跟踪，平滑回到全景 |

瞄准期间镜头不追角色，避免 `ScreenToWorldPoint` 随镜头移动导致拖拽方向跳变。  
子弹跟踪只发生在发射之后，且到最后一颗子弹结束为止。

---

## 5. CameraFramer

纯静态类，不依赖 `MonoBehaviour`，可直接单测。

```csharp
public static CameraPose Frame(
    IReadOnlyList<Vector2> points,
    float padding,
    float minSize,
    float maxSize,
    float aspect,
    Rect bounds,
    float overviewPadding = 0f);

public static CameraPose FrameOverview(
    Rect bounds, float padding, float minSize, float maxSize, float aspect);
```

| 参数 | 含义 |
|---|---|
| `points` | 目标世界坐标；为空时按全景计算 |
| `padding` | 目标框选时的世界单位边距 |
| `minSize` / `maxSize` | 正交尺寸限制 |
| `aspect` | 相机宽高比 |
| `bounds` | 场地边界 |
| `overviewPadding` | 全景额外留白 |

边界规则：

- 视野比场地宽/高时，对应轴居中；
- 视野比场地小时，相机中心被夹在场地内；
- `bounds` 宽高为 0 时不夹取。

`CameraPose`：

```csharp
Vector2 Center;          // 世界中心
float OrthographicSize;  // 正交尺寸
```

---

## 6. CameraRig

挂在主相机上。

| 字段 | 默认值 | 作用 |
|---|---|---|
| `targetCamera` | 自身 Camera | 实际相机 |
| `positionSmoothTime` | 0.25 | 位置平滑时间 |
| `sizeSmoothTime` | 0.35 | 正交尺寸平滑时间 |
| `useUnscaledTime` | false | 是否使用非缩放时间 |
| `shakeAmplitude` | 0.25 | 震动最大位移 |
| `shakeFrequency` | 18 | 震动噪声频率 |
| `shakeDecay` | 1.5 | trauma 每秒衰减 |
| `shakeScale` | 1 | 震动强度倍率 |

公开方法：

```csharp
void SetPose(CameraPose pose, bool snap = false);
void AddShake(float strength);    // 0~1
void SetShakeScale(float scale);  // 以后接玩家设置
CameraPose CurrentPose { get; }
float Trauma { get; }
```

震动使用 trauma 模型：

```text
shake = trauma²
trauma -= decay * deltaTime
offset = PerlinNoise * amplitude * shake
```

震动只加在最终相机位置，不影响 `CameraDirector` 的目标姿态。

---

## 7. CameraDirector

通用目标管理，不订阅战斗事件。

| 字段 | 默认值 | 作用 |
|---|---|---|
| `rig` | 自身 CameraRig | 执行层 |
| `bounds` | 场景 ArenaBounds | 场地边界 |
| `minOrthographicSize` | 4.5 | 最近 |
| `maxOrthographicSize` | 7 | 最远 |
| `framingPadding` | 1 | 目标边距 |
| `overviewPadding` | 0.5 | 全景边距 |
| `boundsPadding` | 0.25 | 场地外允许留白 |
| `maxTargets` | 4 | 最多取景目标数 |

公开方法：

```csharp
void ShowOverview(bool snap = true);
void SetTargets(IReadOnlyList<Transform> targets, bool snap = false);
void AddTarget(Transform target, bool snap = false);
void RemoveTarget(Transform target, bool snap = false);
void FocusSingle(Transform target, float orthographicSize, bool snap = false);  // 特写
void ClearSizeOverride();
void AddShake(float strength);
bool IsOverview { get; }
int TargetCount { get; }
```

行为：

- 目标列表为空 → 自动全景；
- 目标被销毁 → `LateUpdate` 自动剔除；
- 目标数量超过 `maxTargets` → 只取前几个；
- `SetTargets` 会替换列表，`AddTarget`/`RemoveTarget` 用于子弹临时目标；
- `FocusSingle` 走「临时缩放覆盖」：`sizeOverride = true`、`overrideSize = 传入值`，`RefreshPose` 会**直接把中心对准该目标**（不做边界夹取），且**不依赖 `ArenaBounds`**；
- `ClearSizeOverride` 清掉覆盖；任何 `SetTargets` / `ShowOverview` 也会自动清掉，避免常规镜头继承特写缩放；
- `Start` 只在「没有已设目标且没有特写覆盖」时才播全景，防止把开场介绍抢掉。

---

## 8. BattleCameraController

战斗适配层，只负责“什么时候看谁”。

| 字段 | 作用 |
|---|---|
| `director` | CameraDirector |
| `turnController` | 取当前行动角色和对手 |
| `characters` | `TurnController` 为空时的兜底列表 |
| `followProjectiles` | 是否跟踪飞行子弹 |
| `playIntro` | 是否播开场介绍（默认开） |
| `introFirstTeam` | 先特写哪一方（默认 `Player1`，对手自动算） |
| `introOrthographicSize` | 特写缩放（默认 3.2） |
| `introHoldSeconds` | 每个角色停留时间（默认 0.9s） |
| `introMoveSeconds` | 从第一个角色平滑移到第二个的等待时间（默认 0.8s） |
| `hitShakeBase` / `hitShakePerImpulse` | 命中震动 |
| `strongImpactShakeBase` / `strongImpactShakePerImpulse` | 撞障碍震动 |
| `defeatedShake` | 击败震动 |
| `burstShake` | 爆发震动 |

它只订阅 `GameEvents`，不直接修改相机；相机核心可在其他场景复用。

开场介绍（`Start()` 里启动协程）：

```text
snap 到 first（introFirstTeam） → 停 introHoldSeconds
→ 平滑移到对手（snap=false，由 CameraRig 的 smoothTime 过渡）
→ 停 introMoveSeconds + introHoldSeconds
→ ClearSizeOverride → 按 TurnController.CurrentTeam 重算常规目标
```

开场期间 `introPlaying = true`，`OnBattleStarted` / `OnTurnStarted` / `ApplyTargets` 全部提前返回，避免回合事件把镜头抢回双人构图；`OnDisable` 会 `StopIntro()` 并清掉缩放覆盖。

---

## 9. 与输入 / UI / 设置的关系

- `Main Camera` Tag 必须保留，`CharacterLauncher` 依赖 `Camera.main`；
- 瞄准期间不移动镜头；
- 发射后才跟踪子弹；
- UI 是 Screen Space Overlay，不受镜头震动影响；
- 玩家设置以后可以加 `cameraShake`，通过 `CameraRig.SetShakeScale` 接入；
- 本轮没有设置页面，也没有把镜头参数放进 Settings。

---

## 10. 新增镜头行为

### 新场景使用

1. 主相机挂 `CameraRig`；
2. 同物体挂 `CameraDirector`，指定 `rig` 和 `ArenaBounds`；
3. 如果要战斗自动镜头，再挂 `BattleCameraController` 并指定 `CameraDirector`、`TurnController`、角色列表；
4. 其他系统调用：

```csharp
director.ShowOverview(true);
director.SetTargets(new[] { a.transform, b.transform });
director.AddShake(0.5f);
```

### 新增一种战斗镜头状态

在 `BattleCameraController` 里订阅新事件，调用 `director.SetTargets` / `AddTarget` / `ShowOverview`。  
不要在 `CameraRig` 里写具体玩法判断。

### 调整 / 关闭开场介绍

1. 关掉：`BattleCameraController.playIntro` 取消勾选；
2. 换顺序：改 `introFirstTeam`；
3. 拉近拉远：改 `introOrthographicSize`（角色约 2 单位高，2.2 已经很近，4.0 较宽松）；
4. 停留更久：改 `introHoldSeconds`；想更快滑过去改 `CameraRig.positionSmoothTime`（不改 `introMoveSeconds`，它只是等待时长）。

### 调整手感

优先调 Inspector：

- `CameraDirector.framingPadding`：取景松紧；
- `overviewPadding`：全景留白；
- `min/maxOrthographicSize`：拉近/拉远范围；
- `CameraRig.positionSmoothTime` / `sizeSmoothTime`：跟随速度；
- `shakeAmplitude` / `shakeFrequency` / `shakeDecay`：震动强度与持续时间。

所有影响表现的参数都在组件 Inspector，不硬编码在逻辑里。

---

## 11. 验证与已知限制

自动测试：

- EditMode：`CameraFramerTests` 覆盖全景、双目标、边界夹取、宽高比、尺寸限制；
- PlayMode：`CameraRigTests` 覆盖立即定位、目标取景、目标数量、震动 trauma。

当前结果：

- EditMode **76/76**
- PlayMode **33/33**
- `BuildAllBatch`：`BUILD_OK`，`BuildCheck` 通过

已知限制：

- 没有 Cinemachine / Timeline；
- 没有瞄准 look-ahead；
- 没有独立的结算镜头和剧情镜头；
- 取景使用角色根节点位置，没有按角色碰撞体/美术边界精确计算；
- 震动只按冲量粗略映射，未接玩家设置；
- `CameraDirector` 的常规取景（全景/目标包围盒）需要 `ArenaBounds`；没有边界时不更新。**特写（`FocusSingle`）是例外**，不依赖边界；
- 开场介绍期间**不锁输入**：玩家仍可拖拽发射，镜头照常播完；
- 开场介绍**只在场景启动播一次**，`RestartBattle()` 触发的 `BattleStarted` 走全景，不会重播；
- `TestArena` / `MainScene` 已接入，其他场景需要手动挂组件。
