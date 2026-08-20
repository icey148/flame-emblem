using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 独立战斗画面中的多帧人物控件。
/// 优先播放 battle/&lt;state&gt;_N.png 序列帧；缺少序列帧时回退到 battle.png、portrait.png 或原创侧视像素职业模板。
/// </summary>
public partial class AnimatedBattleCharacterControl : Control
{
    /// <summary>当前展示的人物。</summary>
    private UnitModel? _unit;

    /// <summary>当前正在播放的战斗动画状态。</summary>
    private CharacterAnimationState _state = CharacterAnimationState.Idle;

    /// <summary>当前状态已经播放的秒数。</summary>
    private float _stateElapsed;

    /// <summary>程序战斗模板中一个逻辑像素对应的屏幕像素尺寸。</summary>
    private const float BattlePixelScale = 4.0f;

    /// <summary>右侧人物通常需要水平镜像，使双方在战斗画面中面对彼此。</summary>
    public bool MirrorHorizontally { get; set; }

    /// <summary>
    /// 节点就绪后启用最近邻过滤，保证低分辨率战斗帧放大后仍然保持硬边像素。
    /// </summary>
    public override void _Ready()
    {
        TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
    }

    /// <summary>
    /// 设置人物；人物变化时重置为待机状态。
    /// </summary>
    public void SetUnit(UnitModel? unit)
    {
        _unit = unit;
        Play(CharacterAnimationState.Idle);
    }

    /// <summary>
    /// 切换战斗动画状态并从第一帧开始播放。
    /// </summary>
    public void Play(CharacterAnimationState state)
    {
        _state = state;
        _stateElapsed = 0.0f;
        QueueRedraw();
    }

    /// <summary>
    /// 推进当前动画计时并持续重绘。
    /// </summary>
    public override void _Process(double delta)
    {
        _stateElapsed += (float)delta;
        QueueRedraw();
    }

    /// <summary>
    /// 绘制当前战斗人物状态。
    /// </summary>
    public override void _Draw()
    {
        if (_unit is null)
        {
            return;
        }

        Vector2 fallbackOffset = ResolveFallbackOffset();
        float fallbackOpacity = ResolveFallbackOpacity();
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
            DrawBattleTexture(texture, fallbackOffset, fallbackOpacity);
            return;
        }

