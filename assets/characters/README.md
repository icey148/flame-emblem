# Character Art Slots

人物美术使用统一目录约定。正式素材不存在时，游戏会自动使用程序绘制占位人物，不会因为缺图中断运行。

## 正式我方角色生产表

四名当前我方角色已经有固定的原创像素美术生产规范：

- 总生产表：`docs/player-character-production-sheet.md`
- Adrian：`assets/characters/adrian/README.md`
- Celine：`assets/characters/celine/README.md`
- Rowan：`assets/characters/rowan/README.md`
- Mira：`assets/characters/mira/README.md`

当前正式目标规格：

- 地图人物：32×32 px，脚底基准线 y=28
- 战斗人物：96×96 px，脚底基准线建议 y=84
- 头像：64×64 px
- 地图每人 20 帧：四方向 Idle 2 帧 + Walk 3 帧
- 战斗基础：Idle 2 / Attack 4 / Hit 3 / Dodge 3 / Defeat 4
- Mira 额外：Cast 5 帧

角色造型、像素帧和细节保持原创；目标是统一古典日式战棋幻想的修长比例和职业辨识，不直接复制既有商业游戏角色素材。

## 单张兼容素材

每个人物或职业仍然可以使用最简单的单张素材：

```text
assets/characters/<key>/
├── map.png       # 单张地图小人兼容素材
├── portrait.png  # 人物详情头像/半身像
└── battle.png    # 独立战斗演出大图兼容素材
```

## 地图多帧像素动画

如果存在多帧地图素材，游戏会优先使用序列帧，不再使用 `map.png` 的单图移动回退效果。

```text
assets/characters/<key>/map/
├── idle_down_0.png
├── idle_down_1.png
├── idle_up_0.png
├── idle_up_1.png
├── idle_left_0.png
├── idle_left_1.png
├── idle_right_0.png
├── idle_right_1.png
├── walk_down_0.png
├── walk_down_1.png
├── walk_down_2.png
├── walk_up_0.png
├── walk_up_1.png
├── walk_up_2.png
├── walk_left_0.png
├── walk_left_1.png
├── walk_left_2.png
├── walk_right_0.png
├── walk_right_1.png
└── walk_right_2.png
```

地图方向名固定为：`down` / `up` / `left` / `right`。

## 独立战斗多帧动画

战斗演出使用独立的 `battle/` 目录，不需要四方向帧。左/右站位由游戏控制，右侧人物会自动水平镜像以面对对手。

```text
assets/characters/<key>/battle/
├── idle_0.png
├── idle_1.png
├── attack_0.png
├── attack_1.png
├── attack_2.png
├── attack_3.png
├── hit_0.png
├── hit_1.png
├── hit_2.png
├── dodge_0.png
├── dodge_1.png
├── dodge_2.png
├── defeat_0.png
├── defeat_1.png
├── defeat_2.png
├── defeat_3.png
├── cast_0.png
├── cast_1.png
├── cast_2.png
├── cast_3.png
└── cast_4.png
```

当前战斗状态机会根据真实结算结果自动选择：

- `attack`：物理攻击
- `cast`：魔法施放
- `hit`：攻击命中后受击
- `dodge`：攻击未命中时闪避
- `defeat`：这一击实际把目标 HP 降到 0 时倒下
- `idle`：攻击之间的待机状态

必杀不要求单独准备一套人物帧：当前会继续使用攻击动作，并叠加必杀闪光和伤害提示。以后如需专属必杀动作，可以在状态枚举中继续扩展。

主动攻击、反击和速度追击会按照 `CombatResolver` 的真实攻击顺序排队播放；演出层不会重新掷命中或重新计算伤害。

## 帧命名规则

所有帧编号必须从 `0` 连续递增。程序遇到第一张不存在的编号后，会把前面的连续文件视为完整动画。

当前统一状态名：

- `idle`：待机
- `walk`：移动
- `attack`：攻击
- `hit`：受击
- `dodge`：闪避
- `defeat`：倒下
- `cast`：施法

## 查找优先级

1. 人物实例 ID，例如 `adrian`、`mira`
2. 职业 ID，例如 `mage`、`soldier`
3. 阵营默认目录：`player_default` / `enemy_default`

正式原创 PNG/像素序列帧加入后，不需要修改移动、战斗或角色数值代码。