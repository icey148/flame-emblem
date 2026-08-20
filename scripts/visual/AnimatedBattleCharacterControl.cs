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
    /// 程序回退人物使用 96×96 逻辑画布，每个逻辑像素放大 4 倍，和正式 96px 素材保持同一整数显示倍率。
    /// </summary>
    private const float PixelScale = 4.0f;

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

    /// <summary>绘制正式战斗帧；素材不存在时自然回退到程序像素人物。</summary>
    public override void _Draw()
    {
        if (_unit is null)
        {
            return;
        }

        Vector2 fallbackOffset = ResolveFallbackOffset();
        float opacity = ResolveFallbackOpacity();
        int frameCount = CharacterAssetResolver.GetBattleFrameCount(_unit, _state);
        Texture2D? texture = null;

        if (frameCount > 0)
        {
            int frameIndex = ResolveFrameIndex(frameCount);
            texture = CharacterAssetResolver.TryLoadBattleFrame(_unit, _state, frameIndex);
        }

        texture ??= CharacterAssetResolver.TryLoad(_unit, CharacterArtSlot.Battle)
                    ?? CharacterAssetResolver.TryLoad(_unit, CharacterArtSlot.Portrait);

        if (texture is not null)
        {
            DrawBattleTexture(texture, fallbackOffset, opacity);
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
    /// 素材缺失时仍提供职业不同的动作位移。
    /// 轻装前冲/闪避最大，重甲动作短而沉，长袍施法时有轻微浮动。
    /// </summary>
    private Vector2 ResolveFallbackOffset()
    {
        if (_unit is null)
        {
            return Vector2.Zero;
        }

        float direction = MirrorHorizontally ? -1.0f : 1.0f;
        CharacterBodySilhouette body = CharacterAppearanceCatalog.Get(_unit).BodySilhouette;
        float attackDistance = body switch
        {
            CharacterBodySilhouette.Armored => 20.0f,
            CharacterBodySilhouette.Robed => 22.0f,
            _ => 34.0f
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
                -Mathf.Round(Mathf.Abs(Mathf.Sin(_stateElapsed * 10.0f)) * (body == CharacterBodySilhouette.Robed ? 6.0f : 3.0f))),
            CharacterAnimationState.Dodge => new Vector2(
                -direction * Mathf.Sin(Mathf.Clamp(_stateElapsed / 0.28f, 0.0f, 1.0f) * Mathf.Pi) * dodgeDistance,
                0),
            CharacterAnimationState.Hit => new Vector2(Mathf.Round(Mathf.Sin(_stateElapsed * 52.0f) * 5.0f), 0),
            CharacterAnimationState.Defeat => new Vector2(0, Mathf.Min(62.0f, Mathf.Round(_stateElapsed * 68.0f))),
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
    /// 父控件被固定为 420×408，因此这里对 96px 素材是 4×、128px 素材是 3×，不会发生非等比拉伸。
    /// </summary>
    private void DrawBattleTexture(Texture2D texture, Vector2 offset, float opacity)
    {
        Vector2 targetSize = new(384, 384);
        Rect2 target = new(new Vector2(18, 12) + RoundVector(offset), targetSize);
        Color modulate = new(1, 1, 1, opacity);

        if (MirrorHorizontally)
        {
            DrawSetTransform(new Vector2(Size.X, 0), 0.0f, new Vector2(-1, 1));
            Rect2 mirroredTarget = new(
                new Vector2(18 - Mathf.Round(offset.X), 12 + Mathf.Round(offset.Y)),
                targetSize);
            DrawTextureRect(texture, mirroredTarget, false, modulate);
            DrawSetTransform(Vector2.Zero, 0.0f, Vector2.One);
            return;
        }

        DrawTextureRect(texture, target, false, modulate);
    }

    /// <summary>
    /// 绘制原创的修长古典战棋侧视人物。
    /// 人物在 96×96 逻辑画布内使用较小头部、较长腿部和清晰职业外轮廓，避免原先粗短的几何人偶感。
    /// </summary>
    private void DrawProceduralClassicFigure(Vector2 offset, float opacity)
    {
        if (_unit is null)
        {
            return;
        }

        CharacterAppearanceDefinition appearance = CharacterAppearanceCatalog.Get(_unit);
        int side = MirrorHorizontally ? -1 : 1;
        Vector2 origin = new Vector2(18, 12) + RoundVector(offset);

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

        // 披风先于身体绘制，边缘只保留少量强调像素，避免与躯干粘成一个大色块。
        if (appearance.HasCape)
        {
            DrawPixel(origin, side > 0 ? 31 : 49, 37, 17, 31, outline);
            DrawPixel(origin, side > 0 ? 32 : 50, 38, 15, 28, accent.Darkened(0.22f));
            DrawPixel(origin, side > 0 ? 33 : 51, 61, 12, 5, accent.Darkened(0.34f));
        }

        DrawBody(origin, appearance, side, outline, outfit, outfitShadow, outfitLight, accent, metalShadow, leather);
        DrawHead(origin, appearance, side, outline, hair, hairShadow, skin);
        DrawArms(origin, appearance, side, outline, outfitShadow, skin, leather);
        DrawWeapon(origin, appearance, side, outline, accent, metal, metalShadow, leather);
    }

    /// <summary>绘制小头部和清晰前后发层次。</summary>
    private void DrawHead(
        Vector2 origin,
        CharacterAppearanceDefinition appearance,
        int side,
        Color outline,
        Color hair,
        Color hairShadow,
        Color skin)
    {
        // 头部高度约 13px，人物总高度约 66px，比例明显比旧模板修长。
        DrawPixel(origin, 41, 16, 13, 14, outline);
        DrawPixel(origin, 42, 18, 11, 11, skin);
        DrawPixel(origin, 40, 15, 15, 5, hair);
        DrawPixel(origin, side > 0 ? 41 : 51, 19, 4, 9, hairShadow);
        DrawPixel(origin, side > 0 ? 51 : 43, 23, 1, 1, outline);

        // 中长发人物在颈后增加细长发束，但不扩大头部本体。
        if (appearance.BodySilhouette == CharacterBodySilhouette.Robed)
        {
            DrawPixel(origin, side > 0 ? 40 : 51, 25, 4, 12, hairShadow);
            DrawPixel(origin, side > 0 ? 41 : 52, 27, 3, 9, hair);
        }
    }

    /// <summary>根据轻装、重甲和长袍三种模板绘制修长身体。</summary>
    private void DrawBody(
        Vector2 origin,
        CharacterAppearanceDefinition appearance,
        int side,
        Color outline,
        Color outfit,
        Color shadow,
        Color light,
        Color accent,
        Color metalShadow,
        Color leather)
    {
        int attackStep = _state == CharacterAnimationState.Attack ? side * 2 : 0;

        switch (appearance.BodySilhouette)
        {
            case CharacterBodySilhouette.Armored:
                // 重甲：肩甲明显更宽，但腰和腿仍保持纵向比例，不画成方块人。
                DrawPixel(origin, 35, 30, 25, 8, outline);
                DrawPixel(origin, 37, 32, 21, 23, outline);
                DrawPixel(origin, 38, 33, 19, 21, outfit);
                DrawPixel(origin, 34, 31, 7, 7, metalShadow);
                DrawPixel(origin, 56, 31, 7, 7, metalShadow);
                DrawPixel(origin, 39, 34, 5, 17, light);
                DrawPixel(origin, 52, 39, 5, 15, shadow);
                DrawPixel(origin, 38, 50, 19, 4, accent.Darkened(0.12f));

                DrawPixel(origin, 39 + Math.Max(0, attackStep), 54, 7, 25, outline);
                DrawPixel(origin, 51 + Math.Min(0, attackStep), 54, 7, 25, outline);
                DrawPixel(origin, 40 + Math.Max(0, attackStep), 55, 5, 20, shadow);
                DrawPixel(origin, 52 + Math.Min(0, attackStep), 55, 5, 20, shadow);
                DrawPixel(origin, 37 + Math.Max(0, attackStep), 77, 10, 4, metalShadow);
                DrawPixel(origin, 50 + Math.Min(0, attackStep), 77, 10, 4, metalShadow);
                break;

            case CharacterBodySilhouette.Robed:
                // 长袍：窄肩、长袖、腰线偏高，下摆从腰部向脚底逐渐展开。
                DrawPixel(origin, 39, 30, 18, 24, outline);
                DrawPixel(origin, 40, 31, 16, 22, outfit);
                DrawPixel(origin, 41, 32, 5, 17, light);
                DrawPixel(origin, 52, 38, 4, 15, shadow);
                DrawPixel(origin, 39, 49, 18, 4, accent);
                DrawPixel(origin, 35, 52, 26, 29, outline);
                DrawPixel(origin, 37, 52, 22, 27, outfit);
                DrawPixel(origin, 38, 54, 6, 23, light.Darkened(0.06f));
                DrawPixel(origin, 54, 58, 5, 20, shadow);
                if (_state == CharacterAnimationState.Cast)
                {
                    DrawPixel(origin, 38, 77, 20, 2, accent.Lightened(0.28f));
                }
                DrawPixel(origin, 37, 79, 8, 3, metalShadow);
                DrawPixel(origin, 52, 79, 8, 3, metalShadow);
                break;

            default:
                // 轻装：窄肩短上衣、明显腰线和最长腿部比例，体现剑士/弓手的灵活感。
                DrawPixel(origin, 38, 30, 20, 24, outline);
                DrawPixel(origin, 39, 31, 18, 22, outfit);
                DrawPixel(origin, 40, 32, 5, 17, light);
                DrawPixel(origin, 52, 39, 5, 14, shadow);
                DrawPixel(origin, 39, 49, 18, 4, accent);
                DrawPixel(origin, 40, 52, 16, 4, leather);

                DrawPixel(origin, 41 + Math.Max(0, attackStep), 55, 6, 25, outline);
                DrawPixel(origin, 50 + Math.Min(0, attackStep), 55, 6, 25, outline);
                DrawPixel(origin, 42 + Math.Max(0, attackStep), 56, 4, 21, shadow);
                DrawPixel(origin, 51 + Math.Min(0, attackStep), 56, 4, 21, outfitShadowForLeg(outfit, shadow));
                DrawPixel(origin, 39 + Math.Max(0, attackStep), 78, 9, 3, leather);
                DrawPixel(origin, 49 + Math.Min(0, attackStep), 78, 9, 3, leather);
                break;
        }
    }

    /// <summary>轻装第二条腿保持主色与阴影之间的层级，避免两腿粘成一块。</summary>
    private static Color outfitShadowForLeg(Color outfit, Color shadow)
    {
        // 使用传入阴影色作为主体，只轻微向主色靠近，保证有限色阶。
        return shadow.Lerp(outfit, 0.18f);
    }

    /// <summary>按职业和当前动作绘制手臂。</summary>
    private void DrawArms(
        Vector2 origin,
        CharacterAppearanceDefinition appearance,
        int side,
        Color outline,
        Color sleeve,
        Color skin,
        Color leather)
    {
        bool attacking = _state == CharacterAnimationState.Attack;
        bool casting = _state == CharacterAnimationState.Cast;

        if (appearance.BodySilhouette == CharacterBodySilhouette.Robed && casting)
        {
            // 法师施法时一侧宽袖抬起，另一手保持书本位置。
            int handX = side > 0 ? 61 : 28;
            DrawPixel(origin, side > 0 ? 55 : 31, 34, 8, 5, outline);
            DrawPixel(origin, side > 0 ? 56 : 32, 35, 6, 3, sleeve);
            DrawPixel(origin, handX, 33, 3, 3, skin);
            DrawPixel(origin, side > 0 ? 34 : 55, 42, 7, 5, sleeve);
            return;
        }

        int frontArmX = side > 0 ? 55 : 34;
        int backArmX = side > 0 ? 35 : 56;
        int frontExtension = attacking ? side * 4 : 0;
        DrawPixel(origin, frontArmX + Math.Min(0, frontExtension), 36, 7 + Math.Abs(frontExtension), 5, outline);
        DrawPixel(origin, frontArmX + Math.Min(0, frontExtension) + 1, 37, 5 + Math.Abs(frontExtension), 3, sleeve);
        DrawPixel(origin, side > 0 ? frontArmX + 5 + Math.Max(0, frontExtension) : frontArmX - 2 + Math.Min(0, frontExtension), 37, 3, 3, skin);
        DrawPixel(origin, backArmX, 41, 6, 5, leather);
    }

    /// <summary>根据剑、枪、弓、法书绘制强职业识别的武器轮廓。</summary>
    private void DrawWeapon(
        Vector2 origin,
        CharacterAppearanceDefinition appearance,
        int side,
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
                if (attacking)
                {
                    int shaftX = side > 0 ? 57 : 8;
                    DrawPixel(origin, shaftX, 38, 31, 2, leather);
                    DrawPixel(origin, side > 0 ? 86 : 6, 36, 5, 6, metal);
                    DrawPixel(origin, side > 0 ? 85 : 11, 38, 3, 2, metalShadow);
                }
                else
                {
                    int shaftX = side > 0 ? 64 : 30;
                    DrawPixel(origin, shaftX, 17, 2, 61, leather);
                    DrawPixel(origin, shaftX - 2, 13, 6, 6, metal);
                    DrawPixel(origin, shaftX - 1, 12, 4, 3, metalShadow);
                }
                break;

            case CharacterWeaponSilhouette.Bow:
                // 弓用阶梯像素形成长弧，避免缩小后看成直棍。
                int bowCenter = side > 0 ? 67 : 27;
                DrawPixel(origin, bowCenter, 25, 2, 10, accent.Darkened(0.18f));
                DrawPixel(origin, bowCenter + side * 3, 34, 2, 12, accent.Darkened(0.18f));
                DrawPixel(origin, bowCenter, 45, 2, 11, accent.Darkened(0.18f));
                DrawPixel(origin, bowCenter - side * 1, 29, 1, 23, outline);
                if (attacking)
                {
                    int arrowX = side > 0 ? 56 : 12;
                    DrawPixel(origin, arrowX, 39, 32, 1, metal);
                    DrawPixel(origin, side > 0 ? 86 : 11, 37, 3, 5, metal);
                }
                break;

            case CharacterWeaponSilhouette.Tome:
                int tomeX = side > 0 ? 60 : 29;
                DrawPixel(origin, tomeX, 43, 9, 9, outline);
                DrawPixel(origin, tomeX + 1, 44, 3, 7, accent);
                DrawPixel(origin, tomeX + 5, 44, 3, 7, accent.Lightened(0.18f));
                if (casting)
                {
                    int magicX = side > 0 ? 76 : 16;
                    DrawPixel(origin, magicX, 25, 3, 3, accent.Lightened(0.34f));
                    DrawPixel(origin, magicX + side * 4, 34, 5, 5, accent);
                    DrawPixel(origin, magicX, 45, 3, 3, accent.Lightened(0.42f));
                }
                break;

            default:
                if (attacking)
                {
                    int bladeX = side > 0 ? 58 : 12;
                    DrawPixel(origin, bladeX, 35, 28, 2, metal);
                    DrawPixel(origin, side > 0 ? 84 : 10, 33, 5, 6, metal);
                    DrawPixel(origin, side > 0 ? 55 : 39, 32, 4, 9, metalShadow);
                    DrawPixel(origin, side > 0 ? 53 : 40, 37, 8, 2, accent.Darkened(0.15f));
                }
                else
                {
                    int swordX = side > 0 ? 62 : 32;
                    DrawPixel(origin, swordX, 28, 2, 36, metalShadow);
                    DrawPixel(origin, swordX, 24, 2, 12, metal);
                    DrawPixel(origin, swordX - 3, 36, 8, 2, accent.Darkened(0.16f));
                    DrawPixel(origin, swordX, 62, 2, 8, leather);
                }
                break;
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

    /// <summary>把动作偏移四舍五入到整数屏幕像素，防止程序模型落在半像素位置。</summary>
    private static Vector2 RoundVector(Vector2 value)
    {
        return new Vector2(Mathf.Round(value.X), Mathf.Round(value.Y));
    }

    /// <summary>把当前倒下透明度应用到颜色。</summary>
    private static Color Fade(Color color, float opacity)
    {
        color.A *= opacity;
        return color;
    }
}
