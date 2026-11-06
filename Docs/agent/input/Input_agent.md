# 输入模块 — Agent 工作记录（Input_agent）

> 人读版：同目录 `Input.md`。角色：`../character/Character_agent.md`；战斗轮：`../battle/Battle_agent.md`。

## 目录

- [0. 本次改动](#0-本次改动)
- [1. 文件清单](#1-文件清单)
- [2. 架构与依赖](#2-架构与依赖)
- [3. 实现细节](#3-实现细节)
- [4. 场景生成与校验](#4-场景生成与校验)
- [5. 测试与验证](#5-测试与验证)
- [6. 已知限制与待办](#6-已知限制与待办)
- [7. 接手检查清单](#7-接手检查清单)
- [8. 构建副作用记录](#8-构建副作用记录)

---

## 0. 本次改动

目标：把输入从 `TurnController.cs` 里的 `InputRouter` 抽离为独立模块，并选择方案 B：增加 `BattleInputController` 统一把输入路由给当前行动角色。

本次范围：

- 新增 `Assets/Scripts/Input/`；
- `TurnController` 删除 `InputRouter` 和 `using UnityEngine.InputSystem`；
- `CharacterLauncher` 不再在 `Update` 中轮询输入，改为实现 `IAimInputReceiver`；
- `CombatDebugHud` 改用语义动作，不再依赖 `Key`；
- `UIRoot` 的 Esc 改走 `GameInput.ConsumeCancel()`；
- `ArenaBuilder` 生成 `BattleInputController`，`BuildCheck` 增加场景校验；
- 新增 `GameInputTests`（5 个 EditMode 用例）。

未做：

- 手柄；
- 按键重绑定；
- Input Actions 资产；
- UI 指针拦截/输入上下文栈。

## 1. 文件清单

| 文件 | 类型 | 职责 |
|---|---|---|
| `Scripts/Input/InputTypes.cs` | 新增 | `InputSnapshot`、`IInputSource`、`GameInputAction` |
| `Scripts/Input/UnityInputSource.cs` | 新增 | 直接读取 Mouse / Keyboard 的唯一实现 |
| `Scripts/Input/GameInput.cs` | 新增 | 静态门面、当帧缓存、Cancel 消费、测试注入 |
| `Scripts/Input/IAimInputReceiver.cs` | 新增 | 瞄准接收端接口 |
| `Scripts/Battle/BattleInputController.cs` | 新增 | 当前回合 → 当前角色 Launcher 的输入路由（`NineDays.Battle`） |
| `Scripts/Battle/TurnController.cs` | 修改 | 删除 `InputRouter` 类与 InputSystem 引用 |
| `Scripts/Character/CharacterLauncher.cs` | 修改 | 改为 `IAimInputReceiver`，暴露瞄准方法 |
| `Scripts/Debug/CombatDebugHud.cs` | 修改 | 改用 `GameInput.WasPressed(GameInputAction)` |
| `Scripts/UI/Framework/UIRoot.cs` | 修改 | Esc 改走 `GameInput.ConsumeCancel()` |
| `Scripts/Editor/ArenaBuilder.cs` | 修改 | 场景生成 `BattleInputController` 并连引用 |
| `Scripts/Editor/BuildCheck.cs` | 修改 | 校验测试场景输入路由器 |
| `Assets/Tests/EditMode/GameInputTests.cs` | 新增 | 快照、Cancel 消费、注入、坐标换算 |
| `Assets/Tests/PlayMode/BattleInputTests.cs` | 新增 | 只有当前行动角色收到瞄准输入 |

## 2. 架构与依赖

```text
UI / CharacterLauncher / CombatDebugHud
                 ↓
              GameInput
                 ↓
           IInputSource
                 ↓
          UnityInputSource
```

- 设备层只引用 `UnityEngine` / `UnityEngine.InputSystem`；
- `GameInput` 不引用 `TurnController`、`Character`；
- `BattleInputController` 是显式的战斗适配层，允许引用 `TurnController` 和 `CharacterLauncher`；
- `TurnController` 不引用输入模块；
- 后续程序集拆分后，`Input` 目录使用 `NineDays.Input.asmdef`；`BattleInputController` 移到 `Battle` 程序集，避免 Input 反向依赖战斗。详见 `../asmdef/Asmdef.md`。

## 3. 实现细节

### 3.1 InputSnapshot / InputSource

- `InputSnapshot` 是 `readonly struct`，只含 Unity 的 `Vector2` 和 `bool`；
- `IInputSource.Read()` 生成快照；
- `UnityInputSource.Read()` 保持原 `InputRouter` 语义：
  - `Mouse.current` 为空时 `HasPointer=false`、指针为零；
  - 左键 `wasPressedThisFrame / isPressed / wasReleasedThisFrame`；
  - 右键 `wasPressedThisFrame`；
  - Esc `wasPressedThisFrame`；
- `WasPressed` 映射调试动作，保留 F2/T、F3/G 别名。

### 3.2 GameInput

- `Current` 使用 `Time.frameCount` 缓存，保证同一帧 UI、路由器和调试 HUD 读取一致；
- `ConsumeCancel()` 记录 `cancelConsumedFrame`，同一帧只允许一个消费者成功；
- `SetSource` / `Reset` 同时清空快照和消费标记；
- `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` 清理静态状态，兼容 Domain Reload；
- `PointerWorld` 保留原 `screen.z = Abs(camera.z)`，避免瞄准坐标行为改变。

取消优先级：

- `UIRoot` 带 `[DefaultExecutionOrder(-100)]`，先于普通角色逻辑执行；
- UI 只有在 `Router.CanGoBack == true` 时才调用 `ConsumeCancel`；
- 角色在 UI 未消费时才会收到 Esc，避免同一帧同时关页面和取消瞄准。

### 3.3 CharacterLauncher

- 删除 `Update` 输入轮询；
- 实现 `IAimInputReceiver`：
  - `BeginAim(Vector2)`：检查 `CanAct`，记录拖拽起点；
  - `UpdateAim(Vector2)`：更新力度、朝向、瞄准线；
  - `ReleaseAim(Vector2)`：松手走原来的 `Fire()`；
  - `CancelAim()`：公开给路由器/角色状态使用；
- `TargetCamera` 供 `BattleInputController` 做屏幕转世界；
- `CharacterLauncher` 不再引用 `InputRouter`，也不引用 InputSystem。

### 3.4 BattleInputController

- `Awake` 自动查找 `TurnController`（ArenaBuilder 会显式连引用）；
- `ResolveActiveLauncher`：
  - `IsResolving`、`CurrentTeam == None`、`CanAct == false` 时返回 null；
  - 角色变化时先取消旧 Launcher 的瞄准，再缓存新角色；
- `Update`：
  - 关闭 `enableGameplayInput` → 取消当前瞄准；
  - 按下/按住/松开左键分别转发；
  - 右键或 `ConsumeCancel()` 取消；
- 不负责回合推进、命中结算或换人，`TurnController` 仍是唯一回合驱动。

### 3.5 UI 与调试 HUD

- `UIRoot` 不再直接访问 `Keyboard.current`；
- 仍使用 `InputSystemUIInputModule` 处理 EventSystem 点击；
- `CombatDebugHud` 不再用 `Key` 枚举，改用 `GameInputAction`；
- 调试热键语义由 `UnityInputSource` 负责，以后换手柄/重绑定不改 HUD。

## 4. 场景生成与校验

`ArenaBuilder.Build()` 在 `BattleController` 之后创建：

```text
BattleInputController
  turnController = TurnController
  enableGameplayInput = true
```

`BuildCheck.ValidateSceneInput()`：

- 仅当活动场景是 `TestArena` 时检查；
- 必须存在 `BattleInputController`；
- `turnController` 引用不能为空。

`BuildAllBatch` 已通过；`TestArena.unity` 重新生成后包含该组件。旧场景如果未重建，角色不会响应鼠标。

## 5. 测试与验证

### 自动测试

```bat
Unity.exe -batchmode -nographics -projectPath <项目> ^
  -runTests -testPlatform EditMode -testResults Temp\editmode.xml -logFile Temp\editmode.log

Unity.exe -batchmode -projectPath <项目> ^
  -runTests -testPlatform PlayMode -testResults Temp\playmode.xml -logFile Temp\playmode.log
```

本次实测：

| 测试 | 结果 |
|---|---|
| EditMode | **81/81 Passed**（原 76 + 新增 5） |
| PlayMode | **34/34 Passed** |
| `BuildAllBatch` | **BUILD_OK** |
| `BuildCheck` | 通过 |

新增 `GameInputTests` 覆盖：

- `Current` 返回 Fake 快照；
- `ConsumeCancel` 一帧只成功一次；
- `SetSource` 重置消费状态；
- `WasPressed` 委托给输入源；
- `PointerWorld` 投影到相机平面。

新增 `BattleInputTests` 覆盖：

- 当前回合 P1 收到按下并进入瞄准；
- 非当前回合 P2 不进入瞄准；
- 松开后当前角色退出瞄准。

### 手动检查

- 左键拖拽 → 松开发射；
- 右键 / Esc 取消；
- 非当前回合不能操作；
- 换人时旧瞄准被取消；
- UI 能返回时 Esc 先关页面；
- 调试键：空格 / 1 / 2 / E / R / F1 / T(F2) / G(F3)。

## 6. 已知限制与待办

- [ ] 手柄支持：实现新的 `IInputSource`，不改消费者；
- [ ] 按键重绑定：引入 Input Actions 资产，使用语义动作映射；
- [ ] UI 指针拦截：当前页面打开时鼠标仍可能触发瞄准；
- [ ] `enableGameplayInput` 目前只有运行时属性，没有外部暂停/UI 上下文自动控制；
- [ ] 调试热键在正式构建仍启用；
- [ ] 没有输入灵敏度、死区等玩家设置项。

## 7. 接手检查清单

- [ ] 新增输入读取是否只放在 `Input` 模块，业务代码是否只使用 `GameInput` / `IAimInputReceiver`；
- [ ] 是否误把输入逻辑放回 `TurnController`；
- [ ] 新场景是否重新执行 `9Days/Build/Test Arena`，否则缺 `BattleInputController`；
- [ ] 是否检查 `BuildCheck.ValidateSceneInput`；
- [ ] 是否保留 `ConsumeCancel` 的 UI 优先顺序；
- [ ] 是否运行 EditMode / PlayMode / `BuildAllBatch`；
- [ ] 是否同步 `Input.md`、`README.md`、`Character.md`、`UI.md`、`Tooling.md`。

## 8. 构建副作用记录

`BuildAllBatch` 会重写 `CharacterConfig_Default.asset` 和 `Character_Base.prefab`；本次验证后已恢复这两个内容文件的既有版本，保留 `TestArena` 的新输入组件生成结果。以后只改输入代码时不要顺手跑 `Build/Prefabs`。
