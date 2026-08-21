using FlameEmblem.Game;
using Godot;
using System.Reflection;

namespace FlameEmblem.Visual;

/// <summary>
/// 重新绘制缺少正式战斗 PNG 时的原创侧视人物。
/// 视觉目标是修长、优雅、古典日式战棋感：小头、窄腰、长腿、清晰职业轮廓；
/// 不复制任何现成游戏角色、服装或动画帧，正式原创素材存在时会自动让位。
/// </summary>
public partial class RefinedBattleFigureControl : Control
{
    /// <summary>被战斗时间线实际驱动的原人物控件。</summary>
    private AnimatedBattleCharacterControl? _source;

    /// <summary>原人物控件中的当前单位字段。</summary>
    private FieldInfo? _unitField;

    /// <summary>原人物控件中的当前动画状态字段。</summary>
    private FieldInfo? _stateField;

    /// <summary>原人物控件中的状态计时字段。</summary>
    private FieldInfo? _elapsedField;

    /// <summary>新回退人物继续使用 96×96 逻辑画布，每个逻辑像素放大 4 倍。</summary>
    private const float PixelScale = 4.0f;

    /// <summary>绑定现有战斗人物；本层只接管视觉，不碰战斗时间线。</summary>
    public void Bind(AnimatedBattleCharacterControl source)
    {
        _source = source;
        BindingFlags members = BindingFlags.Instance | BindingFlags.NonPublic;
        Type type = typeof(AnimatedBattleCharacterControl);
        _unitField = type.GetField("_unit", members);
        _stateField = type.GetField("_state", members);
        _elapsedField = type.GetField("_stateElapsed", members);

        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = TextureFilterEnum.Nearest;
        SetProcess(true);
        QueueRedraw();
    }

    /// <summary>每帧跟随原人物动画时间刷新。</summary>
    public override void _Process(double delta)
    {
        QueueRedraw();
    }

    /// <summary>
    /// 正式 battle 素材存在时恢复原人物可见性；否则隐藏旧程序身体并绘制新人物。
    /// SelfModulate 只影响原人物自身，不会把作为子节点的新绘制层一起隐藏。
    /// </summary>
    public override void _Draw()
    {
        UnitModel? unit = ReadUnit();
        if (_source is null || unit is null)
        {
            return;
        }

        if (HasFormalBattleArt(unit))
        {
            _source.SelfModulate = Colors.White;
            HideLegacyDetailOverlay(false);
            return;
        }

        // 旧大方块身体完全透明，只保留它继续接收 SetUnit/Play 和推进状态计时。
        _source.SelfModulate = new Color(1, 1, 1, 0);
        HideLegacyDetailOverlay(true);
        DrawRefinedFigure(unit, ReadState(), ReadElapsed());
    }

    /// <summary>只把真正的 battle/battle-state 图视为正式战斗素材；portrait 不再被当作侧视战斗身体。</summary>
    private static bool HasFormalBattleArt(UnitModel unit)
    {
        foreach (CharacterAnimationState state in Enum.GetValues<CharacterAnimationState>())
        {
            if (CharacterAssetResolver.GetBattleFrameCount(unit, state) > 0)
            {
                return true;
            }
        }

        return CharacterAssetResolver.TryLoad(unit, CharacterArtSlot.Battle) is not null;
    }

    /// <summary>隐藏旧的“补细节”层，避免它叠在新骨架脸上；正式素材出现时再恢复。</summary>
    private void HideLegacyDetailOverlay(bool hidden)
    {
        if (_source is null)
        {
            return;
        }

        foreach (BattleCharacterDetailOverlayControl overlay in _source.GetChildren().OfType<BattleCharacterDetailOverlayControl>())
        {
            overlay.Visible = !hidden;
        }
    }

