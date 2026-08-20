# Adrian — 轻装剑士美术简报

目标：原创修长轻装剑士，古典中世纪幻想气质。不要使用大头 Q 版比例，也不要复制任何既有商业游戏角色造型或像素帧。

## 固定规格

- 地图：32×32 px，脚底基准线 y=28
- 战斗：96×96 px，脚底基准线建议 y=84
- 头像：64×64 px
- 地图总帧数：20
- 战斗基础帧：Idle 2 / Attack 4 / Hit 3 / Dodge 3 / Defeat 4

## 角色识别

- 体型：Light
- 武器：Sword
- 棕色短发，明显前额发
- 蓝灰轻甲
- 短蓝披风
- 米白内衬
- 深棕皮革
- 少量黄铜

## 配色

- Hair `#6C4B34`
- Hair shadow `#493124`
- Outfit `#445A7A`
- Outfit shadow `#2F3E57`
- Cape `#526E94`
- Cloth `#D7D1BF`
- Leather `#5A3D2C`
- Brass `#B39455`

## 地图动作

Idle 只做肩线/披风 1px 级变化，脚底绝对不漂浮。

Walk 三帧：

1. 前脚迈出，剑和披风略向后。
2. 双脚交汇，披风回中心。
3. 后脚迈出，剑柄轻微反向摆动。

## 战斗 Attack 四帧

1. `attack_0`：重心后移，剑靠近腰侧。
2. `attack_1`：前脚迈出，上身前倾，开始抬剑。
3. `attack_2`：前冲最大，剑刃穿过身体前方。
4. `attack_3`：挥砍收势，剑落到身体前下方。

## 目录

地图帧放入：

```text
assets/characters/adrian/map/
```

战斗帧放入：

```text
assets/characters/adrian/battle/
```

头像：

```text
assets/characters/adrian/portrait.png
```

完整逐帧要求见 `docs/player-character-production-sheet.md`。
