# 输入模块（人读版）

> 一句话：**设备输入统一走 `GameInput`，`BattleInputController` 只把输入转给当前行动角色；回合控制器不再直接读鼠标键盘。**
> Agent 记录见同目录 `Input_agent.md`；角色瞄准实现见 `../character/Character.md`。

## 目录

1. [模块职责](#1-模块职责)
2. [数据流](#2-数据流)
3. [文件与 API](#3-文件与-api)
4. [输入映射](#4-输入映射)
5. [场景接入](#5-场景接入)
6. [使用示例](#6-使用示例)
7. [扩展手柄 / 重绑定](#7-扩展手柄--重绑定)
8. [已知限制](#8-已知限制)
9. [验收](#9-验收)

---

## 1. 模块职责

| 文件 | 职责 |
|---|---|
| `Input/InputTypes.cs` | `InputSnapshot`、`IInputSource`、`GameInputAction` |
| `Input/UnityInputSource.cs` | 唯一直接访问 `Mouse.current` / `Keyboard.current` 的地方 |
| `Input/GameInput.cs` | 静态输入门面：当帧快照、取消键消费、调试动作、坐标换算 |
| `Input/IAimInputReceiver.cs` | 瞄准输入接收端接口，供 `CharacterLauncher`、AI、网络输入实现 |
| `Battle/BattleInputController.cs` | 战斗适配层：判断当前回合并转发给当前角色的 Launcher；归 `NineDays.Battle` |

边界：

- `Input` 设备层不引用 `Battle` / `Character` / `UI`；
- `BattleInputController` 已移动到 `Battle` 程序集，是唯一允许引用 `TurnController`、`Character` 的输入适配文件；
- `TurnController` 只负责回合、结算和换人，不再读取输入；
- 手柄、重绑定以后通过新的 `IInputSource` 实现接入，消费者不需要改。

## 2. 数据流

```text
Mouse / Keyboard
      ↓
UnityInputSource            读取设备，生成 InputSnapshot
      ↓
GameInput                   当帧缓存 + ConsumeCancel + 语义动作
      ↓
BattleInputController       找到当前回合、可操作的角色
      ↓
CharacterLauncher           实现 IAimInputReceiver，负责瞄准/发射表现
```

UI 的 Esc 也走 `GameInput.ConsumeCancel()`；UI 优先消费，只有 UI 不能返回时才轮到角色取消瞄准。

## 3. 文件与 API

### InputSnapshot

一帧的只读快照：

| 字段 | 类型 | 含义 |
|---|---|---|
| `HasPointer` | `bool` | 是否有鼠标指针 |
| `PointerScreen` | `Vector2` | 屏幕坐标 |
| `PrimaryPressed` | `bool` | 左键本帧按下 |
| `PrimaryHeld` | `bool` | 左键当前按住 |
| `PrimaryReleased` | `bool` | 左键本帧松开 |
| `SecondaryPressed` | `bool` | 右键本帧按下 |
| `CancelPressed` | `bool` | Esc 本帧按下 |

### IInputSource

```csharp
public interface IInputSource
{
    InputSnapshot Read();
    bool WasPressed(GameInputAction action);
}
```

`Read()` 返回设备快照；`WasPressed` 查询语义化调试动作。测试或手柄实现只需替换这个接口。

### GameInput

| 成员 | 入参 | 返回 / 行为 |
|---|---|---|
| `Current` | 无 | 当前帧快照；同一帧多个消费者读到同一份 |
| `ConsumeCancel()` | 无 | 本帧第一次调用返回 true，之后返回 false |
| `WasPressed(action)` | `GameInputAction` | 查询调试动作 |
| `PointerWorld(camera)` | 相机，可为空 | 屏幕坐标转 z=0 玩法平面世界坐标 |
| `SetSource(source)` | `IInputSource` | 替换输入源；测试注入 Fake |
| `Reset()` | 无 | 清理静态状态 |

`PointerWorld` 保持原来的 `screen.z = Abs(camera.z)` 逻辑；相机为空时返回 `Vector2.zero`。

### IAimInputReceiver

| 方法 | 入参 | 说明 |
|---|---|---|
| `BeginAim(pointerWorld)` | 世界坐标 | 开始拖拽瞄准 |
| `UpdateAim(pointerWorld)` | 世界坐标 | 更新拉拽方向/力度/瞄准线 |
| `ReleaseAim(pointerWorld)` | 世界坐标 | 松手发射 |
| `CancelAim()` | 无 | 取消瞄准 |
| `IsAiming` | 无 | 只读，是否正在瞄准 |

`CharacterLauncher` 是当前唯一实现；以后 AI 或网络输入可以直接调用同一组方法。

### BattleInputController

Inspector 参数：

| 字段 | 默认 | 作用 |
|---|---|---|
| `turnController` | 场景自动查找 | 提供当前回合和 `CanAct` 判断 |
| `enableGameplayInput` | true | 关闭后立即取消瞄准，可用于暂停/UI 接管 |

运行逻辑：

1. 没有运行中的回合、正在结算、当前角色 `CanAct == false` 时，取消当前瞄准并退出；
2. 角色切换时先取消旧角色的瞄准；
3. 左键按下 → `BeginAim`；
4. 左键按住 → `UpdateAim`；
5. 左键松开 → `ReleaseAim`；
6. 右键或 `GameInput.ConsumeCancel()` → `CancelAim`。

## 4. 输入映射

### 鼠标

| 游戏动作 | 当前绑定 |
|---|---|
| 瞄准 | 鼠标左键按下 |
| 拖拽 | 鼠标左键按住 + 鼠标位置 |
| 发射 | 鼠标左键松开 |
| 取消瞄准 | 鼠标右键 / Esc |

### 调试动作

| `GameInputAction` | 当前按键 | 用途 |
|---|---|---|
| `TestShot` | 空格 | 当前角色直接发一颗测试球 |
| `ForcePlayer1` | `1` | 强制 P1 回合 |
| `ForcePlayer2` | `2` | 强制 P2 回合 |
| `ExtraTurn` | `E` | 追加一次立即行动 |
| `Restart` | `R` | 重开战斗 |
| `ToggleImpactLog` | `F1` | 冲量详细日志 |
| `ToggleAutoTest` | `F2` / `T` | 自动对射 |
| `ToggleSlowMotion` | `F3` / `G` | 慢动作 |

F 键在 Scene / Hierarchy 视图聚焦时可能被 Unity 编辑器吞掉，测试时先点 Game 视图。

## 5. 场景接入

`ArenaBuilder` 生成 `TestArena` 时会创建：

```text
BattleInputController
  └─ turnController = TurnController
```

`BuildCheck` 会检查：

- `TestArena` 有 `BattleInputController`；
- `turnController` 引用非空。

如果你只打开了旧场景而没有重建，场景里可能没有输入路由器，角色无法瞄准。重新执行 `9Days/Build/Test Arena` 或 `9Days/Build/All`。

## 6. 使用示例

读取输入：

```csharp
InputSnapshot input = GameInput.Current;
if (input.PrimaryPressed) { /* 开始拖拽 */ }
```

取消键只处理一次：

```csharp
if (GameInput.ConsumeCancel()) { /* UI 返回或取消瞄准 */ }
```

调试动作：

```csharp
if (GameInput.WasPressed(GameInputAction.Restart))
    battleController.RestartBattle();
```

测试注入：

```csharp
GameInput.SetSource(new FakeInputSource(snapshot));
// 用完 GameInput.Reset();
```

## 7. 扩展手柄 / 重绑定

当前没有手柄映射，也没有按键重绑定界面；接口已经留好：

1. 新建 `InputActionSource : IInputSource`；
2. 在 Input Actions 资产里定义 Aim / Cancel / Debug 动作；
3. 在 `Read()` 中填 `InputSnapshot`，在 `WasPressed()` 中映射 `GameInputAction`；
4. 启动时调用 `GameInput.SetSource(new InputActionSource(...))`；
5. `CharacterLauncher`、`CombatDebugHud`、`UIRoot` 不需要修改。

手柄瞄准需要额外定义虚拟指针或方向输入，这属于输入源实现，不属于回合逻辑。

## 8. 已知限制

- 当前只支持鼠标 + 键盘，同一套设备轮流操作双方；
- 没有 Input Actions 资产接入，绑定关系在 `UnityInputSource` 内写死；
- 没有手柄、按键重绑定、输入灵敏度设置；
- 没有 UI 指针拦截：全屏页面打开时点击仍可能同时触发瞄准（当前 TestArena 行为）；
- 调试热键始终启用，正式构建需要另行关闭或移到开发构建；
- `GameInput` 是静态门面，测试必须调用 `Reset()` 清理状态。

## 9. 验收

```bat
Unity.exe -batchmode -nographics -projectPath <项目> ^
  -runTests -testPlatform EditMode -testResults Temp\editmode.xml -logFile Temp\editmode.log

Unity.exe -batchmode -projectPath <项目> ^
  -runTests -testPlatform PlayMode -testResults Temp\playmode.xml -logFile Temp\playmode.log
```

当前基线：**EditMode 81/81、PlayMode 34/34**；`BuildAllBatch` = `BUILD_OK`。

手动检查：

- 左键拖拽瞄准、松开发射；
- 右键 / Esc 取消；
- 非当前回合角色不能操作；
- UI 能返回时 Esc 关闭页面；
- 调试热键保持原行为。