    /// <summary>绘制完整原创修长人物。</summary>
    private void DrawRefinedFigure(UnitModel unit, CharacterAnimationState state, float elapsed)
    {
        CharacterAppearanceDefinition appearance = CharacterAppearanceCatalog.Get(unit);
        int side = _source?.MirrorHorizontally == true ? -1 : 1;
        int pose = ResolvePose(state, elapsed);
        Vector2 actionOffset = ResolveActionOffset(appearance, state, elapsed, side);
        float opacity = ResolveOpacity(state, elapsed);

        // 画布整体略向下移动，让更长的腿仍然落在原有战斗台面上。
        Vector2 origin = new Vector2(18, 6) + SnapLogical(actionOffset);

        Color outline = Fade(new Color("191820"), opacity);
        Color hair = Fade(appearance.HairColor, opacity);
        Color hairShadow = Fade(appearance.HairColor.Darkened(0.28f), opacity);
        Color hairLight = Fade(appearance.HairColor.Lightened(0.16f), opacity);
        Color skin = Fade(appearance.SkinColor, opacity);
        Color skinShadow = Fade(appearance.SkinColor.Darkened(0.16f), opacity);
        Color cloth = Fade(appearance.OutfitColor, opacity);
        Color clothShadow = Fade(appearance.OutfitColor.Darkened(0.30f), opacity);
        Color clothLight = Fade(appearance.OutfitColor.Lightened(0.17f), opacity);
        Color accent = Fade(appearance.AccentColor, opacity);
        Color leather = Fade(new Color("4c3428"), opacity);
        Color metal = Fade(new Color("c1c6c4"), opacity);
        Color metalLight = Fade(new Color("e2e4df"), opacity);
        Color metalShadow = Fade(new Color("596067"), opacity);

        DrawCape(origin, appearance, side, pose, outline, accent);
        DrawLegs(origin, appearance, side, pose, outline, cloth, clothShadow, clothLight, leather, metalShadow);
        DrawTorso(origin, appearance, side, pose, outline, cloth, clothShadow, clothLight, accent, leather, metal, metalShadow);
        DrawNeckAndHead(origin, unit, appearance, side, pose, outline, hair, hairShadow, hairLight, skin, skinShadow);
        DrawArms(origin, appearance, side, pose, state, outline, cloth, clothShadow, skin, leather, metalShadow);
        DrawWeapon(origin, appearance, side, pose, state, outline, accent, leather, metal, metalLight, metalShadow);
    }

    /// <summary>把攻击/施法拆成蓄势、最大动作和收势三个姿势。</summary>
    private static int ResolvePose(CharacterAnimationState state, float elapsed)
    {
        float duration = state == CharacterAnimationState.Cast ? 0.50f : 0.34f;
        if (state is not CharacterAnimationState.Attack and not CharacterAnimationState.Cast)
        {
            return 0;
        }

        float t = Mathf.Clamp(elapsed / duration, 0.0f, 1.0f);
        return t < 0.26f ? 1 : t < 0.70f ? 2 : 3;
    }

    /// <summary>用整体位移表现前冲、闪避、受击和倒下，不旋转像素画面以免产生抗锯齿。</summary>
    private static Vector2 ResolveActionOffset(
        CharacterAppearanceDefinition appearance,
        CharacterAnimationState state,
        float elapsed,
        int side)
    {
        float attackDistance = appearance.WeaponSilhouette switch
        {
            CharacterWeaponSilhouette.Spear => 16.0f,
            CharacterWeaponSilhouette.Bow => 6.0f,
            CharacterWeaponSilhouette.Tome => 4.0f,
            _ => 24.0f
        };

        return state switch
        {
            CharacterAnimationState.Attack => new Vector2(
                side * Mathf.Sin(Mathf.Clamp(elapsed / 0.34f, 0, 1) * Mathf.Pi) * attackDistance,
                0),
            CharacterAnimationState.Cast => new Vector2(0, -Mathf.Abs(Mathf.Sin(elapsed * 9.0f)) * 4.0f),
            CharacterAnimationState.Dodge => new Vector2(
                -side * Mathf.Sin(Mathf.Clamp(elapsed / 0.28f, 0, 1) * Mathf.Pi) * 22.0f,
                0),
            CharacterAnimationState.Hit => new Vector2(-side * Mathf.Abs(Mathf.Sin(elapsed * 24.0f)) * 5.0f, 2.0f),
            CharacterAnimationState.Defeat => new Vector2(-side * Mathf.Min(12.0f, elapsed * 15.0f), Mathf.Min(48.0f, elapsed * 55.0f)),
            _ => Vector2.Zero
        };
    }

