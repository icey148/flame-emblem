using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 独立战斗画面中的多帧人物控件。
/// 优先播放正式 battle/&lt;state&gt;_N.png；素材缺失时使用原创的修长古典战棋像素职业模板。
/// </summary>
public partial class AnimatedBattleCharacterControl : Control
{
    /// <summary>当前展示的人物。</summary>
    private UnitModel? _unit;

    /// <summary>当前战斗动画状态。</summary>
    private CharacterAnimationState _state = CharacterAnimationState.Idle;

    /// <summary>当前状态已经播放的秒数。</summary>
    private float _stateElapsed;

    /// <summary>
    /// 程序回退人物使用 96×96 逻辑画布，每个逻辑像素放大 4 倍。
    /// 所有程序人物动作位移都必须锁到该网格，避免攻击时发生 1px 屏幕级抖动。
    /// </summary>
    private const float PixelScale = 4.0f;

    /// <summary>正式战斗人物统一绘制到 384×384 的整数放大区域。</summary>
    private static readonly Vector2 FormalTextureSize = new(384, 384);

    /// <summary>右侧人物水平朝向相反，使双方始终面向彼此。</summary>
    public bool MirrorHorizontally { get; set; }

    /// <summary>使用最近邻过滤，避免正式低分辨率 PNG 被线性采样模糊。</summary>
    public override void _Ready()
    {
        TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
    }

    /// <summary>设置当前人物并回到待机状态。</summary>
    public void SetUnit(UnitModel? unit)
    {
        _unit = unit;
        Play(CharacterAnimationState.Idle);
    }

    /// <summary>切换战斗动画状态并从第一帧开始计时。</summary>
    public void Play(CharacterAnimationState state)
    {
        _state = state;
        _stateElapsed = 0.0f;
        QueueRedraw();
    }

    /// <summary>推进正式序列帧或程序动作计时。</summary>
    public override void _Process(double delta)
    {
        _stateElapsed += Math.Max(0.0f, (float)delta);
        QueueRedraw();
    }

    /// <summary>
    /// 绘制正式战斗帧；素材不存在时自然回退到程序像素人物。
    /// 正式多帧动作已经把位移画进帧本身，因此不会再叠加程序前冲/闪避位移。
    /// </summary>
    public override void _Draw()
    {
        if (_unit is null)
        {
            return;
        }

        Vector2 fallbackOffset = ResolveFallbackOffset();
        float opacity = ResolveFallbackOpacity();
        int frameCount = CharacterAssetResolver.GetBattleFrameCount(_unit, _state);
        bool hasStateFrames = frameCount > 0;
        Texture2D? texture = null;

        if (hasStateFrames)
        {
            int frameIndex = ResolveFrameIndex(frameCount);
            texture = CharacterAssetResolver.TryLoadBattleFrame(_unit, _state, frameIndex);
        }

        texture ??= CharacterAssetResolver.TryLoad(_unit, CharacterArtSlot.Battle)
                    ?? CharacterAssetResolver.TryLoad(_unit, CharacterArtSlot.Portrait);

        if (texture is not null)
        {
            // 多帧正式素材使用自身动作；只有单张兼容图才需要程序位移补足动作感。
            DrawBattleTexture(texture, hasStateFrames ? Vector2.Zero : fallbackOffset, opacity);
            return;
        }

        DrawProceduralClassicFigure(fallbackOffset, opacity);
    }

    /// <summary>根据动画状态决定当前正式序列帧索引。</summary>
    private int ResolveFrameIndex(int frameCount)
    {
        float framesPerSecond = _state switch
        {
            CharacterAnimationState.Attack => 11.0f,
            CharacterAnimationState.Cast => 9.0f,
            CharacterAnimationState.Hit => 10.0f,
            CharacterAnimationState.Dodge => 11.0f,
            CharacterAnimationState.Defeat => 8.0f,
            _ => 4.0f
        };

        int rawIndex = (int)MathF.Floor(_stateElapsed * framesPerSecond);
        if (_state == CharacterAnimationState.Idle)
        {
            return rawIndex % frameCount;
        }

        return Math.Clamp(rawIndex, 0, frameCount - 1);
    }

