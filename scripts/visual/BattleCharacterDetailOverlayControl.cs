using FlameEmblem.Game;
using Godot;
using System.Reflection;

namespace FlameEmblem.Visual;

/// <summary>
/// 给程序回退战斗人物补充第二层像素细节。
/// 正式 battle/portrait 素材存在时自动不绘制，避免程序装饰覆盖未来的原创正式人物帧。
/// </summary>
public partial class BattleCharacterDetailOverlayControl : Control
{
    /// <summary>程序战斗人物的逻辑像素倍率，必须与 AnimatedBattleCharacterControl 保持一致。</summary>
    private const float PixelScale = 4.0f;

    /// <summary>被装饰的战斗人物控件。</summary>
    private AnimatedBattleCharacterControl? _ownerCharacter;

    /// <summary>读取 AnimatedBattleCharacterControl 当前人物的私有字段。</summary>
    private static readonly FieldInfo? UnitField = typeof(AnimatedBattleCharacterControl)
        .GetField("_unit", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>读取 AnimatedBattleCharacterControl 当前动画状态的私有字段。</summary>
    private static readonly FieldInfo? StateField = typeof(AnimatedBattleCharacterControl)
        .GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>读取 AnimatedBattleCharacterControl 当前状态时间的私有字段。</summary>
    private static readonly FieldInfo? StateElapsedField = typeof(AnimatedBattleCharacterControl)
        .GetField("_stateElapsed", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>
    /// 绑定目标人物控件。
    /// 该方法只保存表现引用，不修改战斗数值或动画状态。
    /// </summary>
    public void Bind(AnimatedBattleCharacterControl ownerCharacter)
    {
        _ownerCharacter = ownerCharacter;
        Position = Vector2.Zero;
        Size = ownerCharacter.Size;
        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        QueueRedraw();
    }

    /// <summary>持续跟随父人物尺寸和动画状态刷新细节。</summary>
    public override void _Process(double delta)
    {
        if (_ownerCharacter is null)
        {
            return;
        }

        if (Size != _ownerCharacter.Size)
        {
            Size = _ownerCharacter.Size;
        }

        QueueRedraw();
    }

    /// <summary>
    /// 只在程序回退人物上绘制细节。
    /// 如果当前状态存在正式序列帧，或存在兼容 battle/portrait 图片，则完全让正式素材接管画面。
    /// </summary>
    public override void _Draw()
    {
        if (_ownerCharacter is null ||
            UnitField?.GetValue(_ownerCharacter) is not UnitModel unit ||
            StateField?.GetValue(_ownerCharacter) is not CharacterAnimationState state)
        {
            return;
        }

        if (!UsesProceduralFallback(unit, state))
        {
            return;
        }

        float elapsed = StateElapsedField?.GetValue(_ownerCharacter) is float value ? value : 0.0f;
        CharacterAppearanceDefinition appearance = CharacterAppearanceCatalog.Get(unit);
        int side = _ownerCharacter.MirrorHorizontally ? -1 : 1;
        int pose = ResolveActionPose(state, elapsed);
        Vector2 origin = new Vector2(18, 12) + ResolveSnappedOffset(unit, state, elapsed, side);

        float opacity = state == CharacterAnimationState.Defeat
            ? Mathf.Clamp(1.0f - elapsed * 0.68f, 0.14f, 1.0f)
            : 1.0f;

        Color outline = Fade(new Color(0.055f, 0.050f, 0.060f), opacity);
        Color skinShadow = Fade(appearance.SkinColor.Darkened(0.20f), opacity);
        Color hairLight = Fade(appearance.HairColor.Lightened(0.18f), opacity);
        Color hairShadow = Fade(appearance.HairColor.Darkened(0.28f), opacity);
        Color outfitLight = Fade(appearance.OutfitColor.Lightened(0.24f), opacity);
        Color outfitShadow = Fade(appearance.OutfitColor.Darkened(0.34f), opacity);
        Color accent = Fade(appearance.AccentColor, opacity);
        Color metalLight = Fade(new Color(0.88f, 0.88f, 0.82f), opacity);
        Color metalMid = Fade(new Color(0.55f, 0.57f, 0.56f), opacity);
        Color leatherLight = Fade(new Color(0.43f, 0.31f, 0.22f), opacity);

        DrawFaceAndHair(origin, appearance, side, pose, outline, skinShadow, hairLight, hairShadow);
        DrawCostumeDetails(
            origin,
            appearance,
            side,
            pose,
            outfitLight,
            outfitShadow,
            accent,
            metalLight,
            metalMid,
            leatherLight);
    }

    /// <summary>判断当前人物是否真的在使用程序回退模型。</summary>
    private static bool UsesProceduralFallback(UnitModel unit, CharacterAnimationState state)
    {
        if (CharacterAssetResolver.GetBattleFrameCount(unit, state) > 0)
        {
            return false;
        }

        return CharacterAssetResolver.TryLoad(unit, CharacterArtSlot.Battle) is null &&
               CharacterAssetResolver.TryLoad(unit, CharacterArtSlot.Portrait) is null;
    }

    /// <summary>
    /// 补充脸部、颈部和头发层次。
    /// 仍然只使用整数矩形像素，不增加抗锯齿曲线或高分辨率假细节。
    /// </summary>
    private void DrawFaceAndHair(
        Vector2 origin,
        CharacterAppearanceDefinition appearance,
        int side,
        int pose,
        Color outline,
        Color skinShadow,
        Color hairLight,
        Color hairShadow)
    {
        int headDrop = appearance.WeaponSilhouette == CharacterWeaponSilhouette.Spear && pose == 1 ? 1 : 0;

        // 颈部阴影把头部和衣领分开，避免脸直接粘在胸甲/袍子上。
        DrawPixel(origin, 45, 28 + headDrop, 6, 3, skinShadow);
        DrawPixel(origin, side > 0 ? 50 : 43, 27 + headDrop, 3, 2, skinShadow);

        // 前额高光和两簇刘海让发型不再是一整块矩形帽子。
        DrawPixel(origin, 43, 16 + headDrop, 7, 2, hairLight);
        DrawPixel(origin, side > 0 ? 49 : 44, 18 + headDrop, 4, 2, hairLight);
        DrawPixel(origin, side > 0 ? 50 : 43, 19 + headDrop, 3, 4, hairShadow);
        DrawPixel(origin, side > 0 ? 47 : 47, 18 + headDrop, 2, 3, hairShadow);

        // 眉眼和鼻下阴影保持非常克制，只用几个逻辑像素增强侧脸方向。
        int eyeX = side > 0 ? 51 : 44;
        DrawPixel(origin, eyeX, 22 + headDrop, 1, 1, outline);
        DrawPixel(origin, side > 0 ? 52 : 43, 24 + headDrop, 1, 2, skinShadow);
        DrawPixel(origin, side > 0 ? 49 : 46, 27 + headDrop, 3, 1, skinShadow);

        // 轻装弓手增加一小束后发；法师则延长已有后发的高光层。
        if (appearance.WeaponSilhouette == CharacterWeaponSilhouette.Bow)
        {
            DrawPixel(origin, side > 0 ? 40 : 52, 22 + headDrop, 3, 7, hairShadow);
            DrawPixel(origin, side > 0 ? 40 : 53, 23 + headDrop, 2, 5, hairLight.Darkened(0.08f));
        }
        else if (appearance.BodySilhouette == CharacterBodySilhouette.Robed)
        {
            int hairDrag = pose == 2 ? -side : 0;
            DrawPixel(origin, (side > 0 ? 41 : 52) + hairDrag, 29 + headDrop, 2, 7, hairLight.Darkened(0.05f));
        }
    }

    /// <summary>按职业补充服装结构，让轻装、重甲和长袍近看也拥有不同材质与层次。</summary>
    private void DrawCostumeDetails(
        Vector2 origin,
        CharacterAppearanceDefinition appearance,
        int side,
        int pose,
        Color outfitLight,
        Color outfitShadow,
        Color accent,
        Color metalLight,
        Color metalMid,
        Color leatherLight)
    {
        int bodyStep = ResolveBodyStep(appearance, side, pose);
        int crouch = appearance.WeaponSilhouette == CharacterWeaponSilhouette.Spear && pose == 1 ? 1 : 0;

        switch (appearance.BodySilhouette)
        {
            case CharacterBodySilhouette.Armored:
                // 重甲增加领甲、肩甲高光、胸甲中线和膝甲，使金属结构与布料阴影分开。
                DrawPixel(origin, 43 + bodyStep, 31 + crouch, 10, 3, metalMid);
                DrawPixel(origin, 35 + bodyStep, 32 + crouch, 5, 2, metalLight);
                DrawPixel(origin, 57 + bodyStep, 32 + crouch, 5, 2, metalLight);
                DrawPixel(origin, 47 + bodyStep, 36 + crouch, 2, 13, outfitShadow);
                DrawPixel(origin, 49 + bodyStep, 36 + crouch, 2, 13, metalMid.Darkened(0.08f));
                DrawPixel(origin, 40 + bodyStep, 50 + crouch, 6, 2, accent.Lightened(0.08f));
                DrawPixel(origin, 52 + bodyStep, 50 + crouch, 4, 2, accent.Darkened(0.10f));
                DrawPixel(origin, 40 + Math.Max(0, bodyStep), 66, 5, 4, metalMid);
                DrawPixel(origin, 52 + Math.Min(0, bodyStep), 66, 5, 4, metalMid);
                break;

            case CharacterBodySilhouette.Robed:
                // 长袍增加 V 形领口、袖边、腰饰和竖向袍纹，让法师不再像单色长方形。
                DrawPixel(origin, 45 + bodyStep, 31, 3, 5, accent.Lightened(0.12f));
                DrawPixel(origin, 49 + bodyStep, 31, 3, 5, accent.Darkened(0.05f));
                DrawPixel(origin, 47 + bodyStep, 35, 3, 2, outfitShadow);
                DrawPixel(origin, 39 + bodyStep, 46, 4, 2, accent.Darkened(0.12f));
                DrawPixel(origin, 53 + bodyStep, 46, 4, 2, accent.Darkened(0.12f));
                DrawPixel(origin, 46 + bodyStep, 49, 5, 3, metalMid.Darkened(0.15f));
                DrawPixel(origin, 47 + bodyStep, 50, 3, 1, metalLight);
                DrawPixel(origin, 46 + bodyStep, 55, 2, 21, outfitShadow);
                DrawPixel(origin, 50 + bodyStep, 55, 2, 21, accent.Darkened(0.20f));
                DrawPixel(origin, 39 + bodyStep, 75, 18, 2, accent.Lightened(0.05f));
                break;

            default:
                // 轻装增加衣领、斜胸带、腰扣和靴口；弓手额外显示箭袋肩带。
                DrawPixel(origin, 44 + bodyStep, 31, 8, 3, outfitLight);
                DrawPixel(origin, 47 + bodyStep, 34, 3, 5, outfitShadow);
                DrawSteppedDetail(origin, 42 + bodyStep, 35, 7, side, 1, leatherLight);
                DrawPixel(origin, 46 + bodyStep, 50, 5, 3, metalMid.Darkened(0.18f));
                DrawPixel(origin, 47 + bodyStep, 50, 3, 1, metalLight);
                DrawPixel(origin, 41 + Math.Max(0, bodyStep), 68, 6, 2, leatherLight);
                DrawPixel(origin, 50 + Math.Min(0, bodyStep), 68, 6, 2, leatherLight);

                if (appearance.WeaponSilhouette == CharacterWeaponSilhouette.Bow)
                {
                    int strapStart = side > 0 ? 41 : 53;
                    DrawSteppedDetail(origin, strapStart + bodyStep, 34, 8, side, 1, accent.Darkened(0.18f));
                    DrawPixel(origin, side > 0 ? 36 : 57, 39, 4, 18, leatherLight.Darkened(0.18f));
                    DrawPixel(origin, side > 0 ? 35 : 57, 38, 6, 3, accent.Darkened(0.22f));
                }
                break;
        }

        // 有披风的角色补一个固定扣和上沿高光，让披风与身体分层而不是一整块背景色。
        if (appearance.HasCape)
        {
            DrawPixel(origin, side > 0 ? 39 : 55, 34, 3, 3, metalLight);
            DrawPixel(origin, side > 0 ? 33 : 58, 40, 2, 18, accent.Lightened(0.10f));
        }
    }

    /// <summary>把 Attack/Cast 时间转换成和底层人物一致的三个关键姿势。</summary>
    private static int ResolveActionPose(CharacterAnimationState state, float elapsed)
    {
        if (state == CharacterAnimationState.Attack)
        {
            float normalized = Mathf.Clamp(elapsed / 0.34f, 0.0f, 1.0f);
            return normalized < 0.25f ? 1 : normalized < 0.68f ? 2 : 3;
        }

        if (state == CharacterAnimationState.Cast)
        {
            float normalized = Mathf.Clamp(elapsed / 0.50f, 0.0f, 1.0f);
            return normalized < 0.28f ? 1 : normalized < 0.72f ? 2 : 3;
        }

        return 0;
    }

    /// <summary>复制底层人物的职业跨步规则，使服装细节始终贴在正确身体位置。</summary>
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
            CharacterWeaponSilhouette.Bow => pose == 2 ? -side : 0,
            CharacterWeaponSilhouette.Tome => 0,
            _ => pose switch
            {
                1 => -side,
                2 => side * 3,
                _ => side
            }
        };
    }