    /// <summary>倒下时逐步淡出，其余动作保持完全不透明。</summary>
    private static float ResolveOpacity(CharacterAnimationState state, float elapsed)
    {
        return state == CharacterAnimationState.Defeat
            ? Mathf.Clamp(1.0f - elapsed * 0.58f, 0.18f, 1.0f)
            : 1.0f;
    }

    /// <summary>披风从肩后自然落下，攻击最大姿势才向后展开，不再是固定大矩形。</summary>
    private void DrawCape(
        Vector2 origin,
        CharacterAppearanceDefinition appearance,
        int side,
        int pose,
        Color outline,
        Color accent)
    {
        if (!appearance.HasCape)
        {
            return;
        }

        int drag = pose == 2 ? -side * 4 : pose == 1 ? -side * 2 : 0;
        int backX = side > 0 ? 38 : 52;
        int x = backX + drag;
        Color capeShadow = accent.Darkened(0.30f);

        // 上窄下宽并带缺口，表现轻薄布料而不是整块色板。
        Pixel(origin, x, 31, 8, 4, outline);
        Pixel(origin, x + 1, 32, 6, 4, accent.Darkened(0.10f));
        Pixel(origin, x - side, 35, 9, 12, outline);
        Pixel(origin, x - side + 1, 36, 7, 10, accent.Darkened(0.18f));
        Pixel(origin, x - side * 2, 45, 10, 15, outline);
        Pixel(origin, x - side * 2 + 1, 46, 8, 12, capeShadow);
        Pixel(origin, x - side * 3, 57, 7, 7, capeShadow.Darkened(0.08f));
    }

    /// <summary>绘制比旧模型更长的双腿，并给膝、胫甲、靴口明确分节。</summary>
    private void DrawLegs(
        Vector2 origin,
        CharacterAppearanceDefinition appearance,
        int side,
        int pose,
        Color outline,
        Color cloth,
        Color shadow,
        Color light,
        Color leather,
        Color metalShadow)
    {
        int step = pose == 2 && appearance.WeaponSilhouette is CharacterWeaponSilhouette.Sword or CharacterWeaponSilhouette.Spear
            ? side * 2
            : 0;
        int frontX = side > 0 ? 50 + step : 42 + step;
        int backX = side > 0 ? 43 - step : 49 - step;

        if (appearance.BodySilhouette == CharacterBodySilhouette.Robed)
        {
            // 袍子前开衩露出小腿，让人物仍然有腿长和行动方向，而不是梯形布袋。
            Pixel(origin, 39, 49, 18, 5, outline);
            Pixel(origin, 40, 50, 16, 4, cloth);
            Pixel(origin, 38, 53, 9, 20, outline);
            Pixel(origin, 49, 53, 10, 20, outline);
            Pixel(origin, 39, 54, 7, 17, cloth);
            Pixel(origin, 50, 54, 8, 17, shadow);
            Pixel(origin, 44, 56, 2, 14, light);
            Pixel(origin, 47, 58, 2, 15, new Color(0.08f, 0.07f, 0.09f, cloth.A));
            Pixel(origin, 40, 72, 5, 12, outline);
            Pixel(origin, 51, 72, 5, 12, outline);
            Pixel(origin, 41, 73, 3, 9, leather);
            Pixel(origin, 52, 73, 3, 9, leather);
            Pixel(origin, 39, 83, 7, 3, outline);
            Pixel(origin, 50, 83, 7, 3, outline);
            return;
        }

        Color legMain = appearance.BodySilhouette == CharacterBodySilhouette.Armored ? metalShadow : shadow;
        Color legLight = appearance.BodySilhouette == CharacterBodySilhouette.Armored ? cloth.Lightened(0.20f) : cloth.Darkened(0.10f);

        // 大腿较窄，小腿再收一像素，整体腿长明显高于旧模型。
        Pixel(origin, frontX, 51, 6, 16, outline);
        Pixel(origin, frontX + 1, 52, 4, 14, legMain);
        Pixel(origin, backX, 52, 6, 15, outline);
        Pixel(origin, backX + 1, 53, 4, 13, legLight);

        // 膝部错开一像素，让站姿不再像两根平行柱子。
        int kneeShift = pose == 2 ? side : 0;
        Pixel(origin, frontX + kneeShift, 66, 6, 5, outline);
        Pixel(origin, frontX + kneeShift + 1, 67, 4, 3, appearance.BodySilhouette == CharacterBodySilhouette.Armored ? cloth : leather);
        Pixel(origin, backX - kneeShift, 66, 6, 5, outline);
        Pixel(origin, backX - kneeShift + 1, 67, 4, 3, appearance.BodySilhouette == CharacterBodySilhouette.Armored ? cloth.Darkened(0.12f) : leather);

        Pixel(origin, frontX + kneeShift, 70, 5, 14, outline);
        Pixel(origin, frontX + kneeShift + 1, 71, 3, 11, appearance.BodySilhouette == CharacterBodySilhouette.Armored ? metalShadow : leather);
        Pixel(origin, backX - kneeShift, 70, 5, 14, outline);
        Pixel(origin, backX - kneeShift + 1, 71, 3, 11, appearance.BodySilhouette == CharacterBodySilhouette.Armored ? metalShadow.Darkened(0.08f) : leather.Darkened(0.10f));

        // 靴尖朝向目标，侧视方向会更明显。
        Pixel(origin, frontX - (side < 0 ? 2 : 0) + kneeShift, 83, 8, 4, outline);
        Pixel(origin, backX - (side < 0 ? 2 : 0) - kneeShift, 83, 8, 4, outline);
    }

