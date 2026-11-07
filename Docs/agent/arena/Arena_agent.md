# 场景模块（Agent 记录）

> 面向后续 agent：记录本次改了什么、为什么、涉及文件与函数、坑与待办、验证方式。
> 人读版见 `Arena.md`。

## 1. 本次改动清单

| 文件 | 类型 | 内容 |
|---|---|---|
| `Scripts/Config/ArenaConfig.cs` | 新增 | 地形场地、分辨率、层列表；`Validate` |
| `Scripts/Config/TerrainLayerData.cs` | 新增 | 层数据、美术快照、遮罩、可破坏性 |
| `Scripts/Config/TerrainBreakProfile.cs` | 新增 | 破坏力→范围线性/曲线映射 |
| `Scripts/Scene/TerrainBreakTypes.cs` | 新增 | `TerrainBreakRequest/Result`、形状、来源标记 |
| `Scripts/Scene/ArenaController.cs` | 新增 | 运行时搭建、抹除、追加、并集、`Instance` |
| `Scripts/Scene/TerrainLayer.cs` | 新增 | 单层遮罩、视觉、局部擦除/追加 |
| `Scripts/Scene/TerrainPhysics.cs` | 新增 | 轮廓子对象 + EdgeCollider2D + 静态 Rigidbody2D |
| `Scripts/Scene/TerrainColliderBuilder.cs` | 新增 | 遮罩→边界边→闭环→RDP 简化 |
| `Scripts/Scene/TerrainMask.shader` | 新增 | 世界坐标采样遮罩的 URP Sprite Shader |
| `Scripts/Scene/ArenaAuthoring.cs` | 新增 | 编辑场景入口 |
| `Scripts/Scene/TerrainLayerAuthoring.cs` | 新增 | 编辑期层设置 |
| `Scripts/Editor/ArenaTerrainBaker.cs` | 新增 | 扫描编辑场景、光栅化 Sprite alpha、写配置/遮罩 |
| `Scripts/Editor/ArenaTerrainFactory.cs` | 新增 | 默认配置、遮罩、材质生成 |
| `Scripts/Editor/ArenaBuilder.cs` | 修改 | 删除静态 Ground/Obstacle_Block；加 ArenaController；BattleController.arena |
| `Scripts/Battle/BattleController.cs` | 修改 | 新增 arena 引用；BeginBattle 先 BuildForBattle |
| `Scripts/Events/GameEvents.cs` | 修改 | `TerrainBuilt/TerrainBroken/TerrainAppended` |
| `Scripts/Editor/EditorAssets.cs` | 修改 | ArenaConfig/Mask/Material/Authoring 路径 |
| `Scripts/Editor/BuildCheck.cs` | 修改 | 配置、遮罩尺寸、场景无静态地形 |
| `Scripts/Editor/BuildPipeline.cs` | 修改 | 构建前 EnsureDefaultConfig |
| `Assets/Tests/EditMode/TerrainColliderBuilderTests.cs` | 新增 | 轮廓/洞/RDP |
| `Assets/Tests/EditMode/TerrainBreakProfileTests.cs` | 新增 | 范围映射 |
| `Assets/Tests/EditMode/DefaultArenaConfigTests.cs` | 新增 | 默认配置遮罩可读、可搭建 |
| `Assets/Tests/PlayMode/TerrainTests.cs` | 新增 | 搭建/门槛/范围/追加/重开/射线 |

**场景模块首次落地时未修改** `Projectile` / `ProjectileConfig` / 爆发相关代码，只留接口；后续子弹地形破坏接入见 `Docs/agent/bullet/Bullet_agent.md` §0。

---

## 2. 设计决策与理由

