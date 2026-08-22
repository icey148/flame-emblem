# Flame Emblem — Codex 交接说明

> 本文档用于把当前 ChatGPT 开发上下文完整交给 Codex。**优先级高于 README 中已经过时的“下一阶段”描述**。
>
> 当前工作分支：`feature/playable-srpg-foundation`。截至写本文档时，该分支相对 `main` 为 **356 commits ahead / 0 behind**。`main` 继续作为基线，不要直接在 `main` 上开发。

---

## 1. 项目目标与技术基线

- 项目：个人用原创复古战术 RPG，工作名 `Flame Emblem`。
- 引擎：Godot 4.7.1 .NET。
- 语言：C#。
- 项目目标框架：`.NET 8`。
- 本地环境曾确认安装 `dotnet 10.0.100`，但项目仍以 `net8.0` 为目标。
- 纯离线单机。
- 敌军行为只能使用固定规则 / 确定性规则，不接入 LLM、生成式 AI、联网 AI、ML 推理服务。
- 玩法结构可以高度参考经典格子制 SRPG，但角色、美术、地图、UI 图形、文本、音乐、动画帧保持原创，禁止直接复制商业游戏素材。

---

## 2. 开发硬规则

1. **所有代码都必须有有意义的注释。**
   - 类、核心方法优先 XML 文档注释。
   - 核心字段说明用途。
   - 战斗公式、回合状态、寻路、固定规则、反射桥接等不直观代码必须解释“为什么”。
2. **不要在游戏里加入 AI。**
   - “全体进攻”、敌军行动等只允许固定规则 / coordinator / deterministic logic。
3. **不要反复向用户确认。**
   - 用户已经明确要求直接继续开发；除非会改变整个项目方向，否则直接实施。
4. **不要创建 PR，除非用户明确要求。**
5. **不要重新加入 CI。** 用户明确说不需要 CI，之前 CI 已移除。
6. **不要声称“编译通过 / 测试通过”，除非实际在 Godot 或 dotnet 中验证。**
7. 用户可见名称使用中文，不使用英文显示名；技术内部 ID 可以保留英文以保证 JSON / 存档稳定。
8. 阵营视觉固定：
   - 我方：深蓝。
   - 敌方：玫红 / 酒红。
9. 不要把已经废弃的程序方块人物、旧白框战斗 UI 当成最终方案重新启用。
10. 所有战斗 HUD 数值必须来自真实规则，不允许把设计稿中的示例数字写死。

---

## 3. 最新且唯一有效的战斗视觉标准

**最新用户确认的金边 16:9 战斗设计图是唯一最终标准。它覆盖此前所有“粗白框 / FC 方块人 / 96×96 简化人物”的方案。**

文字版标准同时见：`docs/approved-battle-visual-v2.md`。

### 3.1 战斗构图

- 设计分辨率：`1280×720`。
- 纯黑主背景。
- 整屏细金色双边框，四角有克制的古典装饰。
- 左侧敌军，右侧我方。
- 上半屏是大尺寸高细节正式人物，不是小方块 sprite。
- 人物脚下只有很薄的暗金地面/落脚线，不要恢复旧蓝灰舞台或地形背景。
- 中央伤害框压在人物区与 HUD 交界处。
- “造成 N 伤害”中：文字金色，伤害数字 `N` 使用更大的红色数字。
- 下半屏左右信息面板直接在中线衔接，不留旧版大间隙。
- 左敌面板：酒红/暗红；右我方面板：深蓝。
- 两侧面板外侧各放一张大型正式头像。
- 面板使用细金边，不使用粗白边。
- 中文姓名、职业、武器、`LV`、`HP/HIT/ATC/DEF` 全部保留。
- 统计条继续是离散格，不做现代渐变进度条。

### 3.2 当前代码中的最终布局常量

`ReferenceBattleLayout.cs` 是当前统一布局源：

- `ViewportSize = 1280×720`
- `FrameInset = 10`
- `GroundLineY = 382`
- 左人物区域：`(32, 12)`
- 右人物区域：`(738, 12)`
- 单侧人物控件：`510×370`
- 人物脚底内部基准：`CharacterGroundY = 362`
- 人物最大绘制：`500×350`
- HUD：`(12, 392)`，`1256×316`
- 单侧面板：`628×316`
- `PanelGap = 0`
- 单侧头像宽：`252`
- 中央结果框：`(466,326)`，`348×86`