    /// <summary>绘制窄腰躯干；不同职业只改变肩甲、外衣和袍摆，不改变基本人体比例。</summary>
    private void DrawTorso(
        Vector2 origin,
        CharacterAppearanceDefinition appearance,
        int side,
        int pose,
        Color outline,
        Color cloth,
        Color shadow,
        Color light,
        Color accent,
        Color leather,
        Color metal,
        Color metalShadow)
    {
        int lean = pose == 2 && appearance.WeaponSilhouette is CharacterWeaponSilhouette.Sword or CharacterWeaponSilhouette.Spear
            ? side
            : 0;

        // 肩膀只在上端稍宽，胸腔向腰部收窄，避免旧模型“从肩到胯一样宽”。
        Pixel(origin, 39 + lean, 27, 18, 6, outline);
        Pixel(origin, 40 + lean, 28, 16, 5, cloth);
        Pixel(origin, 41 + lean, 32, 14, 14, outline);
        Pixel(origin, 42 + lean, 32, 12, 13, cloth);
        Pixel(origin, 43 + lean, 33, 3, 10, light);
        Pixel(origin, 51 + lean, 36, 3, 8, shadow);
        Pixel(origin, 43 + lean, 44, 11, 5, outline);
        Pixel(origin, 44 + lean, 44, 9, 4, leather);
        Pixel(origin, 47 + lean, 45, 2, 2, accent);

        if (appearance.BodySilhouette == CharacterBodySilhouette.Armored)
        {
            // 甲片只覆盖胸口与肩头，腰部仍然收紧，形成古典骑士而不是方形机器人。
            Pixel(origin, 37 + lean, 28, 5, 5, outline);
            Pixel(origin, 38 + lean, 29, 4, 3, metalShadow);
            Pixel(origin, 54 + lean, 28, 5, 5, outline);
            Pixel(origin, 54 + lean, 29, 4, 3, metalShadow);
            Pixel(origin, 42 + lean, 31, 12, 4, metal);
            Pixel(origin, 43 + lean, 32, 3, 3, metal.Lightened(0.12f));
            Pixel(origin, 52 + lean, 35, 2, 8, metalShadow);
            Pixel(origin, 42 + lean, 43, 12, 2, accent.Darkened(0.08f));
            Pixel(origin, 41 + lean, 48, 5, 4, metalShadow);
            Pixel(origin, 51 + lean, 48, 5, 4, metalShadow);
        }
        else if (appearance.BodySilhouette == CharacterBodySilhouette.Robed)
        {
            // 高领和细腰让法师更像穿多层布袍的人，而不是从肩直接展开的斗篷。
            Pixel(origin, 42 + lean, 26, 12, 3, accent.Darkened(0.16f));
            Pixel(origin, 41 + lean, 45, 14, 5, outline);
            Pixel(origin, 42 + lean, 46, 12, 3, accent.Darkened(0.12f));
            Pixel(origin, 38 + lean, 48, 6, 7, outline);
            Pixel(origin, 52 + lean, 48, 6, 7, outline);
        }
        else
        {
            // 轻装角色增加斜向胸带和短外衣下摆，建立剑士/弓手的层次。
            int strapX = side > 0 ? 43 : 51;
            for (int y = 32; y <= 41; y += 2)
            {
                Pixel(origin, strapX + (side > 0 ? (y - 32) / 3 : -(y - 32) / 3), y, 2, 3, leather);
            }
            Pixel(origin, 40 + lean, 47, 16, 5, outline);
            Pixel(origin, 41 + lean, 47, 14, 4, cloth.Darkened(0.12f));
        }
    }

