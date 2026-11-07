# 开始界面 Agent 记录

## 1. 任务背景

开始页（`Assets/Scenes/Playmode Scene.unity`）做到一半，存在四个问题：

1. `BattleController` 在 `Awake` 里自动创建 `TurnBanner`，开始页不需要横幅，但角色投球又依赖战斗脚本；
2. `ExitBoxTrigger` 只调用 `Application.Quit()`，编辑器内无效；
3. `StartBoxTrigger` 直接 `SceneManager.LoadScene(targetScene)`，`targetScene` 还是空的 `MainScene`，需要改成由开始页统一更新状态并切场景；
4. `StartScene.cs` 是空模板。

约束：

- `Assets/Scripts/UI` 属于 `NineDays.UI` 程序集，只引用 `NineDays.Input`、`UnityEngine.UI`，**不能引用** `NineDays.Game`（Character/Projectile）或 `NineDays.Battle`（TurnController）；
- 不能新增程序集循环；
- 不改 `Docs/agent` 以外的文档。

## 2. 本次改动

### 2.1 `Assets/Scripts/Battle/BattleController.cs`

新增字段：

```csharp
[Header("显示")]
[SerializeField] private bool showTurnBanner = true;
```

`EnsureTurnBanner()` 改为：

```csharp
if (!showTurnBanner) return;
if (FindAnyObjectByType<TurnBanner>(FindObjectsInactive.Include) != null) return;
gameObject.AddComponent<TurnBanner>();
```

- 默认 `true`，`TestArena` / `Test_BattleScene` 行为不变；
- `FindObjectsInactive.Include` 避免把已存在但未激活的横幅重复创建；
- 开始页在场景里把 `BattleController` GameObject 设为未激活，并且序列化 `showTurnBanner: 0`，双保险。

### 2.2 `Assets/Scripts/test/TurnBanner.cs`

新增运行时属性：

```csharp
public bool Visible { get => showBanner; set { showBanner = value; if (!value) alpha = 0f; } }
```

`Show()` 开头增加 `if (!showBanner) return;`。

### 2.3 `Assets/Scripts/Battle/TurnController.cs`

新增字段：

```csharp
[Tooltip("找不到对手角色时（如开始页只有一名角色），结算后仍由当前队继续行动")]
[SerializeField] private bool repeatWhenOpponentMissing;
```

`EndTurn()` 的推进逻辑：

```csharp
if (autoAdvance)
{
    Team next = OpponentOf(finished);
    if (repeatWhenOpponentMissing && GetCharacter(next) == null) next = finished;
    BeginTurn(next);
}
```

- 默认 `false`，现有战斗场景不受影响；
- 开始页只有 `Character_P1`，开启后投偏也能继续投。

### 2.4 `Assets/Scripts/UI/Start scene/StartScene.cs`

从空模板改为页面控制器，关键点：

| 成员 | 实现 |
|---|---|
| `Awake` | 设置 `Instance`，`pageGroup` 为空时 `GetComponent<CanvasGroup>()`，进入 `Idle` |
| `RequestStart` | 幂等；先 `TrySwitchPage()`；失败后 `UpdatePage(Starting)`，按配置回退 `StartRoutine()` |
| `TrySwitchPage` | 校验 `UIRoot.Instance`、catalog 注册、`UIPageConfig.prefab`，调用 `ReplaceScreen` / `ShowScreen` |
| `RequestExit` | 幂等；`UpdatePage(Exiting)`；`StartCoroutine(ExitRoutine())` |
| `UpdatePage` | 设置 `CanvasGroup.interactable/blocksRaycasts`、可选 `Text`、触发 `PageUpdated` |
| `StartRoutine` | `FadeOut(true)` → 校验场景名 → `SceneManager.LoadScene` |
| `ExitRoutine` | `FadeOut(false)` → `WaitForSecondsRealtime(quitDelay)` → `QuitApplication()` |
| `FadeOut` | 使用 `Time.unscaledDeltaTime`，避免 `timeScale = 0` 时卡住 |
| `QuitApplication` | `#if UNITY_EDITOR` → `EditorApplication.isPlaying = false`；否则 `Application.Quit()` |

字段默认值：`fadeSeconds = 0.35`、`battleSceneName = "Test_BattleScene"`、`quitDelay = 0.1`、
`nextPageId = "Placeholder"`、`replaceCurrentPage = true`、`loadSceneWhenPageMissing = true`。

