# 镜头控制模块 — Agent 工作记录（Camera_agent）

> 人读版：同目录 `Camera.md`。战斗事件：`Docs/agent/battle/Battle.md`；角色：`Docs/agent/character/Character.md`。

## 目录

- [0. 本次范围](#0-本次范围)
- [1. 文件清单](#1-文件清单)
- [2. 设计决策](#2-设计决策)
- [3. 取景数学](#3-取景数学)
- [4. CameraRig](#4-camerarig)
- [5. CameraDirector](#5-cameradirector)
- [6. BattleCameraController](#6-battlecameracontroller)
- [7. 场景与构建](#7-场景与构建)
- [8. 测试](#8-测试)
- [9. 已知限制与待办](#9-已知限制与待办)
- [10. 接手检查清单](#10-接手检查清单)

---

## 0. 本次范围

用户确认：

1. 使用自研镜头框架，不引入 Cinemachine；
2. 战斗开始全景；
3. 回合开始框住双方；
4. 发射后跟踪子弹；
5. 做通用 `CameraDirector`，战斗事件放在适配层。

明确不做：

- 不做剧情运镜、Timeline、Cinemachine；
- 不做鼠标自由缩放；
- 不做设置页面；
- 不新增内容配置资产；
- 不修改子弹/角色/皮肤 prefab。

---

## 1. 文件清单

### 新增运行时

| 文件 | 职责 |
|---|---|
| `Scripts/Camera/CameraPose.cs` | 中心点 + 正交尺寸值对象 |
| `Scripts/Camera/CameraFramer.cs` | 纯数学取景、边界夹取、场地扩展 |
| `Scripts/Camera/CameraRig.cs` | 平滑、正交尺寸、trauma 震动、写入 Camera |
| `Scripts/Camera/CameraDirector.cs` | 全景/目标列表/边界，通用 API |
| `Scripts/Camera/BattleCameraController.cs` | `GameEvents` → CameraDirector |

### 新增测试

| 文件 | 覆盖 |
|---|---|
| `Assets/Tests/EditMode/CameraFramerTests.cs` | 全景、双目标、边界、尺寸、Expand |
| `Assets/Tests/PlayMode/CameraRigTests.cs` | 立即定位、目标取景、震动 trauma |

### 修改

| 文件 | 修改 |
|---|---|
| `Scripts/Editor/ArenaBuilder.cs` | 主相机增加 CameraRig / CameraDirector / BattleCameraController 并连引用 |
| `Scripts/Editor/BuildCheck.cs` | `ValidateSceneCamera()` 检查主相机组件和引用 |
| `Docs/agent/README.md` | 新增 Camera 索引 |
| 旧文档测试数字 | 同步为 76/33 |

---

## 2. 设计决策

### 2.1 自研而不是 Cinemachine

- 项目是单相机 2D 正交，双人固定场地；
- 需求是目标取景、边界、平滑和震动，不是完整虚拟相机系统；
- 不引入新 package，不改现有生成流程；
- `CameraDirector` 暴露目标 API，以后需要 Cinemachine 时可以替换执行层。

### 2.2 分层

```text
BattleCameraController（玩法事件）
  ↓
CameraDirector（通用目标/边界）
  ↓
CameraFramer（纯数学）
  ↓
CameraRig（平滑 + 震动 + Camera 写入）
```

`CameraDirector` 不依赖 `TurnController`、`GameEvents`；  
`CameraRig` 不依赖战斗，任何场景都能用。

### 2.3 瞄准稳定性

- `TurnStarted` 时一次性设置双人目标，不在瞄准期间持续追角色；
- `ShotFired` 后才加入子弹目标；
- `ProjectileExpired` 后移除；
- 避免镜头移动导致 `ScreenToWorldPoint` 改变，干扰拖拽方向。

### 2.4 Camera z 固定

- 主相机保持 `z = -10`；
- `CameraRig.Awake` 记录当前 z；
- 每帧只写 x/y 和 `orthographicSize`；
- 不做旋转、倾斜、透视。

### 2.5 开场介绍（追加）

- 需求：进场景后先分别特写双方（P1 → P2），中间平滑移动，然后回到常规回合镜头；
- 方案：在适配层 `BattleCameraController` 播协程，`CameraDirector` 只增加「临时缩放覆盖 + 单目标」的能力，
  保持「Director 不认玩法事件」的分层；
- 第一个目标 **snap**（硬切），第二个 **平滑**——如果第一个也平滑，会先看到一段无意义的推镜；
- 特写**不做边界夹取**，否则贴边的角色会被推到画面侧边（主场景 P1 在 `x≈-5.66`，紧贴 `playArea` 左边）；
- `CameraDirector.Start` 增加守卫，避免它自己的全景初始化把抢先设置的特写覆盖掉
  （`StartCoroutine` 在 `Start` 里同步执行到第一个 yield 之前，两个 `Start` 的先后不确定）。

---

## 3. 取景数学

`CameraFramer.Frame`：

1. 计算目标点 min/max；
2. 加 `padding`；
3. `size = max(height/2, width/(2*aspect))`；
4. 夹到 `minSize ~ maxSize`；
5. 用 `ClampCenter` 把中心限制到场地内；
6. 视野大于场地时对应轴居中。

`Frame` 空目标时调用全景逻辑：

```text
size = max(bounds.height/2, bounds.width/(2*aspect)) + overviewPadding
```

`CameraDirector` 先 `Expand(bounds.PlayArea, boundsPadding)`，再传给 `Frame`，允许场地外留少量背景。

注意：取景目前使用 `Transform.position`，不是 Renderer/Collider bounds；角色高度通过 `framingPadding` 近似。

---

## 4. CameraRig

- `[DefaultExecutionOrder(100)]`，确保 `CameraDirector.LateUpdate` 先算目标，再由 Rig 写入；
- `Awake`：
  - `targetCamera` 空则 `GetComponent<Camera>()`；
  - 强制 `orthographic = true`；
  - 记录 `baseZ`、当前中心、当前 orthographicSize；
- `LateUpdate`：
  - `Vector2.SmoothDamp` 位置；
  - `Mathf.SmoothDamp` 尺寸；
  - trauma 衰减；
  - Perlin 震动偏移；
  - 写 `transform.position` 与 `orthographicSize`；
- `SetPose(pose, snap)`：
  - `snap=true` 立即同步；测试和战斗开始用；
- `AddShake` / `SetShakeScale`。

参数默认值在 Inspector 中可调，未接入玩家设置。

---

## 5. CameraDirector

- `rig` 为空时自动 `GetComponent<CameraRig>()`；
- `bounds` 为空时 `FindAnyObjectByType<ArenaBounds>()`；
- `Start` 仅在 `targets.Count == 0 && !sizeOverride` 时 `ShowOverview(true)`（避免覆盖外部抢先设置的开场特写）；
- `LateUpdate` 清理销毁目标，空列表自动全景，重新计算姿态；
- `SetTargets` 最多保留 `maxTargets` 个，并清掉特写缩放覆盖；
- `AddTarget` / `RemoveTarget` 供子弹跟踪；
- `AddShake` 转发给 Rig。

特写相关（开场介绍引入）：

| 成员 | 说明 |
|---|---|
| `sizeOverride` / `overrideSize`（私有） | 临时缩放覆盖，常规取景时为 false |
| `FocusSingle(Transform, float size, bool snap)` | 单目标特写；`RefreshPose` 命中该分支时**直接 `CameraPose(target.position, overrideSize)`，不做边界夹取** |
| `ClearSizeOverride()` | 清覆盖并重算一次；`SetTargets` / `ShowOverview` 也会隐式清掉 |

`RefreshPose` 里特写分支被放在 `bounds == null` 检查**之前**——所以没有 `ArenaBounds` 的场景也能播特写，常规取景仍然需要边界。

特写不夹取边界是有意的：主场景 P1 在 `x≈-5.66`，紧贴 `playArea` 左边（`x=-10`），按 `ClampCenter` 算会被推进约 1 个单位，角色就不在画面正中，特写会歪。

---

## 6. BattleCameraController

订阅：

- `BattleStarted` → 清空目标，全景 `snap=true`；
- `TurnStarted(team)` → `GetCharacter(team)` + `GetOpponent(team)` 作为 `turnTargets`；
- `ShotFired` → 加入 `trackedProjectiles`；
- `ProjectileExpired` → 移除并刷新；
- `CharacterHit` → `hitShakeBase + impulse * hitShakePerImpulse`；
- `StrongImpact` → 对应震动参数；
- `CharacterDefeated` → `defeatedShake`；
- `BurstTriggered` → `burstShake`；
- `BattleEnded` → 清空目标、平滑全景。

`OnDisable` 成对退订并清空列表。

`ApplyTargets` 每次新建目标列表：

```text
turnTargets（双方）
+ trackedProjectiles（飞行子弹）
→ director.SetTargets
```

`turnController` 为空时从 `characters` 列表按 Team 查找，兼容非 BattleController 场景。

### 开场介绍（`IntroRoutine`）

- `Start()` → `StartIntro()`；`playIntro` 为 false 或 `director` 为空则跳过；
- 序列：`FocusSingle(first, introOrthographicSize, snap=true)` → 停 `introHoldSeconds` →
  `FocusSingle(second, …, snap=false)`（平滑由 `CameraRig` 的 `positionSmoothTime/sizeSmoothTime` 决定） →
  停 `introMoveSeconds + introHoldSeconds` → `ClearSizeOverride()` → `RestoreNormalTargets()`；
- `RestoreNormalTargets()`：`CurrentTeam != None` 就重进 `OnTurnStarted(team)`（恢复「行动者 + 对手」构图），否则 `ApplyTargets(false)`；
- `introPlaying` 期间：`OnBattleStarted` / `OnTurnStarted` / `ApplyTargets` 全部提前 return，避免被回合事件抢镜；
- `OnDisable` → `StopIntro()`：停协程、复位 `introPlaying`、清缩放覆盖；
- 协程里结束用的是 `yield break;`（迭代器里写 `return;` 会报 `CS1622`）。

---

## 7. 场景与构建

`ArenaBuilder`：

```text
Main Camera
  Camera
  AudioListener
  CameraRig (targetCamera)
  CameraDirector (rig, bounds)
  BattleCameraController (director, turnController, characters)
```

`BuildCheck.ValidateSceneCamera` 只在当前场景是 `TestArena` 时执行：

- 存在 Camera；
- Tag = MainCamera；
- orthographic = true；
- 有 CameraRig / CameraDirector / BattleCameraController；
- `targetCamera` / `rig` / `bounds` / `director` / `turnController` 引用非空。

---

## 8. 测试

### EditMode

`CameraFramerTests`（6 个）：

1. 全景尺寸：16×10、16:9、padding 0.5 → orthographicSize 5.5；
2. 双目标框选并夹到场地；
3. 超过最大尺寸时夹到 max；
4. 空目标与全景一致；
5. 视野大于场地时居中；
6. `Expand` 的边界扩展。

结果：**81/81 Passed**（全量基线；其中相机新增 6 个测试，后续另有输入测试 5 个）。

### PlayMode

`CameraRigTests`（3 个）：

1. `SetPose(..., snap=true)` 立即写位置与尺寸；
2. `CameraDirector` 双目标取景、`TargetCount`/`IsOverview`，之后回全景；
3. `AddShake` 增加 trauma。

结果：**34/34 Passed**（含既有 30 个测试）。

### 构建

`BuildAllBatch`：**BUILD_OK**，`BuildCheck` 全部通过。

---

## 9. 已知限制与待办

- [ ] 没有瞄准 look-ahead、命中点短暂聚焦、结算专属构图；
- [x] 开场介绍（P1 → P2 特写）已实现，见 §6 `IntroRoutine`；
- [ ] 开场介绍期间不锁输入：玩家仍可拖拽发射，只是镜头还在播序列；需要时在 `TurnController` 加临时的 `introLock`；
- [ ] 开场介绍只在场景启动播一次；`RestartBattle()` 走 `BattleStarted` → 全景，需要每局都播就把 `OnBattleStarted` 改成调用 `StartIntro()`；
- [ ] 没有 Cinemachine / Timeline；
- [ ] 取景只用 Transform 点，不按角色 SpriteRenderer / Collider 包围盒计算；
- [ ] 震动按冲量粗略映射，未接玩家设置；
- [ ] `CameraDirector` 常规取景无 `ArenaBounds` 时不更新；特写（`FocusSingle`）不依赖边界；
- [x] `CameraDirector.Start` 已加守卫（`targets.Count == 0 && !sizeOverride` 才播全景），不会再覆盖外部抢先设置的特写姿态；
- [ ] 多个 CameraDirector 同时存在会争抢 CameraRig，当前没有单例约束；
- [ ] `CameraRig` 假设相机 z 固定，不支持旋转/跟随父节点；
- [ ] 未在真实 16:10 / 4:3 / 手机分辨率下人工验收，只做了数学测试。

---

## 10. 接手检查清单

1. 先读 `CameraFramer`（数学）→ `CameraRig`（执行）→ `CameraDirector`（目标）；
2. 调手感优先改 Inspector，不要改 `CameraFramer` 公式；
3. 新增战斗镜头行为：在 `BattleCameraController` 订阅事件，调用 Director API；
4. 新场景接入：主相机 Tag 保持 MainCamera，挂三个组件并连引用；
5. 跑 `BuildAllBatch` 看 `BUILD_OK`；
6. 跑 EditMode / PlayMode；
7. 需要剧情/过场时再评估 Cinemachine，不要现在引入；
8. 提交前确认没有把 `Temp/`、`Library/` 加入 git。

