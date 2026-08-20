# Celine — 轻装弓手美术简报

目标：原创修长弓手，安静、专注、灵巧。职业识别必须主要靠长弓、箭袋和细长侧身轮廓，不只靠换颜色。

## 固定规格

- 地图：32×32 px，脚底基准线 y=28
- 战斗：96×96 px，脚底基准线建议 y=84
- 头像：64×64 px
- 地图总帧数：20
- 战斗基础帧：Idle 2 / Attack 4 / Hit 3 / Dodge 3 / Defeat 4

## 角色识别

- 体型：Light
- 武器：Bow
- 棕橙中短发
- 墨绿轻装
- 浅绿灰披肩
- 深棕箭袋和皮革
- 长弓轮廓清楚

## 配色

- Hair `#A66E42`
- Hair shadow `#70482D`
- Outfit `#4E6A4B`
- Outfit shadow `#344A36`
- Mantle `#788878`
- Cloth `#D9D3BE`
- Leather `#5A3C2A`
- Bow `#7B5434`

## 地图动作

Idle：弓保持稳定，呼吸变化集中在肩线与披肩，箭袋可变化 1px。

Walk 三帧：

1. 弓略向后，箭袋箭尾反向摆 1px。
2. 双脚交汇，弓接近稳定位置。
3. 弓略向前，箭袋回摆。

## 战斗 Attack 四帧

1. `attack_0`：弓处于下位，身体侧向目标。
2. `attack_1`：抬弓，前臂伸直。
3. `attack_2`：拉弦到最大，后肘明显后移。
4. `attack_3`：松弦，弓臂前弹，后手回到脸侧/胸侧。

## 目录

```text
assets/characters/celine/map/
assets/characters/celine/battle/
assets/characters/celine/portrait.png
```

完整逐帧要求见 `docs/player-character-production-sheet.md`。