如果 Codex 调整位置，应以最新参考图的视觉一致性为目标，而不是回到旧版布局。

---

## 4. 人物设计：最新视觉方向

### 4.1 已经正式定稿并且仓库中有 approved WebP 的角色

#### 塞琳 `celine`

- 中文名：塞琳。
- 当前游戏数据职业：`scout` / 游骑兵。
- 当前武器：`short_bow` / 短弓。
- 最新正式视觉：
  - 金色高马尾 / 长马尾。
  - 蓝色发带。
  - 蓝色 + 金色轻甲。
  - 白色围巾 / 披肩。
  - 长弓、箭袋，明显弓手轮廓。
  - 我方深蓝身份必须贯穿战斗人物、头像、地图人物和 UI。
- 仓库已存在：
  - `assets/characters/celine/approved_battle.webp`
  - `assets/characters/celine/approved_portrait.webp`

#### 掠夺者 `raider`

- 中文名：掠夺者。
- 当前职业：`raider` / 掠夺者。
- 最新正式视觉：
  - 深玫红兜帽与破损披风。
  - 深色面罩，脸只露眼部。
  - 棕色皮革和暗金/黄铜扣件。
  - 最新参考图是双短刃 / 短刀轮廓。
  - 敌方玫红身份必须贯穿战斗人物、头像、地图人物和 UI。
- 仓库已存在：
  - `assets/characters/raider/approved_battle.webp`
  - `assets/characters/raider/approved_portrait.webp`
- **当前数据存在视觉不一致：** `data/units.json` 仍装备 `raider_axe`，`data/weapons.json` 名称是“粗斧”，但最新正式视觉是短刃。Codex 应在保持数值平衡的前提下统一武器数据/显示与正式人物设计，例如新增短刃定义或迁移该模板，而不是让 HUD 写“粗斧”但人物拿双刀。

### 4.2 已确定设计方向，但尚未有最新 approved WebP 的角色

这些角色现在**没有** `approved_battle.webp` / `approved_portrait.webp`，这是对战出现黑影/旧人物的核心原因之一。

#### 亚德里安 `adrian`

- 中文名：亚德里安。
- 当前数据职业：`vanguard` / 先锋。
- 当前武器：铁剑。
- 最新概念板视觉优先于早期生产表：年轻领主/剑士气质，深蓝披风与服装、金色扣件，较深的蓝黑/深色头发，单手剑，正统主角轮廓。
- 旧 `docs/player-character-production-sheet.md` 仍写棕发轻装剑士；该文档的颜色/发色属于较早方案，**最新用户确认概念板优先**。

#### 罗文 `rowan`

- 中文名：罗文。
- 当前数据职业：`soldier` / 士兵。
- 当前数据武器：铁枪。
- 最新概念板视觉：棕发、成熟/可靠、厚重金属甲、重装战士气质。
- 最新概念板的文字更像“重装剑士”，但游戏数据仍是枪兵；这是**视觉和玩法数据待统一项**。不要静默改变战斗平衡，先保证美术统一，再决定是保留枪兵规则还是做等价武器迁移。

#### 米拉 `mira`

- 中文名：米拉。
- 当前数据职业：`mage` / 术士。
- 当前武器：`ember` / 火星术。
- 最新概念板视觉：深色长发、白/蓝服装、文静的法系/神官式女性角色，高洁、柔和但仍属于我方深蓝体系。
- 概念板上出现“神官”标签，但当前玩法明确是术士 + HP 消耗魔法。除非决定连玩法一起改，否则应先把这种视觉语言用于术士设计，不要只因为概念图标签就破坏现有魔法规则。

#### 守卫 `guard`

- 中文名：守卫。
- 当前职业：守卫。
- 当前武器：守备枪。
- 最新视觉方向：有头盔、深蓝围巾/披肩、金属甲、长枪，普通军队枪兵轮廓。

#### 队长 / 桥头队长 `captain`

- 数据模板 ID 为 `bridge_captain`，职业 ID 是 `captain`；正式美术键应按职业回退到 `captain`。
- 中文显示：桥头队长。
- 当前武器：队长长剑。
- 最新视觉方向：年长灰发/灰胡、棕红披风、重甲、经验丰富的老兵/勇者气质，使用长剑。

### 4.3 地图人物

最终要求：战斗人物、对话头像、地图人物必须是**同一个角色设计**。