    /// <summary>
    /// 素材缺失时提供职业不同的整体位移。
    /// 程序人物最终还会把该值锁到 4px 逻辑网格；正式单张兼容图只锁到整屏幕像素。
    /// </summary>
    private Vector2 ResolveFallbackOffset()
    {
        if (_unit is null)
        {
            return Vector2.Zero;
        }

        float direction = MirrorHorizontally ? -1.0f : 1.0f;
        CharacterAppearanceDefinition appearance = CharacterAppearanceCatalog.Get(_unit);
        CharacterBodySilhouette body = appearance.BodySilhouette;
        float attackDistance = appearance.WeaponSilhouette switch
        {
            CharacterWeaponSilhouette.Bow => 12.0f,
            CharacterWeaponSilhouette.Spear => 20.0f,
            CharacterWeaponSilhouette.Tome => 8.0f,
            _ => body == CharacterBodySilhouette.Armored ? 20.0f : 34.0f
        };
        float dodgeDistance = body switch
        {
            CharacterBodySilhouette.Armored => 14.0f,
            CharacterBodySilhouette.Robed => 22.0f,
            _ => 30.0f
        };

        return _state switch
        {
            CharacterAnimationState.Attack => new Vector2(
                direction * Mathf.Sin(Mathf.Clamp(_stateElapsed / 0.34f, 0.0f, 1.0f) * Mathf.Pi) * attackDistance,
                0),
            CharacterAnimationState.Cast => new Vector2(
                0,
                -Mathf.Abs(Mathf.Sin(_stateElapsed * 10.0f)) * (body == CharacterBodySilhouette.Robed ? 6.0f : 3.0f)),
            CharacterAnimationState.Dodge => new Vector2(
                -direction * Mathf.Sin(Mathf.Clamp(_stateElapsed / 0.28f, 0.0f, 1.0f) * Mathf.Pi) * dodgeDistance,
                0),
            CharacterAnimationState.Hit => new Vector2(Mathf.Sin(_stateElapsed * 52.0f) * 5.0f, 0),
            CharacterAnimationState.Defeat => new Vector2(0, Mathf.Min(62.0f, _stateElapsed * 68.0f)),
            _ => Vector2.Zero
        };
    }

    /// <summary>倒下状态逐渐淡出，其他状态保持完全可见。</summary>
    private float ResolveFallbackOpacity()
    {
        return _state == CharacterAnimationState.Defeat
            ? Mathf.Clamp(1.0f - _stateElapsed * 0.68f, 0.14f, 1.0f)
            : 1.0f;
    }

    /// <summary>
    /// 按 384×384 的正方形整数区域绘制正式素材。
    /// 96px 素材是整数 4×，128px 素材是整数 3×；位置也锁到整屏幕像素。
    /// </summary>
    private void DrawBattleTexture(Texture2D texture, Vector2 offset, float opacity)
    {
        Vector2 snappedOffset = RoundVector(offset);
        Rect2 target = new(new Vector2(18, 12) + snappedOffset, FormalTextureSize);
        Color modulate = new(1, 1, 1, opacity);

        if (MirrorHorizontally)
        {
            DrawSetTransform(new Vector2(Size.X, 0), 0.0f, new Vector2(-1, 1));
            Rect2 mirroredTarget = new(
                new Vector2(18 - snappedOffset.X, 12 + snappedOffset.Y),
                FormalTextureSize);
            DrawTextureRect(texture, mirroredTarget, false, modulate);
            DrawSetTransform(Vector2.Zero, 0.0f, Vector2.One);
            return;
        }

        DrawTextureRect(texture, target, false, modulate);
    }