    /// <summary>绘制颈部、下颌、鼻梁和分层发型；人物 ID 决定原创个人发型差异。</summary>
    private void DrawNeckAndHead(
        Vector2 origin,
        UnitModel unit,
        CharacterAppearanceDefinition appearance,
        int side,
        int pose,
        Color outline,
        Color hair,
        Color hairShadow,
        Color hairLight,
        Color skin,
        Color skinShadow)
    {
        int headDrop = appearance.WeaponSilhouette == CharacterWeaponSilhouette.Spear && pose == 1 ? 1 : 0;
        int faceX = side > 0 ? 45 : 43;

        // 细颈让头和肩之间有真实间距。
        Pixel(origin, 46, 24 + headDrop, 5, 5, outline);
        Pixel(origin, 47, 24 + headDrop, 3, 4, skinShadow);

        // 头部约 10×12 逻辑像素，比旧 15×14 明显更小。
        Pixel(origin, 43, 12 + headDrop, 11, 12, outline);
        Pixel(origin, 44, 14 + headDrop, 9, 9, skin);
        Pixel(origin, faceX, 21 + headDrop, 7, 3, skinShadow);

        // 侧脸前方只突出一像素鼻梁，下颌向颈部收回。
        int noseX = side > 0 ? 53 : 42;
        Pixel(origin, noseX, 17 + headDrop, 2, 2, skin);
        Pixel(origin, side > 0 ? 51 : 44, 18 + headDrop, 1, 1, outline);
        Pixel(origin, side > 0 ? 52 : 44, 22 + headDrop, 1, 1, outline);

        DrawPersonalHair(origin, unit, appearance, side, pose, headDrop, outline, hair, hairShadow, hairLight);
    }

    /// <summary>给四名主角与通用敌军绘制不同原创发型轮廓。</summary>
    private void DrawPersonalHair(
        Vector2 origin,
        UnitModel unit,
        CharacterAppearanceDefinition appearance,
        int side,
        int pose,
        int headDrop,
        Color outline,
        Color hair,
        Color shadow,
        Color light)
    {
        string id = unit.Id.ToLowerInvariant();

        // 基础发冠都遵循前短后长的侧视层次。
        Pixel(origin, 42, 11 + headDrop, 13, 5, outline);
        Pixel(origin, 43, 12 + headDrop, 11, 4, hair);
        Pixel(origin, 44, 12 + headDrop, 5, 2, light);

        if (id == "celine")
        {
            // Celine：后脑长发束和较轻的前发，强调弓手轻盈感。
            int drag = pose == 2 ? -side * 2 : 0;
            int backX = (side > 0 ? 41 : 52) + drag;
            Pixel(origin, backX, 15 + headDrop, 4, 13, outline);
            Pixel(origin, backX + 1, 16 + headDrop, 3, 11, hair);
            Pixel(origin, backX - side, 25 + headDrop, 4, 7, shadow);
            Pixel(origin, side > 0 ? 49 : 44, 14 + headDrop, 5, 3, hair);
            return;
        }

        if (id == "mira")
        {
            // Mira：更长的后发分成两束，和长袍的垂直轮廓呼应。
            int drag = pose == 2 ? -side : 0;
            int backX = (side > 0 ? 40 : 52) + drag;
            Pixel(origin, backX, 15 + headDrop, 5, 15, outline);
            Pixel(origin, backX + 1, 16 + headDrop, 3, 13, hair);
            Pixel(origin, backX - side, 27 + headDrop, 4, 8, shadow);
            Pixel(origin, side > 0 ? 48 : 44, 13 + headDrop, 6, 4, hair);
            return;
        }

        if (id == "rowan")
        {
            // Rowan：短而整齐的发型，露出更多脸部和颈部，配合重甲职业。
            Pixel(origin, side > 0 ? 42 : 52, 15 + headDrop, 3, 6, shadow);
            Pixel(origin, side > 0 ? 49 : 44, 13 + headDrop, 5, 3, hair);
            return;
        }

        if (id == "adrian")
        {
            // Adrian：不规则短发尖角形成主角辨识度，但保持原创轮廓。
            Pixel(origin, 42, 10 + headDrop, 4, 3, outline);
            Pixel(origin, 49, 9 + headDrop, 4, 4, outline);
            Pixel(origin, 43, 11 + headDrop, 3, 2, hair);
            Pixel(origin, 49, 10 + headDrop, 3, 3, hair);
            Pixel(origin, side > 0 ? 42 : 52, 15 + headDrop, 3, 7, shadow);
            return;
        }

        Pixel(origin, side > 0 ? 42 : 52, 15 + headDrop, 3, 7, shadow);
    }