目前塞琳/掠夺者虽然有最新 `approved_battle.webp` / `approved_portrait.webp`，但最新概念板同规格地图人物还没有以新的 approved 资源完整落仓。其他五个角色也没有。

---

## 5. 当前用户报告的最重要未解决问题

### 对战时多个人物是黑影 / 看不到人物

用户在最新运行中明确报告：**好几个人对战时只显示黑影，没有看到人物。**

当前代码已尝试临时修复：`ApprovedCharacterArtCatalog` 的策略是：

1. 优先加载 `approved_battle.webp` / `approved_portrait.webp`。
2. 如果缺失或解码失败，临时回退到 `CharacterAssetResolver` 的安全旧图集。

但是：

- 目前只有塞琳和掠夺者拥有最新 approved WebP。
- 亚德里安、罗文、米拉、守卫、队长仍依赖旧 fallback。
- 用户已经实际看到黑影，说明 fallback 链在真实 Godot 运行中仍然存在问题，或者某些旧纹理本身没有正确透明/加载。
- **这项没有完成验证。**

Codex 接手后的第一优先级应该是：

1. 在 Godot 中复现黑影。
2. 记录具体角色 ID / class ID。
3. 跟踪：`ApprovedCharacterArtCatalog.TryLoadBattle` → `CharacterAssetResolver.TryLoad` → `BattleSpriteFigureControl`。
4. 检查返回纹理尺寸、alpha、是否为 null、是否是旧损坏图集。
5. 最终方案不是继续修旧方块人，而是尽快为剩余五个角色补齐正式 `approved_battle.webp` / `approved_portrait.webp`，让 fallback 不再成为正常路径。

---

## 6. 已经实现的核心玩法代码

以下属于已经实现的基础系统，不应该因为视觉重做而推翻：

### 地图 / 回合

- JSON 数据驱动章节地图。
- 当前至少有 `chapter_01.json`、`chapter_02.json`。
- 15×10 原型战场基础。
- 地形：平地、森林、石桥、河流、据点。
- 地形移动消耗。
- 河流不可进入。
- 森林移动消耗 2。
- 据点提供防御/回避。
- 最短路寻路。
- 单位阻挡。
- 人物沿合法路径逐格移动，不瞬移。
- 移动后行动菜单。
- 玩家单位行动完成后切换敌军回合。
- 我方全部行动后自动进入敌军回合。
- 数据驱动胜利/失败条件。

### 战斗规则

- 物理 / 魔法伤害类型。
- 物理：力量 vs 防御，并读取地形防御修正。
- 魔法：魔力 vs 魔防。
- 武器：威力、命中、必杀、最小/最大射程、伤害类型、HP 消耗。
- 魔法实际施放消耗 HP。
- 魔法不能主动把施法者扣到 0 HP。
- 命中率。
- 必杀率。
- 必杀伤害 ×3。
- 反击。
- 速度差 `>= 4` 触发追击。
- 主动攻击 → 反击 → 追击的完整交换。
- 短弓当前射程 1~2。
- 长弓当前射程 1~3。
- 1~2 格魔法按射程反击。
- 战斗交换通过 `CombatExchangeResult` / `CombatStrikeResult` 保存实际结算。

### 成长 / EXP

- 玩家攻击和反击可获得 EXP。
- 100 EXP 升级。
- HP / 力量 / 魔力 / 技巧 / 速度 / 幸运 / 防御 / 魔防成长。
- 成长判定使用稳定哈希，防止反复重开刷同一级成长。

### 固定规则敌军

- `EnemyTurnController` 使用固定规则。
- 找最近玩家、根据地形移动、进入射程攻击。
- 不使用 AI/LLM。

### 批量命令

`RightCommandCoordinator` 已实现：

- 全体进攻。
- 全体待机。
- 全体进攻按固定规则：依次朝最近敌军移动，进入武器射程后立即攻击。
- `GroupCommandRecoveryCoordinator` 用于批量命令结束后的按钮/禁用状态恢复。

### 装备 / HUD

- 装备切换。
- 人物详情。
- HP / 等级 / 职业 / EXP 等基础 HUD。
- 战斗预测、战斗日志基础逻辑存在；右侧表现层对旧预测控件有隐藏/重排。

---

## 7. 已经实现的流程系统

### 标题界面

`TitleScreen.cs` 已实现：

