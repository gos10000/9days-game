# 用框架跑通游戏：Agent 记录

> 对应人读版：同目录 `GameFlow.md`。
> 记录时间：2026-11-05。
> 本次只产出方案与教程，**没有改任何代码、资产、场景、ProjectSettings**；`Docs/program/**` 未触碰。

## 1. 本次任务

- 用户目标：开始用现有框架跑通游戏，要一份可执行的方案 + 教程。
- 本次交付：
  - `Docs/agent/gameflow/GameFlow.md`：方案、阶段计划、教程、接口速查、排错、待确认；
  - `Docs/agent/gameflow/GameFlow_agent.md`：证据、缺口、建议改动、风险、验证步骤。

## 2. 勘查方法（可复现）

| 步骤 | 做法 |
|---|---|
| 读设计 | `Docs/plan/需求与玩法最小循环.md`、`Docs/program/summary.md` |
| 读框架文档 | `Docs/agent/README.md`、`tooling/Tooling.md`、`battle/Battle.md`、`character/Character.md`、`ui/UI.md`、`camera/Camera.md`、`arena/Arena.md`、`startscene/StartScene.md`、`asmdef/Asmdef.md` |
| 读关键代码 | `BattleController`、`TurnController`、`BattleSession`、`CharacterLauncher`、`ShotSpawner`、`ProjectileField`、`UIRoot`、`UIPageRouter`、`StartScene`、`ArenaBuilder` |
| 场景核对 | 按 `.meta` 的 GUID 比对场景 YAML 中的 `m_Script`；解析 GameObject / Transform / MonoBehaviour 块，输出对象、Layer、组件 |
| 构建设置 | `ProjectSettings/EditorBuildSettings.asset` |
| 层与排序层 | `ProjectSettings/TagManager.asset` |
| 版本 / 工具 | `ProjectSettings/ProjectVersion.txt`；检查 `C:\Program Files\Unity\Hub\Editor\6000.0.83f1\Editor\Unity.exe` |
| 运行状态 | 检查 `Temp/UnityLockfile`：当前存在，说明 Editor 正在运行，本次未跑 Play / batch |

## 3. 勘查结论与证据

### 3.1 唯一完整接好框架的场景是 TestArena

`Assets/Scripts/Editor/ArenaBuilder.cs` 的 `Build()` 生成：

- `Main Camera` + `CameraRig` + `CameraDirector` + `BattleCameraController`；
- `ArenaBounds` + `ArenaController`；
- `Character_P1/P2`；
- `TurnController`（`autoStart=false`、`autoAdvance=false`）；
- `BattleController`（`autoStart=true`）；
- `BattleInputController`；
- `CombatDebugHud` + `TurnBanner`；
- `UIRoot`（`startupPageId = Placeholder`）；
- `ProjectilePool`（预热 `TestBall`）。

证据：`ArenaBuilder.cs` 第 16–131 行；`TestArena.unity` 中能找到上述脚本 GUID；`UIRoot` 实例的 `m_Modifications` 里有 `propertyPath: startupPageId`、`value: Placeholder`。

影响：P0/P1 用它可以最快验证“框架链路是通的”。

### 3.2 Test_BattleScene 只有回合轮转，没有对局

场景解析结果（普通 GameObject，不含预制体实例）：

```text
Background (Layer 10)
Square (Layer 10)
WallUp (Layer 10)
WallUp (1) (Layer 10)
WallLeft (Layer 10)
WallRight (Layer 10)
Environment (Layer 0)
Main Camera (Layer 0, 无 CameraRig/Director)
TurnController (Guid 268632e5f14992b42a781a8762663841)
```

- 两个角色预制体实例：`Character_P1` 在约 `(-5.66, -1.26)`，`Character_P2` 在约 `(6.70, -1.18)`；
- `TurnController` 序列化值为 `autoStart: 1`、`autoAdvance: 1`、`firstTeam: 1`、`resolveTimeout: 3`；
- **没有** `BattleController`、`ArenaController`、`ArenaBounds`、`ProjectilePool`、`UIRoot`、`CameraRig/Director`、`BattleCameraController`。

影响：可以互相打、能扣血（`CharacterCombat` → `CharacterHealth`），但不会按 9 轮结算、不会出胜负、镜头不跟子弹。

### 3.3 Playmode Scene 是“新开始页”，但被 `nextPageId` 卡住

场景对象（14 个普通 GameObject）：

