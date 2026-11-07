# 战斗轮背景、场地与关卡初始化（人读版）

> 一句话：战斗开始时由 `BattleSceneInitializer` 读取 `BattleLevel` 预制体，按「Inspector 覆盖 > 关卡默认」解析出**背景 prefab / 场地 prefab / 双方出生点**，实例化到场景并写入角色；测试只需在 Inspector 里拖预制体。
>
> 当前状态：**已落地（2026-11-06）**。默认关卡是 `BattleSceneInitializer` 的 `Level` 字段，可在 Inspector 直接替换；`Battle Scene` 已接入 `Level_Default.prefab`。本轮只做背景、场地、关卡初始化，不包含子弹选择、运行时切换面板、相机参数、排序配置、地形破坏等额外功能。

## 目录

1. [目标与范围](#1-目标与范围)
2. [现状与缺口](#2-现状与缺口)
3. [总体设计与优先级](#3-总体设计与优先级)
4. [数据：BattleLevel](#4-数据battlelevel)
5. [运行时：BattlePrefabSlot 与 BattleSceneInitializer](#5-运行时battleprefabslot-与-battlesceneinitializer)
6. [BattleController 接入与初始化时序](#6-battlecontroller-接入与初始化时序)
7. [预制体与场景搭建](#7-预制体与场景搭建)
8. [测试时如何在属性板覆盖](#8-测试时如何在属性板覆盖)
9. [参数与接口速查](#9-参数与接口速查)
10. [测试与验收](#10-测试与验收)
11. [兼容性与边界](#11-兼容性与边界)
12. [实现选择（按默认）](#12-实现选择按默认)
13. [文件索引](#13-文件索引)
14. [已落地状态](#14-已落地状态)

---

## 1. 目标与范围

### 1.1 目标

| 目标 | 验收方式 |
|---|---|
| 背景支持 prefab | 战斗开始时按关卡实例化背景 prefab |
| 场地支持 prefab | 战斗开始时按关卡实例化场地 prefab |
| 关卡支持 prefab | 读取关卡 prefab 的背景、场地、双方出生点 |
| 不同关卡可初始化 | 换一个关卡 prefab，背景/场地/出生点一起变化 |
| 测试可在属性板覆盖 | 开始前在 Inspector 选关卡/背景/场地 prefab，无需改代码 |
| 不破坏旧场景 | 没有 `BattleSceneInitializer` 的旧场景行为不变 |

### 1.2 本轮范围

| 做 | 不做 |
|---|---|
| `BattleLevel` 关卡数据 prefab | 子弹选择 / 选弹 UI |
| `BattleSceneInitializer` 初始化组件 | 运行时切换面板 / 热键 |
| `BattlePrefabSlot` 背景/场地实例化 | `BackgroundConfig` / `ArenaConfig` |
| Inspector 覆盖关卡/背景/场地 | 相机底色、排序层、视差 |
| 双方角色出生点 | 地形破坏、场地边界重建 |

> 背景 prefab 只负责“长什么样”；场地 prefab 负责“长什么样 + 自己带碰撞”。两者都不需要额外配置资产。

---

## 2. 现状与缺口

| 位置 | 现状 | 缺口 |
|---|---|---|
| `BattleController.BeginBattle()` | 只调用 `arena?.BuildForBattle()` | 没有背景/场地 prefab 初始化；没有关卡读取 |
| `ArenaController` | 基于 `ArenaConfig` 的程序化地形 | 不是 prefab 方案；本轮不扩展它 |
| `Character.spawnPoint` | `Awake` 时记录场景位置，无 setter | 不能按关卡设置双方出生点 |
| `BattleLevel` / `BattleSceneInitializer` | 不存在 | 没有关卡数据与初始化入口 |
| `Background` | 现有场景背景是手摆 Sprite | 没有 prefab 实例化入口 |
| `Battle Scene` | 没有背景/场地初始化组件 | 需要接入 |
| `Test_BattleScene` | 静态美术墙 + 背景 | 可作为场地 prefab 来源，或保留静态 |
| `TestArena` | 程序化地形 | 可只加背景初始化，场地继续走 `ArenaController` |

---

## 3. 总体设计与优先级

```text
BattleController
 └─ BattleSceneInitializer
      ├─ level           （默认关卡 prefab）
      ├─ levelOverride   （测试覆盖，可空）
      ├─ backgroundOverride（测试覆盖，可空）
      ├─ arenaOverride   （测试覆盖，可空）
      ├─ backgroundSlot  （BattlePrefabSlot，背景根）
      ├─ arenaSlot       （BattlePrefabSlot，场地根）
      └─ turnController  （取双方 Character，写出生点）
```

解析优先级：

```text
level       = levelOverride != null ? levelOverride : level
background  = backgroundOverride != null ? backgroundOverride : level.backgroundPrefab
arena       = arenaOverride != null ? arenaOverride : level.arenaPrefab
player1Pos  = level.player1Spawn
player2Pos  = level.player2Spawn
```

规则：

- `levelOverride` 优先于 `level`；
- `backgroundOverride` / `arenaOverride` 优先于关卡里的引用；
- 所有覆盖都是 Inspector 字段，不做运行时 UI；
- 每次 `BattleController.BeginBattle()` 都会重新解析，所以按 `R` 重开仍使用当前 Inspector 选择。

---

## 4. 数据：BattleLevel

文件：`Scripts/Battle/BattleLevel.cs`  
挂载：关卡 prefab 根节点

| 字段 | 类型 | 默认 | 含义 |
|---|---|---|---|
| `backgroundPrefab` | GameObject | null | 背景 prefab |
| `arenaPrefab` | GameObject | null | 场地 prefab |
| `player1Spawn` | Vector2 | `(-5, -3.7)` | P1 出生点（世界坐标） |
| `player2Spawn` | Vector2 | `(5, -3.7)` | P2 出生点（世界坐标） |

说明：

- 字段用 `public` + `[Header]`，Inspector 可调，也方便 PlayMode 测试直接构造关卡对象；
- 关卡 prefab **只做数据载体**：根节点挂 `BattleLevel`，不要求放实际背景/场地子物体；
- 运行时只读，不在游戏过程中修改关卡资产；
- 关卡 prefab 名称就是关卡名，不额外加 `levelId` 字段。

示例：

```text
Assets/Prefabs/Levels/Level_Default.prefab
  Level_Default (BattleLevel)
    backgroundPrefab = Assets/Prefabs/Backgrounds/Background_Default.prefab
    arenaPrefab      = Assets/Prefabs/Arenas/Arena_Default.prefab
    player1Spawn     = (-5, -3.7)
    player2Spawn     = (5, -3.7)
```

---

## 5. 运行时：BattlePrefabSlot 与 BattleSceneInitializer

### 5.1 `BattlePrefabSlot`

文件：`Scripts/Scene/BattlePrefabSlot.cs`  
职责：只负责“把某个 prefab 实例化到自己的 root 下”，背景和场地共用。

| 成员 | 入参 | 返回 | 说明 |
|---|---|---|---|
| `Apply(GameObject prefab)` | 目标 prefab，null 表示清空 | — | 同一个 prefab 且实例还活着时不重复实例化 |
| `Clear()` | — | — | 销毁当前实例 |
| `CurrentPrefab` | — | GameObject | 当前 prefab |
| `Instance` | — | GameObject | 当前实例；测试可断言 |

实现要点：

- `root` 为空时用自身 `transform`；
- 实例化用 `Instantiate(prefab, root, false)`，prefab 按自身局部变换放在 root 原点；
- 换 prefab 时先 `SetActive(false)` 再 `Destroy`，避免同帧新旧两个实例同时存在；
- 只做实例化/销毁，不改排序、不改颜色、不找组件；
- prefab 根节点保持 active；槽位不会强制激活，避免把美术刻意隐藏的层打开。

### 5.2 `BattleSceneInitializer`

文件：`Scripts/Battle/BattleSceneInitializer.cs`  
职责：战斗开始时读取关卡并应用覆盖，驱动两个 `BattlePrefabSlot`，写角色出生点。

| 字段 / 成员 | 类型 | 默认 | 说明 |
|---|---|---|---|
| `level` | BattleLevel | null | 默认关卡 prefab |
| `levelOverride` | BattleLevel | null | 测试覆盖关卡；非空优先 |
| `backgroundOverride` | GameObject | null | 测试覆盖背景；非空优先 |
| `arenaOverride` | GameObject | null | 测试覆盖场地；非空优先 |
| `backgroundSlot` | BattlePrefabSlot | null | 背景实例化根；为空自动创建 |
| `arenaSlot` | BattlePrefabSlot | null | 场地实例化根；为空自动创建 |
| `turnController` | TurnController | null | 取双方角色；为空自动查找 |
| `ActiveLevel` | BattleLevel | — | 本次实际使用的关卡 |
| `ActiveBackground` | GameObject | — | 本次实际使用的背景 prefab |
| `ActiveArena` | GameObject | — | 本次实际使用的场地 prefab |
| `HasArenaPrefab` | bool | — | 是否解析到场地 prefab |
| `BackgroundSlot` / `ArenaSlot` | — | BattlePrefabSlot | 两个实例化槽位；测试可读 `Instance` |
| `Apply()` | — | bool | 应用当前关卡/覆盖；返回是否应用了关卡或覆盖 |
| `ApplyLevel(BattleLevel value)` | 关卡 | bool | 设置默认关卡并立即应用（给未来流程/测试用） |
| `SetLevelOverride(BattleLevel value)` | 关卡 | — | 测试/运行时改关卡覆盖 |
| `SetOverrides(GameObject background, GameObject arena)` | 背景、场地 | — | 测试/运行时改背景/场地覆盖 |

`Apply()` 顺序：

1. 解析 `level = levelOverride != null ? levelOverride : level`；
2. 解析 `background = backgroundOverride != null ? backgroundOverride : level?.backgroundPrefab`；
3. 解析 `arena = arenaOverride != null ? arenaOverride : level?.arenaPrefab`；
4. `backgroundSlot.Apply(background)`；
5. `arenaSlot.Apply(arena)`；
6. 如果 `level != null`，把 `player1Spawn` / `player2Spawn` 写到双方 `Character.SetSpawnPoint(...)`；
7. 返回 `level != null || background != null || arena != null`。

槽位自动创建：

- `Awake` 时如果 `backgroundSlot` / `arenaSlot` 为空，创建子物体 `BackgroundRoot` / `ArenaRoot`，各挂一个 `BattlePrefabSlot`；
- 自动槽位会放在**世界原点**，避免父节点偏移把背景/场地带偏；
- 这样场景里只挂一个 `BattleSceneInitializer` 就能用，不必手动摆两个空物体；
- 如果用户手动配了槽位，则尊重手动引用。

出生点：

- `Character.SetSpawnPoint(Vector3 point, bool moveImmediately = false)` 新增；
- `BattleSceneInitializer` 只写 `spawnPoint`，不直接移动刚体；
- 随后的 `BattleController.BeginBattle` 会调用 `Character.ResetForBattle()`，把角色放到新出生点并回满血。

---

## 6. BattleController 接入与初始化时序

新增引用：

```csharp
[SerializeField] private BattleSceneInitializer sceneInitializer;
```

`BattleController` 改动（`Apply` 放在现有 `config` / `turnController` / 角色数量校验通过之后、`arena?.BuildForBattle()` 之前）：

| 位置 | 改动 |
|---|---|
| `Awake()` | `sceneInitializer` 为空时 `FindAnyObjectByType<BattleSceneInitializer>()` |
| `BeginBattle()` | 先 `sceneInitializer?.Apply()`；有场地 prefab 时清掉旧程序地形，没有时才走 `arena?.BuildForBattle()` |
| `BeginBattle()` | 之后才 `characters[i].ResetForBattle()`，保证出生点已更新 |
| 只读属性 | `SceneInitializer`，给调试/测试用 |

示意：

```csharp
sceneInitializer?.Apply();

if (sceneInitializer != null && sceneInitializer.HasArenaPrefab)
    arena?.ClearRuntime();          // 清掉可能残留的程序地形
else
    arena?.BuildForBattle();

for (int i = 0; i < characters.Count; i++)
    if (characters[i] != null) characters[i].ResetForBattle();
```

时序：

```text
BattleController.BeginBattle()
  ├─ sceneInitializer.Apply()
  │    ├─ backgroundSlot.Apply(background)
  │    ├─ arenaSlot.Apply(arena)
  │    └─ Character.SetSpawnPoint(P1/P2)
  ├─ 有场地 prefab：arena?.ClearRuntime()
  ├─ 没有场地 prefab：arena?.BuildForBattle()
  ├─ Character.ResetForBattle()       // 回满血、回出生点
  └─ new BattleSession(...) → BattleStarted
```

三种场景的行为：

| 场景 | `sceneInitializer` | 场地 prefab | 结果 |
|---|---|---|---|
| 旧场景 | 无 | — | 完全走原逻辑 |
| 程序化地形 + 背景 prefab | 有 | 无 | 应用背景/出生点，然后 `ArenaController.BuildForBattle()` |
| 全 prefab 关卡 | 有 | 有 | 应用背景 + 场地 prefab，跳过 `ArenaController` |

> 如果场地 prefab 已经带了静态碰撞，不要同时启用 `ArenaController` 或另一套静态地形；同一个场景只保留一套场地。

---

## 7. 预制体与场景搭建

### 7.1 背景 prefab

路径示例：`Assets/Prefabs/Backgrounds/Background_Default.prefab`

要求：

- 只放表现层：`SpriteRenderer` / 动画 / 粒子；
- 不放 `Collider2D` / `Rigidbody2D`；
- 按世界原点摆放，排序层/序号在 prefab 里配好，保证在角色和场地后面；
- 不同背景就是不同 prefab。

### 7.2 场地 prefab

路径示例：`Assets/Prefabs/Arenas/Arena_Default.prefab`

要求：

- 放地形视觉 + 碰撞；碰撞层使用现有 `Obstacle`；
- 按世界原点摆放；
- 静态碰撞体不要挂 `Rigidbody2D`（或按现有场地规则处理）；
- 不同场地就是不同 prefab。

### 7.3 关卡 prefab

路径示例：`Assets/Prefabs/Levels/Level_Default.prefab`

结构：

```text
Level_Default (BattleLevel)
  backgroundPrefab → Background_Default.prefab
  arenaPrefab      → Arena_Default.prefab
  player1Spawn     → (-5, -3.7)
  player2Spawn     → (5, -3.7)
```

创建更多关卡：复制 prefab，改三个引用/两个位置即可。

### 7.4 战斗场景接线

菜单 `9Days/Build/Battle Scene Setup (Default Level)` 会自动生成默认背景/场地/关卡 prefab，并把默认关卡接到 `Battle Scene`；下面的手工接线字段与它等价。

`Battle Scene` / `TestArena` / `Test_BattleScene` 根节点或管理节点：

| 组件 | 字段 | 值 |
|---|---|---|
| `BattleSceneInitializer` | `Level` | `Level_Default.prefab` |
| | `Level Override` | 测试时选另一个关卡；平时留空 |
| | `Background Override` | 测试时选背景；平时留空 |
| | `Arena Override` | 测试时选场地；平时留空 |
| | `Background Slot` | 可留空（自动创建） |
| | `Arena Slot` | 可留空（自动创建） |
| | `Turn Controller` | 场景里的 `TurnController` |
| `BattleController` | `Scene Initializer` | 上面的 `BattleSceneInitializer` |
| | `Arena` | 用场地 prefab 时留空；用程序化地形时保留原 `ArenaController` |

> 只想换背景/场地、保持场景原出生点时：`Level` 留空，只配 `Background Override` / `Arena Override`；否则关卡的出生点会覆盖角色原位置。
>
> `TestArena` 是 `ArenaBuilder` 生成物：手工加的组件会被 `9Days/Build/All` 覆盖。要在 `TestArena` 长期保留，需要同步改 `Scripts/Editor/ArenaBuilder.cs`；`Battle Scene` 是手摆场景，不受影响。

---

## 8. 测试时如何在属性板覆盖

### 8.1 操作步骤

1. 打开战斗场景，选中 `BattleSceneInitializer`。
2. 想改**默认关卡**（正式使用，不是测试覆盖）：直接把 `Level` 换成另一个 `Level_*.prefab`。
3. 想测**另一个关卡**：把另一个 `Level_*.prefab` 拖到 `Level Override`。
4. 想只测**背景**：把背景 prefab 拖到 `Background Override`。
5. 想只测**场地**：把场地 prefab 拖到 `Arena Override`。
6. 点 Play。`BattleController.BeginBattle()` 会按覆盖优先级应用。
7. 按 `R` 重开仍使用当前 Inspector 选择。

### 8.2 覆盖示例

| 想验证 | `Level Override` | `Background Override` | `Arena Override` |
|---|---|---|---|
| 默认关卡 | 空 | 空 | 空 |
| 另一关卡的背景 + 场地 + 出生点 | `Level_02` | 空 | 空 |
| 只换背景 | 空 | `Background_Night` | 空 |
| 只换场地 | 空 | 空 | `Arena_Test` |
| 关卡 + 单独覆盖背景 | `Level_02` | `Background_Night` | 空 |

优先级：`Override` 字段 > 关卡字段 > 场景原状。

### 8.3 测试限制

- 这是**开始前**的属性板选择，不是运行时切换面板；
- 在 Play 中改覆盖字段后，需要下一次 `BeginBattle` / 按 `R` 才生效；
- 没有热键、没有 UI、没有 Catalog；
- 只想换背景/场地且保持场景原出生点时，不要配 `level`，只用对应 `Override`。

---

## 9. 参数与接口速查

| 位置 | 参数 | 作用 |
|---|---|---|
| `BattleLevel` | `backgroundPrefab` | 关卡默认背景 |
| `BattleLevel` | `arenaPrefab` | 关卡默认场地 |
| `BattleLevel` | `player1Spawn` | P1 出生点（世界坐标） |
| `BattleLevel` | `player2Spawn` | P2 出生点（世界坐标） |
| `BattleSceneInitializer` | `level` | 默认关卡 prefab |
| `BattleSceneInitializer` | `levelOverride` | 测试关卡覆盖 |
| `BattleSceneInitializer` | `backgroundOverride` | 测试背景覆盖 |
| `BattleSceneInitializer` | `arenaOverride` | 测试场地覆盖 |
| `BattleSceneInitializer` | `backgroundSlot` / `arenaSlot` | 实例化根；可空自动创建 |
| `BattleController` | `sceneInitializer` | 战斗开始时调用 `Apply()` |
| `Character` | `SetSpawnPoint(point, moveImmediately=false)` | 写入出生点 |

---

## 10. 测试与验收

### 10.1 PlayMode

| 用例 | 步骤 | 断言 |
|---|---|---|
| `ApplyLevel_UsesPrefabsAndSpawns` | 建关卡 A（背景 BG_A / 场地 AR_A / 出生点 P1/P2）；`ApplyLevel()` | `BackgroundSlot.Instance != null`、`ArenaSlot.Instance != null`；角色 `SpawnPoint` 等于关卡值 |
| `LevelOverride_Wins` | 默认关卡 A，`levelOverride=B` | 使用 B 的背景/场地/出生点 |
| `BackgroundOverride_KeepsArenaFromLevel` | 关卡背景 A，`backgroundOverride=B` | 使用 B 背景；场地仍来自关卡 |
| `ArenaOverride_KeepsBackgroundFromLevel` | 关卡场地 A，`arenaOverride=B` | 使用 B 场地；背景仍来自关卡 |
| `NoLevel_DoesNothing` | 没有关卡/覆盖 | 返回 false；两个槽位都没有实例 |
| `SamePrefab_DoesNotReinstantiate` | 连续 `Apply()` 同一 prefab | `Instance` 引用不变 |
| `BattleController_BeginBattle_UsesLevelSpawn` | 关卡出生点与场景原位置不同；`BeginBattle()` | 角色最终在关卡出生点 |

> 当前结果：EditMode 81/81、PlayMode 41/41（新增 7 个用例）；旧基线 34 个 PlayMode 用例全部保留。

### 10.2 手动验收

1. 新建一个背景 prefab、一个场地 prefab（带地面碰撞）、一个关卡 prefab，按第 7 节接线。
2. Play：场景出现关卡背景 + 场地，双方站在关卡出生点。
3. 在 Inspector 把 `Background Override` 换成另一个背景，Play：背景变，场地不变。
4. 把 `Arena Override` 换成另一个场地，Play：场地变，背景不变。
5. 把 `Level Override` 换成另一个关卡，Play：背景、场地、出生点一起变。
6. 清空所有覆盖，Play：回到默认关卡。
7. 旧场景不挂 `BattleSceneInitializer`：行为与改动前一致。

---

## 11. 兼容性与边界

| 事项 | 说明 |
|---|---|
| 旧场景 | `BattleController.sceneInitializer == null` 时保持原逻辑 |
| 程序化地形 | 关卡没有 `arenaPrefab` 时，`BattleController` 仍调用 `ArenaController.BuildForBattle()` |
| 场地 prefab + 静态地形 | 不要同时启用；同场景只保留一套 |
| 旧程序地形残留 | 有场地 prefab 时 `BattleController` 调 `arena?.ClearRuntime()`，避免旧 `TerrainRoot` 叠加 |
| 背景/场地 prefab 原点 | 统一按世界原点摆放；槽位按 root 原点实例化 |
| 背景碰撞 | 背景 prefab 不应带 Collider；否则会被子弹/角色撞到 |
| 出生点 | 世界坐标；`SetSpawnPoint` 只写值，`ResetForBattle` 才移动 |
| 角色缺失 | 关卡有出生点但场景缺 P1/P2 时，只警告，不崩溃 |
| 关卡 prefab 形态 | 已按“数据载体”落地；可视化预览需要另建场景 |
| 相机/边界 | 本轮不处理；场地大小变化后，`ArenaBounds`/相机的现有配置需要单独调 |
| 资产覆盖 | 直接改关卡 prefab 会影响所有使用它的场景；测试优先用 `Override` 字段 |

---

## 12. 实现选择（按默认）

| # | 选择 | 落地方式 |
|---|---|---|
| 1 | 关卡 prefab 形态 | **数据载体**：根节点挂 `BattleLevel`，引用背景/场地 prefab |
| 2 | `levelOverride` 类型 | **`BattleLevel`** 组件引用，Inspector 直接拖关卡 prefab |
| 3 | 背景/场地 prefab 原点 | **世界原点**；槽位按 root 原点实例化 |
| 4 | 出生点坐标 | **世界坐标**，写入 `Character.SetSpawnPoint` |
| 5 | 测试覆盖方式 | **仅 Inspector**，不做运行时面板/热键 |

---

## 13. 文件索引

| 关注点 | 位置 |
|---|---|
| 关卡数据 | `Scripts/Battle/BattleLevel.cs`（新增） |
| 关卡初始化 | `Scripts/Battle/BattleSceneInitializer.cs`（新增） |
| 背景/场地实例化 | `Scripts/Scene/BattlePrefabSlot.cs`（新增） |
| 角色出生点 | `Scripts/Character/Character.cs`（修改） |
| 战斗接入 | `Scripts/Battle/BattleController.cs`（修改） |
| 默认资产生成/场景接线 | `Scripts/Editor/BattleSceneSetup.cs`（新增） |
| 默认背景 prefab | `Assets/Prefabs/Backgrounds/Background_Default.prefab`（新增） |
| 默认场地 prefab | `Assets/Prefabs/Arenas/Arena_Default.prefab`（新增） |
| 默认关卡 prefab | `Assets/Prefabs/Levels/Level_Default.prefab`（新增） |
| 测试用例 | `Assets/Tests/PlayMode/BattleSceneInitializerTests.cs`（新增） |

---

## 14. 已落地状态

- `Battle Scene` 已生成并挂 `BattleSceneInitializer`；
- 默认关卡是 `BattleSceneInitializer.Level = Level_Default.prefab`，Inspector 可直接替换；
- `BattleController.sceneInitializer` 已接线；
- 默认背景/场地/关卡 prefab 已生成；
- 菜单 `9Days/Build/Battle Scene Setup (Default Level)` 可重建默认资产并重新接线；
- PlayMode 新增 7 个用例全部通过；
- 全量结果：EditMode **81/81**、PlayMode **41/41**；
- 另修复旧 `BattleInputTests` 的隐式队伍依赖（仅测试代码，不改玩法）。

已知限制：

- `TestArena` 是生成物，目前未写入 `BattleSceneInitializer`；需要时手动加或同步改 `ArenaBuilder`；
- 默认背景/场地使用项目内 sky/ground 图和简单碰撞，作为可替换占位；
- 场地 prefab 与 `ArenaController`/静态地形不要同时启用；
- 相机/`ArenaBounds` 未随 prefab 场地自动调整。