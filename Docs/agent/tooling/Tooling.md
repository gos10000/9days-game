# 资源生成与调试工具（人读版）

> 一句话：**所有 prefab / 配置 / 测试场景都由代码生成**，改结构改生成器，改数值改 SO。
> 详细实现见同目录 `Tooling_agent.md`。

## 目录

1. [为什么不用手拖](#1-为什么不用手拖)
2. [菜单](#2-菜单)
3. [batchmode Unity 关闭时](#3-batchmode-unity-关闭时)
4. [生成内容与顺序](#4-生成内容与顺序)
5. [层与排序层](#5-层与排序层)
6. [测试场景 TestArena](#6-测试场景-testarena)
7. [调试 HUD](#7-调试-hud)
8. [正确的工作方式](#8-正确的工作方式)
9. [常见问题](#9-常见问题)

---

## 1. 为什么不用手拖

| 手拖的问题 | 代码生成的好处 |
|---|---|
| 多人同时改 prefab → YAML 冲突几乎无法合并 | 生成器可 diff、可 review |
| 漏引用 / 漏组件只能在 Play 时发现 | `BuildCheck` 生成后立刻断言结构 |
| 改结构要逐个 prefab 重复操作 | 改一次 `PrefabFactory` 重跑即可 |

## 2. 菜单

| 菜单 | 作用 | 何时用 |
|---|---|---|
| `9Days/Setup/Layers & Sorting Layers` | 写 `TagManager`：Layer `Character/Projectile/Obstacle`，Sorting Layer `Character/Projectile/FX` | 新机器、换工程、误删层 |
| `9Days/Build/Prefabs (角色 + 皮肤 + 子弹)` | 生成皮肤 / 配置 / 子弹 / 角色 / 变体 + 结构校验 | 改了 `PrefabFactory` |
| `9Days/Build/Test Arena` | 重建 `TestArena.unity` | 改了 `ArenaBuilder`（**会覆盖场景手改**） |
| `9Days/Build/All (Setup + Prefabs + Arena)` | 全量 | 从零搭环境 |

## 3. batchmode Unity 关闭时

```bat
:: 只重建 prefab + 配置（不动场景，推荐日常用）
Unity.exe -batchmode -quit -projectPath <项目> ^
  -executeMethod NineDays.EditorTools.BuildPipeline.BuildPrefabsBatch ^
  -logFile <项目>\Temp\build_prefabs.log

:: 全量（含重建测试场景）
Unity.exe -batchmode -quit -projectPath <项目> ^
  -executeMethod NineDays.EditorTools.BuildPipeline.BuildAllBatch ^
  -logFile <项目>\Temp\build.log
```

- 成功：日志末尾 `BUILD_OK`，进程退出码 `0`
- 失败：`BUILD_FAILED` + 异常堆栈，退出码 `1`
- **必须先关闭 Unity Editor**（工程被 `Temp/UnityLockfile` 锁住会直接启动失败）

## 4. 生成内容与顺序

```
1. Layer / Sorting Layer           → ProjectSettings/TagManager.asset
2. Skin_Default / Skin_Alt         → Assets/Prefabs/Skins/
3. CharacterConfig_Default         → Assets/Data/Characters/（引用两套皮肤）
4. TestBallConfig                  → Assets/Data/Projectiles/
5. TestBall                        → Assets/Prefabs/Projectiles/（引用子弹配置）
6. AimLine.mat                     → Assets/Materials/
7. Character_Base                  → Assets/Prefabs/Characters/（引用角色配置 + 子弹）
8. Character_P1 / Character_P2     → 变体（team / initialSkinIndex 覆盖）
9. TestArena                       → Assets/Scenes/（仅 BuildAll）
```

顺序不能乱：后一步依赖前一步的资源。

## 5. 层与排序层

| 类型 | 名称 | 值 |
|---|---|---|
| Layer | `Character` | 8 |
| Layer | `Projectile` | 9 |
| Layer | `Obstacle` | 10 |
| Sorting Layer | `Character` / `Projectile` / `FX` | 排在 `Background`、`Default` 之后 |

- 物理碰撞矩阵**没有修改**（默认全碰撞）：子弹与发射者的忽略由 `Projectile` 用 `Physics2D.IgnoreCollision` 处理
- 地面、障碍、空气墙统一用 `Obstacle` 层 → 角色 `groundMask` 与 `obstacleMask` 都指向它

## 6. 测试场景 TestArena

由 `ArenaBuilder` 生成，**不引用 demo 的 `MainScene` / `Background`**：

```
Main Camera      正交 5.5，纯色背景，Tag=MainCamera
ArenaBounds      playArea (-8,-5,16,10)，自动空气墙 + Gizmos 边框
Ground           16×0.6 @ y=-5，Layer=Obstacle
Obstacle_Block   1×1.4 @ (0,-4)，Layer=Obstacle
Character_P1     (-5, -3.7)
Character_P2     (5, -3.7)
TurnController   characters=[P1,P2]，firstTeam=Player1，resolveTimeout=3
DebugHUD         turnController + testProjectilePrefab
```

## 7. 调试 HUD

| 键 | 作用 |
|---|---|
| 左键拖拽 / 右键 / Esc | 瞄准 / 取消 |
| 空格 | 当前回合角色直接朝对手发球 |
| `1` / `2` | 强制切回合 |
| `R` | 重开（角色回出生点、清子弹） |
| `F1` | 冲量详细日志（含未过阈值的碰撞） |
| `T` / `F2` | 自动对射 20 次 + 统计（shots/hits/knockback/strong/getup） |
| `G` / `F3` | 慢动作 0.2x / 1x |

> F 键在 Scene / Hierarchy 视图聚焦时会被 Unity 编辑器吞掉，所以提供 `T` / `G` 别名；按之前先点一下 Game 视图。

## 8. 正确的工作方式

| 想改什么 | 改哪里 |
|---|---|
| 角色结构 / 组件 / 引用 | `Editor/PrefabFactory.cs` → 跑 `9Days/Build/Prefabs` |
| 结构校验规则 | `Editor/BuildCheck.cs`（新增断言后跑生成） |
| 测试场景布局 | `Editor/ArenaBuilder.cs` → `9Days/Build/Test Arena` |
| 数值（伤害 / 击退 / 阈值） | `Assets/Data/**` 的 SO，**不用重跑生成** |
| 皮肤美术 | 新建皮肤 prefab + `CharacterConfig.skins` 条目 |

⚠️ 重新生成会**覆盖** `Character_Base / P1 / P2 / Skin_Default / Skin_Alt / TestBall`，
对这些文件的手动修改会丢失；把改法固化到生成器里。

## 9. 常见问题

| 现象 | 处理 |
|---|---|
| batchmode 一秒退出且无日志 | Editor 还开着（`Temp/UnityLockfile` 存在），关掉再跑 |
| `BUILD_FAILED` 提到某字段为空 | 看异常里的 prefab 路径，通常是生成顺序或引用丢失 |
| 找不到 `Assets/Scenes/TestArena.unity` | 只跑了 `BuildPrefabsBatch`，场景需要 `BuildAll` 或单独 `Build/Test Arena` |
| 生成后场景里角色位置变了 | `BuildAll` 重建了场景；日常用 `BuildPrefabsBatch` 避免 |
| 材质粉红 / 线看不见 | `AimLine.mat` 创建时 shader 回退链为 `URP 2D Sprite-Unlit → Sprites/Default`，看日志 `[Setup] 使用 shader:` |
| 层名字丢了 | 跑 `9Days/Setup/Layers & Sorting Layers`（幂等，不会重复添加） |
