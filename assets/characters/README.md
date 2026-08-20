# Character Art Slots

人物美术使用统一目录约定。正式素材不存在时，游戏会自动使用程序绘制占位人物，不会因为缺图中断运行。

## 单张素材

每个人物或职业仍然可以使用最简单的单张素材：

```text
assets/characters/<key>/
├── map.png       # 单张地图小人兼容素材
├── portrait.png  # 人物详情头像/半身像
└── battle.png    # 独立战斗演出大图兼容素材
```

## 地图多帧像素动画

如果存在多帧地图素材，游戏会优先使用序列帧，不再使用 `map.png` 的滑动占位效果。

目录结构：

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

帧编号必须从 `0` 连续递增。程序遇到第一张不存在的编号后，会把前面的连续文件视为完整动画。

代码已经预留以下状态名，后续可以继续加入对应帧：

- `idle`：待机
- `walk`：移动
- `attack`：攻击
- `hit`：受击
- `dodge`：闪避
- `defeat`：倒下
- `cast`：施法

方向名固定为：`down` / `up` / `left` / `right`。

## 查找优先级

1. 人物实例 ID，例如 `adrian`、`mira`
2. 职业 ID，例如 `mage`、`soldier`
3. 阵营默认目录：`player_default` / `enemy_default`

地图小人使用最近邻纹理过滤，适合低分辨率像素图放大显示。正式原创 PNG/像素序列帧加入后，不需要修改移动、战斗或角色数值代码。