1. **不用 Tilemap 做主方案**：美术是整层图，Tilemap 会强迫格子化或维护隐藏碰撞层；改为世界空间遮罩 + 轮廓。
2. **每层独立遮罩**：层可以重复堆叠；可破坏性/破坏力阈值按层配置。
3. **物理用并集**：如果每层各自生成碰撞，层之间会产生内部边；先把所有 `collisionEnabled` 层 OR 成 global mask，再提取轮廓。
4. **视觉与物理分离**：视觉用 `TerrainMask` Shader 采样层遮罩；物理只读并集遮罩。
5. **破坏请求不绑定爆发**：`TerrainBreakKind` 只是标记；`ApplyTerrainBreak` 是唯一入口。
6. **范围独立可调**：`Radius` 与 `BreakPower` 分开；需要联动时用 `TerrainBreakProfile`。
7. **运行时全部重建**：`ArenaBuilder` 不再生成静态地形；`TestArena` 只有 `ArenaController`；`BattleController.BeginBattle` 触发重建。
8. **层级结构**：`TerrainLayer` 在视觉根上；`TerrainPhysics` 单独一个根，子对象是每个轮廓的 EdgeCollider2D。

---

## 3. 关键实现细节

### 3.1 遮罩→轮廓

`TerrainColliderBuilder.BuildContours`：

- 对每个实心像素，若上/右/下/左邻居为空，加入一条有向边
- 用 `Dictionary<Vector2Int, List<Vector2Int>>` 追溯闭环
- 分叉时取第一条，剩余边留给后续环
- 世界坐标 = `origin + 格子坐标 * pixelSize`
- `Simplify` 用迭代 RDP，保留首尾同一点

坑：

- `EdgeCollider2D` **不能** `usedByComposite = true`。Unity 报 `not valid as the collider is not capable of being composited`；已移除 CompositeCollider2D，改为多个独立 EdgeCollider2D。
- 洞口需要闭环；测试覆盖了实心矩形、中心挖洞、空遮罩。
- 轮廓是边界不是填充体：`Physics2D.OverlapPoint` 在实心内部不一定命中，测试用 `Physics2D.Raycast` 验证上下边界。

### 3.2 遮罩坐标

- 遮罩覆盖 `ArenaConfig.worldRect`，像素中心映射到世界
- Shader 用世界坐标算 UV，所以同一层多个 Sprite 可以共享一张遮罩
- `TerrainLayer.WorldToPixel` 与 Shader 的映射保持一致

### 3.3 视觉

- `TerrainMask.shader` 是 URP Unlit Sprite Pass；采样 `_MainTex` 与 `_MaskTex`
- 每层 `new Material(baseMaterial)`，设置 `_MaskTex`、`_WorldRect`、`_Cutoff`
- `TerrainLayer` 重建 `SpriteRenderer` 时保留 sprite/transform/color/sorting/flip/drawMode
- Bake 只支持 `SpriteDrawMode.Simple`；`Tiled/Sliced` 会抛异常，避免视觉与遮罩不一致

### 3.4 Bake

- `ArenaTerrainBaker.Bake` 扫描 `TerrainLayerAuthoring`
- 收集 `SpriteRenderer`，记录世界坐标/旋转/缩放/排序
- `RasterizePiece` 按 sprite `textureRect`、`bounds`、flip、scale、rotation 映射像素（不依赖 pivot，避免导入 sprite 的 pivot/网格差异）
- 贴图不可读时自动改 `TextureImporter.isReadable = true`
- 遮罩写成 `Texture2D` 资产（`m_IsReadable: 1`）
- 默认配置生成器不会覆盖已存在的 `ArenaConfig`

### 3.5 默认配置

`ArenaTerrainFactory.EnsureDefaultConfig` 生成 Ground（不可破坏）、Wood（required 1）、Stone（required 3），用于没有人工 Bake 时的 P0 演示和测试。`BuildAll` 会调用它。

---

## 4. 兼容性与约束

| 约束 | 说明 |
|---|---|
| `TerrainBreakRequest.Radius` | 世界单位；`maxBreakRadius` 与场地边界会裁剪 |
| `BreakPower` | 只和层 `requiredBreakPower` 比较，不影响角色伤害 |
| `TerrainLayerData.art` | 运行时视觉快照；不要运行时改 SO |
| `ArenaController.BuildForBattle` | 必须在角色回出生点之前调用；`BattleController` 已保证 |
| `ArenaController.Instance` | 场景单例；供未来道具/子弹效果调用 |
| `ArenaConfig` 是运行时只读 | 运行时改的是 `TerrainLayer` 的遮罩副本 |
| 遮罩尺寸 | 所有层必须与 `ArenaConfig.MaskWidth/MaskHeight` 一致；BuildCheck 校验 |
| 场景无静态地形 | `BuildCheck.ValidateSceneArena` 断言 TestArena 无 `TerrainLayer/TerrainPhysics` |
| 运动体 | EdgeCollider2D 是边界；高速子弹已用 Continuous，角色接地检测可命中边界 |

