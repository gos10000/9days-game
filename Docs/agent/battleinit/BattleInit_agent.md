# 战斗轮背景、场地与关卡初始化 — Agent 方案记录

> 人读版：同目录 `BattleInit.md`。  
> **本次状态：已落地（2026-11-06）。** 上一版方案（含选弹、运行时切换面板）已被否决，本版只保留背景 prefab、场地 prefab、关卡 prefab、Inspector 覆盖与默认关卡。

## 目录

- [0. 本次工作边界](#0-本次工作边界)
- [1. 现状证据](#1-现状证据)
- [2. 设计决策与理由](#2-设计决策与理由)
- [3. 新增 / 修改文件](#3-新增--修改文件)
- [4. 关键实现细节](#4-关键实现细节)
- [5. 场景接入](#5-场景接入)
- [6. 兼容性与风险](#6-兼容性与风险)
- [7. 测试计划](#7-测试计划)
- [8. 实现选择（按默认）](#8-实现选择按默认)
- [9. 实施检查清单](#9-实施检查清单)
- [10. 验证命令](#10-验证命令)
- [11. 后续可选扩展](#11-后续可选扩展本轮不做)

---

## 0. 本次工作边界

本次已落地并验证：

- 新增 `BattleLevel`、`BattleSceneInitializer`、`BattlePrefabSlot`；
- 修改 `Character.SetSpawnPoint` 与 `BattleController.BeginBattle`；
- 新增 `BattleSceneSetup` 编辑器工具，生成默认背景/场地/关卡 prefab 并接线 `Battle Scene`；
- 新增 7 个 PlayMode 用例；
- 默认关卡是 `BattleSceneInitializer.Level`，Inspector 可直接替换。

本版**明确不做**：

- 子弹选择 / `AmmoCatalog` / `CharacterLauncher.SetAmmo`；
- 运行时切换面板 / 热键；
- `BackgroundConfig` / `ArenaConfig`；
- 相机底色 / 排序层 / 视差；
- 地形破坏 / 场地边界重建。

### 0.1 实际改动清单

| 文件 | 类型 | 内容 |
|---|---|---|
| `Scripts/Battle/BattleLevel.cs` | 新增 | 背景 prefab、场地 prefab、双方出生点 |
| `Scripts/Battle/BattleSceneInitializer.cs` | 新增 | 关卡/覆盖解析、槽位驱动、出生点写入 |
| `Scripts/Scene/BattlePrefabSlot.cs` | 新增 | 背景/场地 prefab 实例化与销毁 |
| `Scripts/Character/Character.cs` | 修改 | `SetSpawnPoint(point, moveImmediately)` |
| `Scripts/Battle/BattleController.cs` | 修改 | `sceneInitializer` 引用；`BeginBattle` 先应用关卡，再处理程序地形 |
| `Scripts/Editor/BattleSceneSetup.cs` | 新增 | 创建默认 prefab、接线 `Battle Scene`、菜单/batch 入口 |
| `Assets/Tests/PlayMode/BattleSceneInitializerTests.cs` | 新增 | 覆盖优先级、槽位复用、出生点、无关卡 no-op |
| `Assets/Tests/PlayMode/BattleInputTests.cs` | 测试修复 | 显式设置 P1/P2 队伍，避免 `TurnController` 自动收集顺序造成全量偶发失败 |

### 0.2 生成资产与场景

| 资产 | 结果 |
|---|---|
| `Assets/Prefabs/Backgrounds/Background_Default.prefab` | 已生成，sky 图 + sortingOrder -100 |
| `Assets/Prefabs/Arenas/Arena_Default.prefab` | 已生成，ground 图 + 地面碰撞 + 两侧空气墙（Obstacle） |
| `Assets/Prefabs/Levels/Level_Default.prefab` | 已生成，引用上述两项，出生点 `(-5,-3.7)` / `(5,-3.7)` |
| `Assets/Scenes/Battle Scene.unity` | 已挂 `BattleSceneInitializer`，`Level` 指向 `Level_Default`，`BattleController.sceneInitializer` 已接线 |

### 0.3 验证结果

| 项 | 结果 |
|---|---|
| 默认资产生成/场景接线 batch | `BATTLE_SETUP_OK` |
| EditMode | **81/81 Passed** |
| PlayMode | **41/41 Passed**（原 34 + 新增 7） |
| 日志 | `Temp/battle_setup8.log`、`Temp/editmode_final2.xml`、`Temp/playmode_final4.xml` |

---

## 1. 现状证据

| 事实 | 证据 |
|---|---|
| `BattleController.BeginBattle` 只重建程序化地形，不实例化背景/场地 prefab | `Assets/Scripts/Battle/BattleController.cs:91` 只有 `arena?.BuildForBattle()` |
| `ArenaController` 是 `ArenaConfig` 程序化地形，不是 prefab 方案 | `Assets/Scripts/Scene/ArenaController.cs:55-93` |
| `Character.spawnPoint` 在 `Awake` 缓存，没有 setter | `Assets/Scripts/Character/Character.cs:34,87,220-238` |
| `Character.ResetForBattle` 会把刚体放到 `spawnPoint` | `Assets/Scripts/Character/Character.cs:220-238` |
| `BattleController` 在 `Start` 里 `autoStart` 时直接 `BeginBattle` | `Assets/Scripts/Battle/BattleController.cs:65-68` |
| 现有 `Battle Scene` 没有 `ArenaController` / 背景 | 场景 YAML 中无 `ArenaController` GUID；根对象只有 `Battle Page` 与相机 |
| `TestArena` 由 `ArenaBuilder` 生成，已有程序化地形 | `Assets/Scripts/Editor/ArenaBuilder.cs:31-47,79-90` |
| `Test_BattleScene` 是静态美术墙 + 背景 | 场景 YAML 有 `Background` / `Environment` 根节点 |

---

## 2. 设计决策与理由

### 2.1 为什么关卡用 prefab，不用 ScriptableObject

- 需求明确要求“关卡预制体”；
- prefab 可以直接在 Inspector 里挂背景/场地 prefab 引用，测试时拖拽替换；
- 不需要额外的 Catalog / 资源加载代码；
- 复制 prefab 就是新关卡，成本低。

### 2.2 为什么关卡 prefab 是“数据载体”

- 关卡只需要背景、场地、双方出生点；
- 数据载体（根节点 + `BattleLevel` 组件）让运行时可以自由组合“关卡 A 的背景 + 关卡 B 的场地”；
- 如果关卡 prefab 里直接摆背景/场地子物体，测试时只换背景会变复杂；
- 需要可视化预览时，可以另建预览场景，不影响运行时数据。

### 2.3 为什么背景和场地共用一个 `BattlePrefabSlot`

- 两者行为完全一样：销毁旧实例、实例化新 prefab；
- 背景不需要 Collider，场地需要；差异由 prefab 自己决定；
- 一个组件、两个槽位，代码最少。

### 2.4 为什么 `BattleSceneInitializer` 放在 `Scripts/Battle`

- 它需要读取 `TurnController`（`NineDays.Battle`）和 `Character`（`NineDays.Game`）；
- 放在 `Battle` 程序集可以直接引用两者，且不需要 `Camera`；
- `BattlePrefabSlot` 放 `Scripts/Scene`（`NineDays.Game`），任何层都能用。

### 2.5 为什么 `BattleController` 用 `HasArenaPrefab` 决定是否走程序化地形

- 旧场景（`TestArena`）仍用 `ArenaController`；
- 新场景用场地 prefab 时不能同时建程序化地形；
- 用 `HasArenaPrefab` 做单一判断，避免两套地形叠加；
- 没有 `sceneInitializer` 时完全保持原逻辑。

### 2.6 为什么出生点只写 `spawnPoint`，不直接移动刚体

- `BattleController.BeginBattle` 紧接着会调用 `Character.ResetForBattle()`；
- `ResetForBattle` 已经负责清速度、回出生点、回血、清状态；
- 初始化器只写值，避免重复逻辑和 Awake/Start 顺序问题。

---

## 3. 新增 / 修改文件

### 3.1 新增

| 文件 | 类型 | 职责 |
|---|---|---|
| `Assets/Scripts/Battle/BattleLevel.cs` | MonoBehaviour | 关卡数据：背景 prefab、场地 prefab、双方出生点 |
| `Assets/Scripts/Battle/BattleSceneInitializer.cs` | MonoBehaviour | 读取关卡/覆盖，驱动槽位，写出生点 |
| `Assets/Scripts/Scene/BattlePrefabSlot.cs` | MonoBehaviour | 背景/场地 prefab 实例化与销毁 |
| `Assets/Scripts/Editor/BattleSceneSetup.cs` | Editor | 默认 prefab 生成、Battle Scene 接线、菜单/batch 入口 |
| `Assets/Tests/PlayMode/BattleSceneInitializerTests.cs` | Test | 关卡/覆盖/槽位/出生点 |

### 3.2 修改

| 文件 | 改动 |
|---|---|
| `Scripts/Character/Character.cs` | 新增 `SetSpawnPoint(Vector3 point, bool moveImmediately = false)` |
| `Scripts/Battle/BattleController.cs` | 新增 `sceneInitializer` 引用；`Awake` 自动查找；`BeginBattle` 先应用关卡，再按 `HasArenaPrefab` 决定是否 `ArenaController.BuildForBattle` |

### 3.3 新增资产

| 路径 | 说明 |
|---|---|
| `Assets/Prefabs/Backgrounds/Background_Default.prefab` | 默认背景，只放表现层 |
| `Assets/Prefabs/Arenas/Arena_Default.prefab` | 默认场地，地形视觉 + 碰撞 |
| `Assets/Prefabs/Levels/Level_Default.prefab` | 默认关卡，根节点挂 `BattleLevel` |

> 本轮不新增 ScriptableObject、不改 `ArenaController`、不改 `ArenaBounds`、不改子弹/爆发/UI。

---

## 4. 关键实现细节

### 4.1 `BattleLevel`

```csharp
public class BattleLevel : MonoBehaviour
{
    [Header("背景 / 场地")]
    public GameObject backgroundPrefab;
    public GameObject arenaPrefab;

    [Header("角色初始位置（世界坐标）")]
    public Vector2 player1Spawn = new Vector2(-5f, -3.7f);
    public Vector2 player2Spawn = new Vector2(5f, -3.7f);
}
```

- 字段 public 是为了 Inspector 可调、测试可直接构造；
- 不写 `Validate`，空引用由初始化器统一处理；
- 不在运行时改关卡资产。

### 4.2 `BattlePrefabSlot`

```csharp
public class BattlePrefabSlot : MonoBehaviour
{
    [SerializeField] private Transform root;

    public GameObject CurrentPrefab { get; private set; }
    public GameObject Instance { get; private set; }

    public void Apply(GameObject prefab)
    {
        if (prefab == CurrentPrefab && Instance != null) return;
        Clear();
        CurrentPrefab = prefab;
        if (prefab == null) return;

        Transform parent = root != null ? root : transform;
        Instance = Instantiate(prefab, parent, false);
        Instance.name = prefab.name;
    }

    public void Clear()
    {
        if (Instance != null)
        {
            Instance.SetActive(false);
            Destroy(Instance);
        }
        Instance = null;
        CurrentPrefab = null;
    }
}
```

坑：

- `Destroy` 延迟一帧，先 `SetActive(false)`，否则旧背景/旧地形同帧还在；
- `Instantiate(prefab, parent, false)` 保留 prefab 自身局部变换，要求 prefab 按原点摆放；
- 不在槽位里改 sorting/颜色/层，prefab 自己负责。

### 4.3 `BattleSceneInitializer`

```csharp
public class BattleSceneInitializer : MonoBehaviour
{
    [Header("关卡")]
    [SerializeField] private BattleLevel level;
    [SerializeField] private BattleLevel levelOverride;

    [Header("测试覆盖（属性板）")]
    [SerializeField] private GameObject backgroundOverride;
    [SerializeField] private GameObject arenaOverride;

    [Header("槽位")]
    [SerializeField] private BattlePrefabSlot backgroundSlot;
    [SerializeField] private BattlePrefabSlot arenaSlot;

    [Header("角色")]
    [SerializeField] private TurnController turnController;

    public BattleLevel ActiveLevel { get; private set; }
    public GameObject ActiveBackground { get; private set; }
    public GameObject ActiveArena { get; private set; }
    public bool HasArenaPrefab => ActiveArena != null;

    public bool Apply()
    {
        BattleLevel resolved = levelOverride != null ? levelOverride : level;
        ActiveLevel = resolved;
        ActiveBackground = backgroundOverride != null
            ? backgroundOverride
            : (resolved != null ? resolved.backgroundPrefab : null);
        ActiveArena = arenaOverride != null
            ? arenaOverride
            : (resolved != null ? resolved.arenaPrefab : null);

        EnsureSlots();
        backgroundSlot.Apply(ActiveBackground);
        arenaSlot.Apply(ActiveArena);

        if (resolved != null)
        {
            SetSpawnPoint(Team.Player1, resolved.player1Spawn);
            SetSpawnPoint(Team.Player2, resolved.player2Spawn);
        }

        return resolved != null || ActiveBackground != null || ActiveArena != null;
    }
}
```

对外测试/注入入口：`BackgroundSlot` / `ArenaSlot` 只读属性、`SetLevelOverride`、`SetOverrides`；正式玩法只用 Inspector 字段。

`EnsureSlots()`：

```csharp
private void EnsureSlots()
{
    if (backgroundSlot == null)
        backgroundSlot = CreateSlot("BackgroundRoot");
    if (arenaSlot == null)
        arenaSlot = CreateSlot("ArenaRoot");
}

private BattlePrefabSlot CreateSlot(string slotName)
{
    var go = new GameObject(slotName);
    go.transform.SetParent(transform, false);
    go.transform.position = Vector3.zero;
    return go.AddComponent<BattlePrefabSlot>();
}
```

`SetSpawnPoint`：

```csharp
private void SetSpawnPoint(Team team, Vector2 position)
{
    Character character = turnController != null ? turnController.GetCharacter(team) : null;
    if (character == null)
    {
        Debug.LogWarning($"[BattleScene] 缺少 {team} 角色，无法设置出生点");
        return;
    }

    character.SetSpawnPoint(position);
}
```

`Awake`：

- `turnController` 为空时 `FindAnyObjectByType<TurnController>()`；
- 调 `EnsureSlots()`，保证 `BackgroundSlot` / `ArenaSlot` 可读；
- 不在这里 `Apply()`，由 `BattleController.BeginBattle` 调用，保证顺序。

### 4.4 `Character.SetSpawnPoint`

```csharp
public void SetSpawnPoint(Vector3 point, bool moveImmediately = false)
{
    spawnPoint = point;
    if (!moveImmediately || body == null) return;

    body.linearVelocity = Vector2.zero;
    body.angularVelocity = 0f;
    body.position = point;
}
```

- 默认只写值；`ResetForBattle` 会移动；
- 现有场景不调用时，行为不变。

### 4.5 `BattleController.BeginBattle` 接入

```csharp
sceneInitializer?.Apply();

if (sceneInitializer != null && sceneInitializer.HasArenaPrefab)
    arena?.ClearRuntime();
else
    arena?.BuildForBattle();

for (int i = 0; i < characters.Count; i++)
    if (characters[i] != null) characters[i].ResetForBattle();
```

- `sceneInitializer.Apply()` 放在现有参数校验通过之后、`arena` 处理之前；
- 先关卡、再处理程序化地形（有 prefab 场地时清掉旧运行时地形，否则构建）、最后角色重置；
- `Awake` 里 `sceneInitializer` 为空时自动查找；
- `RestartBattle` 走 `BeginBattle`，所以重开会重新应用当前 Inspector 选择。

---

## 5. 场景接入

### 5.1 场景对象

```text
Battle Scene
 ├─ BattleController
 │    └─ sceneInitializer → BattleSceneInitializer
 ├─ BattleSceneInitializer
 │    ├─ level            → Level_Default.prefab
 │    ├─ levelOverride    → 空（测试时选另一个关卡）
 │    ├─ backgroundOverride → 空（测试时选背景）
 │    ├─ arenaOverride    → 空（测试时选场地）
 │    ├─ backgroundSlot   → 空（自动创建 BackgroundRoot）
 │    ├─ arenaSlot        → 空（自动创建 ArenaRoot）
 │    └─ turnController   → 场景里的 TurnController
 └─ TurnController
```

- `BattleController.Arena` 用场地 prefab 时留空；
- 用程序化地形时保留原 `ArenaController`，`BattleSceneInitializer.arenaOverride` 与关卡 `arenaPrefab` 都留空；
- 静态美术场地：`arenaPrefab` 留空，静态物保留；初始化器只换背景/出生点。

### 5.2 `TestArena` / `Test_BattleScene`

| 场景 | 接法 |
|---|---|
| `TestArena`（程序化地形） | 加 `BattleSceneInitializer`，`level` 留空，只配 `backgroundOverride`；`arenaPrefab` 留空；`BattleController.arena` 保留 |
| `Test_BattleScene`（静态美术） | 加 `BattleSceneInitializer`；`arenaPrefab` 留空，静态美术就是场地；背景用 `backgroundOverride`，出生点用 `level` 或保持场景原位置 |
| `Battle Scene`（正式） | 加 `BattleSceneInitializer` + 关卡 prefab；`BattleController.arena` 留空；场地由 prefab 提供 |

> 同一场景不要同时存在“场地 prefab”和 `ArenaController`/静态地形；否则会出现两套碰撞和视觉。

---

## 6. 兼容性与风险

| 风险 | 说明 | 处理 |
|---|---|---|
| 旧场景没有 `BattleSceneInitializer` | `BattleController.sceneInitializer == null` | 保持原 `arena?.BuildForBattle()` 与场景出生点 |
| 关卡只有背景没有场地 | `HasArenaPrefab == false` | 继续走 `ArenaController.BuildForBattle()`（如果有） |
| 关卡只有场地没有背景 | 背景槽位 `Apply(null)` | 场景原背景不被主动删除；如需清空，手动处理 |
| 场地 prefab 与程序化地形同时存在 | 两套地形/碰撞 | 文档明确二选一；`BattleController` 用 `HasArenaPrefab` 跳过程序化 |
| 背景 prefab 带 Collider | 角色/子弹撞到背景 | 文档要求背景只放表现层；`BattlePrefabSlot` 不做强制移除 |
| prefab 原点不对 | 背景/场地偏移 | 统一按世界原点摆放；`Instantiate(..., false)` 保留局部变换 |
| 出生点写晚于 `ResetForBattle` | 角色回到旧位置 | `BattleController` 保证先 `Apply()` 再 `ResetForBattle()` |
| 角色缺失 | 关卡有出生点但场景缺 P1/P2 | `SetSpawnPoint` 只警告，不崩溃 |
| 覆盖字段在 Play 中修改 | 不会立即生效 | 下次 `BeginBattle` / 按 `R` 生效；本轮不做运行时切换 |
| 直接改关卡 prefab | 影响所有使用它的场景 | 测试优先用 `levelOverride` / 背景/场地覆盖 |
| `TestArena` 生成物 | 手工加的组件会被 `Build/All` 覆盖 | 要长期保留就同步改 `Editor/ArenaBuilder.cs`；`Battle Scene` 不受影响 |

---

## 7. 测试计划

### 7.1 PlayMode

`BattleSceneInitializerTests`：

1. 创建简单的背景 prefab、场地 prefab（空 GameObject 即可，场地可加 Collider）；
2. 创建关卡对象（`GameObject` + `BattleLevel`，字段直接赋值）；
3. 创建 `BattleSceneInitializer`，通过 `SetOverrides` / 反射 / public 测试入口注入字段；
4. 断言：
   - `Apply()` 后 `backgroundSlot.Instance`、`arenaSlot.Instance` 非空；
   - `ActiveLevel` / `ActiveBackground` / `ActiveArena` 正确；
   - `levelOverride` 优先；
   - `backgroundOverride` / `arenaOverride` 分别优先；
   - 连续 `Apply()` 同一 prefab，`Instance` 引用不变；
   - 无关卡/覆盖时返回 false，槽位为空。

`BattleController_BeginBattle_AppliesBeforeReset`：

1. 场景里放两个 `Character`，原位置与关卡出生点不同；
2. `BattleController` 赋 `sceneInitializer`；
3. `BeginBattle()`；
4. 断言角色最终位置等于关卡出生点，血量回满。

### 7.2 手动验收

1. 创建 `Background_Default.prefab`、`Arena_Default.prefab`、`Level_Default.prefab`；
2. 战斗场景挂 `BattleSceneInitializer`，配 `level` 与 `turnController`；
3. Play：背景 + 场地 + 出生点正确；
4. 在 Inspector 换 `Background Override` / `Arena Override` / `Level Override`，重新 Play，分别验证；
5. 清空覆盖，Play，回默认关卡；
6. 旧场景不挂该组件，确认行为不变。

### 7.3 回归

- 默认资产/场景接线 batch：`BATTLE_SETUP_OK`；
- EditMode：**81/81 Passed**；
- PlayMode：**41/41 Passed**（原 34 + 新增 7）；
- 全量 PlayMode 期间发现旧 `BattleInputTests` 依赖 `TurnController` 自动收集顺序，已在该测试里显式指定 P1/P2 队伍；只改测试，不改运行时行为；
- 未运行 `BuildAllBatch`，避免重建并覆盖现有角色/子弹 prefab；只运行了本次新增的 `BattleSceneSetup.SetupBatch`。

---

## 8. 实现选择（按默认）

| # | 选择 | 落地 |
|---|---|---|
| 1 | 关卡 prefab 形态 | **数据载体**（根节点 + `BattleLevel`） |
| 2 | `levelOverride` 类型 | **`BattleLevel`** 组件引用 |
| 3 | 背景/场地 prefab 原点 | **世界原点** |
| 4 | 出生点坐标 | **世界坐标** |
| 5 | 测试覆盖方式 | **仅 Inspector**，无运行时面板 |

---

## 9. 实施检查清单

- [x] `BattleLevel` 能在 Inspector 配置背景/场地/出生点
- [x] `BattlePrefabSlot.Apply` 能实例化、替换、清空，旧实例先失活
- [x] `BattleSceneInitializer.Apply` 优先级正确：覆盖 > 关卡
- [x] `EnsureSlots` 能自动创建 `BackgroundRoot` / `ArenaRoot`
- [x] `Character.SetSpawnPoint` 不影响旧场景
- [x] `BattleController.BeginBattle` 先应用关卡再 `ResetForBattle`
- [x] 没有场地 prefab 时仍走 `ArenaController`
- [x] 有场地 prefab 时不走 `ArenaController`
- [x] `Level Override` / `Background Override` / `Arena Override` 在 Inspector 生效
- [x] 旧场景不挂新组件行为不变
- [x] EditMode 81/81、PlayMode 41/41
- [x] `Docs/agent/README.md` 索引已更新
- [x] `Docs/program/**` 未改动

---

## 10. 验证命令

```powershell
$unity   = 'C:\Program Files\Unity\Hub\Editor\6000.0.83f1\Editor\Unity.exe'
$project = 'C:\Users\19314\Desktop\此电脑\program\Project\9days-game'

# 默认关卡/背景/场地 prefab + Battle Scene 接线
& $unity -batchmode -quit -projectPath $project `
  -executeMethod NineDays.EditorTools.BattleSceneSetup.SetupBatch `
  -logFile "$project\Temp\battle_setup.log"

# 生成 + 结构校验（会重建角色/子弹 prefab，按需运行）
& $unity -batchmode -quit -projectPath $project `
  -executeMethod NineDays.EditorTools.BuildPipeline.BuildAllBatch `
  -logFile "$project\Temp\build.log"

# EditMode
& $unity -batchmode -nographics -projectPath $project `
  -runTests -testPlatform EditMode `
  -testResults "$project\Temp\editmode.xml" -logFile "$project\Temp\editmode.log"

# PlayMode
& $unity -batchmode -projectPath $project `
  -runTests -testPlatform PlayMode `
  -testResults "$project\Temp\playmode.xml" -logFile "$project\Temp\playmode.log"
```

> 跑 batchmode 前先关闭 Unity；`Temp/UnityLockfile` 存在时 Unity 会直接退出。

---

## 11. 后续可选扩展（本轮不做）

- 关卡切换流程：主菜单/流程层把选中的 `BattleLevel` 传给 `BattleSceneInitializer.ApplyLevel`；
- 背景/场地运行时切换面板：需要额外的调试 UI；
- 场地大小变化后的 `ArenaBounds` / 相机适配；
- 背景视差：在背景 prefab 上挂独立组件，不影响本方案；
- 关卡 Catalog：关卡数量多了再考虑，当前复制 prefab 足够。
