# 玩家设置框架（人读版）

> 一句话：**玩家设置 = 默认值 SO + 本地 JSON + Service + Applier 接口**。  
> 它不管理子弹、角色、皮肤等内容配置；内容仍由 prefab / `Assets/Scripts/Config` 承载。设置页面暂未实现。

## 目录

1. [模块定位](#1-模块定位)
2. [文件与职责](#2-文件与职责)
3. [数据模型](#3-数据模型)
4. [默认值资产](#4-默认值资产)
5. [存档](#5-存档)
6. [PlayerSettingsService](#6-playersettingsservice)
7. [Applier 扩展点](#7-applier-扩展点)
8. [Bootstrap](#8-bootstrap)
9. [新增设置项 / 分组](#9-新增设置项--分组)
10. [已知限制](#10-已知限制)
11. [验收](#11-验收)

---

## 1. 模块定位

| 内容 | 是否属于本模块 |
|---|---|
| 玩家音量、分辨率、垂直同步等偏好 | 是 |
| 玩家偏好的本地存档 / 重置 | 是 |
| 设置页面 UI | 暂不做，只留接口 |
| 子弹伤害、角色血量、皮肤 prefab | 否，继续走现有内容配置 / prefab |
| 音频、显示、输入系统的实际应用 | 由 `ISettingsApplier` 接入 |

因此本模块和 `Assets/Scripts/Config` 是**两套东西**：

- `Assets/Scripts/Config`：内容定义，策划/开发调，运行时只读；
- `Assets/Scripts/Settings`：玩家偏好，玩家可改，运行时持久化。

---

## 2. 文件与职责

| 文件 | 职责 |
|---|---|
| `Settings/PlayerSettingsData.cs` | 可序列化的设置数据：音频、显示 |
| `Settings/PlayerSettingsDefaults.cs` | ScriptableObject，保存默认值 |
| `Settings/PlayerSettingsValidator.cs` | 把数值钳制到合法范围 |
| `Settings/ISettingsStore.cs` | 存档接口 |
| `Settings/JsonSettingsStore.cs` | JSON 读写、损坏备份 |
| `Settings/PlayerSettingsMigration.cs` | 存档版本迁移入口 |
| `Settings/PlayerSettingsService.cs` | 当前值、修改、保存、重置、通知 |
| `Settings/SettingsApplier.cs` | Applier 接口与 MonoBehaviour 基类 |
| `Settings/PlayerSettingsBootstrap.cs` | 启动时初始化 Service |
| `Settings/Appliers/DisplaySettingsApplier.cs` | 把显示设置应用到 Unity |
| `Editor/SettingsFactory.cs` | 创建默认值资产 |
| `Data/Settings/PlayerSettingsDefaults.asset` | 默认值资产 |

---

## 3. 数据模型

### 音频 `AudioSettingsData`

| 字段 | 类型 | 含义 |
|---|---|---|
| `masterVolume` | float | 主音量，0~1 |
| `musicVolume` | float | 音乐音量，0~1 |
| `sfxVolume` | float | 音效音量，0~1 |
| `mute` | bool | 主静音 |

### 显示 `DisplaySettingsData`

| 字段 | 类型 | 含义 |
|---|---|---|
| `fullscreenMode` | `FullScreenMode` | 全屏模式 |
| `resolutionWidth` / `Height` | int | 分辨率；都为 0 表示沿用当前设备 |
| `vSync` | bool | 垂直同步 |
| `frameRateLimit` | int | 0 = 不限制；大于 0 时限制到 30~240 |
| `qualityLevel` | int | -1 = 沿用当前画质 |

### 根数据 `PlayerSettingsData`

| 字段 | 含义 |
|---|---|
| `version` | 存档版本，当前为 1 |
| `audio` | 音频设置 |
| `display` | 显示设置 |

`Clone()` 是深拷贝；Service 的 `Current` 返回副本，外部不能直接改内部对象。

---

## 4. 默认值资产

路径：`Assets/Data/Settings/PlayerSettingsDefaults.asset`

- 由 `SettingsFactory.EnsureDefaultSettings()` 在 `Build/All` 或 `Build/Prefabs` 时创建；
- 已存在时不覆盖，手调默认值不会被生成器重置；
- 资产内是一份 `PlayerSettingsData`，直接在 Inspector 修改；
- `PlayerSettingsDefaults.CreateDefault()` 返回副本。

默认值只作为“第一次启动 / 重置”的来源，**玩家当前值保存在本地 JSON，不写回 SO**。

---

## 5. 存档

实现：`JsonSettingsStore`

默认路径：

```text
Application.persistentDataPath/NineDays/player-settings.json
```

行为：

| 情况 | 结果 |
|---|---|
| 文件不存在 | 返回默认值副本 |
| JSON 读取/解析失败 | 警告 + 备份为 `.corrupt` + 返回默认值 |
| 正常读取 | 返回反序列化数据，Service 再做校验 |
| 保存 | 先写 `.tmp`，尽量原子替换；失败则覆盖写入并警告 |

`ISettingsStore` 只有两个方法：

```csharp
PlayerSettingsData Load(PlayerSettingsData defaults);
void Save(PlayerSettingsData data);
```

测试或换存储方式时实现这个接口即可，例如以后可以加 `PlayerPrefsSettingsStore`。

---

## 6. PlayerSettingsService

静态门面，API：

| 方法 | 入参 | 返回 / 行为 |
|---|---|---|
| `Initialize(defaults, store, appliers)` | 默认值资产、可选存档、可选 Applier 列表 | 加载、校验、应用并初始化 |
| `Current` | 无 | 返回当前设置副本；未初始化抛异常 |
| `Apply(next, save = true)` | 整份设置 | 校验、应用、保存、触发 `Changed` |
| `Update(mutate, save = true)` | 修改委托 | 在副本上修改后走 `Apply` |
| `ResetToDefaults(save = true)` | 无 | 恢复默认值并保存 |
| `Reload()` | 无 | 从存档重新读取并应用 |
| `Save()` | 无 | 保存当前值 |
| `Shutdown()` | 无 | 清理静态状态，Bootstrap 销毁时调用 |
| `Changed` | 事件 | 应用完成后触发 |

示例：

```csharp
PlayerSettingsService.Update(settings =>
{
    settings.audio.masterVolume = 0.7f;
    settings.display.vSync = false;
});
```

注意：

- 不要直接修改 `PlayerSettingsService.Current.audio`，因为 `Current` 是副本；
- 改完必须调用 `Apply` / `Update` 才会应用和保存；
- `Apply` 会先经过 `PlayerSettingsValidator.Normalize` 钳制非法值。

---

## 7. Applier 扩展点

接口：

```csharp
public interface ISettingsApplier
{
    string Section { get; }
    int Order { get; }
    void Apply(PlayerSettingsData data);
}
```

场景组件通常继承：

```csharp
public abstract class SettingsApplier : MonoBehaviour, ISettingsApplier
```

现有实现：

- `DisplaySettingsApplier`：分辨率、全屏、垂直同步、帧率、画质；  
  `applyInEditor` 默认 false，避免在 Editor Play Mode 改 Game 视图。
- 音频 Applier 暂未实现：当前项目还没有 AudioMixer / 音频管理器，等音乐模块接入后再写。

新增一个 Applier：

1. 新建类继承 `SettingsApplier`；
2. 设置 `Section` 和 `Order`；
3. 实现 `Apply(PlayerSettingsData data)`；
4. 挂到 Bootstrap 子节点，或手动加入 Bootstrap 的 `appliers` 列表。

Service 会按 `Order` 升序执行，单个 Applier 抛异常只会记录错误，不影响其他 Applier。

---

## 8. Bootstrap

`PlayerSettingsBootstrap`：

- `[DefaultExecutionOrder(-1000)]`，早于普通业务；
- 序列化字段 `defaults` 必须指向默认值资产；
- `dontDestroyOnLoad` 默认 true；
- `autoCollectAppliers` 为 true 且列表为空时，从子节点收集 `SettingsApplier`；
- 也支持手动把 Applier 拖进 `appliers` 列表；
- 退出游戏时保存；
- 销毁时调用 `PlayerSettingsService.Shutdown()`。

当前没有在 `TestArena` 中自动加入 Bootstrap，因为还没有设置页面；需要测试设置时再在启动场景放置。

---

## 9. 新增设置项 / 分组

### 新增一个设置项（例如音乐音量已有字段，增加“语音音量”）

1. 在对应数据类加字段，并在 `PlayerSettingsData.Clone()` 里复制；
2. 在 `PlayerSettingsDefaults.asset` 里改默认值；
3. 在 `PlayerSettingsValidator.Normalize` 里加范围钳制；
4. 在对应 Applier 里应用；
5. 设置页面（以后做）绑定控件；
6. 如果数据结构变化导致旧存档不兼容，在 `PlayerSettingsMigration.Upgrade` 里按版本补默认值。

### 新增一个分组（例如语言 / 键位）

1. 新建 `[Serializable]` 数据类；
2. 在 `PlayerSettingsData` 加字段并在 `Clone()` 复制；
3. 在 Validator 中初始化并校验；
4. 新建 `SettingsApplier`，`Section` 使用新常量；
5. 在 `SettingsSections` 加常量（可选）；
6. 更新默认值资产与以后的设置页。

Service、JSON Store、Bootstrap 不需要改。

---

## 10. 已知限制

- 没有设置页面；
- 没有音频 Applier（等音频模块）；
- 没有语言、按键重绑定、Gameplay 设置；
- 当前只有版本 1，迁移入口已留但无实际旧版本；
- `PlayerSettingsDefaults.SetValues` 是公开的，供编辑器和测试使用；运行时应只读；
- Bootstrap 不是自动生成到场景的，需要手动放入启动场景；
- 修改数据结构后旧 JSON 中缺失字段会得到 C# 默认值，必要时要补迁移。

---

## 11. 验收

```bat
Unity.exe -batchmode -quit -projectPath <项目> ^
  -executeMethod NineDays.EditorTools.BuildPipeline.BuildAllBatch ^
  -logFile <项目>\Temp\build.log
```

- 日志出现 `BUILD_OK`；
- `Assets/Data/Settings/PlayerSettingsDefaults.asset` 存在；
- EditMode 测试通过；
- PlayMode 现有测试通过。