---

## 5. 已知限制与待办

- [ ] `Projectile` 直接命中尚未调用 `ArenaController.ApplyTerrainBreak`；`BurstTerrainEffect` 已接入
- [ ] `TerrainBreakKind.Burst` 只是来源标记；爆发框架本身在 `Scripts/Burst`，场景模块不实现爆发逻辑
- [ ] `AppendTerrain` 只有圆形/矩形，没有任意遮罩形状
- [ ] Bake 只支持 `SpriteDrawMode.Simple`；Tiled/Sliced 需要拆分
- [ ] 轮廓每次重建是整层重算；当前场地小，未做脏区域优化
- [ ] `EdgeCollider2D` 是边界而非实体填充；角色从洞内向上跳/在实体内部出生需要额外规则
- [ ] `ArenaBounds` 只处理出界，不处理“掉进场地内部洞”
- [ ] 多层遮罩尺寸不同会被采样，但 BuildCheck 会阻止默认配置进入该状态
- [ ] 未做地形碎屑/断面 Shader/SFX；仅保留 `TerrainBroken` 事件
- [ ] `TerrainBreakProfile` 目前只给外部调用方使用，场景模块不自动套用

---

## 6. 验证方式与结果

```bat
Unity.exe -batchmode -quit -projectPath <项目> -executeMethod NineDays.EditorTools.BuildPipeline.BuildAllBatch
Unity.exe -batchmode -nographics -projectPath <项目> -runTests -testPlatform EditMode
Unity.exe -batchmode -projectPath <项目> -runTests -testPlatform PlayMode
```

2026-11-05 实测：

| 项 | 结果 |
|---|---|
| `BuildAllBatch` | `BUILD_OK`，结构校验通过 |
| Bake 冒烟 | 临时复制项目内 Sprite → Bake → 遮罩非空（通过后已清理临时资产） |
| EditMode | **81/81 Passed**（全量） |
| PlayMode | **34/34 Passed**（全量） |

新增用例：

- `TerrainColliderBuilderTests`：实心矩形、中心洞、空遮罩、RDP
- `TerrainBreakProfileTests`：线性、上限、请求半径
- `DefaultArenaConfigTests`：默认遮罩可读、可搭建、有轮廓
- `TerrainTests`：搭建、擦除、门槛、不可破坏层、范围差异、追加、重开、射线边界

### 手动验收

1. `TestArena` Play：默认 Ground/Wood/Stone 出现
2. 调用 `ApplyTerrainBreak`，确认对应层消失
3. `BreakPower` 不足时范围再大也不动
4. `AppendTerrainCircle` 补回
5. `R` 重开恢复初始地形

---

## 7. 文件与函数索引

| 关注点 | 位置 |
|---|---|
| 配置校验 | `ArenaConfig.Validate` |
| 遮罩→轮廓 | `TerrainColliderBuilder.BuildContours` |
| 轮廓简化 | `TerrainColliderBuilder.Simplify` |
| 单层擦除 | `TerrainLayer.EraseCircle / EraseBox` |
| 单层追加 | `TerrainLayer.AppendCircle / AppendBox` |
| 并集 + 重建 | `ArenaController.RefreshPhysics` |
| 对外破坏入口 | `ArenaController.ApplyTerrainBreak` |
| 对外追加入口 | `ArenaController.AppendTerrainCircle / AppendTerrainBox` |
| 碰撞判定 | `ArenaController.IsTerrainCollider` |
| 编辑期 Bake | `ArenaTerrainBaker.Bake` |
| 默认配置 | `ArenaTerrainFactory.EnsureDefaultConfig` |
| 场景生成 | `ArenaBuilder.Build` |
| 战斗接入 | `BattleController.BeginBattle` |