    /// <summary>按武器动作重绘肩、肘、手三段关系，让动作不再像横向伸出一根方块。</summary>
    private void DrawArms(
        Vector2 origin,
        CharacterAppearanceDefinition appearance,
        int side,
        int pose,
        CharacterAnimationState state,
        Color outline,
        Color cloth,
        Color shadow,
        Color skin,
        Color leather,
        Color metalShadow)
    {
        bool action = state is CharacterAnimationState.Attack or CharacterAnimationState.Cast;
        int shoulderFront = side > 0 ? 54 : 37;
        int shoulderBack = side > 0 ? 39 : 52;

        if (appearance.WeaponSilhouette == CharacterWeaponSilhouette.Bow && action)
        {
            // 持弓臂伸直，拉弦臂折回脸侧，两个肘点清楚分离。
            int reach = pose == 2 ? 9 : 6;
            Pixel(origin, shoulderFront, 31, 5 + reach, 4, outline);
            Pixel(origin, shoulderFront + 1 + Math.Min(0, side * reach), 32, 4 + reach, 2, cloth);
            Pixel(origin, side > 0 ? 58 + reach : 35 - reach, 31, 3, 3, skin);
            int pullX = side > 0 ? 43 - (pose == 2 ? 4 : 1) : 50 + (pose == 2 ? 4 : 1);
            Pixel(origin, pullX, 29, 6, 4, outline);
            Pixel(origin, pullX + 1, 30, 4, 2, leather);
            Pixel(origin, side > 0 ? pullX + 4 : pullX, 29, 2, 2, skin);
            return;
        }

        if (appearance.WeaponSilhouette == CharacterWeaponSilhouette.Tome && state == CharacterAnimationState.Cast)
        {
            // 施法手从胸前抬到肩线上方，另一手托住法书。
            int reach = pose == 2 ? 9 : 5;
            int y = pose == 2 ? 27 : 31;
            int startX = side > 0 ? 53 : 38 - reach;
            Pixel(origin, startX, y, 5 + reach, 4, outline);
            Pixel(origin, startX + 1, y + 1, 3 + reach, 2, cloth);
            Pixel(origin, side > 0 ? startX + 4 + reach : startX - 1, y, 3, 3, skin);
            Pixel(origin, shoulderBack, 38, 7, 4, outline);
            Pixel(origin, shoulderBack + 1, 39, 5, 2, shadow);
            return;
        }

        if (appearance.WeaponSilhouette == CharacterWeaponSilhouette.Spear && action)
        {
            // 枪兵双手一前一后抓杆，最大姿势时两臂接近水平。
            int reach = pose == 2 ? 8 : 4;
            int frontStart = side > 0 ? shoulderFront : shoulderFront - reach;
            Pixel(origin, frontStart, 32, 5 + reach, 4, outline);
            Pixel(origin, frontStart + 1, 33, 3 + reach, 2, appearance.BodySilhouette == CharacterBodySilhouette.Armored ? metalShadow : cloth);
            Pixel(origin, side > 0 ? 48 : 43, 37, 8, 4, outline);
            Pixel(origin, side > 0 ? 49 : 44, 38, 6, 2, leather);
            return;
        }

        // 剑士和普通待机：上臂斜下，攻击时前臂再延伸，保持关节感。
        int attackReach = action ? (pose == 2 ? 8 : pose == 1 ? 2 : 5) : 1;
        int frontX = side > 0 ? shoulderFront : shoulderFront - attackReach;
        Pixel(origin, frontX, 31, 5 + attackReach, 5, outline);
        Pixel(origin, frontX + 1, 32, 3 + attackReach, 3, cloth);
        Pixel(origin, side > 0 ? frontX + 4 + attackReach : frontX - 1, 33, 3, 3, skin);
        Pixel(origin, shoulderBack, 37, 6, 5, outline);
        Pixel(origin, shoulderBack + 1, 38, 4, 3, leather);
    }