```text
StartPage (StartScene Guid 07fa75030dcc2ef4fad99ab9fc9fdb4e)
UIRoot (Guid bd0723367590f8b4e8f710b65f22e579)
BattleController (Guid 420d493a2170def48a092a0d7c67983a, m_IsActive = 0)
TurnController (Guid 268632e5f14992b42a781a8762663841)
BattleInputController (Guid a2450dd914ebaf3498607f5be308675f)
Box_Start (StartBoxTrigger Guid d6248a47d35ab2f41bb17b05c90f13e6)
Box_Exit (ExitBoxTrigger Guid 402fa6906b0962b4faab1bd765557ed3)
Character_P1（预制体实例，开始页投球用）
```

关键字段：

- `StartScene.battleSceneName = Test_BattleScene`；
- `StartScene.nextPageId = Placeholder`；
- 场景里的 `UIRoot.catalog` 指向 `UIPageCatalog_Default`（Guid `a376c9fe6945a364fadfa5ab76696c07`）；
- `UIPageCatalog_Default` 只注册了 `Placeholder`，且 `PlaceholderPage.asset` 的 `prefab` 是已绑定的；
- `UIPageCatalog_Playmode`（Guid `59846285aef8dc44fb37fb63dc22e28f`）只注册了 `StartPage`，但 `StartPage.asset` 的 `prefab` 为 `{fileID: 0}`（未绑定）；且场景里没有引用这个 catalog。

影响：`StartScene.RequestStart()` → `TrySwitchPage("Placeholder")` 成功，**不会走 `SceneManager.LoadScene`**；玩家看到占位页，游戏没开始。

### 3.4 Build Settings 与开始页

`ProjectSettings/EditorBuildSettings.asset`：

```text
SampleScene        enabled: 0
Test_StartScene    enabled: 1   ← 第 0 个
Test_BattleScene   enabled: 1   ← 第 1 个
```

- `Playmode Scene`、`TestArena` 都不在 Build Settings；
- `Test_StartScene` 中：
  - `Box_Exit` 挂 `StartBoxTrigger`，`Box_Start` 挂 `ExitBoxTrigger`（对调）；
  - 场景里没有 `StartScene` 组件；
  - 有 `Canvas`、`EventSystem`、`TurnController`、`Character_P1`。

影响：即使打包，第 0 个场景也无法正确开始；`Test_StartScene` 需要修，或改用 `Playmode Scene`。

### 3.5 子弹与按回合切换的缺口

- `Character_Base.prefab` 的 `CharacterLauncher.projectilePrefab` 指向 `Assets/Prefabs/Projectiles/Bullet/TestBall.prefab`（Guid `471d47ad96264054b9b1c42f08be9df3`）；
- `CharacterLauncher.Fire()`（约第 101–134 行）只调用 `ShotSpawner.Spawn(projectilePrefab, ...)`；
- `ShotSpawner.Spawn`（约第 41–62 行）只用 `prefab.Config` 构建 loadout；
- `Projectile.Configure(...)` 支持运行时注入数值 / 特性 / 爆发。

影响：不做运行时注入，就无法“抽到哪种子弹打哪种”；但底层已经具备注入能力，只需加一个重载与一个 `SetAmmo`。

### 3.6 道具现状

- 已有美术：`Assets/Art/Props/`（黑洞、白洞、重力场、反重力场、障碍）；
- 已有 prefab：`Assets/Prefabs/Projectiles/Prop/BlackHole.prefab`、`WhiteHole.prefab`；
- prefab 里只有 `SpriteRenderer` + 粒子，没有 `Collider2D`，也没有 `RadialGravityField`；
- `RadialGravityField` 已实现（`Scripts/Bullet/RadialGravityField.cs`），`ProjectileField` 只影响 `IsLaunched` 的子弹。

影响：黑洞 / 白洞只差“加 Trigger + 加力场脚本”；重力区需要新增 `GravityZoneField`。

### 3.7 环境与工作区

- Unity `6000.0.83f1`；
- Unity 安装路径存在：`C:\Program Files\Unity\Hub\Editor\6000.0.83f1\Editor\Unity.exe`；
- `Temp/UnityLockfile` 存在 → 当前 Editor 正在运行，batchmode 前必须关闭；
- 工作区已有**未提交改动**（非本次产生）：`Assets/Scenes/Playmode Scene.unity`、`Scripts/Battle/BattleController.cs`、`Scripts/Battle/TurnController.cs`、`Scripts/UI/Start scene/*`、`Scripts/test/TurnBanner.cs`、`Docs/agent/README.md`、`Docs/program/summary.md`，以及未跟踪的 `Assets/Prefabs/UI/Pages/StartPage.prefab`、`Docs/agent/startscene/`。本次结论基于**当前工作区**，不是 `HEAD`。

