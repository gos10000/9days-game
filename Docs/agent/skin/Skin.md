# 皮肤与动画（人读版）

> 一句话：**皮肤 = 可替换的表现层**。逻辑预制体不感知美术，换皮肤只加资源，不改代码。
> 详细实现见同目录 `Skin_agent.md`；角色主文档见 `Docs/agent/character/Character.md`。

## 目录

1. [三层结构](#1-三层结构)
2. [皮肤配置字段](#2-皮肤配置字段)
3. [运行时切换流程](#3-运行时切换流程)
4. [动画契约](#4-动画契约)
5. [新增一套皮肤 5 步](#5-新增一套皮肤-5-步)
6. [序列帧 vs 骨骼](#6-序列帧-vs-骨骼)
7. [Inspector 可调](#7-inspector-可调)
8. [排错](#8-排错)

---

## 1. 三层结构

| 层 | 文件 | 换皮肤时 |
|---|---|---|
| 逻辑 prefab | `Assets/Prefabs/Characters/Character_Base.prefab` | **不动** |
| 皮肤数据 | `CharacterConfig.skins[i]`（`Assets/Data/Characters/CharacterConfig_Default.asset`） | 新增一条 |
| 表现 prefab | `Assets/Prefabs/Skins/Skin_*.prefab` | 新增一个 |

## 2. 皮肤配置字段

| 字段 | 类型 | 说明 |
|---|---|---|
| `skinId` | string | 皮肤标识，便于查找（如 `Default` / `Alt`） |
| `visualPrefab` | GameObject | 表现 prefab，**只能有表现层**：`SortingGroup` + `SpriteRenderer` / 骨骼 + `Animator` |
| `controller` | RuntimeAnimatorController | 可空；为空时用 `visualPrefab` 自带的控制器 |
| `spriteLibrary` | SpriteLibraryAsset | 可空；骨骼皮肤换图集用（写 `SpriteLibrary.spriteLibraryAsset`） |
| `visualOffset` | Vector2 | 脚底对齐偏移，换美术不用改逻辑 prefab |
| `sortingOrder` | int | 排序序号，第 i 个渲染器 = `sortingOrder + i` |
| `icon` | Sprite | 选人界面图标，可空 |

当前两套占位皮肤（代码生成）：

| 皮肤 | 颜色 | 尺寸 | 排序层 | 说明 |
|---|---|---|---|---|
| `Skin_Default` | 近白 `(0.95, 0.95, 0.95)` | 1×2 世界单位 | `Character` / order 10 | 给 P1 |
| `Skin_Alt` | 浅蓝 `(0.45, 0.78, 1)` | 1×2 世界单位 | `Character` / order 10 | 给 P2 |

> 用的是 Unity **引擎内置精灵**（`UI/Skin/UISprite.psd`），不是项目里的 demo 资源。

## 3. 运行时切换流程

1. 销毁旧皮肤实例
2. 重置缓存（渲染器 / 原始颜色 / Animator 参数表）
3. 实例化 `skin.visualPrefab` 到 `Visual` 节点下，位置 = `visualOffset`，旋转归零，缩放 1
4. 收集所有 `SpriteRenderer`，设置 `sortingOrder = skin.sortingOrder + i`，记录原始颜色（闪白恢复用）
5. 找 `Animator`：套 `skin.controller`（若有）、`applyRootMotion = false`、`cullingMode = AlwaysAnimate`、缓存参数 hash
6. 绑定 `SpriteLibrary`（若有）
7. 重新应用朝向 + **重新写入当前状态**（所以换皮肤不会卡在 Idle）

## 4. 动画契约

| 参数 | 类型 | 取值 |
|---|---|---|
| `State` | int | 0 Idle / 1 Aiming / 2 Airborne / 3 Downed / 4 GettingUp |
| `Grounded` | bool | 是否接地 |
| `AimPower` | float | 瞄准力度 0~1 |
| `Hit` | Trigger | 进入 `Airborne` 时触发 |
| `GetUp` | Trigger | 进入 `GettingUp` 时触发 |

- 只有这 5 个参数由代码驱动，其余参数你自己加多少都行
- 运行时按 `Animator.parameters` 缓存 hash，**缺参数会静默跳过**（不会报错，但对应动作不会播放）
- 没有 `Animator` 时，`CharacterVisual` 用代码兜底：受击闪白、击飞后仰、倒地倾斜、起身回正

## 5. 新增一套皮肤 5 步

1. 复制 `Assets/Prefabs/Skins/Skin_Default.prefab` → `Skin_X.prefab`，替换精灵 / 骨骼，按需挂 `Animator`
2. 控制器实现第 4 节 5 个参数（序列帧和骨骼都一样）
3. 在 `CharacterConfig_Default.asset` 的 `skins` 里加一条，填 `visualPrefab` / `visualOffset` / `sortingOrder`
4. 指定给玩家：
   - 固定：在 `Character_P1/P2.prefab` 变体里改 `CharacterVisual.initialSkinIndex`
   - 运行时（选人界面）：`characterVisual.ApplySkin(index)` 或 `ApplySkin(skin)`
5. 跑 `9Days/Build/Prefabs`（或菜单 `9Days/Build/All`）让 `BuildCheck` 校验结构

**皮肤 prefab 的硬性规则**（违反会导致生成失败）：

- 不能有 `Rigidbody2D` / `Collider2D`（否则会被算进角色碰撞，污染受击与发射）
- 根节点必须在原点、旋转为 0（偏移只能写 `visualOffset`）
- 至少要有 `SpriteRenderer` 或 `Animator`

## 6. 序列帧 vs 骨骼

| 方案 | 表现 prefab 结构 | 额外配置 |
|---|---|---|
| 序列帧 / 单图 | `SortingGroup` → `Sprite`(SpriteRenderer + Animator) | `controller` 填帧动画控制器 |
| 骨骼（2D Animation） | `SortingGroup` → Sprite Skin 层级 + Animator | `spriteLibrary` 填 `SpriteLibraryAsset`，`controller` 填骨骼控制器 |

两种都通过 `visualPrefab` 整体替换，**逻辑代码完全一致**。

## 7. Inspector 可调

| 参数 | 默认 | 说明 |
|---|---|---|
| `initialSkinIndex` | -1 | -1 表示用 `CharacterConfig.defaultSkinIndex` |
| `allowFallbackTilt` | true | 没有 Animator 时是否用代码倾斜表现 |
| `fallbackTiltSpeed` | 360 | 倾斜过渡速度（度/秒） |
| `flashDuration` | 0.1 | 受击闪白时长（秒） |

## 8. 排错

| 现象 | 原因 / 处理 |
|---|---|
| 换皮肤后没有动画 | 控制器缺 `State/Grounded/AimPower/Hit/GetUp`，或 `initialSkinIndex` 越界（会被 Clamp 到最后一套） |
| 换皮肤后角色位置偏移 | 皮肤根节点自带位移了，改用 `visualOffset` |
| 皮肤挡住 / 被挡住 | 检查 `sortingOrder` 与 Sorting Layer；皮肤内部的 `SortingGroup` 会覆盖外层顺序 |
| 换皮肤瞬间闪一下 | 旧实例是 `Destroy`（帧末销毁），同一帧最多双渲染一帧，可接受 |
