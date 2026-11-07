# 开始界面（人读版）

> 一句话：开始页复用战斗的投球脚本，把球投进 Start / Exit 盒子；命中开始盒后由 `StartScene` 调用 `UIPageRouter` 切换页面，页面不可用时回退到加载战斗场景。

## 目录

1. [模块职责](#1-模块职责)
2. [场景结构](#2-场景结构)
3. [运行流程](#3-运行流程)
4. [脚本接口](#4-脚本接口)
5. [配置步骤](#5-配置步骤)
6. [排错](#6-排错)
7. [已知限制](#7-已知限制)

---

## 1. 模块职责

| 文件 | 职责 |
|---|---|
| `Scripts/UI/Start scene/StartScene.cs` | 开始页控制器：页面状态、UIPageRouter 页面切换、回退加载场景、退出 |
| `Scripts/UI/Start scene/StartBoxTrigger.cs` | 开始盒：子弹进入后调用 `StartScene.RequestStart()` |
| `Scripts/UI/Start scene/ExitBoxTrigger.cs` | 退出盒：子弹进入后调用 `StartScene.RequestExit()` |
| `Scripts/Battle/TurnController.cs` | 提供角色可行动状态；单人时重复同一队回合 |
| `Scripts/Battle/BattleInputController.cs` | 把鼠标拖拽转发给当前角色（原战斗输入层） |
| `Scripts/Character/CharacterLauncher.cs` | 拖拽瞄准与发射，不改动 |
| `Scripts/Battle/BattleController.cs` | 对局驱动；开始页**不启用**，只用到它的横幅开关 |
| `Scripts/test/TurnBanner.cs` | `OnGUI` 回合横幅；可在 Inspector 或运行时关闭 |

## 2. 场景结构

场景：`Assets/Scenes/Playmode Scene.unity`

```text
Main Camera
UIRoot                     // UI 框架根；本页不使用 UIPage
StartPage                  // CanvasGroup + StartScene
├─ Backgroud / Border      // 背景与地面
├─ Character_P1            // Character + CharacterLauncher（投球）
├─ Box_Start               // 触发器 + StartBoxTrigger
├─ Box_Exit                // 触发器 + ExitBoxTrigger
├─ TurnController          // 单人回合，repeatWhenOpponentMissing = true
├─ BattleInputController   // 鼠标拖拽 → CharacterLauncher
└─ BattleController        // 未激活：不跑对局、不加横幅
```

- 两个盒子都必须至少有一个 `Is Trigger = true` 的 `Collider2D`，且脚本挂在同一个 GameObject 上。
- 盒子只响应 `Projectile` 层（Layer 9），角色走进盒子不会触发。

## 3. 运行流程

1. `TurnController.Start()` → `BeginTurn(Player1)` → `Character.CanAct = true`；
2. 玩家按住左键拖拽、松开发射 → `CharacterLauncher.Fire()` 生成子弹；
3. 子弹进入 `Box_Start` 触发器 → `StartScene.RequestStart()`：
   - 优先 `UIPageRouter.ReplaceScreen(nextPageId)`（`replaceCurrentPage = false` 时用 `ShowScreen`）；
   - 页面未注册 / 没有 prefab / 没有 UIRoot 时，回退到 `CanvasGroup` 淡出 + `SceneManager.LoadScene(battleSceneName)`；
   - 若 `loadSceneWhenPageMissing = false`，则只打错误并回到 `Idle`；
4. 子弹进入 `Box_Exit` 触发器 → `StartScene.RequestExit()`：
   - 页面状态 `Idle → Exiting`；
   - 淡出后退出：编辑器内 `EditorApplication.isPlaying = false`，构建内 `Application.Quit()`；
5. 子弹未命中盒子 → 子弹销毁后 `TurnController.EndTurn()`；因为场上没有 Player2，`repeatWhenOpponentMissing` 让 Player1 继续回合，可以再次投球。

## 4. 脚本接口

### 4.1 StartScene

Inspector 参数：

| 参数 | 类型 | 默认 | 作用 |
|---|---|---|---|
| `pageGroup` | `CanvasGroup` | 同节点自动获取 | 页面淡出与交互锁 |
| `statusText` | `UnityEngine.UI.Text` | 空 | 可选状态文本 |
| `idleText` | `string` | `拖动并松开发射` | 待机提示 |
| `startingText` | `string` | `正在进入战斗…` | 进入时提示 |
| `exitingText` | `string` | `正在退出…` | 退出时提示 |
| `fadeSeconds` | `float` | `0.35` | 淡出时长（unscaled） |
| `battleSceneName` | `string` | `Test_BattleScene` | 回退时要加载的场景名，必须在 Build Settings |
| `nextPageId` | `string` | `Placeholder` | 命中开始盒后通过 `UIPageRouter` 打开的页面 id |
| `replaceCurrentPage` | `bool` | `true` | `true = ReplaceScreen`；`false = ShowScreen` |
| `loadSceneWhenPageMissing` | `bool` | `true` | 页面不可用时是否回退到加载场景 |
| `quitDelay` | `float` | `0.1` | 淡出后延迟退出 |

公开成员：

| 成员 | 入参 | 返回 / 说明 |
|---|---|---|
| `static StartScene Instance` | - | 当前页面实例，`Awake` 赋值 |
| `StartPageState State` | - | `Idle` / `Starting` / `Exiting` |
| `bool IsLeaving` | - | 是否已经离开待机态 |
| `event Action<StartPageState> PageUpdated` | - | 页面状态变化回调 |
| `void RequestStart()` | - | 先切换页面；失败时按配置回退到加载场景 |
| `void RequestExit()` | - | 退出游戏；重复调用会被忽略 |
| `static void QuitApplication()` | - | 编辑器停止 Play，构建内退出应用 |

### 4.2 StartBoxTrigger / ExitBoxTrigger

| 项 | 说明 |
|---|---|
| 触发条件 | `OnTriggerEnter2D`，且对方在 `Projectile` 层 |
| 幂等 | 每个盒子只响应一次（`triggered` 标记） |
| 开始 | `StartScene.Instance.RequestStart()`；实例不存在时打警告 |
| 退出 | `StartScene.Instance.RequestExit()`；实例不存在时直接 `QuitApplication()` |

### 4.3 TurnController.repeatWhenOpponentMissing

| 值 | 行为 |
|---|---|
| `false`（默认） | 原战斗逻辑：结算后切换到对手队 |
| `true` | 对手队没有角色时，结算后继续当前队（开始页单人投球） |

### 4.4 横幅开关

| 入口 | 说明 |
|---|---|
| `BattleController.showTurnBanner` | `false` 时不自动创建 `TurnBanner`；默认 `true` 保持旧行为 |
| `TurnBanner.Visible` | 运行时显隐；`false` 会立即清掉透明度 |

## 5. 配置步骤

1. `nextPageId` 必须是 `UIRoot.catalog` 里已注册、且 `UIPageConfig.prefab` 已绑定的页面；默认 `Placeholder`；
2. `battleSceneName` 必须是 Build Settings 里已启用的场景名（当前为 `Test_BattleScene`），作为页面不可用时的回退；
3. `Box_Start` 挂 `StartBoxTrigger`，`Box_Exit` 挂 `ExitBoxTrigger`；
4. 盒子触发器所在层要与子弹层（`Projectile`）在 Physics 2D 碰撞矩阵中相交（当前全开）；
5. 若场景里保留 `BattleController`，把它设为不激活，或把 `showTurnBanner` 取消勾选；
6. `TurnController.autoStart = true`、`autoAdvance = true`、`repeatWhenOpponentMissing = true`。

## 6. 排错

| 现象 | 原因 | 处理 |
|---|---|---|
| 开始页仍出现回合横幅 | 场景里有激活的 `BattleController` | 取消激活，或关闭 `showTurnBanner` |
| 球穿过盒子没有反应 | 盒子没有 Trigger，或脚本不在 Collider 同节点 | 勾选 `Is Trigger`，脚本挂根节点 |
| 角色走进盒子也触发 | 层过滤失效 | 确认子弹根节点在 `Projectile` 层 |
| 编辑器里点退出没反应 | 旧脚本用 `Application.Quit()` | 已改为 `EditorApplication.isPlaying = false` |
| 投偏一次后不能继续投 | `repeatWhenOpponentMissing` 未开 | 勾选该选项 |
| 命中开始盒没有切页面 | 页面未注册 / prefab 为空 / 没有 UIRoot | 看 Console 警告，检查 catalog 与 `UIPageConfig.prefab` |
| 开始后报场景不存在 | 页面回退触发，且 `battleSceneName` 拼错或未加入 Build Settings | 检查 Build Settings |
| 预制体上的 `StartScene` 显示 Missing | `StartScene.cs` 的顶层类名与文件名不一致，或把 MonoBehaviour 嵌套在另一个类里 | 保证顶层类名 = 文件名；MonoBehaviour 不要嵌套 |

## 7. 已知限制

- 淡出依赖 `CanvasGroup`，只影响 UI 图形；角色/盒子的 `SpriteRenderer` 不会跟着变透明；
- 开始页本身是场景对象，不是 `UIPage`；`UIPageRouter` 只负责打开目标页面，关闭目标页面后开始页仍在，`StartScene` 会回到可再次触发的状态；
- `Assets/Data/UI/Pages/StartPage.asset` 仍未绑定 prefab，也未加入当前使用的 `UIPageCatalog_Default`；
- 开始页的角色与投球物体是场景内物理对象，不适合放进 UI 页 Canvas；
- 没有为开始页新增自动化测试，验证依赖编译、现有 EditMode/PlayMode 基线和手动 Play。