    /// <summary>按剑、枪、弓、法书分别绘制更细长的武器轮廓。</summary>
    private void DrawWeapon(
        Vector2 origin,
        CharacterAppearanceDefinition appearance,
        int side,
        int pose,
        CharacterAnimationState state,
        Color outline,
        Color accent,
        Color leather,
        Color metal,
        Color metalLight,
        Color metalShadow)
    {
        bool action = state is CharacterAnimationState.Attack or CharacterAnimationState.Cast;
        switch (appearance.WeaponSilhouette)
        {
            case CharacterWeaponSilhouette.Spear:
                DrawSpear(origin, side, pose, action, leather, metal, metalLight, metalShadow);
                break;
            case CharacterWeaponSilhouette.Bow:
                DrawBow(origin, side, pose, action, outline, accent, leather);
                break;
            case CharacterWeaponSilhouette.Tome:
                DrawTome(origin, side, pose, state == CharacterAnimationState.Cast, outline, accent, leather);
                break;
            default:
                DrawSword(origin, side, pose, action, accent, leather, metal, metalLight, metalShadow);
                break;
        }
    }

    /// <summary>剑身变窄变长，护手和握把独立，不再像粗银色矩形。</summary>
    private void DrawSword(
        Vector2 origin,
        int side,
        int pose,
        bool action,
        Color accent,
        Color leather,
        Color metal,
        Color metalLight,
        Color metalShadow)
    {
        if (!action)
        {
            int x = side > 0 ? 61 : 33;
            Pixel(origin, x, 27, 2, 24, metalShadow);
            Pixel(origin, x + (side > 0 ? 1 : 0), 25, 1, 24, metal);
            Pixel(origin, x - 2, 48, 6, 2, accent.Darkened(0.18f));
            Pixel(origin, x, 50, 2, 7, leather);
            return;
        }

        if (pose == 1)
        {
            // 蓄势：剑向后上方斜举，用阶梯像素表现斜线。
            int baseX = side > 0 ? 57 : 38;
            for (int i = 0; i < 10; i++)
            {
                Pixel(origin, baseX - side * i, 31 - i * 2, 2, 3, i < 2 ? leather : metal);
            }
            return;
        }

        if (pose == 2)
        {
            // 最大斩击：剑刃接近水平，长度明显超过前臂。
            int startX = side > 0 ? 58 : 15;
            Pixel(origin, startX, 30, 30, 2, metalShadow);
            Pixel(origin, startX, 29, 30, 1, metalLight);
            Pixel(origin, side > 0 ? 58 : 43, 28, 2, 5, leather);
            Pixel(origin, side > 0 ? 55 : 41, 28, 7, 2, accent.Darkened(0.14f));
            return;
        }

        int recoverX = side > 0 ? 60 : 34;
        Pixel(origin, recoverX, 34, 2, 22, metal);
        Pixel(origin, recoverX - 2, 52, 6, 2, accent.Darkened(0.18f));
        Pixel(origin, recoverX, 54, 2, 6, leather);
    }

    /// <summary>长枪以细杆和更小枪头表现长度感，攻击最大姿势完全水平。</summary>
    private void DrawSpear(
        Vector2 origin,
        int side,
        int pose,
        bool action,
        Color leather,
        Color metal,
        Color metalLight,
        Color metalShadow)
    {
        if (action && pose == 2)
        {
            int startX = side > 0 ? 27 : 12;
            Pixel(origin, startX, 36, 57, 2, leather);
            int tipX = side > 0 ? 84 : 9;
            Pixel(origin, tipX, 34, 7, 6, metalShadow);
            Pixel(origin, side > 0 ? tipX + 2 : tipX - 2, 35, 7, 4, metal);
            Pixel(origin, side > 0 ? tipX + 6 : tipX - 3, 36, 3, 2, metalLight);
            return;
        }

        int x = side > 0 ? 62 : 33;
        Pixel(origin, x, 18, 2, 43, leather);
        Pixel(origin, x - 2, 14, 6, 7, metalShadow);
        Pixel(origin, x, 13, 2, 8, metal);
        Pixel(origin, x + (side > 0 ? 1 : 0), 13, 1, 5, metalLight);
    }