    /// <summary>
    /// 绘制原创的修长古典战棋侧视人物。
    /// 人物在 96×96 逻辑画布内使用较小头部、较长腿部和强职业外轮廓。
    /// </summary>
    private void DrawProceduralClassicFigure(Vector2 offset, float opacity)
    {
        if (_unit is null)
        {
            return;
        }

        CharacterAppearanceDefinition appearance = CharacterAppearanceCatalog.Get(_unit);
        int side = MirrorHorizontally ? -1 : 1;
        int pose = ResolveActionPose();

        // 程序人物的整体动作必须按 4px 网格移动，保证所有逻辑像素始终落在同一像素栅格上。
        Vector2 origin = new Vector2(18, 12) + SnapToLogicalPixelGrid(offset);

        Color outline = Fade(new Color(0.055f, 0.050f, 0.060f), opacity);
        Color outfit = Fade(appearance.OutfitColor, opacity);
        Color outfitShadow = Fade(appearance.OutfitColor.Darkened(0.30f), opacity);
        Color outfitLight = Fade(appearance.OutfitColor.Lightened(0.15f), opacity);
        Color hair = Fade(appearance.HairColor, opacity);
        Color hairShadow = Fade(appearance.HairColor.Darkened(0.25f), opacity);
        Color skin = Fade(appearance.SkinColor, opacity);
        Color accent = Fade(appearance.AccentColor, opacity);
        Color metal = Fade(new Color(0.76f, 0.78f, 0.76f), opacity);
        Color metalShadow = Fade(new Color(0.27f, 0.29f, 0.31f), opacity);
        Color leather = Fade(new Color(0.29f, 0.20f, 0.15f), opacity);

        DrawCape(origin, appearance, side, pose, outline, accent);
        DrawBody(origin, appearance, side, pose, outline, outfit, outfitShadow, outfitLight, accent, metalShadow, leather);
        DrawHead(origin, appearance, side, pose, outline, hair, hairShadow, skin);
        DrawArms(origin, appearance, side, pose, outline, outfitShadow, skin, leather);
        DrawWeapon(origin, appearance, side, pose, outline, accent, metal, metalShadow, leather);
    }

    /// <summary>
    /// 把一次 Attack/Cast 拆成三个程序关键姿势：1=蓄势，2=动作最大，3=收势。
    /// 正式序列帧不使用该值。
    /// </summary>
    private int ResolveActionPose()
    {
        if (_state == CharacterAnimationState.Attack)
        {
            float normalized = Mathf.Clamp(_stateElapsed / 0.34f, 0.0f, 1.0f);
            return normalized < 0.25f ? 1 : normalized < 0.68f ? 2 : 3;
        }

        if (_state == CharacterAnimationState.Cast)
        {
            float normalized = Mathf.Clamp(_stateElapsed / 0.50f, 0.0f, 1.0f);
            return normalized < 0.28f ? 1 : normalized < 0.72f ? 2 : 3;
        }

        return 0;
    }

    /// <summary>绘制披风；攻击最大姿势时披风向动作反方向拖出，强化速度感。</summary>
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

