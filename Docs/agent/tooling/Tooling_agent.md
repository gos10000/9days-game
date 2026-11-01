# 资源生成与调试工具 — Agent 工作记录（Tooling_agent）

> 人读版：同目录 `Tooling.md`。角色相关：`Docs/agent/character/Character_agent.md`；皮肤：`Docs/agent/skin/Skin_agent.md`。

## 目录

- [0. 文件清单](#0-文件清单)
- [1. ProjectSetup](#1-projectsetup)
- [2. EditorAssets](#2-editorassets)
- [3. PrefabFactory](#3-prefabfactory)
- [4. ArenaBuilder](#4-arenabuilder)
- [5. BuildCheck](#5-buildcheck)
- [6. BuildPipeline](#6-buildpipeline)
- [7. batchmode 实测](#7-batchmode-实测)
- [8. 离线编译校验](#8-离线编译校验)
- [9. 幂等与覆盖行为](#9-幂等与覆盖行为)
- [10. 坑与处理](#10-坑与处理)
- [11. 未完成与风险](#11-未完成与风险)
- [12. 接手检查清单](#12-接手检查清单)

---

## 0. 文件清单

| 文件 | 类型 | 关键点 |
|---|---|---|
| `Scripts/Editor/ProjectSetup.cs` | 静态工具 + 菜单 | 写 `TagManager`（Layer / Sorting Layer），幂等 |
| `Scripts/Editor/EditorAssets.cs` | 静态工具 | 路径常量、内置精灵、材质、`SerializedObject` 读写 |
| `Scripts/Editor/PrefabFactory.cs` | 生成器 | 皮肤 → 配置 → 子弹 → 材质 → 角色 → 变体 |
| `Scripts/Editor/ArenaBuilder.cs` | 生成器 | 重建 `TestArena.unity` |
| `Scripts/Editor/BuildCheck.cs` | 校验 | 生成后断言，失败抛 `UnityException` |
| `Scripts/Editor/BuildPipeline.cs` | 入口 | 4 个菜单 + `BuildPrefabsBatch` / `BuildAllBatch` |

> 这些脚本放在 `Assets/Scripts/Editor/`（`Editor` 特殊目录）→ 自动编入 `Assembly-CSharp-Editor`，不会进包。

## 1. ProjectSetup

```csharp
ProjectSetup.Apply()   // 菜单：9Days/Setup/Layers & Sorting Layers
```

- `AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")` 拿主资产，`SerializedObject` 改字段
- Layer：从槽位 **8** 开始找空字符串，依次写入 `Character` / `Projectile` / `Obstacle`
- Sorting Layer：在 `m_SortingLayers` 末尾追加，字段 `name` / `uniqueID` / `locked`
  - `uniqueID` 用 FNV-1a 哈希（`StableId`），只需唯一，无需与 Unity 内部算法一致
- 幂等：已存在同名则跳过 → 可反复执行，不会产生重复条目
- 最后 `ApplyModifiedPropertiesWithoutUndo()` + `AssetDatabase.SaveAssets()`

## 2. EditorAssets

| 成员 | 说明 |
|---|---|
| 路径常量 | `CharacterPrefabPath` / `CharacterP1Path` / `CharacterP2Path` / `SkinDefaultPath` / `SkinAltPath` / `TestBallPath` / `CharacterConfigPath` / `ProjectileConfigPath` / `TestArenaScenePath` |
| `EnsureFolder(path)` | 递归创建（`AssetDatabase.IsValidFolder` → `CreateFolder`） |
| `EnsureFolderOfFile(filePath)` | 取目录后调 `EnsureFolder` |
| `LoadSquareSprite()` | `GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd")`（引擎内置） |
| `LoadCircleSprite()` | `GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd")`（引擎内置） |
| `CreateOrLoadMaterial(path, shaderNames...)` | 已存在则复用；否则按 shader 名顺序 `Shader.Find`，都失败则 `LogWarning` 并返回 null |
| `ScaleToWorldSize(t, sprite, worldSize)` | `localScale = worldSize / sprite.bounds.size`（自动适配 PPU） |
| `SetRef/SetInt/SetFloat/SetBool/SetString/SetVector2/SetRect/SetObjectArray` | 统一用 `new SerializedObject(target)` 写，`ApplyModifiedPropertiesWithoutUndo()` |
| `GetRef/GetInt` | 读取私有序列化字段（`BuildCheck` 用） |

> 为什么不用 `GetComponent<T>().field = x`：运行时脚本字段全是 `private [SerializeField]`，只能在 Editor 侧用 `SerializedObject` 写。

## 3. PrefabFactory

### BuildAll 顺序（有依赖，不能调换）

```
EnsureFolder(Prefabs/{Characters,Skins,Projectiles}, Data/{Characters,Projectiles}, Materials)
LoadSquareSprite / LoadCircleSprite
BuildSkin(Skin_Default) → BuildSkin(Skin_Alt)
CreateCharacterConfig(skins)
CreateProjectileConfig()
BuildTestBall(config)
CreateOrLoadMaterial(Assets/Materials/AimLine.mat, "URP/2D/Sprite-Unlit-Default", "Sprites/Default")
BuildCharacterBase(config, ball, lineMaterial)
BuildVariant(P1: team=1, skin=0) / BuildVariant(P2: team=2, skin=1)
SaveAssets + Refresh
```

### BuildSkin

- 根：`SortingGroup`（layer `Character`, order 10）
- 子 `Sprite`：`SpriteRenderer`（内置 UISprite + 颜色 + 排序层/序）
- `ScaleToWorldSize(child, sprite, (1,2))`
- `PrefabUtility.SaveAsPrefabAsset(root, path)` → `DestroyImmediate(root)`

### CreateCharacterConfig

`LoadAssetAtPath` 复用 → 直接赋值 public 字段（`mass/colliderSize/freezeRotation/hitThreshold/knockback*/controlLockTime/impactThreshold/obstacleMask/impactCooldown/landedSpeed/settle*/getUp*/groundCheckRadius/spawnNudge*/defaultSkinIndex`）
→ `skins = { Default, Alt }`（引用皮肤 prefab）→ `SetDirty`。

### BuildTestBall

`Rigidbody2D`(mass=config.mass, Continuous, Interpolate) + `CircleCollider2D`(radius) + `Projectile`
→ 子 `Visual` 用内置 Knob 精灵，`ScaleToWorldSize` 到 `radius*2`
→ `SetRef(projectile, "config"/"body"/"bodyCollider")`
→ Layer = `Projectile`

### BuildCharacterBase

组件：`Rigidbody2D`（mass / freezeRotation / Interpolate / Continuous）+ `CapsuleCollider2D`(1×2) + `Character` + `CharacterCombat` + `CharacterLauncher`
子节点：`Visual`(SortingGroup + CharacterVisual)、`AimPivot`(0.55,0.1)、`GroundCheck`(0,-1)、`HitPoint`、`AimLine`(LineRenderer, useWorldSpace, 关闭)
字段绑定（全部 `SetRef/SetInt`）：

| 目标 | 字段 |
|---|---|
| Character | `config` `body` `bodyCollider` `combat` `visual` `aimPivot` `groundCheck` `hitPoint` `groundMask=Obstacle` |
| CharacterCombat | `character` `body` |
| CharacterVisual | `character` `config` `visualRoot=Visual` `initialSkinIndex=config.defaultSkinIndex` |
| CharacterLauncher | `character` `projectilePrefab` `aimLine` |

Layer = `Character`。

### BuildVariant

`PrefabUtility.InstantiatePrefab(base)` → `SetInt(character,"team")` / `SetInt(visual,"initialSkinIndex")` → `SaveAsPrefabAsset`（Unity 会把"prefab 实例"保存成 **Variant**）→ `DestroyImmediate`。

## 4. ArenaBuilder

```csharp
ArenaBuilder.Build()   // 菜单：9Days/Build/Test Arena
```

1. `EditorSceneManager.NewScene(EmptyScene, Single)`
2. 相机：`Main Camera`，Tag = `MainCamera`，`orthographic = true`，`size = 5.5`，位置 `(0,0,-10)`，纯色背景 `(0.11,0.13,0.19)`，加 `AudioListener`
3. `ArenaBounds`：`playArea = (-8,-5,16,10)`、`createInvisibleWalls = true`、`obstacleLayerName = "Obstacle"`、`recoverOutOfBounds = true`、`checkInterval = 0.2`
4. 静态块（`CreateStaticBlock`）：
   - `Ground`：中心 `(0,-5)`，尺寸 `(16,0.6)`，Layer = `Obstacle`
   - `Obstacle_Block`：中心 `(0,-4)`，尺寸 `(1,1.4)`，Layer = `Obstacle`
   - 结构：根（`BoxCollider2D`，size = 世界尺寸，scale 1）+ 子 `Sprite`（`SpriteRenderer`，`ScaleToWorldSize`）
   - ⚠️ 碰撞体挂在**根**、缩放挂在**子**，否则 `localScale` 会把 collider 一起放大
5. 角色实例：`PrefabUtility.InstantiatePrefab`，P1 `(-5,-3.7)` / P2 `(5,-3.7)`（地面顶 = -4.7，胶囊半高 1 → 中心 -3.7）
6. `TurnController`：`SetObjectArray("characters", {P1,P2})`、`firstTeam = Player1`、`autoStart = true`、`resolveTimeout = 3`
7. `DebugHUD`：`turnController`、`testProjectilePrefab = TestBall.prefab 上的 Projectile`、`showHud = true`
8. `EditorSceneManager.SaveScene(scene, "Assets/Scenes/TestArena.unity")` + `OpenScene`

**覆盖行为**：每次执行都会重建场景，**场景里的手动修改会丢失**。日常只用 `BuildPrefabsBatch`。

## 5. BuildCheck

`ValidateAll()` 依次检查：3 个角色 prefab → 2 个皮肤 → 1 个子弹 → 2 个配置。

### 角色（对 Base / P1 / P2 都跑）

| 断言 | 说明 |
|---|---|
| 存在 | `LoadAssetAtPath<GameObject>` 非空 |
| 有 `Rigidbody2D` / `CapsuleCollider2D` | 物理体必须有 |
| 根节点**无** `SpriteRenderer` | 防止视觉污染根节点 |
| 有 `Character` / `CharacterCombat` / `CharacterLauncher` | 组件齐全 |
| 有子节点 `Visual`，且挂 `SortingGroup` + `CharacterVisual` | 表现层结构 |
| `Visual` 子树**无** `Collider2D` / `Rigidbody2D` | 防止污染物理查询 |
| `AimPivot` 无刚体/碰撞体；`GroundCheck`、`HitPoint` 存在 | 挂点结构 |
| `Character.config` / `body`、`Combat.character`、`Visual.config`、`Launcher.projectilePrefab` 非空 | 引用完整 |

### 皮肤

存在、根节点无 `Rigidbody2D`、子树无 `Collider2D`、`localPosition` ≈ 0、至少有 `SpriteRenderer` 或 `Animator`。

### 子弹

有 `Rigidbody2D` / `CircleCollider2D` / `Projectile`，`Projectile.config` 非空。

### 配置

`CharacterConfig.skins.Count >= 2` 且每套 `visualPrefab` 非空；`ProjectileConfig.radius > 0 && initialSpeed > 0`。

失败统一 `throw new UnityException("[BuildCheck] ...")`。

## 6. BuildPipeline

| 成员 | 说明 |
|---|---|
| `BuildPrefabsMenu()` | 菜单：`9Days/Build/Prefabs ...` → `PrefabFactory.BuildAll` + `BuildCheck.ValidateAll` |
| `BuildArenaMenu()` | 菜单：`9Days/Build/Test Arena` |
| `BuildAllMenu()` | 菜单：全量（Setup + Prefabs + Arena + Check） |
| `BuildPrefabsBatch()` | batchmode：Setup + Prefabs + Check（**不动场景**） |
| `BuildAllBatch()` | batchmode：全量 |

两个 batch 方法都是 `try { ... Debug.Log("BUILD_OK"); } catch (Exception e) { Debug.LogError("BUILD_FAILED\n" + e); EditorApplication.Exit(1); }`。
成功不主动 `Exit(0)`，交给 `-quit`。

## 7. batchmode 实测

| 项 | 结果 |
|---|---|
| 命令 | `-batchmode -quit -projectPath <proj> -executeMethod ...BuildPrefabsBatch -logFile Temp\build_prefabs.log` |
| 耗时 | 首次（资源重建）：20s；全量含场景：40s |
| 退出码 | `0` |
| 日志关键行 | `[BuildCheck] 结构校验全部通过` → `BUILD_OK` → `Exiting without the bug reporter. Application will terminate with return code 0` |
| 前置条件 | Unity Editor 必须关闭；否则 `Temp/UnityLockfile` 占用导致启动失败 |

### 两个实测坑

1. **Unity 关闭会清空 `Temp/`**：`-logFile` 指向 `Temp\build.log` 时目录可能不存在 → 启动前先 `New-Item -ItemType Directory -Force Temp`，或把日志放到工程外
2. **`-logFile` 用相对路径会落到 Editor 工作目录**，建议始终传绝对路径

## 8. 离线编译校验

用于快速验证脚本语法/API，比 batchmode 快得多：

```powershell
dotnet "<Unity>\Editor\Data\DotNetSdkRoslyn\csc.dll" `
  /nologo /noconfig /nostdlib+ /target:library /langversion:9.0 /nowarn:0649 `
  /out:Temp\check.dll `
  /reference:"<Unity>\Editor\Data\NetStandard\ref\2.1.0\netstandard.dll" `
  /reference:<Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/*.dll> `
  /reference:<Editor/Data/Managed/UnityEngine/*.dll> `
  /reference:Library/ScriptAssemblies/Unity.InputSystem.dll `
  /reference:Library/ScriptAssemblies/Unity.2D.Animation.Runtime.dll `
  /reference:Library/ScriptAssemblies/Unity.2D.Common.Runtime.dll `
  <所有 .cs>
```

要点：

| 点 | 说明 |
|---|---|
| 标准库 | Unity 用 netstandard 2.1 引用集（`netstandard.dll` + `compat/2.1.0/shims/netstandard/*.dll`） |
| Editor 脚本 | `Managed/UnityEngine` 目录里**已经包含** `UnityEditor.*Module.dll`，**不要**再引用 `Managed/UnityEditor.dll`，否则 `SerializedProperty` / `MenuItem` 等报 **CS0433 类型重复** |
| `SortingGroup` | 在 `UnityEngine.Rendering` 命名空间，Editor 脚本要 `using UnityEngine.Rendering;` |
| CS0649 | `[SerializeField] private` 字段会被报"从未赋值"，Unity 编译默认抑制；离线校验加 `/nowarn:0649` |
| 局限 | 只验语法/签名，不验 Unity 序列化、`AssetDatabase`、prefab 结构 → 仍需 `BuildCheck` |

## 9. 幂等与覆盖行为

| 资源 | 行为 |
|---|---|
| 目录 | `EnsureFolder` 递归创建，已存在跳过 |
| `CharacterConfig` / `ProjectileConfig` | `LoadAssetAtPath` 复用，字段整体重写（不会重复创建） |
| `Skin_Default` / `Skin_Alt` / `TestBall` / `Character_Base` / `P1` / `P2` | `SaveAsPrefabAsset` **覆盖**同名文件 |
| `TestArena.unity` | 仅 `BuildAll` / `BuildArenaMenu` 覆盖；`BuildPrefabsBatch` 不碰 |
| `AimLine.mat` | 已存在直接复用（不会覆盖手调材质） |
| Layer / Sorting Layer | 幂等，只补缺失项 |

**结论**：可以反复执行不会产生垃圾资源；但**对手工改过的 prefab / 场景会被覆盖**，改结构必须回到生成器。

## 10. 坑与处理

| 坑 | 后果 | 处理 |
|---|---|---|
| `Managed/UnityEditor.dll` + `UnityEditor.*Module.dll` 同时引用 | CS0433 类型重复 | 离线编译只引用模块 dll |
| Editor 打开时跑 batchmode | 启动失败 / 卡住 | 先关 Editor，检查 `Temp/UnityLockfile` |
| `Temp/` 被清空 | `-logFile` 写入失败 | 先建目录或换日志路径 |
| 静态块直接把 sprite 挂在带 collider 的根上 | `localScale` 会把 collider 一起缩放，尺寸错 | 碰撞体在根、缩放给子物体（`CreateStaticBlock` 已这么做） |
| 皮肤/角色 prefab 加 collider | 干扰 `GetComponentsInChildren<Collider2D>` | `BuildCheck` 断言拦截 |
| 生成后角色位置/场景布局变了 | 跑的是 `BuildAll`，重建了场景 | 日常用 `BuildPrefabsBatch` |
| `BuildCheck` 抛异常但看不到具体 | 日志里搜 `BUILD_FAILED` 后的堆栈 | 断言消息带 prefab 路径与缺失字段 |

## 11. 未完成与风险

- [ ] 生成器没有"增量"能力：每次全量重建（资源量上来后可接受，目前 12 个资源）
- [ ] `TestArena` 无版本管理策略：手动微调会被覆盖 → 需要微调的布局应写进 `ArenaBuilder`
- [ ] `BuildCheck` 未校验 Animator 参数契约（等皮肤有控制器后补）
- [ ] 缺少 PlayMode 冒烟测试（无 asmdef）
- [ ] `AimLine.mat` 的 shader 回退链依赖 URP 包；换渲染管线需调整 `CreateOrLoadMaterial` 的 shader 名顺序

## 12. 接手检查清单

1. 先跑 `9Days/Setup/Layers & Sorting Layers` 确认层齐全
2. 改结构：`PrefabFactory` → `9Days/Build/Prefabs` → 看 Console 是否 `BUILD_OK`
3. 只改数值：不要跑生成器，直接改 SO
4. 改场景布局：`ArenaBuilder` → `9Days/Build/Test Arena`（会覆盖手改）
5. 自动化：关 Editor → batchmode → 看 `Temp/build_prefabs.log` 的 `BUILD_OK` / `BUILD_FAILED`
6. 提交前确认没有把 `Temp/`、`Logs/`、`Library/` 加进 git（`.gitignore` 已忽略）


