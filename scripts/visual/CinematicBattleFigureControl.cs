using FlameEmblem.Game;
using Godot;
using System.Reflection;

namespace FlameEmblem.Visual;

/// <summary>
/// 缺少正式 battle PNG 时使用的原创侧视战斗人物层。
/// 人体保持小头、窄腰、长腿和清晰职业轮廓；动作改为蓄势、快速出手、收势三段式，
/// 避免旧程序人物整段正弦滑动和武器突然跳位造成的僵硬感。
/// </summary>
public partial class CinematicBattleFigureControl : Control
{
    /// <summary>实际由战斗时间线驱动的原人物控件。</summary>
    private AnimatedBattleCharacterControl? _source;

    /// <summary>读取原人物当前单位。</summary>
    private FieldInfo? _unitField;

    /// <summary>读取原人物当前动画状态。</summary>
    private FieldInfo? _stateField;

    /// <summary>读取原人物当前状态计时。</summary>
    private FieldInfo? _elapsedField;

    /// <summary>一个逻辑像素放大为 4 个屏幕像素。</summary>
    private const float PixelScale = 4.0f;

    /// <summary>攻击程序动作与原时间线保持一致的基础时长。</summary>
    private const float AttackDuration = 0.34f;

    /// <summary>施法程序动作与原时间线保持一致的基础时长。</summary>
    private const float CastDuration = 0.50f;

    /// <summary>绑定现有战斗人物，只接管视觉，不修改任何命中、伤害或战斗顺序。</summary>
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

    /// <summary>原时间线逐帧推进，本层只跟着请求重绘。</summary>
    public override void _Process(double delta)
    {
        QueueRedraw();
    }

    /// <summary>正式战斗素材优先；没有正式素材时才隐藏旧程序人物并绘制新动作。</summary>
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

