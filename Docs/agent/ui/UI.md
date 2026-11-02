# UI 场景与预制体（人读版）

> 一句话：除主菜单外，所有游戏内 UI 都在 `Assets/Scenes/UIScene.unity` 里用预制体拼装。
> 详细记录见同目录 `UI_agent.md`。

## 目录

1. [生成方式](#1-生成方式)
2. [UIScene 结构](#2-uiscene-结构)
3. [预制体清单](#3-预制体清单)
4. [布局约定](#4-布局约定)
5. [接下来要接的数据](#5-接下来要接的数据)
6. [常见问题](#6-常见问题)

---

## 1. 生成方式

| 方式 | 操作 |
|---|---|
| Unity 菜单 | `9Days/Build/UI (Prefabs + UIScene)` |
| batchmode | `-executeMethod NineDays.EditorTools.BuildPipeline.BuildUIBatch`（需先关闭 Unity） |

生成器 **只在目标不存在时创建**，已存在的预制体 / 场景一律跳过，所以之后的 UI 工作直接改预制体和场景即可，不会被覆盖。想完全重建：先删掉 `Assets/Prefabs/UI` 下的文件和 `UIScene.unity`，再跑菜单。

## 2. UIScene 结构

```
Main Camera        正交 5，纯色深底（只是给 Game 视图一个背景）
EventSystem        InputSystemUIInputModule（默认 UI Actions）
UI_Screen          预制体实例：Canvas(Overlay) + CanvasScaler(1920×1080, Match 0.5) + GraphicRaycaster
├─ HUD             UI_HUD 实例
│  ├─ HealthBar_P1     左上，480×48
│  ├─ HealthBar_P2     右上，480×48
│  ├─ TurnBanner       顶部居中，560×64
│  ├─ BulletType       底部居中，360×72（圆形图标 + 类型名）
│  └─ PauseButton      右下，96×96
├─ Panels          面板容器
│  ├─ PausePanel       全屏遮罩 + 640×520 面板，默认隐藏
│  ├─ ItemSelectPanel  全屏遮罩 + 1400×800 面板，默认隐藏
│  └─ PlacementPanel   底部 200 高操作条，默认隐藏
└─ Overlay         空容器，留给全屏震动 / 受击闪白 / 过场
```

## 3. 预制体清单

`Assets/Prefabs/UI/`

| 预制体 | 内容 | 用途 |
|---|---|---|
| `UI_Button` | 九宫格底 + TMP 文本 + Button，三态配色 | 所有按钮的唯一来源 |
| `UI_HealthBar` | 底框 + Filled 血条 + 数值文本 | 双方血条 |
| `UI_TurnBanner` | 底框 + 回合提示文本 | 回合切换提示 |
| `UI_BulletType` | 底框 + 圆形图标 `BulletIcon`（56×56）+ 类型名 `Label` | 显示当前子弹类型 |
| `UI_HUD` | 上面四个 + PauseButton | 主 HUD |
| `UI_PausePanel` | 遮罩 + 标题 + 继续/重开/设置/返回主菜单 | 暂停界面 |
| `UI_ItemSelectPanel` | 遮罩 + 标题 + 4 道具位 + 4 子弹位 + 确认 | 选取道具子弹界面 |
| `UI_PlacementPanel` | 底条 + 标题 + 4 道具位 + 取消/确认 | 摆放阶段界面 |
| `UI_Screen` | Canvas + 三层容器 | 整屏根预制体 |

## 4. 布局约定

- 参考分辨率 1920×1080，`Match Width Or Height = 0.5`；改分辨率只动 `UI_Screen` 的 CanvasScaler。
- 屏幕四角元素统一 24px 边距（`UIBuilder.Margin` 是生成期常量，之后直接改锚点）。
- 文本一律 TextMeshPro（`LiberationSans SDF`），中文缺字请换字体资产。
- 可缩放面板用 `Image.Type.Sliced`（内置 `UI/Skin/UISprite.psd`），换美术图时保持九宫格边框。
- 面板默认 `SetActive(false)`，由逻辑脚本开关，不要在场景里手动打开后忘记关。

## 5. 接下来要接的数据

以下数据当前项目还没有，先放占位文本，等程序补：

| UI | 缺的数据 |
|---|---|
| 血条 | `Character` 没有 HP 字段（`ProjectileConfig.damage` 目前只透传不扣血） |
| 子弹类型图标 | `ProjectileConfig.icon / displayName` 字段已加，但 niexa 分支没有 `Projectile` 脚本读取，当前图标是静态占位 |
| 选取面板 | 没有道具 / 子弹配置（只有一颗 `TestBallConfig`） |
| 摆放面板 | 没有道具系统与“抽取→选择→摆放”阶段事件 |
| 暂停面板 | 没有 `PauseController`；且 `InputRouter` 未屏蔽 UI，点按钮会被当成拖拽发射 |
| 回合横幅 | `TurnStarted / TurnEnded` 已有，可直接接 |

## 6. 常见问题

| 现象 | 处理 |
|---|---|
| 菜单里没有 `9Days/Build/UI` | 脚本没编译完，等 Unity 转圈结束或重新聚焦窗口 |
| 生成后想改样式 | 直接改预制体；不要重跑生成器（会跳过，不会覆盖，但也不会帮你改结构） |
| 按钮点下去游戏里也在瞄准 | 程序还没加 `EventSystem.current.IsPointerOverGameObject()` 屏蔽，去见第 5 节 |
| 中文显示成方块 | TMP 字体没有中文字形，换中文 SDF 字体资产 |
