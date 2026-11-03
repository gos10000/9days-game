# UI 框架（Agent 记录）

## 本次改了什么

为项目新增可扩展的 uGUI 页面框架，P0 只做框架与占位页，不实现正式游戏页面。

新增：

- `Assets/Scripts/UI/Framework/UIPageTypes.cs`
- `Assets/Scripts/UI/Framework/UIPageArgs.cs`
- `Assets/Scripts/UI/Framework/UIPageConfig.cs`
- `Assets/Scripts/UI/Framework/UIPageCatalog.cs`
- `Assets/Scripts/UI/Framework/UIPage.cs`
- `Assets/Scripts/UI/Framework/UIPageHandle.cs`
- `Assets/Scripts/UI/Framework/UIPageRouter.cs`
- `Assets/Scripts/UI/Framework/UIPageTransition.cs`
- `Assets/Scripts/UI/Framework/UIFadeTransition.cs`
- `Assets/Scripts/UI/Framework/UIRoot.cs`
- `Assets/Scripts/UI/PlaceholderPage.cs`
- `Assets/Scripts/Editor/UIFactory.cs`
- `Assets/Tests/EditMode/UIPageCatalogTests.cs`
- `Assets/Tests/PlayMode/UIPageRouterTests.cs`

修改：

- `Assets/Scripts/NineDays.Game.asmdef`：新增 `UnityEngine.UI` 引用
- `Assets/Scripts/Editor/EditorAssets.cs`：新增 UI prefab / 数据路径常量
- `Assets/Scripts/Editor/PrefabFactory.cs`：构建结束时调用 `UIFactory.BuildAll()`
- `Assets/Scripts/Editor/BuildPipeline.cs`：`Build/Test Arena` 前先生成 UI 资源
- `Assets/Scripts/Editor/ArenaBuilder.cs`：在 `TestArena` 实例化 `UIRoot.prefab`，并把 `startupPageId` 设为 `Placeholder`
- `Assets/Scripts/Editor/BuildCheck.cs`：新增 `ValidateUI()`、`ValidateSceneUI()`
- `Assets/Tests/PlayMode/NineDays.Tests.PlayMode.asmdef`：新增 `UnityEngine.UI` 引用

生成资源：

- `Assets/Prefabs/UI/UIRoot.prefab`
- `Assets/Prefabs/UI/Pages/PlaceholderPage.prefab`
- `Assets/Data/UI/UIPageCatalog_Default.asset`
- `Assets/Data/UI/Pages/PlaceholderPage.asset`

---

## 设计决策

### 为什么用 uGUI

- 工程已有 `com.unity.ugui`，且现有 prefab/场景全部由编辑器脚本生成
- `CanvasGroup`、`Button`、`CanvasScaler` 与当前 prefab 工作流一致
- 后续正式页面可以换成 TMP，不影响 Router/Page 生命周期

### 栈模型

- `screenStack`：主页面栈；`ShowScreen` 压栈并隐藏旧页
- `popupStack`：弹窗栈；`PushPopup` 不隐藏主页面
- `Back()`：弹窗优先，其次主页面
- `ReplaceScreen`：替换当前页且不保留历史
- `Close(page)`：显式关闭；主页面关闭时恢复上一层

### 缓存

- 每个 pageId 最多一个实例，`pageCache` 以 id 为键
- `KeepAlive`：隐藏 `SetActive(false)`，保留实例和参数
- `DestroyOnHide`：隐藏后 `Destroy` 并移除缓存
- 同 id 重复 `ShowScreen` 视为刷新，不重复入栈

### 过渡

- `UIPageTransition` 是 ScriptableObject，`Enter/Exit` 返回 `IEnumerator`
- `UIFadeTransition` 使用 `CanvasGroup.alpha` 与 unscaled time
- 过渡期间 `IsTransitioning = true`，`ShowScreen/PushPopup/ReplaceScreen` 被拒绝
- `ShowScreen` 先立即隐藏旧页，再淡入新页；`Close/Back` 走 Exit 后再隐藏/销毁

### 输入

- `UIRoot` 在 `EventSystem.current == null` 时创建 `EventSystem + InputSystemUIInputModule`
- `InputSystemUIInputModule.AssignDefaultActions()` 确保运行时可用
- `UIRoot.Update` 只在 `Router.CanGoBack` 时消费 Esc，避免抢角色的 Esc

