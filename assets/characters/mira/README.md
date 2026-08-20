# Mira — 长袍法师美术简报

目标：原创长袍法师，冷静、神秘、柔和但危险。职业识别必须靠宽袖、展开袍摆、紫色中长发和法书/法器形成。

## 固定规格

- 地图：32×32 px，脚底基准线 y=28
- 战斗：96×96 px，脚底基准线建议 y=84
- 头像：64×64 px
- 地图总帧数：20
- 战斗基础帧：Idle 2 / Hit 3 / Dodge 3 / Defeat 4
- 施法帧：Cast 5

## 角色识别

- 体型：Robed
- 武器：Tome
- 紫色中长发
- 深紫长袍
- 灰白/淡紫袖口与内衬
- 袍摆宽于肩部
- 法书靠近胸前或身体侧前方

## 配色

- Hair `#76517B`
- Hair shadow `#513657`
- Robe `#514B79`
- Robe shadow `#353252`
- Cuff/lining `#CBC5D2`
- Accent `#A16BB2`
- Silver `#BFC3C8`
- Magic blue-violet `#9DA9D9`
- Magic warm white `#E4DCC6`

## 地图动作

Idle：上身稳定，头发、袖口和袍摆只做 1px 级变化。

Walk 三帧：

1. 袍摆左侧展开 1px，右侧收拢。
2. 袍摆回到中心。
3. 袍摆右侧展开 1px，左侧收拢。

不要画成近战职业的明显大步摆臂。

## 战斗 Cast 五帧

1. `cast_0`：标准待机，法书靠近胸前。
2. `cast_1`：抬起法书/施法手，袖口展开。
3. `cast_2`：另一只手前伸，头发和袍摆开始受魔力影响。
4. `cast_3`：法术核心最亮，手臂完全展开；释放关键帧。
5. `cast_4`：亮度回落，保持施法结束姿势。

## 目录

```text
assets/characters/mira/map/
assets/characters/mira/battle/
assets/characters/mira/portrait.png
```

完整逐帧要求见 `docs/player-character-production-sheet.md`。