## 4. 缺口清单（按优先级）

| 优先级 | 缺口 | 现状 | 处理阶段 |
|---|---|---|---|
| P0 | 开始页命中开始盒打开占位页 | `nextPageId=Placeholder` + Default catalog 有该页 | P1 |
| P0 | 美术战斗场景无 `BattleController` | 只有 `TurnController` | P2 |
| P0 | Build Settings 没有正式入口 | 只有旧开始页 + 战斗场景 | P1/P6 |
| P1 | 回合前抽选 | 无 | P3 |
| P1 | 道具网格搭建 | 无 | P4 |
| P1 | 按回合换子弹 | launcher 固定 prefab | P5 |
| P1 | 结算页 / 重开入口 | 只有 `TurnBanner` 文字 | P6 |
| P2 | 正式 HUD | 只有 `CombatDebugHud`（OnGUI） | P6 |
| P2 | 弹道预览 / 历史轨迹 | 只有直线瞄准线 | P5 |
| P2 | 音频 | 未接 | P6（可选） |
| P3 | 地形破坏接入子弹 | 只有 `BurstTerrainEffect` 接口 | P6（可选） |
| P3 | 技能 | 暂缓 | 不做 |

## 5. 方案决策与理由

### 5.1 相位门放在 `BattleController`，不改 `BattleSession`

- `BattleSession` 是纯 C#，EditMode/PlayMode 直接测；把抽选/搭建塞进去会污染规则层并破坏测试。
- `BattleController` 已经实现 `ITurnActionRunner`，是 `BattleSession` 与场景的唯一边界。
- 现有 `BeginTurn(Team, Action<TurnResult>)` 正好是“一次行动开始”的入口，在前面插 `ITurnPhaseGate` 改动最小。

落地位置：

- `Scripts/Battle/ITurnPhaseGate.cs`（新增，`NineDays.Battle`）；
- `BattleController.BeginTurn` / `HandleTurnControllerResolved`（修改）。

### 5.2 Flow 作为最高层程序集

- 抽选/搭建需要同时访问 `BattleController`、`CharacterLauncher`、`UIRoot`、`PropDefinition`。
- `NineDays.UI` 不引用 `Game`/`Battle`，`NineDays.Battle` 不引用 `UI`，所以这些代码放哪一层都会打破现有依赖。
- 新增 `NineDays.Flow`（`Assets/Scripts/Flow/NineDays.Flow.asmdef`）引用 `NineDays.Battle`、`NineDays.Game`、`NineDays.UI`、`NineDays.Input`，是最小侵入方案。
- 新增 asmdef 后，`NineDays.Tests` / `NineDays.Tests.PlayMode` 的 asmdef 需要加引用，否则测试程序集找不到新类型。

替代方案（不推荐）：把相位代码放 `Scripts/Battle`，通过 `GameEvents` 让 UI 订阅、用静态服务回调。能避免新 asmdef，但调用链更绕、调试更差。

### 5.3 子弹运行时注入

- 现有 `Projectile.Configure(ProjectileStats, traits, bursts)` 已经为运行时注入准备好。
- `ShotSpawner` 新增 `Spawn(prefab, config, shooter, dir, speed)` 重载；旧方法转调，保持兼容。
- `CharacterLauncher` 新增 `SetAmmo(ProjectileConfig)`，Flow 在 `Aim` 相位设置。
- 这样每种子弹只需要“配置 + 视觉 prefab”，不需要复制逻辑脚本，符合 `Docs/program/summary.md` 的“不同子弹复用原型脚本”。

### 5.4 页面只收纯数据

- `NineDays.UI` 的依赖只有 `NineDays.Input`；页面参数不能出现 `Team`、`ProjectileConfig`。
- `DrawPageArgs` 用 `Sprite`、`string`、`Action<int>`；选择结果由 Flow 翻译。
- 这样 UI 可以在主菜单、战斗、结算复用，不会反向依赖玩法。

### 5.5 道具力场复用 `ProjectileField`

- `ProjectileField` 已经处理 Trigger、层过滤、阵营过滤、每物理帧累加。
- 黑洞/白洞直接用 `RadialGravityField`；重力区新增 `GravityZoneField : ProjectileField`。
- 反重力障碍是实体碰撞，不需要力场脚本。

