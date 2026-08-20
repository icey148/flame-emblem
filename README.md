# Flame Emblem

个人用原创 2D 战术 RPG 项目，玩法方向参考经典格子制 SRPG，但角色、剧情、地图、UI、音频和美术素材保持原创。

## 当前技术基线

- Engine: Godot 4.7.1 .NET edition
- Language: C# / .NET 8
- Runtime: 纯本地单机
- Enemy behavior: 固定规则 / 确定性游戏逻辑，不接入 LLM、生成式 AI 或联网 AI 服务
- Prototype rendering: 当前地图、单位和范围高亮均由代码绘制，不依赖外部商业素材

## 当前可玩内容

开发分支：`feature/playable-srpg-foundation`

目前已经实现第二阶段战斗骨架：

- JSON 数据驱动的 15×10 序章地图
- 4 名我方单位和 5 名敌军单位
- 职业数据：职业名称、移动力、最小/最大攻击距离
- 地形系统：平地、森林、石桥、河流、据点
- 带地形移动消耗的最短路寻路
- 河流不可进入、森林移动消耗 2、据点提供防御
- 单位阻挡
- 移动后锁定当前位置，避免重复免费移动
- 1 格与 1~2 格攻击距离
- 战斗预测窗口
- 预测后确认攻击 / 取消攻击目标
- 基础物理伤害与地形防御修正
- 双方反击
- 玩家攻击与反击获得 EXP
- 100 EXP 升级
- 按角色成长率提升 HP / 力量 / 防御 / 速度
- 成长判定使用稳定哈希，避免反复重开刷同一级成长
- 单位 HP、等级、职业、EXP 与生命条 HUD
- 等待 / 手动结束玩家回合
- 玩家全部行动后自动切换敌军回合
- 敌军按固定规则寻找最近玩家、按地形移动并攻击
- 数据驱动胜利条件：当前序章击败 `boss` 即胜利
- 我方全灭失败

## 运行方式

1. 安装 Godot 4.7.1 的 **.NET 版本**。
2. 安装兼容的 .NET SDK（项目当前目标框架为 `net8.0`）。
3. clone 仓库并切换到 `feature/playable-srpg-foundation`。
4. 使用 Godot 打开仓库根目录中的 `project.godot`。
5. 等待 C# 项目恢复依赖并编译，然后运行主场景。

## 操作方式

- 鼠标左键点击蓝色单位：选中。
- 蓝色区域：当前单位根据移动力与地形消耗计算出的可移动范围。
- 森林需要消耗 2 点移动力，河流不可进入。
- 移动后点击攻击范围内的红色敌军：锁定目标并显示战斗预测。
- `确认攻击`：按预测结果执行攻击与可能的反击。
- `取消攻击目标`：保留当前单位位置，重新选择攻击目标。
- `等待（结束当前单位行动）`：结束当前单位行动。
- `结束玩家回合`：让尚未行动的单位放弃本回合并进入敌军回合。

## 数据文件

当前核心内容已经从 C# 硬编码迁移到 `data/`：

- `data/classes.json`：职业 ID、显示名称、移动力、最小/最大攻击距离。
- `data/units.json`：角色模板、职业引用、等级、基础属性、武器威力、成长率。
- `data/chapter_01.json`：地图尺寸、胜利条件、地形坐标和单位部署。

`chapter_01.json` 当前使用：

- `victory_condition: "defeat_target"`
- `victory_target_id: "boss"`
- `terrain[].type`：`plain` / `forest` / `bridge` / `river` / `fort`
- `terrain[].cells`：二维坐标数组，每项格式 `[x, y]`
- `units[].unit_id`：引用 `units.json` 中的角色模板
- `units[].instance_id`：当前章节中的唯一实例 ID
- `units[].team`：`player` 或 `enemy`

## 代码注释规范

这是项目硬性规范：**所有代码文件必须有有意义的注释**。

- C# 类和核心方法尽量使用 XML 文档注释。
- 核心字段需要说明用途。
- 回合状态变化、战斗公式、寻路、敌军规则和不直观分支必须有行内注释。
- 不写“为了有注释而注释”的废话，注释重点解释为什么这样做、规则是什么、未来扩展点在哪里。
- `project.godot`、`.tscn` 等配置文件在格式允许的地方写注释；JSON 不强行加入非法注释，字段含义统一写进文档。

## 当前目录

```text
flame-emblem/
├── data/
│   ├── chapter_01.json
│   ├── classes.json
│   └── units.json
├── project.godot
├── FlameEmblem.csproj
├── scenes/
│   └── main/
│       └── Main.tscn
└── scripts/
    ├── game/
    │   ├── ChapterDataLoader.cs
    │   ├── CombatRules.cs
    │   ├── EnemyTurnController.cs
    │   ├── TerrainRules.cs
    │   ├── UnitClassDefinition.cs
    │   └── UnitModel.cs
    └── main/
        └── MainGame.cs
```

## 下一阶段

下一阶段优先加入：武器数据、命中/暴击/追击、魔法与 HP 消耗、转职、正式战斗演出、剧情对话、世界地图节点和存档。