页面切换逻辑：

```csharp
private bool TrySwitchPage()
{
    if (string.IsNullOrWhiteSpace(nextPageId)) return false;

    UIRoot root = UIRoot.Instance;
    if (root == null || root.Router == null) return false;
    if (root.Catalog == null || !root.Catalog.TryGet(nextPageId, out UIPageConfig config) || config == null) return false;
    if (config.Prefab == null) return false;

    UIPageHandle handle = replaceCurrentPage
        ? root.Router.ReplaceScreen(nextPageId)
        : root.Router.ShowScreen(nextPageId);

    return handle != null;
}
```

- 页面切换成功时**不设置** `IsLeaving`，关闭目标页面后仍可再次投球触发；
- `nextPageId` 为空或切换失败 → 走原来的淡出 + 加载场景；
- 场景当前 `UIRoot.catalog` 为 `UIPageCatalog_Default`，其中只有 `Placeholder` 可用，所以默认目标是 `Placeholder`。

### 2.5 `Assets/Scripts/UI/Start scene/StartBoxTrigger.cs`

- 命名空间改为 `NineDays`；
- 删除 `targetScene` 与 `SceneManager`；
- 只接受 `Projectile` 层（`LayerMask.NameToLayer("Projectile")`），避免角色碰盒子误触发；
- 一次性 `triggered`；
- 调用 `StartScene.Instance.RequestStart()`，无实例时 `Debug.LogWarning`。

### 2.6 `Assets/Scripts/UI/Start scene/ExitBoxTrigger.cs`

- 命名空间改为 `NineDays`；
- 删除 `using UnityEngine.SceneManagement`；
- 同样的 `Projectile` 层过滤与一次性标记；
- 调用 `StartScene.Instance.RequestExit()`，无实例时 `StartScene.QuitApplication()`。

### 2.7 `Assets/Scenes/Playmode Scene.unity`

| 位置 | 改动 |
|---|---|
| `BattleController` GameObject | `m_IsActive: 1 → 0`（不跑对局、不建横幅） |
| `BattleController` 组件 | 序列化 `showTurnBanner: 0` |
| `TurnController` 组件 | 序列化 `repeatWhenOpponentMissing: 1` |
| `Box_Start` 的 MonoBehaviour | 脚本 GUID 由 `ExitBoxTrigger` 改为 `StartBoxTrigger` |
| `Box_Exit` 的 MonoBehaviour | 脚本 GUID 由 `StartBoxTrigger` 改为 `ExitBoxTrigger`，删除旧的 `targetScene` 字段 |

`BattleInputController`（用户本次未提交的 WIP）保持激活，`turnController: {fileID: 0}` 由 `Awake` 自动查找。

## 3. 关键决策

1. **开始页不复用 `BattleController`**：它需要 `BattleConfig`、两名参战者和对局流程，还会自动加横幅；投球只需要 `TurnController + BattleInputController + CharacterLauncher`。
2. **单人回合放在 `TurnController`**：`repeatWhenOpponentMissing` 是通用能力，避免为了开始页在 UI 程序集里反向依赖 Battle。
3. **触发器放 UI 程序集**：用 `LayerMask.NameToLayer("Projectile")` 代替 `GetComponent<Projectile>()`，避免 `NineDays.UI → NineDays.Game` 的新依赖；代价是只按层判断，未来若其它物体也在 `Projectile` 层会误触发。
4. **页面淡出只做 `CanvasGroup`**：开始页的角色与盒子是 `SpriteRenderer`，不受 `CanvasGroup` 影响；当前只作为过渡节奏，不追求整屏淡出。
5. **不创建 `StartPage.prefab`**：开始页包含角色、盒子和物理交互，放进 UI 页 Canvas 会破坏世界坐标渲染；`StartPage.asset` 仍未使用。
6. **开始盒走 `UIPageRouter`**：`StartScene.TrySwitchPage()` 用 `ReplaceScreen` / `ShowScreen` 切换页面；开始页本身不是 `UIPage`，路由只负责打开目标页面。目标页面不可用时保留原来的场景加载回退，避免卡死。

## 4. 已知限制 / 待办

