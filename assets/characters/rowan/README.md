# Rowan — 重甲枪兵美术简报

目标：原创重甲枪兵，可靠、稳、沉。职业识别必须靠宽肩甲、厚胸甲、腿甲和长枪形成，不要只是把轻装人物画胖。

## 固定规格

- 地图：32×32 px，脚底基准线 y=28
- 战斗：96×96 px，脚底基准线建议 y=84
- 头像：64×64 px
- 地图总帧数：20
- 战斗基础帧：Idle 2 / Attack 4 / Hit 3 / Dodge 3 / Defeat 4

## 角色识别

- 体型：Armored
- 武器：Spear
- 深色短发
- 灰蓝铠甲
- 暗红识别色
- 深棕皮革
- 长枪贯穿整体轮廓

## 配色

- Hair `#3D342F`
- Armor `#6A7482`
- Armor shadow `#444D5A`
- Armor highlight `#9098A1`
- Leather `#51382A`
- Accent `#8D4B43`
- Brass `#A88A55`
- Spear highlight `#C9CBC6`

## 地图动作

Idle：上半身几乎不移动，肩甲稳定，枪杆轻微调整。

Walk 三帧：

1. 前脚迈出，但肩甲与胸甲保持稳定。
2. 双脚交汇，枪杆最接近竖直。
3. 另一脚迈出，枪尖/枪尾轻微反向摆动。

不要做明显上下弹跳；重甲感来自低重心和稳定上身。

## 战斗 Attack 四帧

1. `attack_0`：双手握枪，枪尖略抬，身体压低。
2. `attack_1`：后脚蹬地，枪杆拉到身体侧后方。
3. `attack_2`：直线突刺最大，枪杆近水平。
4. `attack_3`：枪尖回收，身体恢复防守重心。

## 目录

```text
assets/characters/rowan/map/
assets/characters/rowan/battle/
assets/characters/rowan/portrait.png
```

完整逐帧要求见 `docs/player-character-production-sheet.md`。
