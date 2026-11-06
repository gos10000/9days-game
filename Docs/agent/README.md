# Agent 文档索引

按「一个工作内容一个文件夹，两份 md」组织：

| 工作内容 | 人读版 | Agent 记录 | 内容 |
|---|---|---|---|
| 角色 | `character/Character.md` | `character/Character_agent.md` | 角色 prefab、状态机、受击/击退、撞击事件、发射、回合 |
| 战斗轮 | `battle/Battle.md` | `battle/Battle_agent.md` | 血量制、9 轮、开局随机先手并整场沿用、额外行动、行动/轮间间隔、回合常驻 HUD、自动验收 |
| 皮肤与动画 | `skin/Skin.md` | `skin/Skin_agent.md` | 皮肤配置、切换流程、Animator 契约、序列帧/骨骼 |
| 子弹 | `bullet/Bullet.md` | `bullet/Bullet_agent.md` | 原型脚本、数值副本、特性组合、发射修改器、飞行影响场、穿透、对象池 |
| 子弹爆发 | `burst/Burst.md` | `burst/Burst_agent.md` | 触发/冷却/重复、伤害/牵引/地形效果、动画事件接口 |
| UI 框架 | `ui/UI.md` | `ui/UI_agent.md` | UIRoot、Canvas 五层、页面栈/弹窗、缓存、过渡、页面扩展 |
| 场景地形 | `arena/Arena.md` | `arena/Arena_agent.md` | 分层遮罩地形、Bake、运行时重建、破坏力/范围、轮廓碰撞、追加接口 |
| 镜头控制 | `camera/Camera.md` | `camera/Camera_agent.md` | 2D 正交取景、开场 P1→P2 特写、回合/子弹目标、场地夹取、平滑、trauma 震动 |
| 玩家设置 | `settings/Settings.md` | `settings/Settings_agent.md` | 默认值 SO、JSON 存档、Service、Applier 扩展、版本迁移；暂不做设置页 |
| 资源生成与调试工具 | `tooling/Tooling.md` | `tooling/Tooling_agent.md` | 菜单/batchmode、层配置、测试场景、HUD、结构校验 |

约定：

- 人读版：简洁、可读，覆盖使用所需的全部信息（出入参、参数表、怎么跑、怎么排错）
- `_agent` 版：详细准确，记录决策理由、实现顺序、验证证据、坑与待办，供后续 agent 接手
- 方案类说明在 `Docs/program/`（**由人维护，agent 不修改**）
