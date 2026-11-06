# 战斗选弹 — Agent 方案记录

> 人读版：同目录 `BattleAmmo.md`。  
> **本次状态：已落地（2026-11-06）。** 默认目录 2 种子弹，`Battle Scene` 已接选弹门与 `AmmoSelectPage`；实现记录见下方 0.1–0.3。

## 目录

- [0. 本次工作边界](#0-本次工作边界)
- [1. 现状证据](#1-现状证据)
- [2. 设计决策与理由](#2-设计决策与理由)
- [3. 新增 / 修改文件](#3-新增--修改文件)
- [4. 关键实现细节](#4-关键实现细节)
- [5. 程序集与依赖](#5-程序集与依赖)
- [6. 场景/资产生成](#6-场景资产生成)
- [7. 测试计划与时长](#7-测试计划与时长)
- [8. 风险与兼容性](#8-风险与兼容性)
- [9. 实现选择（按默认）](#9-实现选择按默认)
- [10. 实施检查清单](#10-实施检查清单)
- [11. 验证命令](#11-验证命令)

---

## 0. 本次工作边界

本次已落地并验证：

- 新增 `AmmoDefinition` / `AmmoCatalog` / `AmmoDraw`；
- 新增 `ITurnStartGate`，`BattleController.BeginTurn` 接入选弹门；
- 新增 `NineDays.Flow` 与 `AmmoSelectionController`；
- 新增 `AmmoSelectPage` / `AmmoOptionView` / `UIAmmoSelectPresenter`；
- `CharacterLauncher` 支持选弹覆盖 prefab；
- 生成默认弹药数据、UI 页面、`Battle Scene` 接线；
- 新增 9 个 EditMode + 3 个 PlayMode 用例。

本轮不做：

- 道具抽选/摆放；
- 弹道预览/历史轨迹；
- 同 prefab 不同数值的选弹（仍由 prefab 的 `ProjectileConfig` 决定）；
- 选弹记录存档、联机同步、动画音效。

### 0.1 实际改动清单

| 文件 | 类型 | 内容 |
|---|---|---|
| `Scripts/Config/AmmoDefinition.cs` | 新增 | prefab、名称、介绍、图标 |
| `Scripts/Config/AmmoCatalog.cs` | 新增 | 目录、校验、测试注入 |
| `Scripts/Bullet/AmmoDraw.cs` | 新增 | 随机/全选/去重纯逻辑 |
| `Scripts/Battle/ITurnStartGate.cs` | 新增 | 回合开始门接口 |
| `Scripts/Battle/BattleController.cs` | 修改 | gate 解析、BeginTurn、Cancel |
| `Scripts/Character/CharacterLauncher.cs` | 修改 | `SetProjectilePrefab` / `ActiveProjectilePrefab` |
| `Scripts/Flow/NineDays.Flow.asmdef` | 新增 | Flow 程序集 |
| `Scripts/Flow/AmmoSelectionController.cs` | 新增 | 抽选、UI、输入门、应用 prefab |
| `Scripts/UI/AmmoSelect/AmmoSelectArgs.cs` | 新增 | `AmmoOption` / `AmmoSelectArgs` |
| `Scripts/UI/AmmoSelect/IAmmoSelectPresenter.cs` | 新增 | UI 桥接接口 |
| `Scripts/UI/AmmoSelect/AmmoOptionView.cs` | 新增 | 单选项视图 |
| `Scripts/UI/AmmoSelect/AmmoSelectPage.cs` | 新增 | 动态按钮页面 |
| `Scripts/UI/AmmoSelect/UIAmmoSelectPresenter.cs` | 新增 | Router Popup 桥接 |
| `Scripts/Editor/AmmoFactory.cs` | 新增 | 默认弹药资产 |
| `Scripts/Editor/UIFactory.cs` | 修改 | 生成选弹页/config，加入 catalog |
| `Scripts/Editor/BattleSceneSetup.cs` | 修改 | UIRoot、AmmoSelection、gate 接线 |
| `Scripts/Editor/BuildPipeline.cs` | 修改 | 生成 Ammo 资产 |
| `Scripts/Editor/EditorAssets.cs` | 修改 | Ammo/UI 路径常量 |
| `Scripts/Editor/NineDays.Editor.asmdef` | 修改 | 引用 Flow |
| `Assets/Tests/*.asmdef` | 修改 | 引用 Flow |
| `Assets/Tests/EditMode/AmmoDrawTests.cs` | 新增 | 抽选逻辑 |
| `Assets/Tests/EditMode/AmmoCatalogTests.cs` | 新增 | 数据目录 |
| `Assets/Tests/PlayMode/AmmoSelectionFlowTests.cs` | 新增 | FakePresenter 流程 |
| `Assets/Tests/PlayMode/AmmoSelectPageTests.cs` | 新增 | 动态按钮/回调 |
| `Assets/Tests/PlayMode/BattleSceneSmokeTests.cs` | 新增 | Battle Scene 首回合选弹页冒烟 |

### 0.2 生成资产与场景

| 资产/场景 | 结果 |
|---|---|
| `Assets/Data/Ammo/AmmoDefinition_Simple.asset` | Simple Bullet + 普通子弹文案/图标 |
| `Assets/Data/Ammo/AmmoDefinition_TestBall.asset` | TestBall + 测试球文案/图标 |
| `Assets/Data/Ammo/AmmoCatalog_Default.asset` | 上述两项 |
| `Assets/Data/UI/Pages/AmmoSelectPage.asset` | Popup / modal / closeOnBack=false |
| `Assets/Prefabs/UI/Pages/AmmoSelectPage.prefab` | Grid + inactive 模板 |
| `Assets/Scenes/Battle Scene.unity` | UIRoot + AmmoSelection + gate 接线 |

### 0.3 验证结果

| 项 | 结果 |
|---|---|
| 生成/接线 batch | `BATTLE_SETUP_OK` |
| EditMode | **90/90 Passed** |
| PlayMode | **44/44 Passed** |
| 新增用例 | EditMode 9、PlayMode 3 |
| 日志 | `Temp/ammo_setup8.log`、`Logs/ammo_editmode.xml`、`Logs/ammo_playmode.xml` |

---

## 1. 现状证据

| 事实 | 证据 |
|---|---|
| `BattleController.BeginTurn` 直接开始回合 | `Assets/Scripts/Battle/BattleController.cs:163-167` |
| `TurnController.BeginTurn` 立即给操作权并发事件 | `Assets/Scripts/Battle/TurnController.cs:79-106` |
| `BattleInputController` 可按开关禁用 | `Assets/Scripts/Battle/BattleInputController.cs:19-42` |
| `CharacterLauncher` 固定 `projectilePrefab` | `Assets/Scripts/Character/CharacterLauncher.cs:13,106,122` |
| `ShotSpawner.Spawn(prefab, shooter, dir, speed)` 读 prefab.Config | `Assets/Scripts/Bullet/ShotSpawner.cs:41-62` |
| UI 框架支持 Popup 栈与模态遮罩 | `Assets/Scripts/UI/Framework/UIPageRouter.cs` |
| `UIPageCatalog_Default` 目前只有 Placeholder | `Assets/Data/UI/UIPageCatalog_Default.asset` |
| `UIRoot.prefab` 已存在，默认 catalog 引用可复用 | `Assets/Prefabs/UI/UIRoot.prefab` |
| `Battle Scene` 目前没有 `UIRoot` | 场景层级只有 `Battle Page` 与相机 |
| 当前子弹 prefab 两个 | `Simple Bullet.prefab`、`TestBall.prefab` |

---

## 2. 设计决策与理由

### 2.1 选弹放在 `BeginTurn` 之前，而不是 `TurnController` 内部

- `TurnController.BeginTurn` 会立即 `SetCanAct(true)`，此时玩家已经可以拖拽；
- 选弹页打开时必须禁止瞄准；
- 在 `BattleController.BeginTurn` 用 gate 包住 `turnController.BeginTurn(actor)`，选弹完成才真正开始回合；
- 不把 UI/选弹逻辑塞进 `TurnController`，保持回合规则纯净。

### 2.2 新增 `NineDays.Flow`，而不是让 Battle 引用 UI

- `BattleController` 只需要 `ITurnStartGate` 接口，不需要知道 UI；
- `AmmoSelectionController` 需要 Game 数据、Battle 角色/回合、UI 页面；
- 新建 `NineDays.Flow` 作为高层流程层，引用 `Battle + Game + UI + Input`；
- 依赖方向保持：`UI` 不引用 Game/Battle，`Battle` 不引用 UI/Flow；
- `Flow` 与 `Camera` / `Debug` 同级，属于编排层。

### 2.3 数据体只绑定 prefab，不复制数值

- 用户要求数据体绑 prefab、名称、介绍、图标；
- 现有 prefab 已带 `ProjectileConfig`；
- `AmmoDefinition` 只保留表现/元数据 + prefab；
- `ShotSpawner` 不改，避免新增 config 重载与额外测试面。

### 2.4 纯抽选逻辑独立

- `AmmoDraw.Pick` 只依赖 `AmmoDefinition`、`int`、`bool`、`Random`；
- 去重、全选、空池、数量夹取都可以 EditMode 秒测；
- PlayMode 只验证“流程 + 页面生成”，不重复测随机规则。

### 2.5 UI 页面只收纯数据

- `AmmoSelectArgs` 里只有 `AmmoOption`（Name/Description/Icon）和 `Action<int>`；
- UI 不引用 `AmmoDefinition` / `Projectile` / `Team`；
- Flow 负责把玩法数据翻译成 UI 数据。

### 2.6 失败回退

- 没有 `UIRoot`、没有页面、catalog 为空、presenter 为空：直接选第一个候选或沿用 launcher 默认 prefab；
- 保证无 UI 的测试/旧场景不被卡住；
- 页面打开后被 `RestartBattle`/`Abort`：`Cancel` 关闭并恢复输入。

---

## 3. 新增 / 修改文件

### 3.1 新增

| 文件 | 类型 | 职责 |
|---|---|---|
| `Scripts/Config/AmmoDefinition.cs` | SO | prefab、名称、介绍、图标 |
| `Scripts/Config/AmmoCatalog.cs` | SO | 全部子弹定义 |
| `Scripts/Bullet/AmmoDraw.cs` | 纯 C# | 随机/全选/去重 |
| `Scripts/Battle/ITurnStartGate.cs` | 接口 | 回合开始前插入选弹门 |
| `Scripts/Flow/AmmoSelectionController.cs` | MonoBehaviour | 抽选、UI 调用、应用 prefab、输入门 |
| `Scripts/Flow/NineDays.Flow.asmdef` | asmdef | Flow 程序集 |
| `Scripts/UI/AmmoSelect/AmmoSelectArgs.cs` | UI 数据 | `AmmoOption` / `AmmoSelectArgs` |
| `Scripts/UI/AmmoSelect/AmmoOptionView.cs` | UI 组件 | 单个选项的图标/名称/介绍/按钮 |
| `Scripts/UI/AmmoSelect/AmmoSelectPage.cs` | UI 页面 | 按数量生成按钮 |
| `Scripts/UI/AmmoSelect/UIAmmoSelectPresenter.cs` | UI 桥接 | Router/Popup 打开/关闭 |
| `Scripts/Editor/AmmoFactory.cs` | Editor | 默认 AmmoDefinition/Catalog 资产 |
| `Assets/Tests/EditMode/AmmoDrawTests.cs` | Test | 抽选/全选/去重/空池 |
| `Assets/Tests/EditMode/AmmoCatalogTests.cs` | Test | 数据校验 |
| `Assets/Tests/PlayMode/AmmoSelectionFlowTests.cs` | Test | FakePresenter 流程 |
| `Assets/Tests/PlayMode/AmmoSelectPageTests.cs` | Test | 动态按钮/回调 |
| `Assets/Tests/PlayMode/BattleSceneSmokeTests.cs` | Test | Battle Scene 首回合选弹页冒烟 |

### 3.2 修改

| 文件 | 改动 |
|---|---|
| `Scripts/Battle/BattleController.cs` | `turnStartGateSource` / `ITurnStartGate`；`BeginTurn` gate；`BeginBattle/Restart/Abort` cancel |
| `Scripts/Character/CharacterLauncher.cs` | `SetProjectilePrefab` / `ResetProjectilePrefab` / `ActiveProjectilePrefab`；`Fire` 使用覆盖 prefab |
| `Scripts/Editor/UIFactory.cs` | 生成 `AmmoSelectPage` prefab/config，加入默认 catalog |
| `Scripts/Editor/BuildPipeline.cs` | 在 Prefabs/All 中调用 AmmoFactory + UIFactory |
| `Scripts/Editor/BattleSceneSetup.cs` | 给 `Battle Scene` 加 `UIRoot`、`AmmoSelection`、presenter，接线 |
| `Scripts/Editor/ArenaBuilder.cs`（可选） | 给 `TestArena` 加同样组件 |
| `Scripts/Editor/EditorAssets.cs` | 新增 UI/Ammo 路径常量 |
| `Scripts/Editor/NineDays.Editor.asmdef` | 引用新增 `NineDays.Flow` |
| `Assets/Tests/NineDays.Tests.asmdef` | 引用 `NineDays.Flow` |
| `Assets/Tests/PlayMode/NineDays.Tests.PlayMode.asmdef` | 引用 `NineDays.Flow` |

### 3.3 新增资产

| 路径 | 说明 |
|---|---|
| `Assets/Data/Ammo/AmmoDefinition_Simple.asset` | `Simple Bullet.prefab` + 普通子弹文案/图标 |
| `Assets/Data/Ammo/AmmoDefinition_TestBall.asset` | `TestBall.prefab` + 测试球文案/图标 |
| `Assets/Data/Ammo/AmmoCatalog_Default.asset` | 上面两项 |
| `Assets/Data/UI/Pages/AmmoSelectPage.asset` | `pageId=AmmoSelect`，Popup，modal，closeOnBack=false |
| `Assets/Prefabs/UI/Pages/AmmoSelectPage.prefab` | 选项容器 + inactive 模板 |

---

## 4. 关键实现细节

### 4.1 `AmmoDefinition`

```csharp
[CreateAssetMenu(menuName = "9Days/配置/弹药定义", fileName = "AmmoDefinition")]
public class AmmoDefinition : ScriptableObject
{
    [Header("子弹")]
    public Projectile prefab;

    [Header("显示")]
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;

    public string ResolvedName => string.IsNullOrWhiteSpace(displayName)
        ? (prefab != null ? prefab.name : "Unnamed")
        : displayName;
}
```

- `prefab` 是 `Projectile` 组件，不是 `GameObject`；
- 不复制 config；
- `OnValidate` 可只补 `displayName`，不改 prefab。

### 4.2 `AmmoCatalog`

```csharp
public class AmmoCatalog : ScriptableObject
{
    public List<AmmoDefinition> ammo = new List<AmmoDefinition>();
    public int Count => ...
    public AmmoDefinition Get(int index) => ...
    public void Validate() { ... }
}
```

- `Validate` 去空、去 `prefab == null`；
- 不改顺序，保证“全选”顺序稳定。

### 4.3 `AmmoDraw.Pick`

```csharp
public static IReadOnlyList<AmmoDefinition> Pick(
    IReadOnlyList<AmmoDefinition> pool, int count, bool all, Random rng)
{
    if (pool == null) return Array.Empty<AmmoDefinition>();
    rng ??= new Random();

    var candidates = new List<AmmoDefinition>();
    for (...) if (pool[i] != null && pool[i].prefab != null) candidates.Add(pool[i]);
    if (candidates.Count == 0) return Array.Empty<AmmoDefinition>();
    if (all || count >= candidates.Count) return candidates;

    count = Math.Max(1, count);
    // partial Fisher-Yates
    for (int i = 0; i < count; i++)
    {
        int j = rng.Next(i, candidates.Count);
        (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
    }
    return candidates.GetRange(0, count);
}
```

- `rng` 由调用方传，测试用 `new Random(seed)`；
- `all` 保持 catalog 顺序；
- 返回新列表，不修改输入。

### 4.4 `AmmoSelectionController`

```csharp
public sealed class AmmoSelectionController : MonoBehaviour, ITurnStartGate
{
    [SerializeField] private AmmoCatalog catalog;
    [SerializeField, Min(1)] private int drawCount = 3;
    [SerializeField] private bool showAllBulletsForTesting;
    [SerializeField] private int randomSeed;
    [SerializeField] private UIAmmoSelectPresenter presenter;
    [SerializeField] private TurnController turnController;
    [SerializeField] private BattleInputController inputController;

    public void BeginTurn(Team actor, Action onReady)
    {
        if (selecting) Cancel();
        selecting = true;
        this.actor = actor;
        this.onReady = onReady;

        picked = AmmoDraw.Pick(catalog != null ? catalog.ammo : null,
                               drawCount, showAllBulletsForTesting, RandomForDraw());
        if (picked.Count == 0)
        {
            selecting = false;
            ResetActorPrefab();
            onReady?.Invoke();
            onReady = null;
            return;
        }

        previousInput = inputController != null && inputController.InputEnabled;
        if (inputController != null) inputController.InputEnabled = false;

        if (presenter == null || !presenter.Show(BuildOptions(picked), OnPicked))
        {
            // UI 不可用：直接选第一个，保证流程继续
            CompleteSelection(picked[0]);
        }
    }

    private void OnPicked(int index)
    {
        if (!selecting || index < 0 || index >= picked.Count) return;
        CompleteSelection(picked[index]);
    }

    private void CompleteSelection(AmmoDefinition def)
    {
        if (!selecting) return;
        selecting = false;

        if (def != null) ApplyToActor(def);
        else ResetActorPrefab();

        presenter?.Hide();
        if (inputController != null) inputController.InputEnabled = previousInput;

        Action callback = onReady;
        onReady = null;
        callback?.Invoke();
    }

    public void Cancel()
    {
        if (!selecting) return;
        selecting = false;
        onReady = null;
        presenter?.Hide();
        if (inputController != null) inputController.InputEnabled = previousInput;
    }
}
```

注意：

- `CompleteSelection` 先置 `selecting=false` 再回调，避免重复点击；
- `presenter.Show` 返回 false 时走回退，不等待 UI；
- `ApplyToActor` / `ResetActorPrefab` 通过 `turnController.GetCharacter(actor)` 找 `CharacterLauncher`；
- `RandomForDraw`：`randomSeed == 0 ? new Random() : persistentRandom`；固定种子在 `Awake` 创建一次；
- `Configure(...)` 仅用于测试/注入，正式场景走 Inspector。

---

### 4.5 `BattleController` 接入

```csharp
[SerializeField] private MonoBehaviour turnStartGateSource;
private ITurnStartGate turnStartGate;

private void Awake()
{
    ...
    turnStartGate = turnStartGateSource as ITurnStartGate;
}

public void SetTurnStartGate(ITurnStartGate gate) => turnStartGate = gate;

public void BeginTurn(Team actor, Action<TurnResult> onResolved)
{
    pendingCallback = onResolved;
    if (turnStartGate != null)
        turnStartGate.BeginTurn(actor, () => turnController.BeginTurn(actor));
    else
        turnController.BeginTurn(actor);
}

public void Abort()
{
    pendingCallback = null;
    turnStartGate?.Cancel();
    turnController?.AbortTurn();
}
```

- `BeginBattle` 开头也调 `turnStartGate?.Cancel()`；
- `RestartBattle` 走 `BeginBattle`；
- 旧场景 `turnStartGate == null` 完全不变。

### 4.6 `CharacterLauncher`

```csharp
private Projectile selectedPrefab;

public Projectile ActiveProjectilePrefab =>
    selectedPrefab != null ? selectedPrefab : projectilePrefab;

public void SetProjectilePrefab(Projectile prefab) => selectedPrefab = prefab;
public void ResetProjectilePrefab() => selectedPrefab = null;
```

`Fire()`：

```csharp
Projectile prefab = ActiveProjectilePrefab;
if (prefab == null) { CancelAim(); return; }
Projectile projectile = ShotSpawner.Spawn(prefab, character, direction, speed);
```

### 4.7 UI 页面

`AmmoSelectPage.OnShow`：

```csharp
var args = args as AmmoSelectArgs;
ClearOptions();
if (args == null) return;

for (int i = 0; i < args.Options.Count; i++)
{
    int index = i;
    AmmoOptionView view = Instantiate(optionTemplate, optionRoot);
    view.gameObject.SetActive(true);
    view.Bind(args.Options[i], () => args.OnSelected?.Invoke(index));
}
```

`AmmoOptionView.Bind` 只改 UI，不持有玩法对象；`OnHide` 清理动态实例。

---

## 5. 程序集与依赖

新增 `Scripts/Flow/NineDays.Flow.asmdef`：

```json
{
  "name": "NineDays.Flow",
  "references": [
    "NineDays.Battle",
    "NineDays.Game",
    "NineDays.UI",
    "NineDays.Input"
  ]
}
```

依赖图：

```text
NineDays.UI     NineDays.Game     NineDays.Battle
      ↑               ↑                  ↑
      └────────── NineDays.Flow ─────────┘
                         ↑
                  NineDays.Editor / Tests
```

- `BattleController` 只引用 `ITurnStartGate`（Battle 内），不引用 Flow/UI；
- `Flow` 引用 `Battle` 获取 `TurnController` / `BattleInputController` / `ITurnStartGate`；
- `Flow` 引用 `UI` 使用 presenter/args；
- `UI` 不引用 Game/Battle/Flow；
- `Editor` 和 Tests 增加 Flow 引用；
- 需要同步更新 `Docs/agent/asmdef/Asmdef.md` 的程序集清单与依赖图。

---

## 6. 场景/资产生成

### 6.1 `UIFactory`

- 生成 `AmmoSelectPage.prefab`；
- 生成 `AmmoSelectPage.asset`（Popup / modal / closeOnBack=false / KeepAlive）；
- 把配置加入 `UIPageCatalog_Default`；
- `UIRoot.prefab` 不需要重写 catalog 引用（它已指向默认 catalog）。

页面 prefab 结构：

```text
AmmoSelectPage (RectTransform + CanvasGroup + AmmoSelectPage)
 ├─ Panel
 │   ├─ Title
 │   ├─ OptionRoot (GridLayoutGroup)
 │   └─ Hint
 └─ OptionTemplate (AmmoOptionView, inactive)
```

### 6.2 `AmmoFactory`

- 创建/补全 `AmmoDefinition_Simple`、`AmmoDefinition_TestBall`；
- 创建 `AmmoCatalog_Default`；
- 定义 prefab 用现有 `Simple Bullet.prefab` / `TestBall.prefab`；
- 图标用 `Assets/Art/Bullets` 下现有图；
- 只补空字段，不覆盖用户内容。

### 6.3 `BattleSceneSetup`

- 先 `AmmoFactory.EnsureDefaults()` + `UIFactory.BuildAll()`，保证 Ammo 数据与页面配置存在；
- 确保 `Battle Scene` 有 `UIRoot`（没有则实例化 `UIRoot.prefab`）；
- 创建 `AmmoSelection` 对象：
  - `AmmoSelectionController`（Flow）
  - `UIAmmoSelectPresenter`（UI）
- 赋值 catalog、drawCount、turnController、battleInput、presenter；
- `BattleController.turnStartGateSource = AmmoSelectionController`；
- 保存场景。

`ArenaBuilder` 可选给 `TestArena` 加同样对象；本轮不强制。

---

## 7. 测试计划与时长

### 7.1 EditMode（核心逻辑，秒级）

`AmmoDrawTests`：

- 5 选 3 → 3 个、不重复；
- 2 选 3 → 2 个；
- `all=true` → 全部且顺序稳定；
- 同种子两次结果一致；
- 空池 / 全 invalid → 空；
- `count=0` → 1 个。

`AmmoCatalogTests`：

- 去空 / 去 invalid；
- `Count` / `Get` 越界；
- `AmmoDefinition.displayName` 空时回退 prefab 名。

### 7.2 PlayMode（3 个，含 1 个场景冒烟）

`AmmoSelectionFlowTests.Selection_AppliesPrefab_AndStartsTurn`：

- 用 FakePresenter 替代 `UIAmmoSelectPresenter`；
- `AmmoSelectionController.Configure(...)` 注入 catalog、drawCount=2、seed；
- 调用 `BeginTurn(Team.Player1, onReady)`：
  - 断言 FakePresenter 收到 2 个选项；
  - 断言 `BattleInputController.InputEnabled == false`；
  - FakePresenter 选择第 2 个；
  - 断言 `CharacterLauncher.ActiveProjectilePrefab == 第 2 个 prefab`；
  - 断言输入恢复、`onReady` 调用一次。

`AmmoSelectPageTests.BuildsButtons_AndInvokesCallback`：

- 运行时构造 page + optionRoot + inactive template；
- `OnShow(new AmmoSelectArgs(3 个选项, callback))`；
- 断言 `optionRoot` 下 3 个 `AmmoOptionView`；
- 点击第 2 个，断言回调索引 1；
- `OnHide` 后动态按钮清空。

`BattleSceneSmokeTests.BattleScene_OpensAmmoSelectPage`：

- `LoadScene("Battle Scene", Additive)`；
- 等待两帧，断言首回合存在且可见的 `AmmoSelectPage`；
- 卸载场景，验证 UIRoot/AmmoSelection/默认关卡接线。

不为每个数量做 PlayMode；页面数量逻辑由 EditMode + 一个 3 选项用例覆盖。

实际新增：

- EditMode：**9 个**（`AmmoDrawTests` 6 + `AmmoCatalogTests` 3）；
- PlayMode：**3 个**（流程、页面、Battle Scene 冒烟）；
- 全量结果：EditMode **90/90**、PlayMode **44/44**。

### 7.3 回归

- 旧场景没有 gate：`BattleController.BeginTurn` 直接走原路径；
- 无 `UIRoot`：presenter 返回 false，选第一个候选继续；
- catalog 为空：不弹页，重置 launcher 默认 prefab；
- 原 41 个 PlayMode 用例保持通过（现总计 43）。

---

## 8. 风险与兼容性

| 风险 | 说明 | 处理 |
|---|---|---|
| Battle 依赖 UI | 通过 Flow + `ITurnStartGate` 隔离 | Battle 不引用 Flow/UI |
| UI 页面缺失 | `UIRoot`/catalog/page 不存在 | presenter 返回 false，回退第一个候选 |
| 页面打开时玩家点击场景 | 角色尚未 `CanAct`，且 `BattleInputController` 关闭 | 双保险 |
| 页面打开时按 R / Abort | 页面残留、输入锁死 | `BeginBattle/Restart/Abort` 调 `Cancel` |
| 选项重复 | `AmmoDraw` 部分 Fisher–Yates | 保证不重复 |
| 数量超过目录 | 自动显示全部 | 不复制/不补空 |
| 测试随机不稳定 | 固定 `randomSeed` / EditMode 注入 Random | 可复现 |
| UI 字体/布局 | 沿用现有 legacy Text + 512 参考分辨率 | 先可用，后续换 TMP |
| 新增 Flow asmdef | 循环引用风险 | Flow 只被 Editor/Tests 和场景组件引用；Battle 不引用 Flow |
| `UIRoot` 重复 | Battle Scene 可能已有 | setup 查找后再创建 |
| 选中 prefab 为 null | 数据损坏 | 跳过应用，沿用 launcher 默认 |

---

## 9. 实现选择（按默认）

| # | 选择 | 落地 |
|---|---|---|
| 1 | 选弹时机 | 每个 `BeginTurn`（含额外行动） |
| 2 | 页面在镜头切换前/后 | 之前：选弹完成才 `TurnController.BeginTurn` |
| 3 | 数量 > 目录 | 显示全部 |
| 4 | 全选顺序 | catalog 顺序 |
| 5 | 随机种子 | 0=每次随机；非 0=固定序列 |
| 6 | 能否取消选弹 | 不能；`closeOnBack=false` |
| 7 | 页面缓存 | KeepAlive，每次重建按钮 |
| 8 | `TestArena` 接线 | 本轮未接；只接 `Battle Scene` |

---

## 10. 实施检查清单

- [x] `AmmoDefinition` / `AmmoCatalog` 能在 Inspector 配置
- [x] `AmmoDraw` 纯逻辑 EditMode 全绿
- [x] `ITurnStartGate` 接入 `BattleController`，旧场景无 gate 行为不变
- [x] `AmmoSelectionController` 能抽选、回退、取消、恢复输入
- [x] `CharacterLauncher.SetProjectilePrefab` 生效
- [x] `AmmoSelectPage` 按数量生成按钮
- [x] `UIAmmoSelectPresenter` 能打开/关闭 Popup
- [x] `UIFactory` 生成页面/config 并加入默认 catalog
- [x] `BattleSceneSetup` 给 `Battle Scene` 加 `UIRoot` 与选弹组件
- [x] `Show All Bullets For Testing` 生效
- [x] `Draw Count` 修改生效
- [x] 新增 9 EditMode + 3 PlayMode 用例通过
- [x] 现有 EditMode 90 / PlayMode 44 全绿
- [x] 同步更新 `Docs/agent/asmdef/Asmdef.md`
- [x] 未修改 `Docs/program/**`

---

## 11. 验证命令

```powershell
$unity   = 'C:\Program Files\Unity\Hub\Editor\6000.0.83f1\Editor\Unity.exe'
$project = 'C:\Users\19314\Desktop\此电脑\program\Project\9days-game'

# 生成 UI 页面 + 弹药数据 + 场景接线（实现后）
& $unity -batchmode -quit -projectPath $project `
  -executeMethod NineDays.EditorTools.BattleSceneSetup.SetupBatch `
  -logFile "$project\Temp\battle_ammo_setup.log"

# EditMode
& $unity -batchmode -nographics -projectPath $project `
  -runTests -testPlatform EditMode `
  -testResults "$project\Temp\ammo_editmode.xml" -logFile "$project\Temp\ammo_editmode.log"

# PlayMode
& $unity -batchmode -projectPath $project `
  -runTests -testPlatform PlayMode `
  -testResults "$project\Temp\ammo_playmode.xml" -logFile "$project\Temp\ammo_playmode.log"
```

> 实现批次需要把 `AmmoFactory` / `UIFactory` 的产物纳入 `BattleSceneSetup` 或单独生成入口；不要直接跑会覆盖美术 prefab 的全量 `BuildAllBatch`，除非确认当前 prefab 无人手动改动。