- 新游戏。
- 继续游戏。
- 退出。
- 无存档时禁用继续。
- 有存档时新游戏需要覆盖确认。
- 继续游戏能根据保存位置进入战斗或世界地图。

### 存档

`SaveGameCoordinator.cs` / `SaveGameData.cs` 已实现本地存读档：

- 战斗位置保存。
- 世界地图位置保存。
- 跨章节恢复。
- 战斗快照恢复。
- 移动/批量进攻等不安全时机限制存读档。

### 转职

`PromotionCatalog` + `PromotionCoordinator` 已有可测试转职入口与规则桥接。

### 章节流程

- `ChapterFlowCoordinator` 已存在。
- 章节数据驱动。
- 胜利后可进入后续流程。

### 对话

- `ChapterDialogueCoordinator` 已实现章节开场/胜利对话、左右人物、继续/跳过等。
- `ApprovedDialoguePresentationCoordinator` 已把对话 UI 改成暗底金边、大头像、金色说话者姓名的最新正式视觉方向。
- 正式头像仍受“只有塞琳/掠夺者有 approved portrait”限制。

### 世界地图

`WorldMapScreen.cs` + `WorldMapCatalog` + `data/world_map.json` 已实现原创世界地图节点、路线、解锁、章节间入口与长期存档基础。

**注意：README 的“下一阶段：转职/剧情对话/世界地图/存档”已经过时，这些模块现在都有代码。**

---

## 8. 当前正式战斗表现代码架构

### `RetroBattleAnimationCoordinator.cs`

职责：

- 接收真实 `CombatExchangeResult`。
- 固定敌左我右。
- 管理 Intro / Windup / Impact / Recovery / Outro 时间线。
- 更新攻击者、受击者状态。
- 更新原始结果文字。
- 同步逐击 HP。

**不要把战斗规则塞进这个表现类。**

### `ReferenceBattleLayout.cs`

当前金边最终界面的统一布局常量。位置/尺寸应从这里读取。

### `BattlePresentationPolishCoordinator.cs`

当前最终表现总装配层：

- 纯黑背景。
- 动态添加 `ApprovedBattleFrameControl`。
- 配置左右人物。
- 配置 HUD。
- 配置特效层。
- 把原始结果框改成金边。
- 动态添加 `ApprovedBattleResultControl`。
- 隐藏旧人物渲染节点，只保留它们作为状态/动画计时源。

### `ApprovedBattleFrameControl.cs`

已经实现：

- 整屏金色双边框。
- 四角装饰。
- 脚下暗金线。
- 底部中央蓝色菱形装饰。

### `ApprovedBattleResultControl.cs`

已经实现：

- 读取原始结果 Label 的文本。
- 把“造成 N 伤害”拆成三段。
- 金色文字 + 红色大伤害数字。
- 不改变真实伤害值。

### `ApprovedCharacterArtCatalog.cs`

当前正式美术入口：

- 优先读取 `approved_battle.webp` / `approved_portrait.webp`。
- 直接 `FileAccess.GetFileAsBytes` + `Image.LoadWebpFromBuffer`，绕过旧 import 缓存。
- 按人物 ID / 职业 ID 解析资源键。
- 当前还保留临时 `CharacterAssetResolver` fallback，以避免 approved 资源缺失时直接空白。
- **fallback 仍需要真实运行验证。**

### `ReferenceBattleSpriteCatalog.cs`

只作为战斗人物资源入口转发到 `ApprovedCharacterArtCatalog`，旧 `battle_v3` 不应再作为正式路径。

### `BattleSpriteFigureControl.cs`

- 绘制正式大人物。
- 保持等比缩放。
- 锁脚底线。
- 攻击/施法/闪避/受击目前主要通过“整张静态人物图整体位移/淡出”表现。
- **还不是最终多帧角色动画。**

### `RetroBattleStatusHudControl.cs`

已经重做成：

- 外侧大型头像。
- 暗红/深蓝双面板。
- 细金边。
- 姓名、职业、武器、LV。
- HP/HIT/ATC/DEF 数值和离散格条。
- 数值读取真实 `CombatRules` / UnitModel / 武器数据。

### `CharacterPortraitControl.cs`

对话/HUD 正式头像控件。

### `ApprovedDialoguePresentationCoordinator.cs`

对话框最终视觉整理：

- 暗底金边。
- 大头像。
- 金色说话者名字。
- 正文浅色。
- 继续/跳过按钮改为暗底金边。