    /// <summary>弓使用窄弧、弦和握把三层，拉满时弦靠近脸侧。</summary>
    private void DrawBow(
        Vector2 origin,
        int side,
        int pose,
        bool action,
        Color outline,
        Color accent,
        Color leather)
    {
        int x = side > 0 ? 68 : 27;
        int bend = action && pose == 2 ? 3 : 1;
        Color wood = accent.Darkened(0.25f);

        Pixel(origin, x, 24, 2, 7, wood);
        Pixel(origin, x + side * bend, 30, 2, 8, wood);
        Pixel(origin, x + side * (bend + 1), 38, 2, 8, wood);
        Pixel(origin, x + side * bend, 46, 2, 8, wood);
        Pixel(origin, x, 54, 2, 7, wood);
        Pixel(origin, x + (side > 0 ? 1 : 0), 36, 2, 7, leather);

        int stringX = action && pose == 2 ? (side > 0 ? 49 : 46) : x;
        DrawLine(
            origin + new Vector2(stringX * PixelScale, 25 * PixelScale),
            origin + new Vector2(stringX * PixelScale, 59 * PixelScale),
            outline,
            PixelScale,
            false);
    }

    /// <summary>法书缩小到手掌尺度，施法时在书页上方出现离散符文像素。</summary>
    private void DrawTome(
        Vector2 origin,
        int side,
        int pose,
        bool casting,
        Color outline,
        Color accent,
        Color leather)
    {
        int x = side > 0 ? 39 : 51;
        Pixel(origin, x, 36, 8, 7, outline);
        Pixel(origin, x + 1, 37, 3, 5, leather);
        Pixel(origin, x + 4, 37, 3, 5, accent.Darkened(0.18f));
        Pixel(origin, x + 3, 37, 1, 5, accent.Lightened(0.24f));

        if (!casting)
        {
            return;
        }

        int lift = pose == 2 ? 0 : 2;
        Pixel(origin, 47, 26 + lift, 2, 2, accent.Lightened(0.34f));
        Pixel(origin, 43, 29 + lift, 2, 2, accent);
        Pixel(origin, 52, 30 + lift, 2, 2, accent.Lightened(0.20f));
    }

    /// <summary>读取当前人物。</summary>
    private UnitModel? ReadUnit()
    {
        return _source is null ? null : _unitField?.GetValue(_source) as UnitModel;
    }

    /// <summary>读取当前动作状态。</summary>
    private CharacterAnimationState ReadState()
    {
        return _source is not null && _stateField?.GetValue(_source) is CharacterAnimationState state
            ? state
            : CharacterAnimationState.Idle;
    }

    /// <summary>读取当前动作已经播放的秒数。</summary>
    private float ReadElapsed()
    {
        return _source is not null && _elapsedField?.GetValue(_source) is float elapsed
            ? elapsed
            : 0.0f;
    }

    /// <summary>逻辑坐标锁到 4px 栅格。</summary>
    private static Vector2 SnapLogical(Vector2 value)
    {
        return new Vector2(
            Mathf.Round(value.X / PixelScale) * PixelScale,
            Mathf.Round(value.Y / PixelScale) * PixelScale);
    }

    /// <summary>按逻辑像素绘制硬边矩形。</summary>
    private void Pixel(Vector2 origin, int x, int y, int width, int height, Color color)
    {
        DrawRect(
            new Rect2(
                origin + new Vector2(x * PixelScale, y * PixelScale),
                new Vector2(width * PixelScale, height * PixelScale)),
            color,
            true);
    }

    /// <summary>给颜色统一套用动作透明度。</summary>
    private static Color Fade(Color color, float opacity)
    {
        return new Color(color.R, color.G, color.B, color.A * opacity);
    }
}