        _source.SelfModulate = new Color(1, 1, 1, 0);
        HideLegacyDetailOverlay(true);
        DrawCinematicFigure(unit, ReadState(), ReadElapsed());
    }

    /// <summary>只有 battle 单图或任意 battle 状态帧才算正式侧视战斗素材。</summary>
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

    /// <summary>隐藏旧补细节层，避免和新的完整人物骨架叠在一起。</summary>
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

        foreach (RefinedBattleFigureControl refined in _source.GetChildren().OfType<RefinedBattleFigureControl>())
        {
            refined.Visible = !hidden;
        }
    }

    /// <summary>绘制一帧完整战斗人物。</summary>
    private void DrawCinematicFigure(UnitModel unit, CharacterAnimationState state, float elapsed)
    {
        CharacterAppearanceDefinition appearance = CharacterAppearanceCatalog.Get(unit);
        int side = _source?.MirrorHorizontally == true ? -1 : 1;
        ActionPose pose = ResolvePose(state, elapsed);
        Vector2 motion = ResolveBodyMotion(appearance, state, elapsed, side);
        float opacity = ResolveOpacity(state, elapsed);
        Vector2 origin = new Vector2(18, 5) + SnapLogical(motion);

        Color outline = Fade(new Color("171820"), opacity);
        Color hair = Fade(appearance.HairColor, opacity);
        Color hairShadow = Fade(appearance.HairColor.Darkened(0.27f), opacity);
        Color hairLight = Fade(appearance.HairColor.Lightened(0.16f), opacity);
        Color skin = Fade(appearance.SkinColor, opacity);
        Color skinShadow = Fade(appearance.SkinColor.Darkened(0.17f), opacity);
        Color cloth = Fade(appearance.OutfitColor, opacity);
        Color clothShadow = Fade(appearance.OutfitColor.Darkened(0.31f), opacity);
        Color clothLight = Fade(appearance.OutfitColor.Lightened(0.19f), opacity);
        Color accent = Fade(appearance.AccentColor, opacity);
        Color leather = Fade(new Color("4b3429"), opacity);
        Color metal = Fade(new Color("bcc4c7"), opacity);
        Color metalLight = Fade(new Color("e0e5e3"), opacity);
        Color metalShadow = Fade(new Color("56616b"), opacity);

        DrawGrounding(origin, unit, opacity);
        DrawCape(origin, appearance, side, pose, outline, accent);
        DrawLegs(origin, appearance, side, pose, outline, cloth, clothShadow, clothLight, leather, metalShadow);
        DrawTorso(origin, appearance, side, pose, outline, cloth, clothShadow, clothLight, accent, leather, metal, metalShadow);
        DrawHead(origin, unit, appearance, side, pose, outline, hair, hairShadow, hairLight, skin, skinShadow);
        DrawArms(origin, appearance, side, pose, state, outline, cloth, clothShadow, skin, leather, metalShadow);
        DrawWeapon(origin, appearance, side, pose, state, outline, accent, leather, metal, metalLight, metalShadow);
    }

    /// <summary>人物脚下使用低矮阴影和阵营细线，既落地又加强深蓝/粉红识别。</summary>
    private void DrawGrounding(Vector2 origin, UnitModel unit, float opacity)
    {
        Color shadow = new(0.05f, 0.06f, 0.08f, 0.42f * opacity);
        Color team = Fade(TeamVisualPalette.Primary(unit.Team), opacity);
        Pixel(origin, 34, 87, 29, 3, shadow);
        Pixel(origin, 39, 88, 19, 1, team);
    }

    /// <summary>把 Attack/Cast 时间切成待机、蓄势、出手、收势四种姿势。</summary>
    private static ActionPose ResolvePose(CharacterAnimationState state, float elapsed)
    {
        if (state == CharacterAnimationState.Attack)
        {
            float t = Mathf.Clamp(elapsed / AttackDuration, 0.0f, 1.0f);
            return t < 0.31f
                ? ActionPose.Windup
                : t < 0.61f
                    ? ActionPose.Strike
                    : ActionPose.Recover;
        }

        if (state == CharacterAnimationState.Cast)
        {
            float t = Mathf.Clamp(elapsed / CastDuration, 0.0f, 1.0f);
            return t < 0.36f
                ? ActionPose.Windup
                : t < 0.72f
                    ? ActionPose.Strike
                    : ActionPose.Recover;
        }

        return ActionPose.Idle;
    }

    /// <summary>
    /// 计算整体重心位移。
    /// 剑/枪只在真正出手阶段快速向前，弓手基本原地拉弓，法师只做轻微上浮；
    /// 受击也只做一次后仰，不再连续抖动。
    /// </summary>
    private static Vector2 ResolveBodyMotion(
        CharacterAppearanceDefinition appearance,
        CharacterAnimationState state,
        float elapsed,
        int side)
    {
        if (state == CharacterAnimationState.Attack)
        {
            float t = Mathf.Clamp(elapsed / AttackDuration, 0.0f, 1.0f);
            float distance = appearance.WeaponSilhouette switch
            {
                CharacterWeaponSilhouette.Spear => 16.0f,
                CharacterWeaponSilhouette.Bow => 2.0f,
                CharacterWeaponSilhouette.Tome => 0.0f,
                _ => 24.0f
            };

            if (t < 0.31f)
            {
                // 蓄势略向后压重心，给后续出手留出视觉距离。
                float windup = EaseOut(t / 0.31f);
                return new Vector2(-side * windup * Math.Min(4.0f, distance * 0.18f), 0);
            }

            if (t < 0.61f)
            {
                // 出手阶段快速前冲，视觉速度集中在很短的一段，而不是整段来回滑。
                float strike = EaseOut((t - 0.31f) / 0.30f);
                return new Vector2(side * strike * distance, 0);
            }

            // 收势逐步退回原位。
            float recover = EaseInOut((t - 0.61f) / 0.39f);
            return new Vector2(side * (1.0f - recover) * distance, 0);
        }

        if (state == CharacterAnimationState.Cast)
        {
            float t = Mathf.Clamp(elapsed / CastDuration, 0.0f, 1.0f);
            float lift = t < 0.72f ? EaseOut(t / 0.72f) * 3.0f : (1.0f - EaseInOut((t - 0.72f) / 0.28f)) * 3.0f;
            return new Vector2(0, -lift);
        }

        if (state == CharacterAnimationState.Dodge)
        {
            float t = Mathf.Clamp(elapsed / 0.28f, 0.0f, 1.0f);
            return new Vector2(-side * Mathf.Sin(t * Mathf.Pi) * 24.0f, 0);
        }

        if (state == CharacterAnimationState.Hit)
        {
            float t = Mathf.Clamp(elapsed / 0.24f, 0.0f, 1.0f);
            float recoil = Mathf.Sin(t * Mathf.Pi) * 8.0f;
            return new Vector2(-side * recoil, Mathf.Sin(t * Mathf.Pi) * 2.0f);
        }

        if (state == CharacterAnimationState.Defeat)
        {
            float t = Mathf.Clamp(elapsed / 0.90f, 0.0f, 1.0f);
            return new Vector2(-side * EaseOut(t) * 10.0f, EaseInOut(t) * 44.0f);
        }

        return Vector2.Zero;
    }

    /// <summary>倒下时缓慢淡出，其余动作保持完全不透明。</summary>
    private static float ResolveOpacity(CharacterAnimationState state, float elapsed)
    {
        if (state != CharacterAnimationState.Defeat)
        {
            return 1.0f;
        }

        float t = Mathf.Clamp(elapsed / 1.10f, 0.0f, 1.0f);
        return Mathf.Lerp(1.0f, 0.18f, EaseInOut(t));
    }

    /// <summary>披风在蓄势时收紧、出手时向后展开、收势时逐步落回。</summary>
    private void DrawCape(
        Vector2 origin,
        CharacterAppearanceDefinition appearance,
        int side,
        ActionPose pose,
        Color outline,
        Color accent)
    {
        if (!appearance.HasCape)
        {
            return;
        }

        int drag = pose switch
        {
            ActionPose.Windup => -side,
            ActionPose.Strike => -side * 5,
            ActionPose.Recover => -side * 2,
            _ => 0
        };
        int x = (side > 0 ? 38 : 52) + drag;
        Color cape = accent.Darkened(0.16f);
        Color capeShadow = accent.Darkened(0.32f);

        Pixel(origin, x, 31, 8, 4, outline);
        Pixel(origin, x + 1, 32, 6, 3, cape);
        Pixel(origin, x - side, 35, 9, 13, outline);
        Pixel(origin, x - side + 1, 36, 7, 11, cape);
        Pixel(origin, x - side * 2, 46, 10, 16, outline);
        Pixel(origin, x - side * 2 + 1, 47, 8, 13, capeShadow);
        Pixel(origin, x - side * 3, 59, 7, 5, capeShadow.Darkened(0.08f));
    }

    /// <summary>根据职业与动作重心绘制双腿；近战出手跨步，弓手后压，法师保持稳定。</summary>
    private void DrawLegs(
        Vector2 origin,
        CharacterAppearanceDefinition appearance,
        int side,
        ActionPose pose,
        Color outline,
        Color cloth,
        Color shadow,
        Color light,
        Color leather,
        Color metalShadow)
    {
        if (appearance.BodySilhouette == CharacterBodySilhouette.Robed)
        {
            // 长袍保持窄腰和开衩，施法时只让下摆略向后拖，不把整个身体拉成梯形。
            int robeDrag = pose == ActionPose.Strike ? -side : 0;
            Pixel(origin, 39 + robeDrag, 49, 18, 5, outline);
            Pixel(origin, 40 + robeDrag, 50, 16, 4, cloth);
            Pixel(origin, 38 + robeDrag, 53, 9, 20, outline);
            Pixel(origin, 49 + robeDrag, 53, 10, 20, outline);
            Pixel(origin, 39 + robeDrag, 54, 7, 17, cloth);
            Pixel(origin, 50 + robeDrag, 54, 8, 17, shadow);
            Pixel(origin, 44 + robeDrag, 56, 2, 14, light);
            Pixel(origin, 47 + robeDrag, 58, 2, 15, new Color(0.08f, 0.07f, 0.10f, cloth.A));
            Pixel(origin, 40, 72, 5, 12, outline);
            Pixel(origin, 51, 72, 5, 12, outline);
            Pixel(origin, 41, 73, 3, 9, leather);
            Pixel(origin, 52, 73, 3, 9, leather);
            Pixel(origin, 39, 83, 7, 3, outline);
            Pixel(origin, 50, 83, 7, 3, outline);
            return;
        }

        int stride = pose == ActionPose.Strike && appearance.WeaponSilhouette is CharacterWeaponSilhouette.Sword or CharacterWeaponSilhouette.Spear
            ? side * 4
            : pose == ActionPose.Windup && appearance.WeaponSilhouette == CharacterWeaponSilhouette.Bow
                ? -side * 2
                : 0;
        int frontX = (side > 0 ? 50 : 42) + stride;
        int backX = (side > 0 ? 43 : 49) - stride / 2;
        Color legMain = appearance.BodySilhouette == CharacterBodySilhouette.Armored ? metalShadow : shadow;

        Pixel(origin, frontX, 51, 6, 16, outline);
        Pixel(origin, frontX + 1, 52, 4, 14, legMain);
        Pixel(origin, backX, 52, 6, 15, outline);
        Pixel(origin, backX + 1, 53, 4, 13, cloth.Darkened(0.10f));

        Pixel(origin, frontX, 66, 6, 5, outline);
        Pixel(origin, frontX + 1, 67, 4, 3, appearance.BodySilhouette == CharacterBodySilhouette.Armored ? cloth : leather);
        Pixel(origin, backX, 66, 6, 5, outline);
        Pixel(origin, backX + 1, 67, 4, 3, appearance.BodySilhouette == CharacterBodySilhouette.Armored ? cloth.Darkened(0.12f) : leather);

        Pixel(origin, frontX, 70, 5, 14, outline);
        Pixel(origin, frontX + 1, 71, 3, 11, appearance.BodySilhouette == CharacterBodySilhouette.Armored ? metalShadow : leather);
        Pixel(origin, backX, 70, 5, 14, outline);
        Pixel(origin, backX + 1, 71, 3, 11, appearance.BodySilhouette == CharacterBodySilhouette.Armored ? metalShadow.Darkened(0.08f) : leather.Darkened(0.10f));

        Pixel(origin, frontX - (side < 0 ? 2 : 0), 83, 8, 4, outline);
        Pixel(origin, backX - (side < 0 ? 2 : 0), 83, 8, 4, outline);
    }

    /// <summary>绘制窄腰躯干，并让近战在出手时产生轻微前倾而不是整个人平移。</summary>
    private void DrawTorso(
        Vector2 origin,
        CharacterAppearanceDefinition appearance,
        int side,
        ActionPose pose,
        Color outline,
        Color cloth,
        Color shadow,
        Color light,
        Color accent,
        Color leather,
        Color metal,
        Color metalShadow)
    {
        int lean = pose switch
        {
            ActionPose.Windup when appearance.WeaponSilhouette is CharacterWeaponSilhouette.Sword or CharacterWeaponSilhouette.Spear => -side,
            ActionPose.Strike when appearance.WeaponSilhouette is CharacterWeaponSilhouette.Sword or CharacterWeaponSilhouette.Spear => side * 2,
            ActionPose.Windup when appearance.WeaponSilhouette == CharacterWeaponSilhouette.Bow => -side,
            _ => 0
        };

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
            // 重甲只在肩胸增加甲片，腰部仍然收紧。
            Pixel(origin, 37 + lean, 28, 5, 5, outline);
            Pixel(origin, 38 + lean, 29, 4, 3, metalShadow);
            Pixel(origin, 54 + lean, 28, 5, 5, outline);
            Pixel(origin, 54 + lean, 29, 4, 3, metalShadow);
            Pixel(origin, 42 + lean, 31, 12, 4, metal);
            Pixel(origin, 43 + lean, 32, 3, 3, metal.Lightened(0.12f));
            Pixel(origin, 52 + lean, 35, 2, 8, metalShadow);
            Pixel(origin, 42 + lean, 43, 12, 2, accent.Darkened(0.10f));
        }
        else if (appearance.BodySilhouette == CharacterBodySilhouette.Robed)
        {
            Pixel(origin, 42 + lean, 26, 12, 3, accent.Darkened(0.16f));
            Pixel(origin, 41 + lean, 45, 14, 5, outline);
            Pixel(origin, 42 + lean, 46, 12, 3, accent.Darkened(0.12f));
        }
        else
        {
            // 轻装保留斜胸带和短外衣层级。
            int strapX = side > 0 ? 43 : 51;
            for (int y = 32; y <= 41; y += 2)
            {
                Pixel(origin, strapX + (side > 0 ? (y - 32) / 3 : -(y - 32) / 3), y, 2, 3, leather);
            }
            Pixel(origin, 40 + lean, 47, 16, 5, outline);
            Pixel(origin, 41 + lean, 47, 14, 4, cloth.Darkened(0.12f));
        }
    }

    /// <summary>绘制细颈、小头和原创个人发型。</summary>
    private void DrawHead(
        Vector2 origin,
        UnitModel unit,
        CharacterAppearanceDefinition appearance,
        int side,
        ActionPose pose,
        Color outline,
        Color hair,
        Color hairShadow,
        Color hairLight,
        Color skin,
        Color skinShadow)
    {
        int headDrop = appearance.WeaponSilhouette == CharacterWeaponSilhouette.Spear && pose == ActionPose.Windup ? 2 : 0;
        Pixel(origin, 46, 24 + headDrop, 5, 5, outline);
        Pixel(origin, 47, 24 + headDrop, 3, 4, skinShadow);

        Pixel(origin, 43, 12 + headDrop, 11, 12, outline);
        Pixel(origin, 44, 14 + headDrop, 9, 9, skin);
        Pixel(origin, side > 0 ? 45 : 43, 21 + headDrop, 7, 3, skinShadow);
        Pixel(origin, side > 0 ? 53 : 42, 17 + headDrop, 2, 2, skin);
        Pixel(origin, side > 0 ? 51 : 44, 18 + headDrop, 1, 1, outline);
        Pixel(origin, side > 0 ? 52 : 44, 22 + headDrop, 1, 1, outline);

        DrawPersonalHair(origin, unit, side, pose, headDrop, outline, hair, hairShadow, hairLight);
    }

    /// <summary>四名主角使用不同原创发型；通用敌军保持短发轮廓。</summary>
    private void DrawPersonalHair(
        Vector2 origin,
        UnitModel unit,
        int side,
        ActionPose pose,
        int headDrop,
        Color outline,
        Color hair,
        Color shadow,
        Color light)
    {
        string id = unit.Id.ToLowerInvariant();
        Pixel(origin, 42, 11 + headDrop, 13, 5, outline);
        Pixel(origin, 43, 12 + headDrop, 11, 4, hair);
        Pixel(origin, 44, 12 + headDrop, 5, 2, light);

        if (id == "celine")
        {
            int drag = pose == ActionPose.Strike ? -side * 2 : 0;
            int backX = (side > 0 ? 41 : 52) + drag;
            Pixel(origin, backX, 15 + headDrop, 4, 13, outline);
            Pixel(origin, backX + 1, 16 + headDrop, 3, 11, hair);
            Pixel(origin, backX - side, 25 + headDrop, 4, 7, shadow);
            return;
        }

        if (id == "mira")
        {
            int drag = pose == ActionPose.Strike ? -side : 0;
            int backX = (side > 0 ? 40 : 52) + drag;
            Pixel(origin, backX, 15 + headDrop, 5, 15, outline);
            Pixel(origin, backX + 1, 16 + headDrop, 3, 13, hair);
            Pixel(origin, backX - side, 27 + headDrop, 4, 8, shadow);
            return;
        }

        if (id == "adrian")
        {
            Pixel(origin, 42, 10 + headDrop, 4, 3, outline);
            Pixel(origin, 49, 9 + headDrop, 4, 4, outline);
            Pixel(origin, 43, 11 + headDrop, 3, 2, hair);
            Pixel(origin, 49, 10 + headDrop, 3, 3, hair);
        }

        Pixel(origin, side > 0 ? 42 : 52, 15 + headDrop, 3, id == "rowan" ? 5 : 7, shadow);
    }

    /// <summary>按武器类型绘制有明确肩、肘、手关系的手臂姿势。</summary>
    private void DrawArms(
        Vector2 origin,
        CharacterAppearanceDefinition appearance,
        int side,
        ActionPose pose,
        CharacterAnimationState state,
        Color outline,
        Color cloth,
        Color shadow,
        Color skin,
        Color leather,
        Color metalShadow)
    {
        bool action = state is CharacterAnimationState.Attack or CharacterAnimationState.Cast;
        int frontShoulder = side > 0 ? 54 : 37;
        int backShoulder = side > 0 ? 39 : 52;

        if (appearance.WeaponSilhouette == CharacterWeaponSilhouette.Bow && action)
        {
            int reach = pose == ActionPose.Strike ? 10 : pose == ActionPose.Windup ? 7 : 8;
            int frontX = side > 0 ? frontShoulder : frontShoulder - reach;
            Pixel(origin, frontX, 31, 5 + reach, 4, outline);
            Pixel(origin, frontX + 1, 32, 3 + reach, 2, cloth);
            Pixel(origin, side > 0 ? frontX + 4 + reach : frontX - 1, 31, 3, 3, skin);

            int pullBack = pose == ActionPose.Strike ? 5 : pose == ActionPose.Windup ? 2 : 1;
            int pullX = side > 0 ? 44 - pullBack : 47 + pullBack;
            Pixel(origin, pullX, 27, 7, 5, outline);
            Pixel(origin, pullX + 1, 28, 5, 3, leather);
            Pixel(origin, side > 0 ? pullX + 5 : pullX, 27, 2, 2, skin);
            return;
        }

        if (appearance.WeaponSilhouette == CharacterWeaponSilhouette.Tome && state == CharacterAnimationState.Cast)
        {
            int reach = pose == ActionPose.Strike ? 10 : pose == ActionPose.Windup ? 5 : 7;
            int y = pose == ActionPose.Strike ? 25 : 29;
            int frontX = side > 0 ? 53 : 38 - reach;
            Pixel(origin, frontX, y, 5 + reach, 4, outline);
            Pixel(origin, frontX + 1, y + 1, 3 + reach, 2, cloth);
            Pixel(origin, side > 0 ? frontX + 4 + reach : frontX - 1, y, 3, 3, skin);
            Pixel(origin, backShoulder, 38, 7, 4, outline);
            Pixel(origin, backShoulder + 1, 39, 5, 2, shadow);
            return;
        }

        if (appearance.WeaponSilhouette == CharacterWeaponSilhouette.Spear && action)
        {
            int reach = pose == ActionPose.Strike ? 10 : pose == ActionPose.Windup ? 4 : 7;
            int y = pose == ActionPose.Windup ? 35 : 32;
            int frontX = side > 0 ? frontShoulder : frontShoulder - reach;
            Pixel(origin, frontX, y, 5 + reach, 4, outline);
            Pixel(origin, frontX + 1, y + 1, 3 + reach, 2, appearance.BodySilhouette == CharacterBodySilhouette.Armored ? metalShadow : cloth);
            Pixel(origin, side > 0 ? 47 : 43, 38, 9, 4, outline);
            Pixel(origin, side > 0 ? 48 : 44, 39, 7, 2, leather);
            return;
        }

        // 剑士：蓄势时手臂靠后，最大斩击才真正伸直。
        int attackReach = !action
            ? 1
            : pose switch
            {
                ActionPose.Windup => 2,
                ActionPose.Strike => 10,
                _ => 5
            };
        int swordFrontX = side > 0 ? frontShoulder : frontShoulder - attackReach;
        Pixel(origin, swordFrontX, 31, 5 + attackReach, 5, outline);
        Pixel(origin, swordFrontX + 1, 32, 3 + attackReach, 3, cloth);
        Pixel(origin, side > 0 ? swordFrontX + 4 + attackReach : swordFrontX - 1, 33, 3, 3, skin);
        Pixel(origin, backShoulder, 37, 6, 5, outline);
        Pixel(origin, backShoulder + 1, 38, 4, 3, leather);
    }

    /// <summary>根据剑、枪、弓、法书绘制与身体三段式动作匹配的武器。</summary>
    private void DrawWeapon(
        Vector2 origin,
        CharacterAppearanceDefinition appearance,
        int side,
        ActionPose pose,
        CharacterAnimationState state,
        Color outline,
        Color accent,
        Color leather,
        Color metal,
        Color metalLight,
        Color metalShadow)
    {
        switch (appearance.WeaponSilhouette)
        {
            case CharacterWeaponSilhouette.Spear:
                DrawSpear(origin, side, pose, state == CharacterAnimationState.Attack, leather, metal, metalLight, metalShadow);
                break;
            case CharacterWeaponSilhouette.Bow:
                DrawBow(origin, side, pose, state == CharacterAnimationState.Attack, outline, accent, leather, metal);
                break;
            case CharacterWeaponSilhouette.Tome:
                DrawTome(origin, side, pose, state == CharacterAnimationState.Cast, outline, accent, leather);
                break;
            default:
                DrawSword(origin, side, pose, state == CharacterAnimationState.Attack, accent, leather, metal, metalLight, metalShadow);
                break;
        }
    }

    /// <summary>剑的三个动作关键帧分别是后上蓄势、水平前斩和前下收势。</summary>
    private void DrawSword(
        Vector2 origin,
        int side,
        ActionPose pose,
        bool attacking,
        Color accent,
        Color leather,
        Color metal,
        Color metalLight,
        Color metalShadow)
    {
        if (!attacking || pose == ActionPose.Idle)
        {
            int x = side > 0 ? 61 : 33;
            Pixel(origin, x, 27, 2, 24, metalShadow);
            Pixel(origin, x + (side > 0 ? 1 : 0), 25, 1, 24, metal);
            Pixel(origin, x - 2, 48, 6, 2, accent.Darkened(0.18f));
            Pixel(origin, x, 50, 2, 7, leather);
            return;
        }

        if (pose == ActionPose.Windup)
        {
            int baseX = side > 0 ? 57 : 38;
            for (int i = 0; i < 10; i++)
            {
                Pixel(origin, baseX - side * i, 32 - i * 2, 2, 3, i < 2 ? leather : metal);
            }
            return;
        }

        if (pose == ActionPose.Strike)
        {
            int startX = side > 0 ? 58 : 14;
            Pixel(origin, startX, 30, 31, 2, metalShadow);
            Pixel(origin, startX, 29, 31, 1, metalLight);
            Pixel(origin, side > 0 ? 58 : 42, 28, 2, 5, leather);
            Pixel(origin, side > 0 ? 55 : 40, 28, 7, 2, accent.Darkened(0.14f));
            return;
        }

        // 收势剑刃前下压，和蓄势方向形成明确反差。
        int recoverX = side > 0 ? 59 : 35;
        for (int i = 0; i < 9; i++)
        {
            Pixel(origin, recoverX + side * i, 35 + i * 2, 2, 3, i < 2 ? leather : metal);
        }
    }

    /// <summary>长枪在三段动作里始终保持横向连续性，避免旧版从竖枪瞬间跳成水平枪。</summary>
    private void DrawSpear(
        Vector2 origin,
        int side,
        ActionPose pose,
        bool attacking,
        Color leather,
        Color metal,
        Color metalLight,
        Color metalShadow)
    {
        if (!attacking || pose == ActionPose.Idle)
        {
            int x = side > 0 ? 62 : 33;
            Pixel(origin, x, 18, 2, 43, leather);
            Pixel(origin, x - 2, 14, 6, 7, metalShadow);
            Pixel(origin, x, 13, 2, 8, metal);
            return;
        }

        int shaftY = pose == ActionPose.Windup ? 39 : pose == ActionPose.Strike ? 35 : 40;
        int length = pose == ActionPose.Windup ? 39 : pose == ActionPose.Strike ? 60 : 45;
        int startX = side > 0
            ? (pose == ActionPose.Strike ? 29 : 38)
            : (pose == ActionPose.Strike ? 8 : 18);
        Pixel(origin, startX, shaftY, length, 2, leather);

        int tipX = side > 0 ? startX + length : startX - 6;
        Pixel(origin, tipX, shaftY - 2, 7, 6, metalShadow);
        Pixel(origin, side > 0 ? tipX + 1 : tipX - 1, shaftY - 1, 6, 4, metal);
        Pixel(origin, side > 0 ? tipX + 5 : tipX - 2, shaftY, 3, 2, metalLight);
    }

    /// <summary>弓手先举弓拉弦、再拉满、最后松弦回正；身体不向敌人滑行。</summary>
    private void DrawBow(
        Vector2 origin,
        int side,
        ActionPose pose,
        bool attacking,
        Color outline,
        Color accent,
        Color leather,
        Color metal)
    {
        int x = side > 0 ? 68 : 27;
        int bend = attacking && pose == ActionPose.Strike ? 4 : attacking && pose == ActionPose.Windup ? 2 : 1;
        Color wood = accent.Darkened(0.25f);

        Pixel(origin, x, 24, 2, 7, wood);
        Pixel(origin, x + side * bend, 30, 2, 8, wood);
        Pixel(origin, x + side * (bend + 1), 38, 2, 8, wood);
        Pixel(origin, x + side * bend, 46, 2, 8, wood);
        Pixel(origin, x, 54, 2, 7, wood);
        Pixel(origin, x + (side > 0 ? 1 : 0), 36, 2, 7, leather);

        int stringX = attacking && pose == ActionPose.Strike
            ? (side > 0 ? 49 : 46)
            : attacking && pose == ActionPose.Windup
                ? (side > 0 ? 56 : 40)
                : x;
        DrawSteppedLine(origin, x, 25, stringX, 39, outline);
        DrawSteppedLine(origin, stringX, 39, x, 59, outline);

        // 拉弦阶段保留箭，松弦后的收势不再把箭粘在弓上；飞行箭由特效层负责。
        if (attacking && pose is ActionPose.Windup or ActionPose.Strike)
        {
            int arrowStart = side > 0 ? stringX : stringX - 28;
            Pixel(origin, arrowStart, 39, 28, 1, metal);
            Pixel(origin, side > 0 ? arrowStart + 26 : arrowStart, 37, 3, 5, metal);
        }
    }

    /// <summary>法书保持手掌尺度；施法阶段用离散符文像素表现聚能、释放与余辉。</summary>
    private void DrawTome(
        Vector2 origin,
        int side,
        ActionPose pose,
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

        if (pose == ActionPose.Windup)
        {
            Pixel(origin, 47, 28, 2, 2, accent.Lightened(0.30f));
            Pixel(origin, 43, 31, 2, 2, accent);
            Pixel(origin, 52, 31, 2, 2, accent.Lightened(0.18f));
            return;
        }

        if (pose == ActionPose.Strike)
        {
            Pixel(origin, 47, 23, 3, 3, accent.Lightened(0.42f));
            Pixel(origin, 40, 29, 3, 3, accent);
            Pixel(origin, 55, 29, 3, 3, accent.Lightened(0.24f));
            Pixel(origin, 47, 34, 2, 2, accent.Lightened(0.50f));
            return;
        }

        Pixel(origin, 43, 27, 2, 2, accent.Darkened(0.04f));
        Pixel(origin, 53, 31, 2, 2, accent.Lightened(0.18f));
    }

    /// <summary>用整数逻辑像素连接两点，避免弓弦使用平滑抗锯齿线。</summary>
    private void DrawSteppedLine(Vector2 origin, int fromX, int fromY, int toX, int toY, Color color)
    {
        int dx = toX - fromX;
        int dy = toY - fromY;
        int steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
        if (steps <= 0)
        {
            Pixel(origin, fromX, fromY, 1, 1, color);
            return;
        }

        for (int index = 0; index <= steps; index++)
        {
            float t = index / (float)steps;
            int x = (int)MathF.Round(fromX + dx * t);
            int y = (int)MathF.Round(fromY + dy * t);
            Pixel(origin, x, y, 1, 1, color);
        }
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

    /// <summary>读取当前状态已经播放的秒数。</summary>
    private float ReadElapsed()
    {
        return _source is not null && _elapsedField?.GetValue(_source) is float elapsed
            ? elapsed
            : 0.0f;
    }

    /// <summary>二次缓出；用于出手阶段快速达到最大位移。</summary>
    private static float EaseOut(float t)
    {
        float clamped = Mathf.Clamp(t, 0.0f, 1.0f);
        return 1.0f - (1.0f - clamped) * (1.0f - clamped);
    }

    /// <summary>平滑起落；用于收势和倒下，不产生突然速度跳变。</summary>
    private static float EaseInOut(float t)
    {
        float clamped = Mathf.Clamp(t, 0.0f, 1.0f);
        return clamped * clamped * (3.0f - 2.0f * clamped);
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

    /// <summary>程序动作关键姿势。</summary>
    private enum ActionPose
    {
        /// <summary>待机。</summary>
        Idle,

        /// <summary>蓄势。</summary>
        Windup,

        /// <summary>出手最大姿势。</summary>
        Strike,

        /// <summary>收势。</summary>
        Recover
    }
}