### 弹窗遮罩

- `UIPageConfig.modal = true` 时，`PushPopup` 创建一个全屏 `Image` + `CanvasGroup` 的 `ModalBlocker`
- 遮罩放在 Popup 层、页面之前，阻止下层点击
- 关闭弹窗时同步销毁

---

## 关键实现细节

### UIRoot

`Awake` 中：

1. 单例检查
2. 可选 `DontDestroyOnLoad`
3. `BuildCanvas()`：Canvas / CanvasScaler / GraphicRaycaster
4. `EnsureLayer()`：五个全屏 RectTransform 层
5. `EnsureEventSystem()`
6. `Router = GetComponent<UIPageRouter>()`，没有则 `AddComponent`
7. `Router.Configure(catalog, layers)`

`Configure(catalog, startupPage)` 供场景/测试重新注入注册表；`Start` 仅在 `startupPageId` 非空时自动打开页面。

### UIPageRouter

- `GetOrCreate(config)`：`Instantiate(prefab, layer)` → 取 `UIPage` → `Bind` → `OnCreate`
- `Bind` 强制页面根节点全屏拉伸、设置 CanvasGroup 初始状态
- `BeginShow`：Alpha 0、禁用交互、`Show(args)`、可选过渡
- `HideAndCleanup`：`Hide()`；按缓存策略销毁
- `ShowExisting`：恢复上一层主页面并重新 `OnShow`

### UIPage

- `Bind/Show/Hide/Dispose/Create` 是 internal，只有 Router 调用
- `OnShow` 参数是 `UIPageArgs` 基类，页面自行转具体类型
- `Close()` 委托给 Router，避免页面直接操作栈

### BuildCheck

新增校验：

- catalog 存在且数量 > 0
- `catalog.Validate()` 检查重复 id 与配置
- 每个 prefab 有 `UIPage`、`CanvasGroup`
- `UIRoot.prefab` 有 catalog 引用
- `TestArena` 有 `UIRoot` 且 catalog 非空

---

## 验证

| 项 | 结果 |
|---|---|
| `BuildAllBatch` | `BUILD_OK`，`[BuildCheck] 结构校验全部通过` |
| EditMode | **59/59 Passed** |
| PlayMode | **30/30 Passed** |

新增测试覆盖：

- EditMode：注册表查找、重复 id、缺 `UIPage`、缺 `CanvasGroup`、空 id
- PlayMode：主页面切换/返回、弹窗优先关闭、KeepAlive 复用、五层与 EventSystem、淡入完成

运行方式（Unity 6 batchmode）：

```text
Unity.exe -batchmode -projectPath <项目> -executeMethod NineDays.EditorTools.BuildPipeline.BuildAllBatch -logFile <日志>
Unity.exe -batchmode -projectPath <项目> -runTests -testPlatform EditMode -testResults <xml> -logFile <日志>
Unity.exe -batchmode -projectPath <项目> -runTests -testPlatform PlayMode -testResults <xml> -logFile <日志>
```

---

## 已知限制 / 待办

1. 正式页面未实现：MainMenu、BattleHUD、Pause、Result、Settings 仍是 TODO
2. `TurnBanner` 仍是 OnGUI；后续应迁移为 `BattleHUD` 页面
3. 页面资源同步 `Instantiate`；未接 Addressables/异步加载
4. 占位页使用 legacy `Text`；正式 UI 建议 TMP
5. `UIPageConfig.closeOnBack` 已参与 `Back()` 判断，但 `CloseAll` / 页面内显式 `Close()` 不受它限制
6. 同一 pageId 只有一个实例；同页多实例需要改 `pageCache` 键
7. `UIRoot.Update` 的 Esc 与角色输入目前是“UI 可返回时优先”，复杂输入上下文后续细化
8. `UIRoot` 销毁时不会逐个调用页面 `OnDispose`（页面随 GameObject 销毁）；需要释放非 Unity 资源时应显式 `Router.CloseAll()`
9. `UIRoot` 的 Canvas 在运行时创建；编辑器场景里看不到 Canvas 预览，这是刻意的代码生成方式
