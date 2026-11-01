# 皮肤与动画 — Agent 工作记录（Skin_agent）

> 面向后续接手 agent：实现细节、约束、坑、验证与待办。
> 人读版：同目录 `Skin.md`；角色主文档：`Docs/agent/character/Character_agent.md`。

## 目录

- [0. 涉及文件](#0-涉及文件)
- [1. ApplySkin 执行顺序](#1-applyskin-执行顺序)
- [2. 排序 SortingGroup 嵌套](#2-排序-sortinggroup-嵌套)
- [3. 朝向与兜底倾斜](#3-朝向与兜底倾斜)
- [4. 闪白](#4-闪白)
- [5. Animator 参数契约](#5-animator-参数契约)
- [6. 占位皮肤的生成规格](#6-占位皮肤的生成规格)
- [7. 结构校验](#7-结构校验)
- [8. 坑与处理](#8-坑与处理)
- [9. 已知限制](#9-已知限制)
- [10. 验证记录](#10-验证记录)
- [11. 待办](#11-待办)
- [12. 接手检查清单](#12-接手检查清单)

---

## 0. 涉及文件

| 文件 | 角色 |
|---|---|
| `Assets/Scripts/Config/CharacterConfig.cs` | `Skin` 嵌套结构定义（`[Serializable]`） |
| `Assets/Scripts/Character/CharacterVisual.cs` | 皮肤实例化/切换、朝向、闪白、Animator 桥、无动画兜底 |
| `Assets/Scripts/Editor/PrefabFactory.cs` | `BuildSkin()` 生成占位皮肤；`CreateCharacterConfig()` 写 `skins` 列表 |
| `Assets/Scripts/Editor/BuildCheck.cs` | `ValidateSkin()` 结构断言 |
| `Assets/Prefabs/Skins/Skin_Default.prefab` / `Skin_Alt.prefab` | 生成的占位皮肤 |

## 1. ApplySkin 执行顺序

```
ApplySkin(int index)
  → CurrentSkinIndex = index
  → ApplySkin(CharacterConfig.Skin skin)

ApplySkin(Skin)
  1. Destroy(skinInstance)                // 帧末销毁，允许一帧双渲染
  2. skinInstance = null; animator = null;
     renderers = empty; baseColors = empty; animatorParams.Clear()
  3. visualRoot.localRotation = identity  // 兜底倾斜可能改过它，必须复位
  4. skin == null || skin.visualPrefab == null → 直接 return（保留无皮肤状态）
  5. Instantiate(skin.visualPrefab, visualRoot, false)
     localPosition = skin.visualOffset；localRotation = identity；localScale = one
  6. renderers = GetComponentsInChildren<SpriteRenderer>(true)
     for i: sortingOrder = skin.sortingOrder + i；baseColors[i] = renderer.color
  7. animator = GetComponentInChildren<Animator>(true)
     若有 skin.controller → runtimeAnimatorController = controller
     applyRootMotion = false；cullingMode = AlwaysAnimate；CacheAnimatorParams()
  8. SpriteLibrary（若有）→ spriteLibraryAsset = skin.spriteLibrary
  9. ApplyFacing()（恢复翻面）
 10. OnStateChanged(character.State)（重写状态与触发器）
```

**为什么第 10 步必须存在**：换皮肤会换掉 Animator，若不重写状态，新控制器会停在默认状态（视觉上"卡 Idle"）。
第 3 步必须存在：无动画时代的兜底倾斜会改 `visualRoot.localRotation`，换皮肤前要归零。

## 2. 排序 SortingGroup 嵌套

- 角色 `Visual` 节点挂着 `SortingGroup`（layer=`Character`, order=10）
- 皮肤 prefab 根节点**也**挂 `SortingGroup`（同层同序）
- 嵌套 SortingGroup 时**内层生效**：所以皮肤的排序由皮肤自己的 `SortingGroup` 决定
- 同时 `CharacterVisual` 会给每个 `SpriteRenderer` 设 `sortingOrder = skin.sortingOrder + i`，
  保证同一皮肤内多部件（身体/武器/挂件）的相对顺序稳定

## 3. 朝向与兜底倾斜

- 朝向：`FaceTo(Vector2 direction)` → `facing = sign(direction.x)` → `SpriteRenderer.flipX = facing < 0`
  - 只翻表现，**不动碰撞体**（物理体 `freezeRotation = true`，也不动旋转）
  - `|direction.x| < 0.01` 时忽略（防止抖动）
- 无 Animator 时（`allowFallbackTilt && animator == null`）每帧：

```
targetAngle = Airborne ? -25 * facing
            : Downed   ? -80 * facing
            : 0
visualRoot.localRotation = Euler(0,0, MoveTowardsAngle(current, target, fallbackTiltSpeed * dt))
```

## 4. 闪白

```
Flash() → Flash(flashDuration)
  StopCoroutine(旧协程)
  SetRendererColors(FlashColor = (1, 0.35, 0.35, 1))
  wait flashDuration（WaitForSeconds，受 timeScale 影响）
  RestoreRendererColors()（逐项回到 baseColors）
```

- `baseColors` 在 `ApplySkin` 时快照，换皮肤不会串色
- 受击时由 `CharacterCombat.ReceiveHit` 调用（低于 `hitThreshold` 也会闪）

## 5. Animator 参数契约

```csharp
AnimParams.State / Grounded / AimPower / Hit / GetUp   // StringToHash 常量
```

- `CacheAnimatorParams()` 遍历 `animator.parameters`，把 `nameHash` 存入 `HashSet<int>`
- 写入前判断 `Contains(hash)`，**缺参数静默跳过**（避免 Unity 刷警告、也支持空控制器）
- 这是"软契约"：**没有自动化测试校验**（当前无 asmdef），只能靠文档 + 运行时表现
- 将来补测试时建议：遍历所有 `CharacterSkin`，断言 `controller` 含这 5 个参数且类型正确

## 6. 占位皮肤的生成规格

| 项 | 值 |
|---|---|
| 精灵 | `AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd")`（引擎内置，非项目资源） |
| 结构 | 根（`SortingGroup`）→ 子 `Sprite`（`SpriteRenderer`） |
| 尺寸 | `ScaleToWorldSize(child, sprite, (1, 2))`，自动适配精灵 PPU |
| 排序 | `sortingLayerName = "Character"`，`sortingOrder = 10` |
| 颜色 | Default `(0.95,0.95,0.95)`；Alt `(0.45,0.78,1)` |
| Animator | 未挂（用于验证"无美术也能跑"） |

## 7. 结构校验

对 `Skin_Default` / `Skin_Alt` 逐项断言：

1. 存在（`LoadAssetAtPath` 非空）
2. 根节点无 `Rigidbody2D`
3. **所有子节点**无 `Collider2D`（`GetComponentsInChildren<Collider2D>(true)`）
4. 根节点 `localPosition.sqrMagnitude < 0.0001`（必须在原点）
5. 至少有 `SpriteRenderer` 或 `Animator`

失败会抛 `UnityException` → `BUILD_FAILED` → batchmode 退出码 1。

**未校验**：控制器参数是否齐全、根节点旋转是否为 0（代码里会强制归零）、`visualOffset` 合理性。
根节点旋转之所以不校验：`ApplySkin` 里已强制 `localRotation = identity`，皮肤自带旋转会被覆盖。

## 8. 坑与处理

| 坑 | 后果 | 处理 |
|---|---|---|
| 皮肤里放了 `Collider2D` | 会被 `GetComponentsInChildren<Collider2D>` 算进角色碰撞集合 → 受击判定、发射忽略都错乱 | `BuildCheck` 硬性禁止 |
| 皮肤根节点带位移 | 角色看起来偏移，且换皮肤时位置不一致 | 统一走 `visualOffset`；`BuildCheck` 禁止根节点位移 |
| 兜底倾斜残留旋转 | 换皮肤后皮肤是歪的 | `ApplySkin` 第 3 步复位 `visualRoot.localRotation` |
| `SortingGroup` 编译不到 | Editor 脚本报 CS0246 | 它在 `UnityEngine.Rendering` 命名空间，需要 `using UnityEngine.Rendering;` |
| 皮肤里 `Animator` 在深层子节点 | 找不到控制器/参数 | 用 `GetComponentInChildren<Animator>(true)`（已支持） |
| 旧皮肤一帧残留 | 换皮肤瞬间可能双渲染一帧 | `Destroy` 是帧末销毁；若在意可改对象池复用 |

## 9. 已知限制

1. **无自动化契约测试**：没有 asmdef，测试程序集无法引用运行时程序集；当前只靠 `BuildCheck` 验结构
2. **骨骼皮肤未实测**：`spriteLibrary` 只在代码里写入了 `SpriteLibrary.spriteLibraryAsset`，没有真实骨骼资源验证过
3. **`ApplySkin` 每次都会 `Instantiate`**：频繁换皮肤（选人界面来回切）建议后续加对象池
4. `character.StateChanged` 只在状态"变化"时触发；`ApplySkin` 内部主动调用一次 `OnStateChanged` 来补齐初值
5. 闪白用 `WaitForSeconds`，慢动作（`Time.timeScale = 0.2`）时闪白会变长

## 10. 验证记录

| 项 | 方式 | 结果 |
|---|---|---|
| 生成 | `BuildPrefabsBatch` | `Skin_Default` / `Skin_Alt` 生成成功，`BuildCheck` 通过 |
| 结构 | 读 prefab YAML | 根节点只有 `SortingGroup`，子节点 `Sprite` 有 `SpriteRenderer`，无碰撞体 |
| 变色区分 | 人工 Play | P1 白、P2 蓝，排序正确（在 Ground 之上、HUD 之下） |
| 无 Animator 兜底 | 人工 Play | 受击闪白、击飞后仰、倒地倾斜、起身回正均生效 |
| 运行时换皮肤 | **未人工验证** | `ApplySkin(int)` 逻辑已实现，等美术/选人界面时验证 |

## 11. 待办

- [ ] 真实美术接入（序列帧 + 骨骼各一套）
- [ ] 骨骼皮肤实测（`SpriteLibraryAsset` 切换 + Sprite Skin 表现）
- [ ] 皮肤契约自动校验（补 asmdef 后写成 EditMode 测试）
- [ ] 选人界面：预览 + `ApplySkin` + `icon`
- [ ] 皮肤资源按需加载（Addressables）与对象池

## 12. 接手检查清单

1. 先看 `CharacterConfig.Skin` 字段，再看 `CharacterVisual.ApplySkin` 的 10 个步骤
2. 新增皮肤：只加 prefab + `skins` 条目，**不要动** `Character_Base.prefab`
3. 改排序：优先改 `CharacterConfig.Skin.sortingOrder`，不要改皮肤内部 renderer 顺序
4. 改完跑 `9Days/Build/Prefabs`（会重新生成占位皮肤并覆盖 `Skin_Default/Alt`；**新增的皮肤文件不会被删**）
5. 若手工改了 `Skin_Default.prefab` 后被重建覆盖，把改法固化到 `PrefabFactory.BuildSkin`

