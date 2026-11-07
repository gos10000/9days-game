# Agent 文档索引

> 最后同步：2026-11-06。全量验收：EditMode `90/90`，PlayMode `44/44`（含战斗关卡初始化 7 个 + 战斗选弹 9 EditMode / 3 PlayMode）；`BuildAllBatch` 本次未运行，见 `battleinit/BattleInit_agent.md`。

## 配置与操作先看这里

| 想做什么 | 入口 | 需要注意 |
|---|---|---|
| 改玩家默认音量 / 分辨率 | `Assets/Data/Settings/PlayerSettingsDefaults.asset` | 只改 SO，不改玩家本地 JSON；设置页暂未实现 |
| 新增玩家设置项 / 分组 | `Scripts/Settings/PlayerSettingsData.cs` + Assets + Applier | 还要考虑 `PlayerSettingsValidator` 和存档迁移 |
| 接入玩家设置服务 | 启动场景放 `PlayerSettingsBootstrap` | 指定默认值资产；当前 `TestArena` 未自动放 |
| 改角色 / 子弹 / 战斗数值 | `Assets/Data/**` 下对应 SO | **不要顺手跑 `Build/Prefabs`**，生成器会重写部分配置 |
| 改 prefab 结构 / 引用 | `Scripts/Editor/PrefabFactory.cs` | 改完跑 `9Days/Build/Prefabs`，会覆盖同名 prefab |
| 改场地地形 | `ArenaAuthoring` 场景摆层 → `9Days/Arena/Bake Terrain` | 已有 `ArenaConfig` 不会被默认生成器覆盖 |
| 加 UI 页面 | `UIPage` + `UIPageConfig` + `UIPageCatalog` | 页面 prefab 根节点必须有 `CanvasGroup` + `UIPage` |
| 调镜头手感 | `TestArena` 主相机的 `CameraRig` / `CameraDirector` | 其他场景需手动挂组件，见 `camera/Camera.md` |
| 改鼠标 / 键盘输入 | `Assets/Scripts/Input/` | 业务只走 `GameInput`；手柄/重绑定换 `IInputSource` |
| 新增运行时模块 / asmdef | `Assets/Scripts/` 对应目录 + `Docs/agent/asmdef/Asmdef.md` | 先确认依赖方向，避免新增 asmdef 循环 |
| 改测试场景布局 | `Scripts/Editor/ArenaBuilder.cs` | `Build/All` 会重建并覆盖 `TestArena.unity` |

## 配置边界

- `Assets/Scripts/Config` + `Assets/Data/*`：角色、子弹、战斗、地形等**内容定义**，运行时只读；
- `Assets/Scripts/Settings` + `Assets/Data/Settings`：**玩家偏好**，玩家可改并持久化；
- `Assets/Scripts/UI`：页面框架，不负责玩法数值；
- `Assets/Scripts/Input`：设备输入与输入路由，不参与回合规则；
- `Assets/Scripts/Battle`：回合/对局/战斗输入路由，单向依赖 Game；
- `Assets/Scripts/Camera`：场景镜头控制，参数在 Inspector；
- `Docs/program/**`：人维护的设计方案，agent 不修改。

## 文档索引

| 工作内容 | 人读版 | Agent 记录 | 内容 |
|---|---|---|---|
| 角色 | `character/Character.md` | `character/Character_agent.md` | 角色 prefab、状态机、受击/击退、撞击、发射、回合 |
| 战斗轮 | `battle/Battle.md` | `battle/Battle_agent.md` | 血量制、9 轮、先手、额外行动、行动/轮间间隔、验收 |
| 战斗背景/场地/关卡初始化 | `battleinit/BattleInit.md` | `battleinit/BattleInit_agent.md` | 背景/场地/关卡 prefab、出生点、Inspector 覆盖 |
| 战斗选弹 | `battleammo/BattleAmmo.md` | `battleammo/BattleAmmo_agent.md` | 随机抽选、数量可调、测试全选、AmmoSelectPage |
| 皮肤与动画 | `skin/Skin.md` | `skin/Skin_agent.md` | 皮肤配置、切换、Animator 契约、序列帧/骨骼 |
| 子弹 | `bullet/Bullet.md` | `bullet/Bullet_agent.md` | 原型、数值副本、特性、发射修改器、对象池 |
| 子弹爆发 | `burst/Burst.md` | `burst/Burst_agent.md` | 触发/冷却/重复、伤害/牵引/地形效果、事件接口 |
| UI 框架 | `ui/UI.md` | `ui/UI_agent.md` | UIRoot、Canvas 五层、页面栈/弹窗、缓存、过渡 |
| 场景地形 | `arena/Arena.md` | `arena/Arena_agent.md` | 分层遮罩、Bake、运行时重建、破坏/追加 |
| 镜头控制 | `camera/Camera.md` | `camera/Camera_agent.md` | 2D 正交取景、回合/子弹目标、边界、震动 |
| 输入 | `input/Input.md` | `input/Input_agent.md` | 设备快照、GameInput、战斗输入路由、调试动作 |
| 程序集拆分 | `asmdef/Asmdef.md` | `asmdef/Asmdef_agent.md` | 8 个编译单元、依赖方向、Game 聚合层边界 |
| 玩家设置 | `settings/Settings.md` | `settings/Settings_agent.md` | 默认值 SO、JSON、Service、Applier、版本迁移 |
| 开始界面 | `startscene/StartScene.md` | `startscene/StartScene_agent.md` | 投球命中开始/退出盒、页面状态、场景切换、横幅开关 |
| 资源生成与调试 | `tooling/Tooling.md` | `tooling/Tooling_agent.md` | 菜单/batchmode、生成顺序、测试场景、HUD、校验 |

## 文档约定

- 人读版：简洁、可执行，覆盖入口、参数、用法、排错；
- `_agent` 版：记录决策、文件/函数、验证证据、已知限制和接手步骤；
- 过时的测试数字、旧 git 状态、已废弃方案不再保留；
- 方案类说明在 `Docs/program/`，**由人维护，agent 不修改**。