- `StartPage.asset` 的 `prefab` 为空，未加入当前使用的 `UIPageCatalog_Default`；如果以后要把开始页做成 UI 页，需要先把角色/盒子拆到场景层，再让页面只保留 UI。
- 默认 `nextPageId = "Placeholder"`，因为当前 catalog 里只有 `Placeholder` 有可用 prefab；正式页面建好后在 Inspector 改 `nextPageId` 即可。
- 触发器只按 `Projectile` 层过滤，没有判断“哪一方的子弹”；开始页只有一名角色，暂无影响。
- 页面淡出不影响世界物体，若需要整屏淡出，需要在 `UIRoot` 上叠一层全屏 `Image` 或用相机后处理。
- 开始页没有自动化测试；`Playmode Scene.unity` 也不在 Build Settings 中，无法用 PlayMode 测试直接加载。
- `Test_StartScene.unity`（Build Settings 里的第一个场景）仍是旧版本，未同步本次改动；正式打包前需要替换或重建。

## 5. 验证

| 项目 | 结果 |
|---|---|
| Unity 批处理编译（`-batchmode -quit`） | 投球/开始页首轮改动：`NineDays.UI.dll`、`NineDays.Battle.dll` 生成成功，无 `error CS` |
| EditMode | 81/81 通过（页面切换改动前） |
| PlayMode | 34/34 通过（页面切换改动前） |
| 页面切换改动 | 按用户要求跳过测试；只做编译检查 |

手动验收清单（需在编辑器内 Play `Playmode Scene.unity`）：

1. 场景启动后不出现回合横幅；
2. 按住左键拖拽、松开发射，子弹能飞出；
3. 子弹投偏后，角色可以再次投球；
4. 子弹进入 `Box_Start` 触发器后，Console 出现 `[StartScene] 已切换页面：Placeholder`，`Placeholder` 页面出现；把 `nextPageId` 清空或改成未注册的 id 时，应回退加载 `Test_BattleScene`；
5. 子弹进入 `Box_Exit` 触发器后，Console 出现 `[StartScene] 页面状态更新：Exiting`，随后停止 Play；
6. 角色走进盒子不会触发开始/退出。

## 6. 接手步骤

1. 先读 `Docs/agent/startscene/StartScene.md`；
2. 改开始页流程时只改 `StartScene`；盒子脚本只负责“命中后调用”；
3. 若要让开始页进入真正的 UI 页面体系，先确认角色/盒子的渲染方案，再动 `StartPage.asset` 与 `UIPageCatalog`；
4. 改完运行 `Unity.exe -batchmode -quit -projectPath <项目>` 检查编译，再跑 EditMode/PlayMode。

## 7. 2026-11-05 预制体脚本识别修复

**现象**：`Assets/Prefabs/UI/Pages/StartPage.prefab` 与 `Playmode Scene` 里的 `StartScene` 组件显示 Missing，预制体无法识别脚本。

**原因**：`StartScene.cs` 被改成 `public sealed class StartPage : UIPage`，并在内部嵌套 `public class StartScene : MonoBehaviour`。

- Unity 要求 MonoBehaviour 的顶层类名与文件名一致；文件名为 `StartScene.cs`，顶层类却是 `StartPage`，GUID `07fa75030dcc2ef4fad99ab9fc9fdb4e` 无法解析到组件；
- 嵌套的 MonoBehaviour 不能作为组件挂载/序列化。

**修复**：

| 文件 | 改动 |
|---|---|
| `Assets/Scripts/UI/Start scene/StartScene.cs` | 恢复顶层 `public sealed class StartScene : MonoBehaviour`；字段、状态机、切页、淡出、退出逻辑全部保留 |
| `Assets/Scripts/UI/Start scene/StartBoxTrigger.cs` | `StartPage.StartScene.Instance` → `StartScene.Instance` |
| `Assets/Scripts/UI/Start scene/ExitBoxTrigger.cs` | 同上；`StartPage.StartScene.QuitApplication()` → `StartScene.QuitApplication()` |

- `.meta` GUID 未变，`StartPage.prefab` / `Playmode Scene` 不需要改；
- 预制体上的序列化字段（`pageGroup`、`battleSceneName`、`nextPageId` 等）与 `StartScene` 字段同名，数据保留；
- `StartPage.prefab` 仍是**场景对象 prefab**，不是 `UIPage`；`StartPage.asset` 的 prefab 仍为空，若以后要页面化需另开任务。

**验证**：

- `dotnet build NineDays.UI.csproj`：0 warning、0 error；
- 未按用户要求跑 EditMode / PlayMode / Play。

**排错**：以后若再出现 Missing，先检查“顶层类名 = 文件名”，再检查脚本是否嵌套在别的类里。