    /// <summary>复制底层人物的整体动作偏移，并锁到 4px 网格。</summary>
    private static Vector2 ResolveSnappedOffset(
        UnitModel unit,
        CharacterAnimationState state,
        float elapsed,
        int side)
    {
        CharacterAppearanceDefinition appearance = CharacterAppearanceCatalog.Get(unit);
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

        Vector2 raw = state switch
        {
            CharacterAnimationState.Attack => new Vector2(
                side * Mathf.Sin(Mathf.Clamp(elapsed / 0.34f, 0.0f, 1.0f) * Mathf.Pi) * attackDistance,
                0),
            CharacterAnimationState.Cast => new Vector2(
                0,
                -Mathf.Abs(Mathf.Sin(elapsed * 10.0f)) * (body == CharacterBodySilhouette.Robed ? 6.0f : 3.0f)),
            CharacterAnimationState.Dodge => new Vector2(
                -side * Mathf.Sin(Mathf.Clamp(elapsed / 0.28f, 0.0f, 1.0f) * Mathf.Pi) * dodgeDistance,
                0),
            CharacterAnimationState.Hit => new Vector2(Mathf.Sin(elapsed * 52.0f) * 5.0f, 0),
            CharacterAnimationState.Defeat => new Vector2(0, Mathf.Min(62.0f, elapsed * 68.0f)),
            _ => Vector2.Zero
        };

        return new Vector2(
            Mathf.Round(raw.X / PixelScale) * PixelScale,
            Mathf.Round(raw.Y / PixelScale) * PixelScale);
    }

    /// <summary>用一串整数逻辑像素补充斜向服装细节。</summary>
    private void DrawSteppedDetail(
        Vector2 origin,
        int startX,
        int startY,
        int steps,
        int stepX,
        int stepY,
        Color color)
    {
        for (int index = 0; index < steps; index++)
        {
            DrawPixel(origin, startX + stepX * index, startY + stepY * index, 1, 1, color);
        }
    }

    /// <summary>绘制一个 96×96 逻辑画布中的整数像素块。</summary>
    private void DrawPixel(Vector2 origin, int x, int y, int width, int height, Color color)
    {
        DrawRect(
            new Rect2(
                origin + new Vector2(x * PixelScale, y * PixelScale),
                new Vector2(width * PixelScale, height * PixelScale)),
            color,
            true);
    }

    /// <summary>把倒下透明度应用到叠加层颜色。</summary>
    private static Color Fade(Color color, float opacity)
    {
        color.A *= opacity;
        return color;
    }
}