### `Main.tscn`

当前已经挂载核心 coordinator，包括：

- `RetroBattlefieldLayer`
- `CharacterVisualCoordinator`
- `MapFigurePolishCoordinator`
- `BattleMovementPauseCoordinator`
- `PostMoveActionCoordinator`
- `RetroBattleAnimationCoordinator`
- `BattleWeaponEffectCoordinator`
- `RetroHudCoordinator`
- `TeamPresentationCoordinator`
- `RightCommandCoordinator`
- `GroupCommandRecoveryCoordinator`
- `PromotionCoordinator`
- `SaveGameCoordinator`
- `ChapterFlowCoordinator`
- `ChapterDialogueCoordinator`
- `ApprovedDialoguePresentationCoordinator`
- `BattlePresentationPolishCoordinator`

---

## 9. 已完成 / 未完成状态表

| 模块 | 状态 | 说明 |
| --- | --- | --- |
| 基础地图规则 | 已实现 | 地形、寻路、阻挡、逐格移动 |
| 玩家回合/敌军回合 | 已实现 | 敌军为固定规则 |
| 武器/魔法/射程 | 已实现 | JSON 数据驱动 |
| 命中/必杀/反击/追击 | 已实现 | 必杀×3，速差>=4追击 |
| HP 消耗魔法 | 已实现 | 不能把自己主动扣到 0 |
| EXP/升级/成长 | 已实现 | 稳定哈希成长 |
| 批量进攻/待机 | 已实现 | 固定规则 |
| 转职基础 | 已实现 | 有 coordinator + catalog |
| 章节对话 | 已实现 | 视觉仍需正式头像补齐 |
| 世界地图 | 已实现基础 | 原创程序绘制，仍可继续美化 |
| 存档/读档 | 已实现基础 | 战斗/世界地图位置 |
| 标题菜单 | 已实现 | 新游戏/继续/退出 |
| 金边最终战斗框架 | 已实现代码 | 需要本地运行验收 |
| 金色/红数字中央伤害框 | 已实现代码 | 需要本地运行验收 |
| 最终战斗 HUD | 已实现代码 | 需要本地运行验收 |
| 塞琳正式战斗人物 | 已有资源 | `approved_battle.webp` |
| 塞琳正式头像 | 已有资源 | `approved_portrait.webp` |
| 掠夺者正式战斗人物 | 已有资源 | `approved_battle.webp` |
| 掠夺者正式头像 | 已有资源 | `approved_portrait.webp` |
| 亚德里安正式最新人物/头像 | 未完成 | 只有旧资源/fallback |
| 罗文正式最新人物/头像 | 未完成 | 只有旧资源/fallback |
| 米拉正式最新人物/头像 | 未完成 | 只有旧资源/fallback |
| 守卫正式最新人物/头像 | 未完成 | 只有旧资源/fallback |
| 队长正式最新人物/头像 | 未完成 | 只有旧资源/fallback |
| 最新设计地图人物 | 未完成 | 需和头像/战斗人物统一 |
| 正式多帧战斗动作 | 未完成 | 目前主要移动静态人物图 |
| 黑影问题 | 未完成验收 | 用户最后仍报告多角色黑影 |
| 最终整体视觉验收 | 未完成 | 必须在用户 Godot 运行后看截图 |

---

## 10. 已废弃方案：Codex 不要恢复

这些文件可能仍在仓库里作为历史/兼容代码，但**不能重新成为最终表现**：

- `ReferenceBattleFigureControl` 程序矩形人物。
- `CinematicBattleFigureControl` 旧程序人物作为最终角色。
- `RefinedBattleFigureControl` 旧程序人物作为最终角色。
- `battle_ref.png` 80×75 方块人物作为正式战斗人物。
- `battle_v3.b64` 96×96 简化人物作为正式战斗人物。
- 程序生成头像作为正式对话头像。
- 旧“纯黑 + 粗白框 + 大色块”战斗 UI 作为最终标准。
- 旧蓝灰/地形式战斗舞台背景。

它们最多只能临时兼容，不应继续投入美术精修。

---

## 11. 文档冲突与优先级

当前仓库存在多轮视觉迭代文档，优先级如下：

