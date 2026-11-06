# 战斗选弹（人读版）

> 一句话：每次行动开始前，`AmmoSelectionController` 从 `AmmoCatalog` 随机抽 `drawCount` 种子弹（Inspector 可调；测试可改为全选），弹出 `AmmoSelectPage`；玩家点选后把 prefab 写入 `CharacterLauncher`，再真正开始回合输入。
>
> 当前状态：**已落地（2026-11-06）**。默认目录含 2 种子弹；`Battle Scene` 已接 `UIRoot`、`AmmoSelectionController` 与 `AmmoSelectPage`。本轮只做选弹与选弹页，不改子弹飞行、爆发、道具系统。

## 目录

1. [目标与范围](#1-目标与范围)
2. [现状与接入点](#2-现状与接入点)
3. [总体架构](#3-总体架构)
4. [数据资产](#4-数据资产)
5. [随机抽选逻辑](#5-随机抽选逻辑)
6. [运行时流程](#6-运行时流程)
7. [UI 页面](#7-ui-页面)
8. [场景接线与资产生成](#8-场景接线与资产生成)
9. [Inspector 参数与测试开关](#9-inspector-参数与测试开关)
10. [测试策略与时长控制](#10-测试策略与时长控制)
11. [代码质量与兼容性](#11-代码质量与兼容性)
12. [实现选择（按默认）](#12-实现选择按默认)
13. [文件索引](#13-文件索引)
14. [已落地状态](#14-已落地状态)

---

## 1. 目标与范围

| 目标 | 验收方式 |
|---|---|
| 每次行动开始前提供随机子弹 | 每个 `BattleController.BeginTurn` 都先弹选弹页 |
| 数量在 Inspector 可调 | `AmmoSelectionController.drawCount`，`Min(1)` |
| 测试时可选择所有子弹 | `showAllBulletsForTesting` 打开后忽略数量，展示目录全部 |
| UI 页面在发射前浮现 | Popup 页覆盖战斗画面，选择完成后关闭 |
| 按钮数量随选项数量生成 | 选项 1–N 个都只用一个页面 prefab |
| 子弹有数据体 | `AmmoDefinition`：prefab、名称、介绍、图标 |
| 测试快 | 随机/去重/全选放 EditMode；PlayMode 只保留 2 个集成用例 |

本轮不做：

- 道具抽取、道具摆放；
- 子弹弹道预览、历史轨迹；
- 复杂抽卡动画、音效；
- 联机/网络同步；
- 存档中的选弹记录。

---

## 2. 现状与接入点

| 位置 | 现状 | 对本功能的影响 |
|---|---|---|
| `BattleController.BeginTurn` | 直接 `turnController.BeginTurn(actor)` | 需要在这之前插入选弹门 |
| `TurnController.BeginTurn` | 立即设置 `CanAct` 并广播 `TurnStarted` | 选弹完成前不能调用它，否则玩家可以开始瞄准 |
| `BattleInputController` | 按当前回合角色转发输入 | 选弹页打开时置 `InputEnabled=false` |
| `CharacterLauncher` | 固定 `projectilePrefab` | 新增 `SetProjectilePrefab`，选弹后覆盖 |
| `UIRoot` / `UIPageRouter` | 已有 Canvas 五层与 Popup 栈 | 选弹页用 Popup 层 + 模态遮罩 |
| `UIPageCatalog_Default` | 目前只有 `Placeholder` | 需要加入 `AmmoSelect` 页面 |
| `Battle Scene` | 目前没有 `UIRoot` | 场景接线时需要补 `UIRoot` |
| `AmmoDefinition` / `AmmoCatalog` | 不存在 | 新增数据资产 |
| 现有子弹 prefab | `Simple Bullet.prefab`、`TestBall.prefab` | 可直接作为前两个默认数据项 |

---

## 3. 总体架构

```text
BattleController
 └─ ITurnStartGate（Battle 接口）
      └─ AmmoSelectionController（Flow 层）
           ├─ AmmoDraw（Game，纯 C#）
           ├─ AmmoCatalog / AmmoDefinition（Game）
           ├─ IAmmoSelectPresenter（UI 接口）
           │    └─ UIAmmoSelectPresenter → UIRoot.Router → AmmoSelectPage
           ├─ TurnController → CharacterLauncher.SetProjectilePrefab
           └─ BattleInputController（选弹期间关闭输入）
```

分层约束：

- `AmmoDefinition` / `AmmoCatalog` / `AmmoDraw` 放 `NineDays.Game`；
- `ITurnStartGate` 放 `NineDays.Battle`，`BattleController` 只认识接口；
- `AmmoSelectionController` 放新建 `NineDays.Flow`，依赖 `Battle + Game + UI + Input`；
- `AmmoSelectPage` / `AmmoSelectArgs` / `UIAmmoSelectPresenter` 放 `NineDays.UI`，不引用 Game/Battle；
- `NineDays.Battle` **不新增 UI 依赖**，保持“Battle → Game/Input”的现有方向；
- `Flow` 是高层的流程编排层，与 `Camera` / `Debug` 同级，只做流程，不写玩法数值。

---

## 4. 数据资产

### 4.1 `AmmoDefinition`（SO）

路径：`Assets/Scripts/Config/AmmoDefinition.cs`  
菜单：`Create > 9Days > 配置 > 弹药定义`

| 字段 | 类型 | 默认 | 含义 |
|---|---|---|---|
| `prefab` | `Projectile` | null | 子弹 prefab，直接决定外观与 `ProjectileConfig` |
| `displayName` | string | 空 | 选项名称；为空时回退 `prefab.name` |
| `description` | string | 空 | 选项介绍 |
| `icon` | Sprite | null | 选项图标；为空时 UI 显示占位 |

`Validate()`：

- `prefab` 必须非空；
- `displayName` 为空时用 prefab 名称；
- 不改 prefab、不改 config。

> 数值仍由 prefab 上的 `ProjectileConfig` 决定；本轮不做“同 prefab 不同数值”的额外配置，避免扩大 `ShotSpawner` 和测试面。

### 4.2 `AmmoCatalog`（SO）

路径：`Assets/Scripts/Config/AmmoCatalog.cs`  
菜单：`Create > 9Days > 配置 > 弹药目录`

| 成员 | 入参 | 返回 | 说明 |
|---|---|---|---|
| `ammo` | — | `List<AmmoDefinition>` | Inspector 配置的全部子弹 |
| `Count` | — | int | 有效数量 |
| `Get(int index)` | 下标 | `AmmoDefinition` | 越界返回 null |
| `Validate()` | — | — | 去空、去无效 prefab |

默认资产：

```text
Assets/Data/Ammo/AmmoDefinition_Simple.asset
Assets/Data/Ammo/AmmoDefinition_TestBall.asset
Assets/Data/Ammo/AmmoCatalog_Default.asset
```

默认绑定：

| 定义 | prefab | 图标 |
|---|---|---|
| `AmmoDefinition_Simple` | `Simple Bullet.prefab` | `normal ammo.png` |
| `AmmoDefinition_TestBall` | `TestBall.prefab` | `guiding bolt.png` |

> 以后新增子弹只需要：建 prefab → 建 `AmmoDefinition` → 加入 `AmmoCatalog`，不改代码。

---

## 5. 随机抽选逻辑

文件：`Assets/Scripts/Bullet/AmmoDraw.cs`（纯 C#，Game 层）

```csharp
public static IReadOnlyList<AmmoDefinition> Pick(
    IReadOnlyList<AmmoDefinition> pool, int count, bool all, Random rng)
```

| 入参 | 含义 |
|---|---|
| `pool` | 目录里的有效定义 |
| `count` | Inspector 的 `drawCount` |
| `all` | 测试全选开关 |
| `rng` | 随机源；测试传固定种子 |

规则：

- `all == true`：按目录顺序返回全部有效项；
- `count < 1`：按 1 处理；
- `count >= pool.Count`：返回全部有效项（不重复）；
- `count < pool.Count`：部分 Fisher–Yates，返回 `count` 个**不重复**项；
- `pool` 为空或全无效：返回空列表；
- `rng` 为空时用 `new Random()` 兜底；
- 不修改传入列表、不使用 `UnityEngine.Random` 全局状态。

`AmmoSelectionController` 持有一个 `System.Random`：

- `randomSeed == 0`：每次抽选使用新的随机源；
- `randomSeed != 0`：`Awake` 时用固定种子创建一次并持续使用，跨回合序列可复现；
- `Cancel` / `RestartBattle` 不重置随机源；需要从同一序列重新开始时，重开场景或重新进入 Play。

---

## 6. 运行时流程

### 6.1 `ITurnStartGate`

文件：`Assets/Scripts/Battle/ITurnStartGate.cs`

```csharp
public interface ITurnStartGate
{
    void BeginTurn(Team actor, Action onReady);
    void Cancel();
}
```

`BattleController` 新增：

```csharp
[SerializeField] private MonoBehaviour turnStartGateSource;
private ITurnStartGate turnStartGate;
```

- `Awake` 时把 `turnStartGateSource as ITurnStartGate` 缓存；
- 没有配置时可以通过 `FindObjectsByType<MonoBehaviour>` 找到实现者（可选）；
- 提供 `SetTurnStartGate(ITurnStartGate)`，供测试/Flow 注入；
- `BeginTurn`：
  ```csharp
  pendingCallback = onResolved;
  if (turnStartGate != null)
      turnStartGate.BeginTurn(actor, () => turnController.BeginTurn(actor));
  else
      turnController.BeginTurn(actor);
  ```
- `BeginBattle` / `RestartBattle` / `Abort` 调 `turnStartGate?.Cancel()`。

这样旧场景没有 gate 时行为完全不变。

### 6.2 `AmmoSelectionController`

文件：`Assets/Scripts/Flow/AmmoSelectionController.cs`  
实现：`ITurnStartGate`

| 字段 | 默认 | 含义 |
|---|---|---|
| `catalog` | null | 子弹目录 |
| `drawCount` | 3 | 每次随机展示数量，`Min(1)` |
| `showAllBulletsForTesting` | false | 测试全选 |
| `randomSeed` | 0 | 0=随机；非 0=可复现 |
| `presenter` | null | `UIAmmoSelectPresenter` |
| `turnController` | null | 找角色用 |
| `inputController` | null | 选弹期间禁用战斗输入 |
| `Configure(...)` | — | 测试注入：catalog / presenter / turn / input / 数量 / 全选 / 种子 |

流程：

```text
BattleController.BeginTurn(actor, onResolved)
  → gate.BeginTurn(actor, onReady)
      → AmmoDraw.Pick(...)
      → 无候选：onReady()，沿用 launcher 默认 prefab
      → 有候选：
          inputController.InputEnabled = false
          presenter.Show(options, OnOptionSelected)
              → AmmoSelectPage 弹出
          OnOptionSelected(index)
              → 写 CharacterLauncher.SetProjectilePrefab(picked[index].prefab)
              → presenter.Hide()
              → inputController.InputEnabled = 原值
              → onReady()
  → turnController.BeginTurn(actor)   // 这时角色才 CanAct
```

细节：

- `BeginTurn` 一定会回调 `onReady`，除非 `Cancel()`；
- `Cancel()` 关闭页面、恢复输入、丢弃回调；`OnDisable` 也调用 `Cancel()`；
- 页面/UI 不可用：直接选抽到的第一个并继续，保证无 UI 场景和测试不被卡住；
- 目录为空：不弹页面、**不改变输入状态**，调用 `ResetProjectilePrefab()` 后直接 `onReady()`；
- 选中的定义 prefab 为空：调用 `ResetProjectilePrefab()`，沿用默认；
- 不在 `TurnController` 内部弹 UI，保持回合逻辑与表现分离。

### 6.3 `CharacterLauncher`

新增：

```csharp
public void SetProjectilePrefab(Projectile prefab);
public void ResetProjectilePrefab();
public Projectile ActiveProjectilePrefab { get; }
```

`Fire()` 使用：

```csharp
Projectile prefab = selectedPrefab != null ? selectedPrefab : projectilePrefab;
```

`ShotSpawner` 不需要改：子弹数值继续来自 prefab 上的 `ProjectileConfig`。

---

## 7. UI 页面

### 7.1 纯 UI 数据

`Assets/Scripts/UI/AmmoSelect/AmmoSelectArgs.cs`

```csharp
public sealed class AmmoOption
{
    public string Name { get; }
    public string Description { get; }
    public Sprite Icon { get; }
}

public sealed class AmmoSelectArgs : UIPageArgs
{
    public IReadOnlyList<AmmoOption> Options { get; }
    public Action<int> OnSelected { get; }
}
```

UI 层只认识字符串、Sprite、索引和回调，不认识 `AmmoDefinition` / `Projectile`。

### 7.2 `AmmoOptionView`

文件：`Scripts/UI/AmmoSelect/AmmoOptionView.cs`

- 字段：`Image icon`、`Text nameText`、`Text descriptionText`、`Button button`；
- `Bind(AmmoOption option, Action onClick)`：先确保引用，再设置图标/名称/介绍，最后绑定按钮；
- `Awake` / `Bind` 都可以从自身/子节点补齐引用（模板是 inactive，不能只依赖 Awake）；
- 模板在页面 prefab 里保持 inactive。

### 7.3 `AmmoSelectPage`

文件：`Scripts/UI/AmmoSelect/AmmoSelectPage.cs`  
基类：`UIPage`

| 字段 | 作用 |
|---|---|
| `titleText` | “选择子弹”标题 |
| `optionRoot` | 选项容器，挂 `GridLayoutGroup` |
| `optionTemplate` | `AmmoOptionView` 模板，inactive；放在 `optionRoot` 外，避免清空时被销毁 |

`OnShow(UIPageArgs args)`：

1. 转 `AmmoSelectArgs`；
2. 清空 `optionRoot` 下旧按钮；
3. 按 `Options.Count` 循环 `Instantiate(optionTemplate, optionRoot)`；
4. 激活按钮，调用 `Bind`，点击回调 `args.OnSelected(index)`；
5. 不自己关闭页面，由 `UIAmmoSelectPresenter` 统一关闭。

`OnHide` / `OnDispose`：清空动态按钮，移除监听，避免 KeepAlive 页面残留。

### 7.4 `UIAmmoSelectPresenter`

文件：`Scripts/UI/AmmoSelect/UIAmmoSelectPresenter.cs`  
实现：`IAmmoSelectPresenter`

```csharp
public interface IAmmoSelectPresenter
{
    bool Show(IReadOnlyList<AmmoOption> options, Action<int> onSelected);
    void Hide();
}
```

- `pageId`：Inspector 可调，默认 `AmmoSelect`；
- `Show`：检查 `UIRoot.Instance`、`Router`、`Catalog` 是否有 `pageId`；
- 用 `Router.PushPopup(pageId, new AmmoSelectArgs(...))` 打开；
- `Hide`：关闭保存的 `UIPageHandle`；
- 页面缺失/关闭失败返回 `false`，由 `AmmoSelectionController` 回退。

### 7.5 页面配置

```text
pageId:        AmmoSelect
prefab:        Assets/Prefabs/UI/Pages/AmmoSelectPage.prefab
layer:         Popup
cachePolicy:   KeepAlive
closeOnBack:   false
modal:         true
transition:    空（可后续加淡入）
```

---

## 8. 场景接线与资产生成

### 8.1 `UIFactory`

扩展 `Scripts/Editor/UIFactory.cs`：

1. 生成 `AmmoSelectPage.prefab`：
   - 根：`RectTransform + CanvasGroup + AmmoSelectPage`；
   - 标题、提示、`GridLayoutGroup` 容器、inactive 选项模板；
2. 生成 `Assets/Data/UI/Pages/AmmoSelectPage.asset`；
3. 把 `AmmoSelect` 配置加入 `UIPageCatalog_Default`（保留 Placeholder）；
4. `UIRoot.prefab` 继续引用默认 catalog。

### 8.2 `AmmoFactory`

新增 `Scripts/Editor/AmmoFactory.cs`：

- 确保 `Assets/Data/Ammo/`；
- 创建/补全 `AmmoDefinition_Simple`、`AmmoDefinition_TestBall`；
- 创建 `AmmoCatalog_Default`；
- 只补空值，不覆盖用户改过的名称/介绍/图标。

`BuildPipeline` 的 `Build/Prefabs` / `Build/All` 需要依次调用 `AmmoFactory.EnsureDefaults()` 和 `UIFactory.BuildAll()`，保证资产可复现。

### 8.3 `BattleSceneSetup`

扩展现有 `Scripts/Editor/BattleSceneSetup.cs`：

1. 先调用 `AmmoFactory.EnsureDefaults()` + `UIFactory.BuildAll()`，保证 Ammo 数据与页面配置存在；
2. 确保 `Battle Scene` 有 `UIRoot`（没有就实例化 `UIRoot.prefab`）；
3. 创建 `AmmoSelection` 对象，挂：
   - `AmmoSelectionController`（Flow）
   - `UIAmmoSelectPresenter`（UI）
4. 赋值：
   - `catalog = AmmoCatalog_Default`
   - `drawCount = 3`
   - `turnController` / `battleInput`
   - `BattleController.turnStartGateSource = AmmoSelectionController`
5. 保存场景。

`ArenaBuilder` 可选同步给 `TestArena` 加同样的组件；不强制，避免影响程序化地形测试。

---

## 9. Inspector 参数与测试开关

`AmmoSelectionController`：

| 参数 | 默认 | 作用 |
|---|---|---|
| `Catalog` | `AmmoCatalog_Default` | 全部可选子弹 |
| `Draw Count` | 3 | 每次随机展示数量；大于目录数量时自动显示全部 |
| `Show All Bullets For Testing` | false | 测试时展示全部，忽略数量 |
| `Random Seed` | 0 | 0=每次随机；非 0=可复现 |
| `Presenter` | `UIAmmoSelectPresenter` | UI 桥接 |
| `Turn Controller` | 自动查找 | 找行动角色 |
| `Input Controller` | 自动查找 | 选弹期间关闭输入 |

`AmmoSelectPage` 的按钮数量完全由传入 `Options.Count` 决定，不需要在 Inspector 固定数量。

---

## 10. 测试策略与时长控制

### 10.1 EditMode（快，覆盖核心逻辑）

| 用例 | 断言 |
|---|---|
| `Pick_ReturnsRequestedCount` | 5 选 3 → 3 个，且不重复 |
| `Pick_CountGreaterThanPool` | 2 选 3 → 2 个 |
| `Pick_AllReturnsCatalogOrder` | `all=true` → 全部，保持目录顺序 |
| `Pick_Seeded_IsDeterministic` | 同种子两次结果一致 |
| `Pick_EmptyPool` | 返回空，不抛异常 |
| `Pick_ClampsZeroCount` | 0 → 至少 1 |
| `AmmoCatalog_Validate` | 去空/无效 prefab，`Count` 正确 |
| `AmmoCatalog_GetOutOfRange` | 越界返回 null |
| `AmmoDefinition_DisplayNameFallback` | 空名称回退 prefab 名 |

这些是纯 C# / SO 测试，执行时间可忽略。

### 10.2 PlayMode（3 个，含 1 个场景冒烟）

| 用例 | 覆盖 |
|---|---|
| `AmmoSelectionFlowTests.Selection_AppliesPrefab_AndStartsTurn` | 用 FakePresenter：选项数量、选中后 launcher prefab、输入恢复、`onReady` 只调用一次 |
| `AmmoSelectPageTests.BuildsButtons_AndInvokesCallback` | 直接实例化页面对象，传入 3 个选项，断言生成 3 个按钮，点击回调索引正确 |
| `BattleSceneSmokeTests.BattleScene_OpensAmmoSelectPage` | 加载 `Battle Scene`，断言首回合出现可见选弹页，验证场景接线 |

不做的测试：

- 不为 1/2/3/6/N 个选项分别做 PlayMode；数量逻辑由 EditMode 覆盖；
- 不做完整 `Battle Scene` 加载/UI 点击测试；用 FakePresenter 隔开；
- 不做随机分布/概率测试；只测去重和可复现。

预计新增 PlayMode 用例 2 个；实际保留 3 个（1 个场景冒烟），整个 PlayMode 套件约 2.3 秒。  
实际结果：EditMode **90/90**、PlayMode **44/44**（新增 EditMode 9 个、PlayMode 3 个）。

### 10.3 手动验收

1. `Battle Scene` Play：每回合先出现选弹页；
2. Inspector 改 `Draw Count`，Play，选项数量变化；
3. 打开 `Show All Bullets For Testing`，选项变为目录全部；
4. 点选后页面关闭，鼠标拖拽发射的是所选 prefab；
5. 下一回合重新随机；
6. 页面打开时无法拖拽角色；
7. 按 `R` 重开/战斗结束不会残留选弹页。

---

## 11. 代码质量与兼容性

| 点 | 方案 |
|---|---|
| UI 与玩法解耦 | UI 页面只收 `AmmoOption`（字符串/Sprite/索引），不引用 Game/Battle |
| Battle 不反向依赖 UI | `BattleController` 只依赖 `ITurnStartGate`；Flow 实现 gate 并引用 UI |
| 纯逻辑可测 | `AmmoDraw` 不依赖 Unity 对象，随机源注入 |
| 无 UI 可运行 | Presenter 缺失/页面缺失/空目录都回退，不阻断战斗 |
| 输入安全 | 选弹期间 `BattleInputController.InputEnabled=false`；选弹完成才 `BeginTurn` |
| 页面复用 | 一个 `AmmoSelectPage` 模板按数量生成按钮；不按数量建多套 prefab |
| 取消 | `BeginBattle/Restart/Abort` 调 gate.Cancel，关闭页面并恢复输入 |
| 旧场景 | 没有 gate / 没有 catalog 时行为与当前一致 |
| 数值来源 | 仍由 prefab 上的 `ProjectileConfig` 决定，不复制数值 |
| 测试时长 | 随机/全选/空池全部 EditMode；PlayMode 只测一个流程 + 一个页面 |

---

## 12. 实现选择（按默认）

| # | 选择 | 落地 |
|---|---|---|
| 1 | 选弹时机 | **每次 `BeginTurn`**（含额外行动） |
| 2 | 页面时机 | **先选弹，再 `TurnController.BeginTurn`** |
| 3 | 数量大于目录 | 显示全部，不重复 |
| 4 | 全选顺序 | **保持目录顺序** |
| 5 | `randomSeed` | 0=每次随机；非 0=固定序列 |
| 6 | 选弹取消 | 不能；`closeOnBack=false` |
| 7 | 页面缓存 | **KeepAlive**，每次 `OnShow` 重建按钮 |

---

## 13. 文件索引

| 关注点 | 位置 |
|---|---|
| 弹药数据 | `Scripts/Config/AmmoDefinition.cs`（新增） |
| 弹药目录 | `Scripts/Config/AmmoCatalog.cs`（新增） |
| 纯随机抽选 | `Scripts/Bullet/AmmoDraw.cs`（新增） |
| 回合门接口 | `Scripts/Battle/ITurnStartGate.cs`（新增） |
| 战斗接入 | `Scripts/Battle/BattleController.cs`（修改） |
| 子弹发射覆盖 | `Scripts/Character/CharacterLauncher.cs`（修改） |
| 流程编排 | `Scripts/Flow/AmmoSelectionController.cs`（新增，Flow asmdef） |
| UI 参数/选项 | `Scripts/UI/AmmoSelect/AmmoSelectArgs.cs`（新增） |
| UI 桥接接口 | `Scripts/UI/AmmoSelect/IAmmoSelectPresenter.cs`（新增） |
| UI 选项视图 | `Scripts/UI/AmmoSelect/AmmoOptionView.cs`（新增） |
| UI 页面 | `Scripts/UI/AmmoSelect/AmmoSelectPage.cs`（新增） |
| UI 桥接 | `Scripts/UI/AmmoSelect/UIAmmoSelectPresenter.cs`（新增） |
| UI 生成 | `Scripts/Editor/UIFactory.cs`（修改） |
| 弹药资产生成 | `Scripts/Editor/AmmoFactory.cs`（新增） |
| 生成入口 | `Scripts/Editor/BuildPipeline.cs`（修改） |
| 场景接线 | `Scripts/Editor/BattleSceneSetup.cs`（修改） |
| 程序集 | `Scripts/Flow/NineDays.Flow.asmdef`（新增） |
| 测试 | `Assets/Tests/EditMode/AmmoDrawTests.cs`、`Assets/Tests/EditMode/AmmoCatalogTests.cs`、`Assets/Tests/PlayMode/AmmoSelectionFlowTests.cs`、`Assets/Tests/PlayMode/AmmoSelectPageTests.cs`、`Assets/Tests/PlayMode/BattleSceneSmokeTests.cs`（新增） |

---

## 14. 已落地状态

已生成/接线：

- `Assets/Data/Ammo/AmmoDefinition_Simple.asset`、`AmmoDefinition_TestBall.asset`、`AmmoCatalog_Default.asset`；
- `Assets/Data/UI/Pages/AmmoSelectPage.asset`，`pageId=AmmoSelect`，Popup / modal / closeOnBack=false；
- `Assets/Prefabs/UI/Pages/AmmoSelectPage.prefab`，含 `GridLayoutGroup` 与 inactive 模板；
- `UIPageCatalog_Default` 已包含 `Placeholder` + `AmmoSelect`；
- `Battle Scene` 已加 `UIRoot`、`AmmoSelection`（`AmmoSelectionController` + `UIAmmoSelectPresenter`）；
- `BattleController.turnStartGateSource` 已指向 `AmmoSelectionController`。

Inspector 用法：

1. 选中 `AmmoSelection`；
2. `Draw Count` 改随机数量；
3. 勾 `Show All Bullets For Testing` 测试全部；
4. `Random Seed` 可选固定序列；
5. 进入 Play，每回合先出现选弹页。

验证结果：

| 项 | 结果 |
|---|---|
| 默认资产/页面/场景接线 | `BATTLE_SETUP_OK` |
| EditMode | **90/90 Passed** |
| PlayMode | **44/44 Passed** |
| 新增用例 | EditMode 9、PlayMode 3 |

已知限制：

- 默认目录只有 2 种子弹，后续通过 `AmmoDefinition` + `AmmoCatalog` 扩展；
- `TestArena` 未接入选弹门，测试请用 `Battle Scene`，或后续同步 `ArenaBuilder`；
- 页面视觉沿用 legacy `Text` 和占位图，后续可替换 TMP/美术；
- 本轮未实现道具抽取、弹道预览、选弹存档。