        DrawProceduralPixelFigure(fallbackOffset, fallbackOpacity);
    }

    /// <summary>
    /// 根据当前状态选择战斗序列帧。
    /// 待机循环播放；攻击、受击、闪避、施法和倒下停在最后一帧，直到状态机切换。
    /// </summary>
    private int ResolveFrameIndex(int frameCount)
    {
        float fps = _state switch
        {
            CharacterAnimationState.Attack => 12.0f,
            CharacterAnimationState.Cast => 10.0f,
            CharacterAnimationState.Hit => 10.0f,
            CharacterAnimationState.Dodge => 12.0f,
            CharacterAnimationState.Defeat => 8.0f,
            _ => 4.0f
        };

        int rawIndex = (int)MathF.Floor(_stateElapsed * fps);
        if (_state == CharacterAnimationState.Idle)
        {
            return rawIndex % frameCount;
        }

        return Math.Clamp(rawIndex, 0, frameCount - 1);
    }

    /// <summary>
    /// 在缺少正式状态帧时提供基本动作位移。
    /// 位移刻意保持短促，角色本体仍由像素块职业模板绘制。
    /// </summary>
    private Vector2 ResolveFallbackOffset()
    {
        float direction = MirrorHorizontally ? -1.0f : 1.0f;
        return _state switch
        {
            CharacterAnimationState.Attack => new Vector2(
                direction * Mathf.Sin(Mathf.Clamp(_stateElapsed / 0.32f, 0.0f, 1.0f) * Mathf.Pi) * 34.0f,
                0),
            CharacterAnimationState.Cast => new Vector2(0, -Mathf.Abs(Mathf.Sin(_stateElapsed * 12.0f)) * 4.0f),
            CharacterAnimationState.Dodge => new Vector2(
                -direction * Mathf.Sin(Mathf.Clamp(_stateElapsed / 0.25f, 0.0f, 1.0f) * Mathf.Pi) * 28.0f,
                0),
            CharacterAnimationState.Hit => new Vector2(Mathf.Round(Mathf.Sin(_stateElapsed * 55.0f) * 6.0f), 0),
            CharacterAnimationState.Defeat => new Vector2(0, Mathf.Min(52.0f, Mathf.Round(_stateElapsed * 70.0f))),
            _ => Vector2.Zero
        };
    }

    /// <summary>
    /// 倒下状态逐渐淡出；其他状态保持完全可见。
    /// </summary>
    private float ResolveFallbackOpacity()
    {
        return _state == CharacterAnimationState.Defeat
            ? Mathf.Clamp(1.0f - _stateElapsed * 0.75f, 0.12f, 1.0f)
            : 1.0f;
    }

    /// <summary>
    /// 绘制正式战斗纹理，并根据左右站位做水平镜像。
    /// </summary>
    private void DrawBattleTexture(Texture2D texture, Vector2 offset, float opacity)
    {
        Rect2 target = new(
            new Vector2(18, 12) + offset,
            new Vector2(Mathf.Max(1, Size.X - 36), Mathf.Max(1, Size.Y - 24)));
        Color modulate = new(1, 1, 1, opacity);

        if (MirrorHorizontally)
        {
            DrawSetTransform(new Vector2(Size.X, 0), 0.0f, new Vector2(-1, 1));
            Rect2 mirroredTarget = new(
                new Vector2(18 - offset.X, 12 + offset.Y),
                target.Size);
            DrawTextureRect(texture, mirroredTarget, false, modulate);
            DrawSetTransform(Vector2.Zero, 0.0f, Vector2.One);
            return;
        }

        DrawTextureRect(texture, target, false, modulate);
    }

    /// <summary>
    /// 没有正式战斗帧时绘制原创侧视像素职业模板。
    /// 模板采用有限色阶、硬边矩形与职业武器轮廓，不复制任何既有游戏人物素材。
    /// </summary>
    private void DrawProceduralPixelFigure(Vector2 offset, float opacity)
    {
        if (_unit is null)
        {
            return;
        }

        CharacterAppearanceDefinition appearance = CharacterAppearanceCatalog.Get(_unit);
        int side = MirrorHorizontally ? -1 : 1;
        Vector2 origin = new Vector2(Size.X * 0.5f - 64.0f, Size.Y * 0.11f) + offset;

        Color outline = ApplyOpacity(new Color(0.065f, 0.055f, 0.065f), opacity);
        Color outfit = ApplyOpacity(appearance.OutfitColor, opacity);
        Color outfitShadow = ApplyOpacity(appearance.OutfitColor.Darkened(0.34f), opacity);
        Color outfitLight = ApplyOpacity(appearance.OutfitColor.Lightened(0.18f), opacity);
        Color hair = ApplyOpacity(appearance.HairColor, opacity);
        Color hairShadow = ApplyOpacity(appearance.HairColor.Darkened(0.22f), opacity);
        Color skin = ApplyOpacity(appearance.SkinColor, opacity);
        Color accent = ApplyOpacity(appearance.AccentColor, opacity);
        Color metal = ApplyOpacity(new Color(0.82f, 0.84f, 0.82f), opacity);
        Color darkMetal = ApplyOpacity(new Color(0.28f, 0.29f, 0.30f), opacity);

        // 披风先画在身体后方，剑士/法师等有披风角色因此拥有更宽的侧视轮廓。
        if (appearance.HasCape)
        {
            DrawBattlePixel(origin, 7, 15, 15, 16, accent.Darkened(0.22f));
            DrawBattlePixel(origin, side > 0 ? 5 : 20, 18, 4, 12, accent.Darkened(0.34f));
        }

        // 双腿与靴子保持明显分离；攻击时前脚迈出一格，形成复古战斗动画的前冲姿态。
        int frontLegShift = _state == CharacterAnimationState.Attack ? side * 2 : 0;
        DrawBattlePixel(origin, 10 + Math.Max(0, frontLegShift), 28, 4, 9, outline);
        DrawBattlePixel(origin, 17 + Math.Min(0, frontLegShift), 28, 4, 9, outline);
        DrawBattlePixel(origin, 10 + Math.Max(0, frontLegShift), 28, 3, 6, outfitShadow);
        DrawBattlePixel(origin, 17 + Math.Min(0, frontLegShift), 28, 3, 6, outfitShadow);
        DrawBattlePixel(origin, 8 + Math.Max(0, frontLegShift), 35, 7, 3, darkMetal);
        DrawBattlePixel(origin, 16 + Math.Min(0, frontLegShift), 35, 7, 3, darkMetal);

        // 躯干使用外轮廓、主色、高光、腰带四个层次，保持低色数仍能读出护甲/服装结构。
        DrawBattlePixel(origin, 8, 13, 14, 17, outline);
        DrawBattlePixel(origin, 9, 14, 12, 15, outfit);
        DrawBattlePixel(origin, 9, 14, 4, 11, outfitLight);
        DrawBattlePixel(origin, 18, 20, 3, 9, outfitShadow);
        DrawBattlePixel(origin, 8, 24, 14, 3, accent);

        // 头部同样只使用矩形像素，面部朝向由单侧眼睛和前发位置提示。
        DrawBattlePixel(origin, 10, 4, 10, 10, outline);
        DrawBattlePixel(origin, 11, 5, 8, 8, skin);
        DrawBattlePixel(origin, 9, 3, 11, 4, hair);
        DrawBattlePixel(origin, side > 0 ? 10 : 17, 6, 3, 6, hairShadow);
        DrawBattlePixel(origin, side > 0 ? 17 : 11, 9, 1, 1, outline);

        DrawProceduralArms(origin, appearance, side, outfitShadow, skin);
        DrawProceduralBattleWeapon(origin, appearance, side, accent, metal, darkMetal);
    }

    /// <summary>
    /// 根据当前动作绘制手臂位置。
    /// 攻击/施法时手臂前伸，其余状态保持护在身体两侧。
    /// </summary>
    private void DrawProceduralArms(
        Vector2 origin,
        CharacterAppearanceDefinition appearance,
        int side,
        Color sleeve,
        Color skin)
    {
        bool activePose = _state is CharacterAnimationState.Attack or CharacterAnimationState.Cast;
        int frontX = side > 0 ? 22 : 5;
        int backX = side > 0 ? 5 : 22;

        if (activePose)
        {
            DrawBattlePixel(origin, frontX, 16, 5, 3, sleeve);
            DrawBattlePixel(origin, frontX + (side > 0 ? 4 : -1), 16, 2, 2, skin);
            DrawBattlePixel(origin, backX, 18, 3, 7, sleeve);
            return;
        }

        DrawBattlePixel(origin, frontX, 17, 3, 8, sleeve);
        DrawBattlePixel(origin, backX, 17, 3, 8, sleeve);
    }

    /// <summary>
    /// 按职业绘制侧视战斗武器/法书。
    /// 攻击状态会切换成前伸轮廓，施法状态会在法书前方显示魔力像素。
    /// </summary>
    private void DrawProceduralBattleWeapon(
        Vector2 origin,
        CharacterAppearanceDefinition appearance,
        int side,
        Color accent,
        Color metal,
        Color darkMetal)
    {
        bool attacking = _state == CharacterAnimationState.Attack;
        bool casting = _state == CharacterAnimationState.Cast;
        int front = side > 0 ? 26 : 3;

        switch (appearance.WeaponSilhouette)
        {
            case CharacterWeaponSilhouette.Spear:
                if (attacking)
                {
                    int spearX = side > 0 ? 25 : -8;
                    DrawBattlePixel(origin, spearX, 16, 16, 2, darkMetal);
                    DrawBattlePixel(origin, spearX + (side > 0 ? 14 : 0), 15, 4, 4, metal);
                }
                else
                {
                    DrawBattlePixel(origin, front, 7, 2, 26, darkMetal);
                    DrawBattlePixel(origin, front - 1, 4, 4, 5, metal);
                }
                break;

            case CharacterWeaponSilhouette.Bow:
                // 弓使用分段像素弧线；攻击状态在弓前增加箭杆。
                DrawBattlePixel(origin, front, 10, 2, 6, accent.Darkened(0.16f));
                DrawBattlePixel(origin, front + side * 2, 15, 2, 8, accent.Darkened(0.16f));
                DrawBattlePixel(origin, front, 22, 2, 6, accent.Darkened(0.16f));
                DrawBattlePixel(origin, front, 14, 1, 10, darkMetal);
                if (attacking)
                {
                    int arrowX = side > 0 ? front + 2 : front - 12;
                    DrawBattlePixel(origin, arrowX, 18, 12, 1, metal);
                    DrawBattlePixel(origin, arrowX + (side > 0 ? 11 : 0), 17, 2, 3, metal);
                }
                break;

            case CharacterWeaponSilhouette.Tome:
                // 法书始终保持打开状态；施法时在角色前方生成三颗魔力像素。
                DrawBattlePixel(origin, side > 0 ? 24 : 4, 18, 5, 6, darkMetal);
                DrawBattlePixel(origin, side > 0 ? 25 : 5, 19, 2, 4, accent);
                DrawBattlePixel(origin, side > 0 ? 27 : 7, 19, 1, 4, accent.Lightened(0.24f));
                if (casting)
                {
                    int magicX = side > 0 ? 31 : 0;
                    DrawBattlePixel(origin, magicX, 10, 2, 2, accent.Lightened(0.30f));
                    DrawBattlePixel(origin, magicX + side * 2, 15, 3, 3, accent);
                    DrawBattlePixel(origin, magicX, 22, 2, 2, accent.Lightened(0.42f));
                }
                break;

            default:
                if (attacking)
                {
                    int bladeX = side > 0 ? 25 : -4;
                    DrawBattlePixel(origin, bladeX, 15, 11, 2, metal);
                    DrawBattlePixel(origin, bladeX + (side > 0 ? 0 : 9), 14, 2, 4, metal);
                    DrawBattlePixel(origin, side > 0 ? 24 : 5, 14, 2, 5, darkMetal);
                }
                else
                {
                    DrawBattlePixel(origin, front, 9, 2, 18, darkMetal);
                    DrawBattlePixel(origin, front, 5, 2, 8, metal);
                    DrawBattlePixel(origin, front - 2, 14, 6, 2, accent.Darkened(0.18f));
                }
                break;
        }
    }

    /// <summary>
    /// 绘制一个战斗模板逻辑像素块。
    /// 所有坐标最终都落在固定倍数网格上，保证画面没有抗锯齿边缘。
    /// </summary>
    private void DrawBattlePixel(Vector2 origin, int x, int y, int width, int height, Color color)
    {
        DrawRect(
            new Rect2(
                origin + new Vector2(x * BattlePixelScale, y * BattlePixelScale),
                new Vector2(width * BattlePixelScale, height * BattlePixelScale)),
            color,
            true);
    }

    /// <summary>把颜色透明度乘以当前倒下动画透明度。</summary>
    private static Color ApplyOpacity(Color color, float opacity)
    {
        color.A *= opacity;
        return color;
    }
}