1. **本文件 `docs/codex-handoff.md`**：最新交接状态。
2. **`docs/approved-battle-visual-v2.md`**：最新正式战斗视觉文字规范。
3. 当前实现中的 `ReferenceBattleLayout.cs`。
4. `docs/player-character-production-sheet.md` / `pixel-character-production-spec.md`：早期生产技术规范，动作帧/画布规格仍可参考，但具体发色、服装颜色若和最新概念图冲突，以最新概念图为准。
5. `README.md`：玩法基础说明有用，但“下一阶段”已经明显过时。
6. `docs/battle-ui-reference-spec.md`：更早的白框参考阶段，不能覆盖最新金边最终设计。

---

## 12. Codex 接手后的推荐执行顺序

### P0 — 先修用户能直接看到的问题

1. 本地 Godot 4.7.1 .NET 编译。
2. 修所有编译错误；不要继续扩功能直到能启动。
3. 复现“多人对战黑影”。
4. 确认塞琳/掠夺者是否正确显示最新 approved WebP。
5. 确认亚德里安/罗文/米拉/守卫/队长 fallback 为什么会黑。
6. 只要能做正式资源，优先补 approved 资源，不要继续美化旧方块 fallback。

### P1 — 补齐整套人物

为以下五个资源键补：

```text
assets/characters/adrian/approved_battle.webp
assets/characters/adrian/approved_portrait.webp
assets/characters/rowan/approved_battle.webp
assets/characters/rowan/approved_portrait.webp
assets/characters/mira/approved_battle.webp
assets/characters/mira/approved_portrait.webp
assets/characters/guard/approved_battle.webp
assets/characters/guard/approved_portrait.webp
assets/characters/captain/approved_battle.webp
assets/characters/captain/approved_portrait.webp
```

然后补同设计的地图人物。

### P2 — 视觉数据统一

- 掠夺者双短刃 vs 当前“粗斧”数据。
- 罗文重装设计 vs 当前枪兵数据。
- 米拉概念板神官式外观 vs 当前术士/HP 魔法规则。
- 亚德里安最新深色头发概念 vs 旧生产表棕发。

优先保留已经可玩的战斗规则与数值平衡；如果只需改名称/武器视觉，可以做等价数据迁移，避免无意改变难度。

### P3 — 正式动作

- 把当前“静态全身图整体位移”升级为真正的正式动作帧：Idle / Attack / Cast / Hit / Dodge / Defeat。
- 动作必须继续对齐现有 0.34s 物理 / 0.50s 魔法时间线，或者同时调整时间线但不要改变战斗结算顺序。
- 特效锚点要跟正式人物尺寸重新校准。

### P4 — 地图与世界统一

- 最新角色地图人物。
- 战场 TileSet/地形美术接口继续美化。
- 世界地图可保持现有功能，再做视觉升级。

---

## 13. 本地验证清单

每次重要修改后至少检查：

1. Godot C# 能否编译。
2. 标题页能启动。
3. 新游戏能进入章节。
4. 塞琳 vs 掠夺者能否正确显示金边最终战斗 UI。
5. 其余 5 个角色是否仍有黑影。
6. 左敌右我是否稳定。
7. 中央结果框是否会遮挡 HUD 文字。
8. HP/HIT/ATC/DEF 是否是真实数据。
9. 伤害数字是否和实际 `CombatStrikeResult.Damage` 一致。
10. 对话左右头像是否都正常。
11. 保存/读取、转职、群体进攻不会因为视觉协调器反射失效。
12. 结束战斗后 `_blocker` 正常隐藏，不锁死输入。

---

## 14. 当前 Git / 协作规则

- Repo：`icey148/flame-emblem`
- Base：`main`
- 开发分支：`feature/playable-srpg-foundation`
- 当前比较：`356 ahead / 0 behind`（写本文档时）。
- 不开 PR，除非用户明确要求。
- 不加 CI。
- 直接在 feature 分支继续提交。
- 不要把“静态检查完成”写成“编译通过”。

---

## 15. 给 Codex 的一句话任务定义

> 在不破坏现有可玩 SRPG 规则、存档、转职、章节、固定规则敌军和世界地图的前提下，先解决对战人物黑影问题，再把亚德里安、塞琳、罗文、米拉、掠夺者、守卫、队长全部统一到用户最后确认的“黑底 + 细金边 + 大尺寸高细节人物 + 外侧正式头像 + 酒红/深蓝状态面板 + 红色伤害数字”设计；旧方块人物和程序头像不得重新成为最终输出。