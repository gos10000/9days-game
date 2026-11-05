# 场景模块（人读版）

> 一句话：**场景是程序重建的分层遮罩地形；破坏是在遮罩上挖洞，再用轮廓重建物理边界。**

## 目录

1. [模块概览](#1-模块概览)
2. [数据资产](#2-数据资产)
3. [编辑期：人工摆层 → Bake](#3-编辑期人工摆层--bake)
4. [运行时搭建](#4-运行时搭建)
5. [破坏接口](#5-破坏接口)
6. [追加地形接口](#6-追加地形接口)
7. [可调参数](#7-可调参数)
8. [测试与验收](#8-测试与验收)
9. [已知限制与排错](#9-已知限制与排错)
10. [文件索引](#10-文件索引)

---

## 1. 模块概览

```
编辑期
  ArenaAuthoring.unity
    └─ 多层 Sprite 美术（可重复堆叠）
         ↓ Bake
  ArenaConfig.asset + 每层遮罩 Texture2D

运行时
  BattleController.BeginBattle()
    └─ ArenaController.BuildForBattle()
         ├─ 按层创建 SpriteRenderer（遮罩材质）
         ├─ 复制初始遮罩
         ├─ 并集所有 collisionEnabled 层
         └─ 从并集提取轮廓 → EdgeCollider2D

破坏
  TerrainBreakRequest
    └─ ApplyTerrainBreak()
         ├─ 按 BreakPower 过滤可破坏层
         ├─ 在层遮罩上挖圆形/矩形
         ├─ 重新并集
         └─ 重建轮廓碰撞
```

设计目标：

- 美术是一层一层的图，不要求逐格 Tile
- 地面/斜坡由美术 alpha 决定，碰撞自动贴合
- 运行时全部程序重建，编辑场景不保留静态地形
- 破坏力与角色伤害完全独立
- 破坏范围是独立可调参数，也可以通过 Profile 与破坏力联动

---

## 2. 数据资产

### `ArenaConfig`

路径：`Assets/Data/Arena/ArenaConfig_Default.asset`

| 字段 | 类型 | 含义 |
|---|---|---|
| `worldRect` | Rect | 地形遮罩覆盖的世界范围 |
| `pixelsPerUnit` | int | 遮罩分辨率（像素/世界单位） |
| `layers` | List<TerrainLayerData> | 地形层列表 |

派生：

- `MaskWidth = worldRect.width * pixelsPerUnit`
- `MaskHeight = worldRect.height * pixelsPerUnit`
- `PixelSize = worldRect.width / MaskWidth`

### `TerrainLayerData`

| 字段 | 类型 | 含义 |
|---|---|---|
| `layerId` | string | 层 id，追加接口用它找层 |
| `destructible` | bool | 是否可被抹除 |
| `requiredBreakPower` | float | 抹除该层所需最低破坏力 |
| `collisionEnabled` | bool | 是否参与物理并集 |
| `alphaThreshold` | float | Bake 时 alpha 阈值 |
| `initialMask` | Texture2D | Bake 出的初始遮罩 |
| `art` | List<TerrainArtPiece> | 运行时重建视觉的 Sprite 列表 |

### `TerrainArtPiece`

一个普通 `SpriteRenderer` 的 Bake 快照：`sprite`、位置/旋转/缩放、颜色、排序、翻转、drawMode/size/tileMode。运行时按这些字段重建视觉，但材质会替换成地形遮罩材质。

### `TerrainBreakProfile`（可选）

| 字段 | 含义 |
|---|---|
| `baseRadius` | 基础范围 |
| `radiusPerPower` | 每点破坏力增加的范围 |
| `minRadius` / `maxRadius` | 范围夹取 |
| `useCurve` | 是否使用曲线 |
| `radiusByPower` | 横轴破坏力、纵轴范围 |

默认线性：

```text
radius = baseRadius + breakPower * radiusPerPower
```

如果希望破坏力和范围独立调，不创建 Profile，直接给 `TerrainBreakRequest.Radius` 赋值即可。

---

## 3. 编辑期：人工摆层 → Bake

### 3.1 创建编辑场景

菜单 `9Days/Arena/Create Authoring Scene` 生成：

```
Assets/Scenes/ArenaAuthoring.unity
 └─ ArenaAuthoring
     └─ Layers
```

`ArenaAuthoring`：`config`（可空）、`layersRoot`、`worldRect`、`pixelsPerUnit`。

### 3.2 摆层

在 `Layers` 下建层根节点，挂 `TerrainLayerAuthoring`：

| 字段 | 含义 |
|---|---|
| `layerId` | 唯一 id |
| `destructible` | 是否可破坏 |
| `requiredBreakPower` | 破坏力门槛 |
| `collisionEnabled` | 是否参与物理 |
| `alphaThreshold` | alpha 阈值 |

层根下面可以放任意多个普通 `SpriteRenderer`，重复堆叠形成地面。

要求：

- 使用 `SpriteRenderer.drawMode = Simple`
- 不要把 `Tiled` / `Sliced` 放在需要 Bake 遮罩的层里
- Sprite 贴图需要可读；Bake 会自动尝试开启 `Read/Write`

### 3.3 Bake

菜单 `9Days/Arena/Bake Terrain`：

1. 读取所有 `TerrainLayerAuthoring`
2. 收集层内所有 `SpriteRenderer`
3. 把每个 Sprite 的不透明像素映射到世界遮罩
4. 保存 `Assets/Data/Arena/Masks/<layerId>_Mask.asset`
5. 写入 `ArenaConfig`

> `BuildAll` 只在 `ArenaConfig` 缺失时生成默认配置；已有配置不会被覆盖。

---

## 4. 运行时搭建

### 4.1 入口

`BattleController.BeginBattle()`：

```text
校验 participants
→ arena.BuildForBattle()
→ characters.ResetForBattle()
→ new BattleSession(...)
```

先搭地形，再回出生点，避免出生在未重建的地形里。`RestartBattle()` 会再次调用 `BeginBattle()`，所以重开也会恢复地形。

### 4.2 `ArenaController`

| 字段 | 含义 |
|---|---|
| `config` | ArenaConfig |
| `bounds` | ArenaBounds，可空 |
| `terrainMaterial` | 遮罩材质 |
| `obstacleLayerName` | 物理层，默认 Obstacle |
| `simplifyTolerance` | 轮廓简化容差 |
| `edgeRadius` | EdgeCollider2D 厚度 |
| `maxBreakRadius` | 单次最大破坏半径 |
| `logBuild` | 重建日志 |

对外：

- `BuildForBattle()`
- `ApplyTerrainBreak(in TerrainBreakRequest)`
- `AppendTerrainCircle(layerId, center, radius)`
- `AppendTerrainBox(layerId, center, size)`
- `GetLayer(layerId)`
- `IsTerrainCollider(Collider2D)`
- `ClearRuntime()`
- `Instance`

### 4.3 `TerrainLayer`

每层运行时对象：

- 创建运行时遮罩（复制 initialMask）
- 创建每层遮罩材质
- 重建层内所有 SpriteRenderer
- `EraseCircle` / `EraseBox`
- `AppendCircle` / `AppendBox`

视觉：自定义 `NineDays/TerrainMask` Shader 按世界坐标采样遮罩，遮罩黑的地方透明。

### 4.4 `TerrainPhysics`

物理根：

- `Rigidbody2D`（Static）
- 每个轮廓一个子对象 + `EdgeCollider2D`
- 每次破坏/追加后清除旧轮廓、重建新轮廓

> 没有使用 `CompositeCollider2D`：Unity 不允许 `EdgeCollider2D` 参与 Composite。每个闭合轮廓单独一个 EdgeCollider2D，边界仍然连续。

---

## 5. 破坏接口

### 5.1 请求

```csharp
TerrainBreakRequest request = TerrainBreakRequest.Circle(
    breakPower: 3f,
    radius: 1.5f,
    point: hitPoint,
    source: gameObject,
    kind: TerrainBreakKind.Burst);

arena.ApplyTerrainBreak(request);
```

| 字段 | 含义 |
|---|---|
| `BreakPower` | 决定能破坏哪些层 |
| `Radius` | 圆形范围（世界单位） |
| `Shape` | Circle / Box |
| `Size` | Box 尺寸 |
| `Point` / `Normal` | 命中位置/法线 |
| `Source` | 来源对象，可空 |
| `Kind` | Impact / Burst / Item / Other，仅标记 |

结果：

```csharp
TerrainBreakResult
{
    int AffectedLayers;
    int AffectedPixels;
    bool AnythingChanged;
}
```

规则：

- `!destructible` 层永远不动
- `BreakPower < requiredBreakPower` 的层不动
- 满足条件的层在 `Radius`/`Size` 范围内清除像素
- 范围会被 `maxBreakRadius` 和场地边界裁剪
- 一次请求可以同时破坏多层

### 5.2 与爆发的关系

场景模块**不实现爆发逻辑**；`BurstTerrainEffect` 已通过 `TerrainBreakRequest` 调用 `ApplyTerrainBreak`，其他调用方也走同一入口：

```csharp
arena.ApplyTerrainBreak(new TerrainBreakRequest(
    burst.BreakPower,
    burst.Radius,
    TerrainBreakShape.Circle,
    default,
    burstPoint,
    Vector2.zero,
    source,
    TerrainBreakKind.Burst));
```

---

## 6. 追加地形接口

```csharp
arena.AppendTerrainCircle("Wood", center, radius);
arena.AppendTerrainBox("Stone", center, size);
```

行为：

- 在指定层遮罩上画回白色
- 重新并集并重建轮廓
- 返回实际写入像素数
- 层不存在时返回 0

> 目前只提供圆形/矩形。更复杂的形状可以后续增加 `AppendTerrainMask`，不影响现有接口。



---

## 7. 可调参数

| 位置 | 参数 | 作用 |
|---|---|---|
| `ArenaConfig` | `worldRect` | 场地范围 |
| `ArenaConfig` | `pixelsPerUnit` | 遮罩精度；越高越精细、越耗内存 |
| `TerrainLayerData` | `destructible` | 该层是否可破坏 |
| `TerrainLayerData` | `requiredBreakPower` | 该层破坏力门槛 |
| `TerrainLayerData` | `collisionEnabled` | 是否参与碰撞 |
| `TerrainLayerAuthoring` | `alphaThreshold` | Bake 阈值 |
| `ArenaController` | `maxBreakRadius` | 防止一次抹掉全场 |
| `ArenaController` | `simplifyTolerance` | 轮廓简化，越大点越少 |
| `ArenaController` | `edgeRadius` | 边界碰撞厚度 |
| `TerrainBreakProfile` | `baseRadius` / `radiusPerPower` / `min` / `max` | 破坏力→范围联动 |
| `TerrainBreakProfile` | `useCurve` / `radiusByPower` | 曲线联动 |

所有影响游戏表现的参数都在 Inspector / SO 中，不硬编码。

---

## 8. 测试与验收

### 自动测试

| 类型 | 内容 |
|---|---|
| EditMode | 轮廓提取、挖洞双轮廓、RDP 简化、破坏范围 Profile、默认配置搭建 |
| PlayMode | 搭建层、破坏力门槛、不可破坏层、范围大小、追加、重开恢复、射线命中边界 |

当前结果（全量基线）：

- EditMode **81/81**
- PlayMode **34/34**

### 手动验收

1. 打开 `TestArena`，Play。
2. 战斗开始后应出现默认地形（地面 + Wood + Stone）。
3. 默认配置下 Wood 需要破坏力 1，Stone 需要 3。
4. 通过测试/调试代码调用 `ApplyTerrainBreak`：
   - `BreakPower=1` 只削 Wood
   - `BreakPower=3` 同时削 Wood/Stone
   - Ground 永远不变
5. 调整 `Radius` 验证范围变化。
6. 调用 `AppendTerrainCircle` 验证补地形。
7. 按 `R` 重开，地形恢复。

> 当前 `Projectile` 直接命中还没有接地形破坏；场景模块只提供接口。
> `BurstTerrainEffect` 已通过 `ArenaController.ApplyTerrainBreak` 接入；以后子弹命中效果也走同一入口。

---

## 9. 已知限制与排错

| 现象 | 原因 / 处理 |
|---|---|
| Bake 报 `DrawMode` 不支持 | 把 `Tiled`/`Sliced` 拆成多个普通 Sprite |
| Bake 报贴图不可读 | 工具会尝试开启 Read/Write；内置贴图无法改，请用项目内 Sprite |
| 遮罩有锯齿/范围不准 | 提高 `pixelsPerUnit`；重跑 Bake |
| 地形视觉有洞但碰撞还在 | 确认该层 `collisionEnabled`，并检查 `BuildCheck`；调用后会重建 |
| 地形碰撞有缝 | 提高遮罩分辨率；`EdgeCollider2D` 是边界，不是填充体 |
| 角色掉进洞里出不来 | 现有 `ArenaBounds` 只处理出界；洞内跌落属于玩法，需要额外复活/死亡规则 |
| 重开地形没恢复 | 确认 `BattleController.BeginBattle` 调用 `BuildForBattle`，且 `ArenaConfig` 初始遮罩未被运行时修改 |

---

## 10. 文件索引

| 关注点 | 位置 |
|---|---|
| 地形数据 | `Scripts/Config/ArenaConfig.cs` |
| 层数据 / 美术快照 | `Scripts/Config/TerrainLayerData.cs` |
| 破坏力→范围 Profile | `Scripts/Config/TerrainBreakProfile.cs` |
| 破坏请求/结果 | `Scripts/Scene/TerrainBreakTypes.cs` |
| 运行时搭建/接口 | `Scripts/Scene/ArenaController.cs` |
| 单层遮罩/视觉 | `Scripts/Scene/TerrainLayer.cs` |
| 轮廓碰撞 | `Scripts/Scene/TerrainPhysics.cs` |
| 遮罩→轮廓算法 | `Scripts/Scene/TerrainColliderBuilder.cs` |
| 遮罩 Shader | `Scripts/Scene/TerrainMask.shader` |
| 编辑期组件 | `Scripts/Scene/ArenaAuthoring.cs` / `TerrainLayerAuthoring.cs` |
| Bake 工具 | `Scripts/Editor/ArenaTerrainBaker.cs` |
| 默认配置生成 | `Scripts/Editor/ArenaTerrainFactory.cs` |
| 场景生成 | `Scripts/Editor/ArenaBuilder.cs` |
| 结构校验 | `Scripts/Editor/BuildCheck.cs` |

