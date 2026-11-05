# UI 框架（人读版）

> 一句话：**UIRoot 提供 Canvas/层，UIPageRouter 负责页面栈，页面只关心自己的 prefab 和 OnShow 数据。**

## 目录

1. [模块职责](#1-模块职责)
2. [整体结构](#2-整体结构)
3. [页面 prefab 结构](#3-页面-prefab-结构)
4. [UIPageConfig / UIPageCatalog](#4-uipageconfig--uipagecatalog)
5. [UIPageRouter API](#5-uipagerouter-api)
6. [UIPage 生命周期](#6-uipage-生命周期)
7. [过渡动画](#7-过渡动画)
8. [输入与 EventSystem](#8-输入与-eventsystem)
9. [扩展一个新页面](#9-扩展一个新页面)
10. [生成与验证](#10-生成与验证)
11. [已知限制](#11-已知限制)

---

## 1. 模块职责

| 文件 | 职责 |
|---|---|
| `UI/Framework/UIPageTypes.cs` | `UIPageLayer`、`UIPageCachePolicy` |
| `UI/Framework/UIPageArgs.cs` | 页面参数基类 |
| `UI/Framework/UIPageConfig.cs` | 单页面配置资产 |
| `UI/Framework/UIPageCatalog.cs` | 页面注册表与 id 校验 |
| `UI/Framework/UIPage.cs` | 页面基类与生命周期 |
| `UI/Framework/UIPageHandle.cs` | 一次页面显示的句柄 |
| `UI/Framework/UIPageRouter.cs` | Screen 栈、Popup 栈、缓存、过渡 |
| `UI/Framework/UIPageTransition.cs` | 过渡基类 |
| `UI/Framework/UIFadeTransition.cs` | 默认淡入淡出 |
| `UI/Framework/UIRoot.cs` | Canvas 五层、EventSystem、Router 入口 |
| `UI/PlaceholderPage.cs` | 框架占位页 |

`TurnBanner`、`CombatDebugHud` 仍是 `OnGUI` 调试 UI，不属于本框架；正式页面可以逐步替换它们。

---

## 2. 整体结构

```text
UIRoot
 ├─ Canvas (Screen Space - Overlay)
 │   ├─ BackgroundLayer
 │   ├─ ScreenLayer
 │   ├─ PopupLayer
 │   ├─ OverlayLayer
 │   └─ ToastLayer
 ├─ EventSystem
 └─ UIPageRouter
      └─ UIPageCatalog
```

| 层 | 用途 |
|---|---|
| `Background` | 全屏背景 |
| `Screen` | 主页面：主菜单、战斗 HUD、结算 |
| `Popup` | 暂停、确认框、设置 |
| `Overlay` | 顶层提示/调试面板 |
| `Toast` | 短暂提示 |

层容器由 `UIRoot` 运行时创建并撑满 Canvas；页面 prefab 不再自带 Canvas。

---

## 3. 页面 prefab 结构

根节点必须是：

```text
PageRoot (RectTransform)
 ├─ CanvasGroup
 ├─ UIPage 子类
 └─ 内容节点...
```

要求：

- 根节点是 `RectTransform`
- 根节点有 `CanvasGroup`
- 根节点有 `UIPage`（或其子类）
- 不添加 Canvas / GraphicRaycaster；使用 UIRoot 的根 Canvas
- 根节点锚点由 Router 自动设为全屏拉伸

`CanvasGroup` 负责透明度和输入拦截；页面内容可以自由使用 `Image`、`Text`、`Button` 或 TMP。

---

## 4. UIPageConfig / UIPageCatalog

### UIPageConfig

菜单：`Create > 9Days > UI > Page Config`

| 字段 | 含义 |
|---|---|
| `pageId` | 唯一页面 id，Router 使用 |
| `prefab` | 页面 prefab |
| `layer` | 目标层 |
| `cachePolicy` | `KeepAlive` 或 `DestroyOnHide` |
| `closeOnBack` | `false` 时 `Back()` 不会关闭该页面 |
| `modal` | 弹窗是否创建全屏遮罩，阻止下层点击 |
| `transition` | 可选过渡资产；空 = 立即显示/隐藏 |

### UIPageCatalog

菜单：`Create > 9Days > UI > Page Catalog`

- `pages` 是 `UIPageConfig` 列表
- `Rebuild()` 建立 id → config 字典
- `Find(id)` 找不到或 id 重复会抛异常
- `Validate()` 检查每个配置的 prefab 结构

默认资产：

```text
Assets/Data/UI/UIPageCatalog_Default.asset
Assets/Data/UI/Pages/PlaceholderPage.asset
```

---

## 5. UIPageRouter API

通过 `UIRoot.Instance.Router` 访问。

| 方法 | 入参 | 返回/行为 |
|---|---|---|
| `ShowScreen(pageId, args)` | 页面 id、`UIPageArgs` | 打开主页面；当前页隐藏；返回 `UIPageHandle` |
| `PushPopup(pageId, args)` | 页面 id、`UIPageArgs` | 打开弹窗；不隐藏主页面 |
| `ReplaceScreen(pageId, args)` | 页面 id、`UIPageArgs` | 替换当前主页面，不保留历史 |
| `Back()` | 无 | 先关弹窗，再回退主页面；成功返回 `true` |
| `Close(page)` | `UIPage` | 关闭指定页面；主页面关闭后恢复上一层 |
| `CloseAll()` | 无 | 关闭并销毁所有页面、清空缓存 |
| `CurrentScreen` | 无 | 当前主页面 |
| `CurrentPopup` | 无 | 当前弹窗 |
| `CanGoBack` | 无 | 是否有弹窗或上一层主页面 |
| `IsTransitioning` | 无 | 是否正在过渡；此时新的切换会被拒绝 |

`UIPageHandle`：

```csharp
UIPageHandle handle = UIRoot.Instance.Router.ShowScreen("BattleHUD", args);
handle.Page;      // 页面实例
handle.IsOpen;    // 是否仍显示
handle.Close();   // 关闭
```

行为细节：

- `ShowScreen` 会把当前 Screen 压入栈并隐藏，`Back` 恢复
- `PushPopup` 默认创建半透明 `ModalBlocker`，阻止点击下层
- `KeepAlive` 页面隐藏后保留实例；`DestroyOnHide` 页面隐藏后销毁
- 过渡进行中拒绝并发切换，避免栈状态错乱

---

## 6. UIPage 生命周期

| 方法 | 调用时机 | 用途 |
|---|---|---|
| `OnCreate()` | 页面实例第一次创建后一次 | 缓存引用、初始化 |
| `OnShow(UIPageArgs args)` | 每次显示 | 填数据、订阅 `GameEvents` |
| `OnHide()` | 每次隐藏 | 取消订阅、清理临时状态 |
| `OnDispose()` | `CloseAll` / 销毁前 | 释放资源 |

辅助方法：

| 方法 | 含义 |
|---|---|
| `SetAlpha(float)` | 过渡使用；0 透明，1 不透明 |
| `SetInteractable(bool)` | 同时设置 `CanvasGroup.interactable` 与 `blocksRaycasts` |
| `Close()` | 让 Router 关闭自己 |

参数通过 `UIPageArgs` 传入，页面自己定义子类并在 `OnShow` 中转换：

```csharp
public sealed class BattleHudArgs : UIPageArgs
{
    public int Round;
    public Team Actor;
}
```

页面不要依赖全局静态状态；需要的数据从 args 或现有事件/服务获取。

---

## 7. 过渡动画

默认资产类型：`UIFadeTransition`（`Create > 9Days > UI > Transitions > Fade`）。

- 使用 `CanvasGroup.alpha`
- `duration` 与 `AnimationCurve` 在 Inspector 中调
- 页面的 `OnShow` 先执行，再执行淡入
- `Exit` 用于关闭/回退；完成后才隐藏或销毁
- 需要更复杂的动画时继承 `UIPageTransition`，或者直接在页面 prefab 上用 Animator

过渡资产为空时页面立即显示/隐藏。

---

## 8. 输入与 EventSystem

`UIRoot` 初始化时：

- 创建 `Canvas`、`CanvasScaler`、`GraphicRaycaster`
- 若场景没有 `EventSystem`，创建 `EventSystem + InputSystemUIInputModule`
- 使用 `InputSystemUIInputModule.AssignDefaultActions()`

返回键：

- `UIRoot.handleBackInput = true` 时监听 `Keyboard.current.escapeKey`
- 只有 `Router.CanGoBack` 为 true 时才消费
- 没有可返回页面时，Esc 继续留给角色/游戏逻辑
- 角色的 Esc 与 UI 的 Esc 同时存在时，建议由 UIRoot 先处理；后续正式页面可改为独立 Cancel action

---

## 9. 扩展一个新页面

1. 新建页面 prefab：
   ```text
   PageRoot (RectTransform + CanvasGroup + MyPage)
     ├─ Background
     ├─ Title
     └─ Buttons
   ```
2. 写页面脚本：
   ```csharp
   public sealed class MyPage : UIPage
   {
       public override void OnShow(UIPageArgs args)
       {
           // 更新文本/按钮
       }
   }
   ```
3. 创建 `UIPageConfig` 并填写 `pageId`、`prefab`、`layer`、`transition`。
4. 把配置加入 `UIPageCatalog.pages`。
5. 调用：
   ```csharp
   UIRoot.Instance.Router.ShowScreen("MyPage");
   UIRoot.Instance.Router.PushPopup("Confirm");
   UIRoot.Instance.Router.Back();
   ```

不需要修改 Router、UIRoot 或 Canvas。

---

## 10. 生成与验证

`Build/Prefabs` 会调用 `UIFactory` 生成：

```text
Assets/Prefabs/UI/UIRoot.prefab
Assets/Prefabs/UI/Pages/PlaceholderPage.prefab
Assets/Data/UI/UIPageCatalog_Default.asset
Assets/Data/UI/Pages/PlaceholderPage.asset
```

`UIFactory` 只保证占位页存在，不会清空你手动加入 catalog 的其他页面。`Build/Test Arena` 会把 `UIRoot` 放进 `TestArena`，并把 `startupPageId` 设为 `Placeholder`；运行场景即可看到占位页，用 `CLOSE` 按钮关闭。

`BuildCheck` 校验：

- 页面注册表存在且至少一页
- 页面 id 不重复
- 每个页面 prefab 有 `UIPage` + `CanvasGroup` + `RectTransform`
- `UIRoot` prefab 有 catalog 引用
- `TestArena` 有 `UIRoot`

测试：

- EditMode：页面配置/注册表校验
- PlayMode：页面栈、返回、弹窗、缓存、Canvas 层、EventSystem、淡入完成

---

## 11. 已知限制

- 正式页面（MainMenu / BattleHUD / Pause / Result）尚未实现，只提供占位页
- `TurnBanner` 仍是 `OnGUI`，尚未迁移到页面
- 页面资源同步加载；尚未接入 Addressables/异步加载
- 页面内容目前使用 legacy `Text`；正式 UI 可替换为 TMP
- `CanGoBack()` 会遵循 `UIPageConfig.closeOnBack`；`Close(page)` / `CloseAll()` 是显式关闭，不受该字段限制
- 同一 pageId 只保留一个实例；需要同页多实例时需扩展缓存键
- 返回键与角色输入目前是“UI 能返回时优先”，复杂输入上下文后续再细化