### 5.6 场景策略

| 场景 | 用途 | 是否手改 | 注意 |
|---|---|---|---|
| `TestArena` | 框架基线 / 自动验收 | 否（生成） | `Build/Test Arena` 会覆盖 |
| `Test_BattleScene` | 正式美术战斗场景 | 是（加组件） | 保留美术墙与角色摆位 |
| `Playmode Scene` | 正式入口（开始页） | 是（改 `StartScene` 字段） | 清空 `nextPageId` |
| `Test_StartScene` | 旧开始页 | 暂不使用 | 盒子脚本对调、缺 `StartScene` |

## 6. 建议改动文件清单

### 6.1 新增

| 文件 | 程序集 | 职责 |
|---|---|---|
| `Scripts/Battle/ITurnPhaseGate.cs` | `NineDays.Battle` | 相位门接口 |
| `Scripts/Flow/NineDays.Flow.asmdef` | 新 | Flow 程序集 |
| `Scripts/Flow/TurnFlowController.cs` | Flow | 抽选 → 搭建 → 瞄准 |
| `Scripts/Flow/DrawService.cs` | Flow | 抽 4 去重（纯逻辑，可测） |
| `Scripts/Flow/TurnLoadout.cs` | Flow | 本回合选择结果 |
| `Scripts/Flow/PropPlacementController.cs` | Flow | 网格放置 / 校验 / 预览 |
| `Scripts/Flow/MatchFlowBootstrap.cs` | Flow | 注册 gate、结算页、清理 |
| `Scripts/Props/PropDefinition.cs` | `NineDays.Game` | 道具数据 |
| `Scripts/Props/PropCatalog.cs` | Game | 道具注册表 |
| `Scripts/Props/PlacedProp.cs` / `PropRegistry.cs` | Game | 已放置道具 / 注册表（拆除子弹用） |
| `Scripts/Bullet/GravityZoneField.cs` | Game | 区域重力 |
| `Scripts/Bullet/Traits/DemolishTrait.cs` | Game | 拆除沿途道具 / 地形 |
| `Scripts/Bullet/Traits/TracerTrait.cs` | Game | 曳光标记 |
| `Scripts/Bullet/TrajectoryPreview.cs` | Game | 预计弹道 |
| `Scripts/Bullet/ShotHistoryTracker.cs` | Game | 上一发轨迹 |
| `Scripts/UI/Pages/DrawPage.cs` | `NineDays.UI` | 抽选页 |
| `Scripts/UI/Pages/PlacementPage.cs` | UI | 搭建页 |
| `Scripts/UI/Pages/BattleHudPage.cs` | UI | 战斗 HUD |
| `Scripts/UI/Pages/ResultPage.cs` | UI | 结算页 |
| `Scripts/Editor/BattleSceneSetup.cs`（可选） | `NineDays.Editor` | 幂等接线菜单 |

### 6.2 修改

| 文件 | 改动 |
|---|---|
| `Scripts/Battle/BattleController.cs` | 加 `phaseGate`、`SetPhaseGate`、`BeginTurn` / `HandleTurnControllerResolved` 两处 |
| `Scripts/Bullet/ShotSpawner.cs` | 加带 `ProjectileConfig` 的重载；旧方法转调 |
| `Scripts/Character/CharacterLauncher.cs` | 加 `ammo` 字段、`SetAmmo`、`Fire` 使用它 |
| `Scripts/Bullet/Projectile.cs` | 加 `IgnoreFields` |
| `Scripts/Bullet/ProjectileField.cs` | `OnTriggerStay2D` 跳过 `IgnoreFields` |
| 测试 asmdef | 加 `NineDays.Flow` 引用 |
| `Assets/Scenes/Playmode Scene.unity` | `StartScene.nextPageId` 清空、`battleSceneName` 改战斗场景 |
| `Assets/Scenes/Test_BattleScene.unity` | 按教程 B 加组件 |
| `ProjectSettings/EditorBuildSettings.asset` | 入口 / 战斗场景顺序（P6 定稿） |

### 6.3 不要改

- `Docs/program/**`（人维护）；
- 生成器管理的 `Character_Base/P1/P2`、`Skin_*`、`TestBall`、`CharacterConfig`、`BattleConfig`、`ProjectileConfig`——除非同步改 `PrefabFactory`。

## 7. 风险与已知限制

