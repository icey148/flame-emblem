# Flame Emblem

个人用原创 2D 战术 RPG 项目，玩法方向参考经典格子制 SRPG，但角色、剧情、地图、UI、音频和美术素材保持原创。

## 当前技术基线

- Engine: Godot 4.7.1 .NET edition
- Language: C# / .NET 8
- Runtime: 纯本地单机
- Enemy behavior: 固定规则 / 确定性目标选择与移动逻辑，不接入 LLM、生成式 AI 或联网 AI 服务
- Prototype rendering: 当前地图、单位和范围高亮均由代码绘制，不依赖外部商业素材

## 当前可玩内容

开发分支：`feature/playable-srpg-foundation`

目前已经实现第三阶段战斗骨架：

- JSON 数据驱动的 15×10 序章地图
- 4 名我方单位和 5 名敌军单位
- 职业数据：职业名称、移动力以及预留职业射程字段
- 地形系统：平地、森林、石桥、河流、据点
- 带地形移动消耗的最短路寻路
- 河流不可进入、森林移动消耗 2、据点提供防御/回避
- 单位阻挡
- 移动后锁定当前位置，避免重复免费移动
- 独立武器 / 法术数据表
- 武器属性：威力、命中、必杀、最小/最大射程、伤害类型、HP 消耗
- 物理伤害：力量对防御，并受到地形防御修正
- 魔法伤害：魔力对魔防
- 魔法每次实际施放消耗 HP，并且不能主动把施法者扣到 0 HP
- 命中率
- 必杀率与 3 倍必杀伤害
- 速度差达到 4 时触发追击
- 主动攻击 → 反击 → 速度追击的完整战斗交换
- 2 格短弓无法近距离反击
- 1~2 格魔法可在对应射程反击
- 战斗预测显示伤害 / 命中 / 必杀 / 攻击次数 / HP 消耗
- 逐击战斗记录显示 miss、critical、伤害和施法 HP 消耗
- 玩家攻击与反击获得 EXP
- 100 EXP 升级
- 成长属性：HP / 力量 / 魔力 / 技巧 / 速度 / 幸运 / 防御 / 魔防
- 成长判定使用稳定哈希，避免反复重开刷同一级成长
- 单位 HP、等级、职业、装备、EXP 与生命条 HUD
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

如果已经 clone 过仓库，可在本地项目目录执行：

```bash
git checkout feature/playable-srpg-foundation
git pull
```

## 操作方式

- 鼠标左键点击蓝色单位：选中。
- 蓝色区域：当前单位根据移动力与地形消耗计算出的可移动范围。
- 森林需要消耗 2 点移动力，河流不可进入。
- 移动后点击攻击范围内的红色敌军：锁定目标并显示完整战斗预测。
- `确认攻击`：实际掷命中/必杀并执行可能的反击和追击。
- `取消攻击目标`：保留当前单位位置，重新选择攻击目标。
- `等待（结束当前单位行动）`：结束当前单位行动。
- `结束玩家回合`：让尚未行动的单位放弃本回合并进入敌军回合。

## 数据文件

当前核心内容已经从 C# 硬编码迁移到 `data/`：

- `data/classes.json`：职业 ID、显示名称、移动力和职业层预留射程。
- `data/weapons.json`：武器/法术 ID、伤害类型、威力、命中、必杀、射程和 HP 消耗。
- `data/units.json`：角色模板、职业/装备引用、等级、八项基础属性和成长率。
- `data/chapter_01.json`：地图尺寸、胜利条件、地形坐标和单位部署。

`weapons.json` 主要字段：

- `damage_type`：`physical` / `magical`
- `might`：威力
- `hit`：基础命中
- `critical`：基础必杀
- `min_range` / `max_range`：攻击射程
- `hp_cost`：每次实际使用需要支付的 HP，普通武器填 0

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
│   ├── units.json
│   └── weapons.json
├── project.godot
├── FlameEmblem.csproj
├── scenes/
│   └── main/
│       └── Main.tscn
└── scripts/
    ├── game/
    │   ├── BattlePresentationFormatter.cs
    │   ├── ChapterDataLoader.cs
    │   ├── CombatResolver.cs
    │   ├── CombatRules.cs
    │   ├── EnemyTurnController.cs
    │   ├── TerrainRules.cs
    │   ├── UnitClassDefinition.cs
    │   ├── UnitModel.cs
    │   └── WeaponDefinition.cs
    └── main/
        └── MainGame.cs
```

## 下一阶段

下一阶段优先加入：真正独立的战斗演出场景、单位详情面板、转职基础、武器背包/切换、剧情对话、世界地图节点和存档。