        int drag = pose == 2 ? -side * 3 : pose == 1 ? -side : 0;
        int capeX = (side > 0 ? 31 : 49) + drag;
        DrawPixel(origin, capeX, 37, 17, 31, outline);
        DrawPixel(origin, capeX + 1, 38, 15, 28, accent.Darkened(0.22f));
        DrawPixel(origin, capeX + 2, 61, 12, 5, accent.Darkened(0.34f));
    }

    /// <summary>绘制小头部和清晰前后发层次。</summary>
    private void DrawHead(
        Vector2 origin,
        CharacterAppearanceDefinition appearance,
        int side,
        int pose,
        Color outline,
        Color hair,
        Color hairShadow,
        Color skin)
    {
        // 枪兵蓄势时头部轻微压低；其余职业保持稳定，避免所有动作都像整个人一起弹跳。
        int headDrop = appearance.WeaponSilhouette == CharacterWeaponSilhouette.Spear && pose == 1 ? 1 : 0;
        DrawPixel(origin, 41, 16 + headDrop, 13, 14, outline);
        DrawPixel(origin, 42, 18 + headDrop, 11, 11, skin);
        DrawPixel(origin, 40, 15 + headDrop, 15, 5, hair);
        DrawPixel(origin, side > 0 ? 41 : 51, 19 + headDrop, 4, 9, hairShadow);
        DrawPixel(origin, side > 0 ? 51 : 43, 23 + headDrop, 1, 1, outline);

        // 长袍施法者保留颈后长发束，施法最大姿势时略向后拖出。
        if (appearance.BodySilhouette == CharacterBodySilhouette.Robed)
        {
            int hairDrag = pose == 2 ? -side : 0;
            DrawPixel(origin, (side > 0 ? 40 : 51) + hairDrag, 25 + headDrop, 4, 12, hairShadow);
            DrawPixel(origin, (side > 0 ? 41 : 52) + hairDrag, 27 + headDrop, 3, 9, hair);
        }
    }

    /// <summary>根据轻装、重甲和长袍三种模板绘制修长身体。</summary>
    private void DrawBody(
        Vector2 origin,
        CharacterAppearanceDefinition appearance,
        int side,
        int pose,
        Color outline,
        Color outfit,
        Color shadow,
        Color light,
        Color accent,
        Color metalShadow,
        Color leather)
    {
        int bodyStep = ResolveBodyStep(appearance, side, pose);
        int crouch = appearance.WeaponSilhouette == CharacterWeaponSilhouette.Spear && pose == 1 ? 1 : 0;

        switch (appearance.BodySilhouette)
        {
            case CharacterBodySilhouette.Armored:
                // 重甲：宽肩但长腿；枪刺时只小幅跨步，维持沉稳低重心。
                DrawPixel(origin, 35 + bodyStep, 30 + crouch, 25, 8, outline);
                DrawPixel(origin, 37 + bodyStep, 32 + crouch, 21, 23, outline);
                DrawPixel(origin, 38 + bodyStep, 33 + crouch, 19, 21, outfit);
                DrawPixel(origin, 34 + bodyStep, 31 + crouch, 7, 7, metalShadow);
                DrawPixel(origin, 56 + bodyStep, 31 + crouch, 7, 7, metalShadow);
                DrawPixel(origin, 39 + bodyStep, 34 + crouch, 5, 17, light);
                DrawPixel(origin, 52 + bodyStep, 39 + crouch, 5, 15, shadow);
                DrawPixel(origin, 38 + bodyStep, 50 + crouch, 19, 4, accent.Darkened(0.12f));

                DrawPixel(origin, 39 + Math.Max(0, bodyStep), 54, 7, 25, outline);
                DrawPixel(origin, 51 + Math.Min(0, bodyStep), 54, 7, 25, outline);
                DrawPixel(origin, 40 + Math.Max(0, bodyStep), 55, 5, 20, shadow);
                DrawPixel(origin, 52 + Math.Min(0, bodyStep), 55, 5, 20, shadow);
                DrawPixel(origin, 37 + Math.Max(0, bodyStep), 77, 10, 4, metalShadow);
                DrawPixel(origin, 50 + Math.Min(0, bodyStep), 77, 10, 4, metalShadow);
                break;

            case CharacterBodySilhouette.Robed:
                // 长袍：窄肩、长袖和展开袍摆；施法时上身稳定，主要由袖口/袍摆表现力量。
                DrawPixel(origin, 39 + bodyStep, 30, 18, 24, outline);
                DrawPixel(origin, 40 + bodyStep, 31, 16, 22, outfit);
                DrawPixel(origin, 41 + bodyStep, 32, 5, 17, light);
                DrawPixel(origin, 52 + bodyStep, 38, 4, 15, shadow);
                DrawPixel(origin, 39 + bodyStep, 49, 18, 4, accent);
                DrawPixel(origin, 35 + bodyStep, 52, 26, 29, outline);
                DrawPixel(origin, 37 + bodyStep, 52, 22, 27, outfit);
                DrawPixel(origin, 38 + bodyStep, 54, 6, 23, light.Darkened(0.06f));
                DrawPixel(origin, 54 + bodyStep, 58, 5, 20, shadow);
                if (_state == CharacterAnimationState.Cast)
                {
                    int glowWidth = pose == 2 ? 22 : 16;
                    DrawPixel(origin, 48 - glowWidth / 2 + bodyStep, 77, glowWidth, 2, accent.Lightened(0.28f));
                }
                DrawPixel(origin, 37 + bodyStep, 79, 8, 3, metalShadow);
                DrawPixel(origin, 52 + bodyStep, 79, 8, 3, metalShadow);
                break;

            default:
                // 轻装：剑士会跨步前斩；弓手在拉满时反向压重心，不会像近战一样扑向目标。
                DrawPixel(origin, 38 + bodyStep, 30, 20, 24, outline);
                DrawPixel(origin, 39 + bodyStep, 31, 18, 22, outfit);
                DrawPixel(origin, 40 + bodyStep, 32, 5, 17, light);
                DrawPixel(origin, 52 + bodyStep, 39, 5, 14, shadow);
                DrawPixel(origin, 39 + bodyStep, 49, 18, 4, accent);
                DrawPixel(origin, 40 + bodyStep, 52, 16, 4, leather);

                DrawPixel(origin, 41 + Math.Max(0, bodyStep), 55, 6, 25, outline);
                DrawPixel(origin, 50 + Math.Min(0, bodyStep), 55, 6, 25, outline);
                DrawPixel(origin, 42 + Math.Max(0, bodyStep), 56, 4, 21, shadow);
                DrawPixel(origin, 51 + Math.Min(0, bodyStep), 56, 4, 21, OutfitShadowForLeg(outfit, shadow));
                DrawPixel(origin, 39 + Math.Max(0, bodyStep), 78, 9, 3, leather);
                DrawPixel(origin, 49 + Math.Min(0, bodyStep), 78, 9, 3, leather);
                break;
        }
    }

    /// <summary>根据职业武器和关键姿势返回身体在逻辑像素中的跨步量。</summary>
    private static int ResolveBodyStep(CharacterAppearanceDefinition appearance, int side, int pose)
    {
        if (pose == 0)
        {
            return 0;
        }

        return appearance.WeaponSilhouette switch
        {
            CharacterWeaponSilhouette.Spear => pose switch
            {
                1 => -side,
                2 => side * 2,
                _ => side
            },
            CharacterWeaponSilhouette.Bow => pose switch
            {
                2 => -side,
                _ => 0
            },
            CharacterWeaponSilhouette.Tome => 0,
            _ => pose switch
            {
                1 => -side,
                2 => side * 3,
                _ => side
            }
        };
    }

    /// <summary>轻装第二条腿保持主色与阴影之间的层级，避免两腿粘成一块。</summary>
    private static Color OutfitShadowForLeg(Color outfit, Color shadow)
    {
        // 使用传入阴影色作为主体，只轻微向主色靠近，保证有限色阶。
        return shadow.Lerp(outfit, 0.18f);
    }

    /// <summary>按武器和当前关键姿势绘制手臂，让剑/枪/弓/法书的动作语言真正不同。</summary>
    private void DrawArms(
        Vector2 origin,
        CharacterAppearanceDefinition appearance,
        int side,
        int pose,
        Color outline,
        Color sleeve,
        Color skin,
        Color leather)
    {
        bool attacking = _state == CharacterAnimationState.Attack;
        bool casting = _state == CharacterAnimationState.Cast;

        if (appearance.WeaponSilhouette == CharacterWeaponSilhouette.Tome && casting)
        {
            // 法师：先举书，再把施法手完全伸向目标，最后缓慢回落。
            int castFrontX = side > 0 ? 55 : 33;
            int extension = pose == 2 ? 8 : pose == 1 ? 4 : 6;
            DrawPixel(origin, castFrontX + Math.Min(0, side * extension), pose == 2 ? 31 : 34, 7 + extension, 5, outline);
            DrawPixel(origin, castFrontX + Math.Min(0, side * extension) + 1, pose == 2 ? 32 : 35, 5 + extension, 3, sleeve);
            DrawPixel(origin, side > 0 ? castFrontX + 5 + extension : castFrontX - 2 - extension, pose == 2 ? 32 : 35, 3, 3, skin);
            DrawPixel(origin, side > 0 ? 34 : 55, 42, 7, 5, sleeve);
            return;
        }

        if (appearance.WeaponSilhouette == CharacterWeaponSilhouette.Bow)
        {
            // 弓手：持弓手向前，拉弦手在第二关键姿势明显后撤到脸侧后方。
            int bowArmX = side > 0 ? 54 : 35;
            int bowExtension = attacking ? (pose == 1 ? 4 : 7) : 3;
            DrawPixel(origin, bowArmX + Math.Min(0, side * bowExtension), 36, 7 + bowExtension, 4, outline);
            DrawPixel(origin, bowArmX + Math.Min(0, side * bowExtension) + 1, 37, 5 + bowExtension, 2, sleeve);

            int drawHandX = side > 0 ? 43 : 51;
            int pullBack = attacking && pose == 2 ? -side * 7 : attacking && pose == 1 ? -side * 3 : 0;
            DrawPixel(origin, drawHandX + pullBack, 32, 7, 4, leather);
            DrawPixel(origin, drawHandX + pullBack + (side > 0 ? 5 : -2), 32, 3, 3, skin);
            return;
        }

        if (appearance.WeaponSilhouette == CharacterWeaponSilhouette.Spear && attacking)
        {
            // 枪兵：双手沿枪杆前后分开；最大突刺时两臂接近水平，体现直线发力。
            int extension = pose == 2 ? 7 : pose == 1 ? 2 : 5;
            int frontArmX = side > 0 ? 54 : 35;
            int backArmX = side > 0 ? 42 : 49;
            DrawPixel(origin, frontArmX + Math.Min(0, side * extension), 36, 7 + extension, 4, outline);
            DrawPixel(origin, frontArmX + Math.Min(0, side * extension) + 1, 37, 5 + extension, 2, sleeve);
            DrawPixel(origin, backArmX + Math.Min(0, side * 3), 41, 9, 4, leather);
            return;
        }

        // 剑士与非攻击状态共用基础手臂；剑士最大挥击时前臂延伸最明显。
        int frontX = side > 0 ? 55 : 34;
        int backX = side > 0 ? 35 : 56;
        int frontExtension = attacking
            ? side * (pose == 2 ? 8 : pose == 1 ? 1 : 5)
            : 0;
        DrawPixel(origin, frontX + Math.Min(0, frontExtension), 36, 7 + Math.Abs(frontExtension), 5, outline);
        DrawPixel(origin, frontX + Math.Min(0, frontExtension) + 1, 37, 5 + Math.Abs(frontExtension), 3, sleeve);
        DrawPixel(
            origin,
            side > 0 ? frontX + 5 + Math.Max(0, frontExtension) : frontX - 2 + Math.Min(0, frontExtension),
            37,
            3,
            3,
            skin);
        DrawPixel(origin, backX, 41, 6, 5, leather);
    }

    /// <summary>根据剑、枪、弓、法书和关键姿势绘制强职业识别的武器轮廓。</summary>
    private void DrawWeapon(
        Vector2 origin,
        CharacterAppearanceDefinition appearance,
        int side,
        int pose,
        Color outline,
        Color accent,
        Color metal,
        Color metalShadow,
        Color leather)
    {
        bool attacking = _state == CharacterAnimationState.Attack;
        bool casting = _state == CharacterAnimationState.Cast;

        switch (appearance.WeaponSilhouette)
        {
            case CharacterWeaponSilhouette.Spear:
                DrawSpear(origin, side, pose, attacking, metal, metalShadow, leather);
                break;

            case CharacterWeaponSilhouette.Bow:
                DrawBow(origin, side, pose, attacking, outline, accent, metal);
                break;

            case CharacterWeaponSilhouette.Tome:
                DrawTome(origin, side, pose, casting, outline, accent);
                break;

            default:
                DrawSword(origin, side, pose, attacking, accent, metal, metalShadow, leather);
                break;
        }
    }

    /// <summary>剑士使用蓄势斜举、水平斩击、下压收势三种明显不同的剑位。</summary>
    private void DrawSword(
        Vector2 origin,
        int side,
        int pose,
        bool attacking,
        Color accent,
        Color metal,
        Color metalShadow,
        Color leather)
    {
        if (!attacking)
        {
            int swordX = side > 0 ? 62 : 32;
            DrawPixel(origin, swordX, 28, 2, 36, metalShadow);
            DrawPixel(origin, swordX, 24, 2, 12, metal);
            DrawPixel(origin, swordX - 3, 36, 8, 2, accent.Darkened(0.16f));
            DrawPixel(origin, swordX, 62, 2, 8, leather);
            return;
        }

        if (pose == 1)
        {
            // 蓄势：剑向身后上方抬起，用阶梯像素表现硬边斜线。
            DrawSteppedPixels(origin, side > 0 ? 52 : 42, 38, 7, side, -1, 2, 2, metal);
            DrawPixel(origin, side > 0 ? 50 : 44, 39, 7, 2, accent.Darkened(0.15f));
            return;
        }

        if (pose == 2)
        {
            // 最大挥击：剑刃拉成长水平轮廓，和枪刺的细长杆区分开。
            int bladeX = side > 0 ? 58 : 10;
            DrawPixel(origin, bladeX, 35, 30, 2, metal);
            DrawPixel(origin, side > 0 ? 85 : 8, 33, 5, 6, metal);
            DrawPixel(origin, side > 0 ? 55 : 39, 32, 4, 9, metalShadow);
            DrawPixel(origin, side > 0 ? 53 : 40, 37, 8, 2, accent.Darkened(0.15f));
            return;
        }

        // 收势：剑向前下方压低，和第一帧形成方向反差。
        DrawSteppedPixels(origin, side > 0 ? 58 : 37, 40, 7, side, 1, 2, 2, metal);
        DrawPixel(origin, side > 0 ? 55 : 39, 38, 7, 2, accent.Darkened(0.15f));
    }

    /// <summary>枪兵蓄势时收枪，最大姿势形成贯穿画面的长水平枪杆。</summary>
    private void DrawSpear(
        Vector2 origin,
        int side,
        int pose,
        bool attacking,
        Color metal,
        Color metalShadow,
        Color leather)
    {
        if (!attacking)
        {
            int shaftX = side > 0 ? 64 : 30;
            DrawPixel(origin, shaftX, 17, 2, 61, leather);
            DrawPixel(origin, shaftX - 2, 13, 6, 6, metal);
            DrawPixel(origin, shaftX - 1, 12, 4, 3, metalShadow);
            return;
        }

        if (pose == 1)
        {
            // 蓄势时枪尖略高并靠近身体，下一帧突刺会形成强烈长度变化。
            int startX = side > 0 ? 48 : 28;
            DrawSteppedPixels(origin, startX, 42, 9, side, -1, 2, 2, leather);
            int tipX = startX + side * 9;
            DrawPixel(origin, tipX + (side > 0 ? 0 : -3), 31, 5, 5, metal);
            return;
        }

        if (pose == 2)
        {
            int shaftX = side > 0 ? 54 : 8;
            DrawPixel(origin, shaftX, 38, 36, 2, leather);
            DrawPixel(origin, side > 0 ? 87 : 5, 36, 6, 6, metal);
            DrawPixel(origin, side > 0 ? 86 : 11, 38, 3, 2, metalShadow);
            return;
        }

        // 回枪时枪杆略向下倾，动作不会突然跳回完全竖直。
        DrawSteppedPixels(origin, side > 0 ? 55 : 39, 39, 12, side, 1, 2, 2, leather);
        DrawPixel(origin, side > 0 ? 79 : 13, 49, 5, 5, metal);
    }

    /// <summary>弓手在拉满关键姿势明确显示弓弦后撤和箭矢，放箭后箭杆向目标方向离弓。</summary>
    private void DrawBow(
        Vector2 origin,
        int side,
        int pose,
        bool attacking,
        Color outline,
        Color accent,
        Color metal)
    {
        int bowCenter = side > 0 ? 67 : 27;
        int forward = attacking && pose >= 2 ? side * 2 : 0;
        bowCenter += forward;

        // 弓臂用三段阶梯轮廓保持长弓识别。
        DrawPixel(origin, bowCenter, 25, 2, 10, accent.Darkened(0.18f));
        DrawPixel(origin, bowCenter + side * 3, 34, 2, 12, accent.Darkened(0.18f));
        DrawPixel(origin, bowCenter, 45, 2, 11, accent.Darkened(0.18f));

        int topX = bowCenter;
        int bottomX = bowCenter;
        int stringCenterX = attacking && pose == 2 ? bowCenter - side * 10 : bowCenter - side;
        // 弓弦用硬边阶梯像素连接上下端与拉弦点，不使用抗锯齿线。
        DrawSteppedConnection(origin, topX, 29, stringCenterX, 39, outline);
        DrawSteppedConnection(origin, stringCenterX, 39, bottomX, 52, outline);

        if (!attacking)
        {
            return;
        }

        if (pose <= 2)
        {
            int arrowStart = side > 0 ? stringCenterX : stringCenterX - 30;
            DrawPixel(origin, arrowStart, 39, 30, 1, metal);
            DrawPixel(origin, side > 0 ? arrowStart + 28 : arrowStart, 37, 3, 5, metal);
            return;
        }

        // 放箭关键姿势把箭移到弓前方，让“松弦”而不是静态持箭更清楚。
        int releasedX = side > 0 ? bowCenter + 6 : bowCenter - 38;
        DrawPixel(origin, releasedX, 39, 30, 1, metal);
        DrawPixel(origin, side > 0 ? releasedX + 28 : releasedX, 37, 3, 5, metal);
    }

    /// <summary>法书施法分为聚能、释放、余辉三个阶段。</summary>
    private void DrawTome(
        Vector2 origin,
        int side,
        int pose,
        bool casting,
        Color outline,
        Color accent)
    {
        int tomeX = side > 0 ? 60 : 29;
        DrawPixel(origin, tomeX, 43, 9, 9, outline);
        DrawPixel(origin, tomeX + 1, 44, 3, 7, accent);
        DrawPixel(origin, tomeX + 5, 44, 3, 7, accent.Lightened(0.18f));

        if (!casting)
        {
            return;
        }

        int magicX = side > 0 ? 76 : 16;
        if (pose == 1)
        {
            DrawPixel(origin, magicX, 31, 3, 3, accent.Lightened(0.30f));
            DrawPixel(origin, magicX - side * 4, 39, 2, 2, accent);
            return;
        }

        if (pose == 2)
        {
            DrawPixel(origin, magicX, 23, 4, 4, accent.Lightened(0.40f));
            DrawPixel(origin, magicX + side * 5, 33, 7, 7, accent);
            DrawPixel(origin, magicX, 46, 4, 4, accent.Lightened(0.50f));
            return;
        }

        DrawPixel(origin, magicX + side * 5, 27, 3, 3, accent.Lightened(0.34f));
        DrawPixel(origin, magicX + side * 10, 37, 4, 4, accent.Darkened(0.06f));
        DrawPixel(origin, magicX + side * 4, 47, 2, 2, accent.Lightened(0.44f));
    }

    /// <summary>绘制一串等间隔硬边像素块，用于剑刃、枪杆等斜向轮廓。</summary>
    private void DrawSteppedPixels(
        Vector2 origin,
        int startX,
        int startY,
        int steps,
        int stepX,
        int stepY,
        int width,
        int height,
        Color color)
    {
        for (int index = 0; index < steps; index++)
        {
            DrawPixel(origin, startX + stepX * index, startY + stepY * index, width, height, color);
        }
    }

    /// <summary>用整数逻辑像素近似两点连线，专门绘制弓弦等细硬边元素。</summary>
    private void DrawSteppedConnection(Vector2 origin, int fromX, int fromY, int toX, int toY, Color color)
    {
        int deltaX = toX - fromX;
        int deltaY = toY - fromY;
        int steps = Math.Max(Math.Abs(deltaX), Math.Abs(deltaY));
        if (steps <= 0)
        {
            DrawPixel(origin, fromX, fromY, 1, 1, color);
            return;
        }

        for (int index = 0; index <= steps; index++)
        {
            float t = index / (float)steps;
            int x = (int)MathF.Round(Mathf.Lerp(fromX, toX, t));
            int y = (int)MathF.Round(Mathf.Lerp(fromY, toY, t));
            DrawPixel(origin, x, y, 1, 1, color);
        }
    }

    /// <summary>绘制一个 96×96 逻辑画布中的整数像素矩形。</summary>
    private void DrawPixel(Vector2 origin, int x, int y, int width, int height, Color color)
    {
        DrawRect(
            new Rect2(
                origin + new Vector2(x * PixelScale, y * PixelScale),
                new Vector2(width * PixelScale, height * PixelScale)),
            color,
            true);
    }

    /// <summary>把单张正式图片的动作偏移四舍五入到整屏幕像素。</summary>
    private static Vector2 RoundVector(Vector2 value)
    {
        return new Vector2(Mathf.Round(value.X), Mathf.Round(value.Y));
    }

    /// <summary>把程序人物整体位移锁定到 4px 逻辑像素网格，防止移动后像素栅格错位。</summary>
    private static Vector2 SnapToLogicalPixelGrid(Vector2 value)
    {
        return new Vector2(
            Mathf.Round(value.X / PixelScale) * PixelScale,
            Mathf.Round(value.Y / PixelScale) * PixelScale);
    }

    /// <summary>把当前倒下透明度应用到颜色。</summary>
    private static Color Fade(Color color, float opacity)
    {
        color.A *= opacity;
        return color;
    }
}
