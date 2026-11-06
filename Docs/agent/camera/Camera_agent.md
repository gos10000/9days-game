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
- `Start` 若有 bounds 则 `ShowOverview(true)`；
- `LateUpdate` 清理销毁目标，空列表自动全景，重新计算姿态；
- `SetTargets` 最多保留 `maxTargets` 个；
- `AddTarget` / `RemoveTarget` 供子弹跟踪；
- `AddShake` 转发给 Rig。

无边界场景当前不会更新姿态；这是限制，不是 bug。

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
- [ ] 没有 Cinemachine / Timeline；
- [ ] 取景只用 Transform 点，不按角色 SpriteRenderer / Collider 包围盒计算；
- [ ] 震动按冲量粗略映射，未接玩家设置；
- [ ] `CameraDirector` 无 `ArenaBounds` 时不更新，纯自定义场景要手动指定；
- [ ] `CameraDirector.Start` 会强制全景，若外部在 Start 前设置自定义姿态会被覆盖；
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

