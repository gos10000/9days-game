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
| `sortingOrder` | int | 排序基准：部件最终 order = `sortingOrder + 美术在 prefab 里设的 order`（不是遍历序号）；部件挂错 Sorting Layer 时会被归并到多数部件所在层 |
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
4. 收集所有 `SpriteRenderer`：快照美术原 order 与原始颜色 → 统一 Sorting Layer 到多数部件所在层 → `sortingOrder = skin.sortingOrder + 美术原 order`（保留美术相对顺序）
5. 找 `Animator`：套 `skin.controller`（若有）、`applyRootMotion = false`、`cullingMode = AlwaysAnimate`、缓存参数 hash
6. 绑定 `SpriteLibrary`（若有）
7. 重新应用朝向 + **重新写入当前状态**（所以换皮肤不会卡在 Idle）

## 4. 动画契约

动画由 `SkinAnimator`（与 `Animator` 同挂在皮肤 prefab 根上）按**状态名**驱动，控制器里须有同名状态（名字都可在 Inspector 改）：

| 逻辑状态 / 事件 | 播放的状态 | 默认状态名 | 说明 |
|---|---|---|---|
| 开始瞄准 | 过程 → 循环 | `PreAiming` → `Aiming` | `Aiming` 循环 |
| 发射 | 一次性 | `Fire` | 播完回 Idle / Aiming |
| 轻受击（冲量 < `hitThreshold`） | 一次性 | `LightHIt`（大写 I） | 逻辑状态不变，纯表现 |
| 被击飞（Airborne） | 抢占 | `HeaveHit` | 倒地 / 起身走 `downedState` / `gettingUpState`（默认空 = 保持上一帧） |
| 血量归零（Defeated） | 两段 | `Death` → `Died` | `Death` 播完自动落 `Died` 循环保持，直到 `ResetForBattle` |

- 唯一由代码写的 Animator 参数是 float `AimPower`（瞄准力度 0~1）
- 轻受击阈值读 `CharacterConfig.hitThreshold`，与逻辑层同源，不会两边脱节
- 状态名找不到只 warning 不报错；`Death` 缺失时直接落 `Died`
- `AnimParams`（`State/Grounded/Hit/GetUp`）是预留的软契约，当前代码未驱动
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

`SkinAnimator`（挂皮肤 prefab 根）：

| 参数 | 默认 | 说明 |
|---|---|---|
| `lightHitState` / `airborneState` | `LightHIt` / `HeaveHit` | 轻受击 / 被击飞映射的状态名 |
| `downedState` / `gettingUpState` | 空 | 留空 = 保持上一帧表现 |
| `deathState` / `diedState` | `Death` / `Died` | 死亡过程 / 死亡保持 |
| `preAim/fire/lightHit/deathDurationOverride` | 0 | 0 = 使用动画片段自身长度 |
| `logPhaseChanges` | false | Console 打印阶段流转，排错用 |

## 8. 排错

| 现象 | 原因 / 处理 |
|---|---|
| 换皮肤后没有动画 | 控制器缺同名状态或缺 `AimPower` 参数，或 `initialSkinIndex` 越界（会被 Clamp 到最后一套） |
| 换皮肤后角色位置偏移 | 皮肤根节点自带位移了，改用 `visualOffset` |
| 皮肤挡住 / 被挡住 | 检查 `sortingOrder` 与 Sorting Layer；皮肤内部的 `SortingGroup` 会覆盖外层顺序 |
| 换皮肤瞬间闪一下 | 旧实例是 `Destroy`（帧末销毁），同一帧最多双渲染一帧，可接受 |
| 皮肤部件相互遮挡错乱 | 相对顺序取自各部件自己在 prefab 里设的 order；部件挂错 Sorting Layer 会被归并到多数部件所在层 |
| 轻受击不播 `LightHIt` | 状态名大小写要对（大写 I）；被击飞 / 倒地 / 死亡期间会被故意跳过 |
| 死亡后角色僵住不播动画 | Controller 里没有 `Death` / `Died` 状态或名字不一致；两者都缺会停在最后一帧 |
