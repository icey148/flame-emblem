# Character Art Slots

人物美术使用统一目录约定。正式素材不存在时，游戏会自动使用程序绘制占位人物，不会因为缺图中断运行。

每个人物或职业可创建一个目录：

```text
assets/characters/<key>/
├── map.png       # 战棋地图小人，建议透明背景
├── portrait.png  # 人物详情头像/半身像
└── battle.png    # 独立战斗演出大图/序列帧入口
```

查找优先级：

1. 人物实例 ID，例如 `adrian`、`mira`
2. 职业 ID，例如 `mage`、`soldier`
3. 阵营默认目录：`player_default` / `enemy_default`

当前程序已经会自动尝试加载这些路径；因此未来加入正式原创 PNG 时，不需要修改战斗规则代码。

> 后续如果地图人物改成多帧 `SpriteFrames` / `AnimatedSprite2D`，会继续沿用同一人物 key 和目录结构，只扩充动画文件定义。