| 风险 | 说明 | 规避 |
|---|---|---|
| 手改场景冲突 | `.unity` YAML 冲突难合并 | 用 Editor 改；一次只改一个场景 |
| 生成器覆盖 | `Build/Prefabs` 覆盖同名 prefab / 配置 | 只改数值时不要跑；要改结构就改 `PrefabFactory` |
| `TestArena` 被覆盖 | `Build/Test Arena` 重建场景 | 只当验收场，不做美术 |
| asmdef 循环 | Flow 若被低层引用会循环 | Flow 只被场景 / 高层引用；更新测试 asmdef |
| 测试基线 | 新增相位可能影响现有 PlayMode | 先跑基线；新逻辑优先纯 C# 单测 |
| 设计未定 | 道具回合子弹、先手规则、数量等 | 先确认第 9 节，再动数据模型 |
| 相位卡死 | gate 没调用 `onReady`，或场景卸载 | gate 加取消 / 超时；`OnDestroy` 清理 |
| 静态与程序地形重复 | 同时挂 `ArenaController` 与美术地形 | 二选一 |

## 8. 验证方式

### 8.1 自动

```powershell
$unity   = 'C:\Program Files\Unity\Hub\Editor\6000.0.83f1\Editor\Unity.exe'
$project = 'C:\Users\19314\Desktop\此电脑\program\Project\9days-game'

& $unity -batchmode -quit -projectPath $project `
  -executeMethod NineDays.EditorTools.BuildPipeline.BuildAllBatch `
  -logFile "$project\Temp\build.log"

& $unity -batchmode -nographics -projectPath $project `
  -runTests -testPlatform EditMode `
  -testResults "$project\Temp\editmode.xml" -logFile "$project\Temp\editmode.log"

& $unity -batchmode -projectPath $project `
  -runTests -testPlatform PlayMode `
  -testResults "$project\Temp\playmode.xml" -logFile "$project\Temp\playmode.log"
```

成功标准：

- `Temp/build.log` 末尾 `BUILD_OK`；
- `editmode.xml` / `playmode.xml` 的 `failed="0"`；
- 当前基线：EditMode 81、PlayMode 34。

> `BuildCheck.ValidateAll` 是 `internal static`；如果 `-executeMethod` 直接调用报找不到方法，用 `BuildAllBatch`（内部已调用）。

### 8.2 手动

1. `TestArena`：关占位页 → 拖拽发射 → 9 轮 → 胜负；
2. `Playmode Scene`：投球进开始盒 → 加载战斗；
3. `Test_BattleScene`（P2 后）：同上，且镜头跟随；
4. 抽选（P3 后）：每回合 4 选 1，未选不能发射；
5. 搭建（P4 后）：放置 / 撤销 / 完成 / 范围预览 / 重开清空；
6. 子弹（P5 后）：6 种行为差异、预览与历史轨迹。

### 8.3 本次验证状态

- **未运行** Unity / 测试 / Play：Editor 正被 `Temp/UnityLockfile` 锁定，且本次没有代码改动；
- 本文所有现状结论均来自源码、场景 YAML、`.meta` GUID 与 ProjectSettings 静态核对。

## 9. 待确认（同人读版第 13 节）

1. 选到道具后发射什么子弹（推荐：普通子弹）；
2. 先手规则（推荐：保留“每轮血少先”）；
3. 抽选频率（推荐：每个行动者回合）；
4. 道具数量 / 场上上限（推荐：每回合 1 个、场上 3 个）；
5. 网格大小与可放置区域（推荐：1×1 格，PlayArea 内）；
6. 道具是否常驻 / 可被拆除（推荐：常驻、可拆）；
7. 9 轮 vs 血量归零（推荐：都保留）；
8. 只当前行动方选道具（推荐：是）。

## 10. 接手步骤

1. 先确认第 9 节 1–6 项；
2. 跑 P0 基线（BuildAllBatch + 两套测试），记录结果；
3. 做 P1：改 `Playmode Scene` 的 `StartScene` 字段 + Build Settings，验证端到端；
4. 做 P2：按 `GameFlow.md` 第 7 节给 `Test_BattleScene` 加组件，验证与 TestArena 行为一致；
5. 做 P3：加 `ITurnPhaseGate` + Flow + DrawPage；先写纯逻辑测试；
6. 做 P4/P5/P6，每阶段结束更新 `GameFlow.md` / 本文件，并跑一次基线；
7. 所有新增玩法参数保持 `[SerializeField]` 或 SO，不硬编码。


