# 玩家设置框架 — Agent 工作记录（Settings_agent）

> 人读版：同目录 `Settings.md`。内容配置文档：`Docs/agent/character/Character.md`、`Docs/agent/bullet/Bullet.md`。

## 目录

- [0. 本次范围](#0-本次范围)
- [1. 文件清单](#1-文件清单)
- [2. 设计决策](#2-设计决策)
- [3. 数据与存档](#3-数据与存档)
- [4. Service 生命周期](#4-service-生命周期)
- [5. Applier 与 Bootstrap](#5-applier-与-bootstrap)
- [6. 编辑器/构建集成](#6-编辑器构建集成)
- [7. 测试](#7-测试)
- [8. 已知限制与待办](#8-已知限制与待办)
- [9. 接手检查清单](#9-接手检查清单)

---

## 0. 本次范围

目标：只做**面向玩家的设置框架**，便于后续新增设置项；设置页面暂不做。

明确不做：

- 不做内容配置目录（上一版 `ConfigCatalog/ConfigAsset` 方案已废弃）；
- 不改子弹、角色、皮肤、爆发等 prefab / `Assets/Scripts/Config`；
- 不做设置 UI；
- 不做音频 Applier、语言、按键重绑定。

---

## 1. 文件清单

### 新增运行时

| 文件 | 职责 |
|---|---|
| `Scripts/Settings/PlayerSettingsData.cs` | `PlayerSettingsData` / `AudioSettingsData` / `DisplaySettingsData`，深拷贝 |
| `Scripts/Settings/PlayerSettingsDefaults.cs` | 默认值 SO，`CreateDefault()` |
| `Scripts/Settings/PlayerSettingsValidator.cs` | 音量、分辨率、全屏、帧率、画质钳制 |
| `Scripts/Settings/ISettingsStore.cs` | 存档接口 |
| `Scripts/Settings/JsonSettingsStore.cs` | JSON 读写、`.corrupt` 备份、临时文件写入 |
| `Scripts/Settings/PlayerSettingsMigration.cs` | `version` 迁移入口（当前 v1） |
| `Scripts/Settings/PlayerSettingsService.cs` | 静态门面、Applier 顺序、Changed 事件 |
| `Scripts/Settings/SettingsApplier.cs` | `SettingsSections`、`ISettingsApplier`、`SettingsApplier` |
| `Scripts/Settings/PlayerSettingsBootstrap.cs` | 场景启动、单例、Applier 收集、退出保存 |
| `Scripts/Settings/Appliers/DisplaySettingsApplier.cs` | 显示设置应用示例 |

### 新增编辑器

| 文件 | 职责 |
|---|---|
| `Scripts/Editor/SettingsFactory.cs` | 创建默认值资产；已存在不覆盖 |

### 修改

| 文件 | 修改 |
|---|---|
| `Scripts/Editor/EditorAssets.cs` | 新增 `PlayerSettingsDefaultsPath` |
| `Scripts/Editor/BuildPipeline.cs` | BuildPrefabs / BuildAll / BuildArena / 两个 Batch 路径调用 `SettingsFactory` |
| `Scripts/Editor/BuildCheck.cs` | 新增 `ValidateSettings()`，检查默认值资产与规范化 |
| `Docs/agent/README.md` | 新增 Settings 索引 |

### 生成资产

| 资产 | 来源 |
|---|---|
| `Assets/Data/Settings/PlayerSettingsDefaults.asset` | `SettingsFactory.EnsureDefaultSettings()` |

### 测试

| 文件 | 覆盖 |
|---|---|
| `Assets/Tests/EditMode/PlayerSettingsTests.cs` | 深拷贝、校验、JSON 存档、Service、Applier 顺序、默认资产 |

---

## 2. 设计决策

### 2.1 玩家设置与内容配置分离

- `Assets/Scripts/Config` 仍是内容定义：`BattleConfig`、`CharacterConfig`、`ProjectileConfig`、`ArenaConfig` 等；
- `Assets/Scripts/Settings` 只保存玩家偏好；
- 不建立内容 Catalog，也不让玩家设置引用 prefab / Sprite / Animator。

理由：子弹、角色需要绑定个性化资产，prefab + 自身 SO 更合适；玩家设置只需要值、默认值、应用逻辑。

### 2.2 类型化设置数据，不做通用 key-value

`PlayerSettingsData` 是明确的强类型结构：

- Inspector 默认值可读性高；
- `JsonUtility` 可直接序列化；
- 编译期能发现字段错误；
- 新增设置项需要改数据类 + 默认值 + Applier，这是有意的显式成本，避免反射/泛型配置中心。

### 2.3 默认值 SO + 玩家值 JSON

- SO 只读，避免运行时改资产；
- JSON 支持版本、嵌套、损坏备份，比 `PlayerPrefs` 更适合后期扩展；
- `ISettingsStore` 允许替换成 `PlayerPrefs`、云存档或测试内存实现。

### 2.4 Service 静态门面

沿用项目 `GameEvents` 的静态访问方式。测试通过 `Shutdown/ResetForTests` 清理静态状态；生命周期由 `PlayerSettingsBootstrap` 管理。

`Current` 返回深拷贝，避免调用方绕过 `Apply` 修改内部值。

---

## 3. 数据与存档

### 数据字段

- 音频：`masterVolume`、`musicVolume`、`sfxVolume`、`mute`；
- 显示：`fullscreenMode`、`resolutionWidth/Height`、`vSync`、`frameRateLimit`、`qualityLevel`；
- 根：`version`、`audio`、`display`。

### JSON 路径

`Path.Combine(Application.persistentDataPath, "NineDays", "player-settings.json")`

### 关键实现点

- `JsonSettingsStore.Load`：
  - 文件不存在 → 默认值；
  - `JsonUtility.FromJson` 结果为空或异常 → 默认值 + `.corrupt` 备份；
  - 成功后调用 `PlayerSettingsMigration.Upgrade`。
- `Save`：
  - 创建目录；
  - `JsonUtility.ToJson(data, true)`；
  - 写 `.tmp`；
  - 已存在目标时 `File.Replace`，失败回调覆盖写入。
- `PlayerSettingsValidator.Normalize`：
  - 初始化空的 `audio/display`；
  - 音量 `Clamp01`；
  - 分辨率一对为 0 时都归零，否则钳制 320~16384；
  - 帧率 0 或 30~240；
  - 画质 -1 或 `QualitySettings.names` 合法下标。

---

## 4. Service 生命周期

```text
PlayerSettingsBootstrap.Awake
  → PlayerSettingsService.Initialize(defaults, JsonSettingsStore, appliers)
      → store.Load(defaults.CreateDefault())
      → PlayerSettingsMigration.Upgrade
      → Validator.Normalize
      → ApplyAppliers（按 Order）
```

- `Initialize` 重复调用先 `Shutdown()`；
- `Current` 未初始化时抛 `InvalidOperationException`；
- `Apply` / `Update`：
  - 克隆当前值；
  - 校验；
  - 替换 current；
  - 应用 Applier；
  - 可选保存；
  - 触发 `Changed`。
- `Shutdown` 清空 defaults、store、appliers、current 和 `Changed` 订阅；
- `PlayerSettingsBootstrap.OnApplicationQuit` 调 `Save`；
- `PlayerSettingsBootstrap.OnDestroy` 调 `Shutdown`。

注意：`Changed` 是全局静态事件，测试必须在 TearDown 调 `Shutdown`，否则订阅会残留。

---

## 5. Applier 与 Bootstrap

### Applier

```csharp
public interface ISettingsApplier
{
    string Section { get; }
    int Order { get; }
    void Apply(PlayerSettingsData data);
}
```

- `SettingsApplier : MonoBehaviour` 是场景组件基类；
- `Order` 升序执行；相同 Order 的排序不保证稳定；
- 单个 Applier 抛异常：记录 `Debug.LogError`，继续执行其他 Applier。

### DisplaySettingsApplier

- 设置 `Screen.SetResolution` 或 `Screen.fullScreenMode`；
- `QualitySettings.vSyncCount`；
- `Application.targetFrameRate`；
- `QualitySettings.SetQualityLevel`；
- `applyInEditor` 默认 false，避免 Play Mode 改 Game 视图；
- 未加入任何场景，需要时手动挂载。

### Bootstrap

- `[DefaultExecutionOrder(-1000)]`；
- 单例，重复实例直接销毁；
- `defaults` 为空时报错，不初始化 Service；
- `autoCollectAppliers && appliers.Count == 0` 时从子节点收集；
- 手动列表支持把 Applier 放在别处。

---

## 6. 编辑器/构建集成

- `EditorAssets.PlayerSettingsDefaultsPath = "Assets/Data/Settings/PlayerSettingsDefaults.asset"`；
- `SettingsFactory.EnsureDefaultSettings()`：
  - 已有资产直接返回，不覆盖手调默认值；
  - 新建时写入音频/显示默认值；
- `BuildPipeline` 的 BuildPrefabsMenu / BuildAllMenu / BuildArenaMenu / BuildPrefabsBatch / BuildAllBatch 都会调用 SettingsFactory；
- `BuildCheck.ValidateSettings()`：
  - 默认资产存在；
  - `audio/display` 分组存在；
  - `PlayerSettingsValidator.Normalize` 成功；
- `SettingsFactory` 只创建缺失默认资产，不覆盖已有默认值；内容配置生成仍由 `PrefabFactory` 负责，二者的覆盖行为不要混淆。

---

## 7. 测试

### EditMode

`PlayerSettingsTests` 覆盖：

1. 默认资产存在且可通过校验；
2. `Clone` 深拷贝；
3. Validator 钳制音量、分辨率、全屏枚举、帧率、画质；
4. JSON 文件不存在 → 默认值；
5. JSON 保存/加载往返；
6. 损坏 JSON → 备份 + 默认值；
7. Service 初始化并应用 Applier；
8. `Update` 应用、保存、触发 `Changed`；
9. `ResetToDefaults`；
10. Applier 按 `Order` 执行；
11. 未初始化访问 `Current` 抛异常。

结果：**EditMode 76/76 Passed**（2026-11-04，全量）。

### PlayMode

现有 PlayMode 测试不依赖 Settings 场景对象；全量回归 **33/33 Passed**（2026-11-04）。

### 构建

`BuildAllBatch`：**BUILD_OK**，`BuildCheck` 通过，默认设置资产已生成。

---

## 8. 已知限制与待办

- [ ] 暂无设置页面；后续页面应通过 `PlayerSettingsService.Update/Apply` 和 `Current` 绑定；
- [ ] 暂无音频 Applier；等 AudioMixer / 音频模块后读取 `Current.audio`；
- [ ] 暂无语言、按键重绑定、Gameplay 设置；
- [ ] 当前只支持 v1 存档迁移入口，无实际旧版本；
- [ ] `PlayerSettingsDefaults.SetValues` 是公开方法，运行时按约定不应调用；
- [ ] Bootstrap 未自动放入 TestArena，因为当前没有设置页面；
- [ ] 新增数据结构后，旧 JSON 缺字段会得到 C# 默认值；需要时补 `PlayerSettingsMigration`；
- [ ] 没有测试实际 `Screen.SetResolution` 与音频效果，避免编辑器/CI 环境副作用；
- [ ] `PrefabFactory` 仍会重写内容配置（角色/战斗/子弹）；玩家设置默认值不受影响，但调内容数值时不要顺手跑 `Build/Prefabs`。

---

## 9. 接手检查清单

1. 先读 `PlayerSettingsData`、`PlayerSettingsService`、`JsonSettingsStore`；
2. 改设置项：数据类 → 默认值资产 → Validator → Applier → UI（以后）；
3. 新增分组：数据类 + `PlayerSettingsData.Clone` + Validator + Applier；
4. 新存储：实现 `ISettingsStore`，Service 不变；
5. 场景接入：启动场景放 `PlayerSettingsBootstrap`，指定默认值资产和 Applier；
6. 跑 `BuildAllBatch` 确认 `BUILD_OK`；
7. 跑 EditMode / PlayMode 全量测试；
8. 提交前确认没有把 `Temp/`、`Library/` 加入 git。

