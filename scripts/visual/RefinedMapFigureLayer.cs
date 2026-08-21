using FlameEmblem.Game;
using Godot;
using System.Collections;
using System.Reflection;

namespace FlameEmblem.Visual;

/// <summary>
/// 替换 UnitCharacterLayer 的旧程序地图人物绘制，同时复用它已经稳定的移动路径、视觉中心和朝向。
/// 正式 map PNG/方向序列帧仍然优先；缺少素材时才绘制原创修长地图小人。
/// </summary>
public partial class RefinedMapFigureLayer : Node2D
{
    /// <summary>实际负责地图移动状态的原人物层。</summary>
    private UnitCharacterLayer? _source;

    /// <summary>原人物层当前移动字典字段，用于判断单个单位是否在走路。</summary>
    private FieldInfo? _motionsField;

    /// <summary>原人物层计算视觉中心的方法。</summary>
    private MethodInfo? _resolveVisualCenterMethod;

    /// <summary>原人物层计算面朝方向的方法。</summary>
    private MethodInfo? _resolveFacingMethod;

    /// <summary>原人物层选择正式方向序列帧的方法。</summary>
    private MethodInfo? _resolveAnimationFrameMethod;

    /// <summary>程序地图人物继续使用 2× 逻辑像素。</summary>
    private const float PixelScale = 2.0f;

    /// <summary>正式地图人物最大整数占用边长，与原人物层保持一致。</summary>
    private const int FormalMapMaxFootprint = 48;

    /// <summary>正式人物脚底相对格子中心的偏移。</summary>
    private const float FormalMapFeetOffsetY = 20.0f;

    /// <summary>绑定现有地图人物层。</summary>
    public void Bind(UnitCharacterLayer source)
    {
        _source = source;
        BindingFlags members = BindingFlags.Instance | BindingFlags.NonPublic;
        Type type = typeof(UnitCharacterLayer);
        _motionsField = type.GetField("_motions", members);
        _resolveVisualCenterMethod = type.GetMethod("ResolveVisualCenter", members);
        _resolveFacingMethod = type.GetMethod("ResolveFacing", members);
        _resolveAnimationFrameMethod = type.GetMethod("ResolveAnimationFrame", members);

        TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        ZIndex = 1;
        SetProcess(true);
        QueueRedraw();
    }

    /// <summary>原层继续推进移动，本层每帧只请求重绘。</summary>
    public override void _Process(double delta)
    {
        QueueRedraw();
    }

    /// <summary>隐藏旧人物层自身绘制，并按同一移动状态重新绘制所有存活单位。</summary>
    public override void _Draw()
    {
        if (_source is null)
        {
            return;
        }

        // SelfModulate 只影响 UnitCharacterLayer 自身，不影响它的子节点，因此新绘制层仍然可见。
        _source.SelfModulate = new Color(1, 1, 1, 0);

        foreach (UnitModel unit in _source.Units.Where(unit => unit.IsAlive))
        {
            DrawUnit(unit);
        }
    }

    /// <summary>绘制阵营底座、正式素材或修长程序人物，以及选中角标。</summary>
    private void DrawUnit(UnitModel unit)
    {
        if (_source is null)
        {
            return;
        }

        Vector2 visualCenter = RoundVector(ReadVisualCenter(unit));
        bool moving = IsUnitMoving(unit);
        CharacterFacing facing = ReadFacing(unit);
        CharacterAnimationState animationState = moving
            ? CharacterAnimationState.Walk
            : CharacterAnimationState.Idle;
        Texture2D? frame = ReadAnimationFrame(unit, animationState, facing);

        Color teamColor = unit.Team == UnitTeam.Player
            ? new Color(0.20f, 0.52f, 1.0f)
            : new Color(0.93f, 0.24f, 0.24f);
        if (unit.HasActed)
        {
            teamColor = teamColor.Darkened(0.35f);
        }

        // 继续保留窄阵营底座，避免角色在复杂地形上失去阵营辨识。
        DrawRect(
            new Rect2(visualCenter + new Vector2(-16, 20), new Vector2(32, 4)),
            new Color(0.04f, 0.05f, 0.07f, 0.90f),
            true);
        DrawRect(
            new Rect2(visualCenter + new Vector2(-13, 21), new Vector2(26, 2)),
            teamColor,
            true);

        if (frame is not null)
        {
            DrawFormalMapTexture(unit, frame, visualCenter);
        }
        else
        {
            Texture2D? mapTexture = CharacterAssetResolver.TryLoad(unit, CharacterArtSlot.Map);
            if (mapTexture is not null)
            {
                DrawFormalMapTexture(unit, mapTexture, visualCenter);
            }
            else
            {
                DrawRefinedProceduralUnit(unit, visualCenter, facing, moving);
            }
        }

        if (ReferenceEquals(unit, _source.SelectedUnit))
        {
            DrawSelectionCorners(
                new Rect2(visualCenter + new Vector2(-20, -25), new Vector2(40, 50)),
                new Color(1.0f, 0.84f, 0.25f));
        }
    }

    /// <summary>正式地图素材保持原比例、整数倍率和统一脚底基准。</summary>
    private void DrawFormalMapTexture(UnitModel unit, Texture2D texture, Vector2 center)
    {
        int sourceWidth = Math.Max(1, texture.GetWidth());
        int sourceHeight = Math.Max(1, texture.GetHeight());
        int largestSide = Math.Max(sourceWidth, sourceHeight);
        int integerScale = Math.Clamp(FormalMapMaxFootprint / largestSide, 1, 2);
        int targetWidth = sourceWidth * integerScale;
        int targetHeight = sourceHeight * integerScale;
        float targetX = Mathf.Round(center.X - targetWidth / 2.0f);
        float feetY = Mathf.Round(center.Y + FormalMapFeetOffsetY);
        float targetY = feetY - targetHeight;
        Rect2 target = new(new Vector2(targetX, targetY), new Vector2(targetWidth, targetHeight));

        DrawTextureRect(texture, target, false);
        if (unit.HasActed)
        {
            DrawRect(target, new Color(0.04f, 0.05f, 0.07f, 0.32f), true);
        }
    }

    /// <summary>绘制新的修长地图小人：小头、细颈、窄腰和更长腿部。</summary>
    private void DrawRefinedProceduralUnit(
        UnitModel unit,
        Vector2 center,
        CharacterFacing facing,
        bool moving)
    {
        CharacterAppearanceDefinition appearance = CharacterAppearanceCatalog.Get(unit);
        Vector2 origin = SnapGrid(center + new Vector2(-16, -23));
        bool alternateStep = moving && ((int)(Time.GetTicksMsec() / 110) % 2 == 1);

        Color outline = new("17171d");
        Color cloth = appearance.OutfitColor;
        Color shadow = cloth.Darkened(0.30f);
        Color light = cloth.Lightened(0.17f);
        Color hair = appearance.HairColor;
        Color hairShadow = hair.Darkened(0.24f);
        Color leather = new("4b3429");
        Color metal = new("c1c6c4");
        Color metalShadow = new("555d63");

        DrawMapCape(origin, appearance, facing, outline);
        DrawMapLegs(origin, appearance, alternateStep, outline, cloth, shadow, leather, metalShadow);
        DrawMapTorso(origin, appearance, outline, cloth, shadow, light, leather, metalShadow);
        DrawMapHead(origin, unit, appearance, facing, outline, hair, hairShadow);
        DrawMapWeapon(origin, appearance, facing, metal, metalShadow, leather);

        if (unit.HasActed)
        {
            // 用细条纹暗化而不是整块灰色蒙版，继续保留角色色彩。
            Color acted = new(0.02f, 0.03f, 0.04f, 0.30f);
            for (int y = 2; y <= 21; y += 3)
            {
                Pixel(origin, 4, y, 9, 1, acted);
            }
        }
    }

    /// <summary>地图披风采用上窄下宽的轮廓，避免旧版背后大矩形。</summary>
    private void DrawMapCape(
        Vector2 origin,
        CharacterAppearanceDefinition appearance,
        CharacterFacing facing,
        Color outline)
    {
        if (!appearance.HasCape)
        {
            return;
        }

        int shift = facing == CharacterFacing.Left ? 1 : facing == CharacterFacing.Right ? -1 : 0;
        Color cape = appearance.AccentColor.Darkened(0.20f);
        Pixel(origin, 5 + shift, 8, 6, 3, outline);
        Pixel(origin, 6 + shift, 9, 4, 2, cape);
        Pixel(origin, 4 + shift, 11, 8, 7, outline);
        Pixel(origin, 5 + shift, 11, 6, 6, cape);
        Pixel(origin, 5 + shift, 17, 5, 3, cape.Darkened(0.12f));
    }

    /// <summary>腿部占人物接近三分之一高度，让棋盘小人不再显得大头短腿。</summary>
    private void DrawMapLegs(
        Vector2 origin,
        CharacterAppearanceDefinition appearance,
        bool alternateStep,
        Color outline,
        Color cloth,
        Color shadow,
        Color leather,
        Color metalShadow)
    {
        if (appearance.BodySilhouette == CharacterBodySilhouette.Robed)
        {
            // 长袍中间保留开衩，能看到双脚方向。
            Pixel(origin, 4, 13, 8, 6, outline);
            Pixel(origin, 5, 13, 6, 5, cloth);
            Pixel(origin, 5, 14, 2, 4, cloth.Lightened(0.10f));
            Pixel(origin, 9, 14, 2, 4, shadow);
            Pixel(origin, 7, 15, 2, 4, new Color(0.07f, 0.07f, 0.09f));
            Pixel(origin, 5, 19, 2, 3, leather);
            Pixel(origin, 9, 19, 2, 3, leather);
            Pixel(origin, 4, 21, 4, 2, outline);
            Pixel(origin, 8, 21, 4, 2, outline);
            return;
        }

        int leftX = alternateStep ? 5 : 6;
        int rightX = alternateStep ? 10 : 9;
        Color leg = appearance.BodySilhouette == CharacterBodySilhouette.Armored ? metalShadow : shadow;

        Pixel(origin, leftX, 14, 2, 8, outline);
        Pixel(origin, rightX, 14, 2, 8, outline);
        Pixel(origin, leftX, 15, 1, 6, leg);
        Pixel(origin, rightX, 15, 1, 6, leg.Darkened(0.08f));
        Pixel(origin, leftX - 1, 21, 4, 2, outline);
        Pixel(origin, rightX - 1, 21, 4, 2, outline);
        Pixel(origin, leftX, 20, 2, 2, leather);
        Pixel(origin, rightX, 20, 2, 2, leather);
    }

    /// <summary>所有职业共享窄腰人体基础，仅用肩甲、胸带和袍领建立职业差异。</summary>
    private void DrawMapTorso(
        Vector2 origin,
        CharacterAppearanceDefinition appearance,
        Color outline,
        Color cloth,
        Color shadow,
        Color light,
        Color leather,
        Color metalShadow)
    {
        // 肩部 8px 宽、腰部 6px 宽，相比旧版上下同宽更加接近人体轮廓。
        Pixel(origin, 4, 7, 8, 3, outline);
        Pixel(origin, 5, 8, 6, 6, outline);
        Pixel(origin, 6, 8, 4, 5, cloth);
        Pixel(origin, 6, 8, 1, 4, light);
        Pixel(origin, 9, 10, 1, 3, shadow);
        Pixel(origin, 5, 13, 6, 2, outline);
        Pixel(origin, 6, 13, 4, 1, leather);

        if (appearance.BodySilhouette == CharacterBodySilhouette.Armored)
        {
            Pixel(origin, 3, 8, 3, 3, outline);
            Pixel(origin, 10, 8, 3, 3, outline);
            Pixel(origin, 4, 8, 2, 2, metalShadow);
            Pixel(origin, 10, 8, 2, 2, metalShadow);
            Pixel(origin, 6, 8, 4, 2, cloth.Lightened(0.20f));
            Pixel(origin, 6, 12, 4, 1, appearance.AccentColor);
        }
        else if (appearance.BodySilhouette == CharacterBodySilhouette.Robed)
        {
            Pixel(origin, 6, 7, 4, 2, appearance.AccentColor.Darkened(0.12f));
        }
        else
        {
            // 轻装人物增加一条斜胸带，地图尺寸下也能看出服装层次。
            Pixel(origin, 6, 8, 1, 2, leather);
            Pixel(origin, 7, 10, 1, 2, leather);
            Pixel(origin, 8, 12, 1, 2, leather);
        }
    }

    /// <summary>头部缩到 5×5，并给四名玩家角色不同的原创发型。</summary>
    private void DrawMapHead(
        Vector2 origin,
        UnitModel unit,
        CharacterAppearanceDefinition appearance,
        CharacterFacing facing,
        Color outline,
        Color hair,
        Color hairShadow)
    {
        Pixel(origin, 7, 6, 2, 2, outline);
        Pixel(origin, 7, 6, 2, 1, appearance.SkinColor.Darkened(0.12f));
        Pixel(origin, 6, 1, 5, 6, outline);
        Pixel(origin, 7, 3, 3, 3, appearance.SkinColor);
        Pixel(origin, 6, 1, 5, 3, hair);

        string id = unit.Id.ToLowerInvariant();
        if (id == "celine")
        {
            Pixel(origin, facing == CharacterFacing.Left ? 10 : 5, 3, 2, 7, hairShadow);
            Pixel(origin, facing == CharacterFacing.Left ? 11 : 4, 8, 2, 4, hair);
        }
        else if (id == "mira")
        {
            Pixel(origin, 5, 3, 2, 8, hairShadow);
            Pixel(origin, 10, 3, 2, 8, hair);
            Pixel(origin, 5, 9, 2, 4, hairShadow);
            Pixel(origin, 10, 9, 2, 4, hair);
        }
        else if (id == "adrian")
        {
            Pixel(origin, 6, 0, 2, 2, hair);
            Pixel(origin, 9, 0, 2, 2, hair);
        }

        if (facing != CharacterFacing.Up)
        {
            int eyeX = facing switch
            {
                CharacterFacing.Left => 7,
                CharacterFacing.Right => 9,
                _ => 8
            };
            Pixel(origin, eyeX, 4, 1, 1, outline);
        }
        else
        {
            Pixel(origin, 7, 3, 3, 3, hairShadow);
        }
    }

    /// <summary>地图武器继续保持夸张长度，但杆/刃厚度收细一档。</summary>
    private void DrawMapWeapon(
        Vector2 origin,
        CharacterAppearanceDefinition appearance,
        CharacterFacing facing,
        Color metal,
        Color darkMetal,
        Color leather)
    {
        int side = facing == CharacterFacing.Left ? -1 : 1;
        int anchorX = side < 0 ? 3 : 13;

        switch (appearance.WeaponSilhouette)
        {
            case CharacterWeaponSilhouette.Spear:
                Pixel(origin, anchorX, 4, 1, 15, leather);
                Pixel(origin, anchorX - 1, 2, 3, 3, darkMetal);
                Pixel(origin, anchorX, 1, 1, 4, metal);
                break;

            case CharacterWeaponSilhouette.Bow:
                Color wood = appearance.AccentColor.Darkened(0.24f);
                Pixel(origin, anchorX, 5, 1, 4, wood);
                Pixel(origin, anchorX + side, 9, 1, 5, wood);
                Pixel(origin, anchorX, 14, 1, 4, wood);
                DrawLine(
                    origin + new Vector2(anchorX * PixelScale, 6 * PixelScale),
                    origin + new Vector2(anchorX * PixelScale, 17 * PixelScale),
                    darkMetal,
                    PixelScale,
                    false);
                break;

            case CharacterWeaponSilhouette.Tome:
                Pixel(origin, anchorX - (side < 0 ? 1 : 0), 10, 2, 3, darkMetal);
                Pixel(origin, anchorX - (side < 0 ? 1 : 0), 10, 1, 2, appearance.AccentColor);
                break;

            default:
                Pixel(origin, anchorX, 6, 1, 10, darkMetal);
                Pixel(origin, anchorX, 3, 1, 5, metal);
                Pixel(origin, anchorX - 1, 8, 3, 1, appearance.AccentColor.Darkened(0.18f));
                break;
        }
    }

    /// <summary>调用原人物层计算出的当前视觉中心。</summary>
    private Vector2 ReadVisualCenter(UnitModel unit)
    {
        if (_source is null || _resolveVisualCenterMethod is null)
        {
            return Vector2.Zero;
        }

        return _resolveVisualCenterMethod.Invoke(_source, new object[] { unit }) is Vector2 center
            ? center
            : Vector2.Zero;
    }

    /// <summary>调用原人物层的朝向计算，保证转弯和停下方向完全一致。</summary>
    private CharacterFacing ReadFacing(UnitModel unit)
    {
        if (_source is null || _resolveFacingMethod is null)
        {
            return CharacterFacing.Down;
        }

        return _resolveFacingMethod.Invoke(_source, new object[] { unit }) is CharacterFacing facing
            ? facing
            : CharacterFacing.Down;
    }

    /// <summary>复用原人物层的正式序列帧选择逻辑。</summary>
    private Texture2D? ReadAnimationFrame(
        UnitModel unit,
        CharacterAnimationState state,
        CharacterFacing facing)
    {
        if (_source is null || _resolveAnimationFrameMethod is null)
        {
            return null;
        }

        return _resolveAnimationFrameMethod.Invoke(_source, new object[] { unit, state, facing }) as Texture2D;
    }

    /// <summary>读取移动字典，判断当前单位是否正在逐格移动。</summary>
    private bool IsUnitMoving(UnitModel unit)
    {
        if (_source is null || _motionsField?.GetValue(_source) is not IDictionary motions)
        {
            return false;
        }

        return motions.Contains(unit.Id);
    }

    /// <summary>逻辑像素原点锁到 2px 网格。</summary>
    private static Vector2 SnapGrid(Vector2 value)
    {
        return new Vector2(
            Mathf.Round(value.X / PixelScale) * PixelScale,
            Mathf.Round(value.Y / PixelScale) * PixelScale);
    }

    /// <summary>屏幕位置锁到整数像素。</summary>
    private static Vector2 RoundVector(Vector2 value)
    {
        return new Vector2(Mathf.Round(value.X), Mathf.Round(value.Y));
    }

    /// <summary>绘制 2× 逻辑像素块。</summary>
    private void Pixel(Vector2 origin, int x, int y, int width, int height, Color color)
    {
        DrawRect(
            new Rect2(
                origin + new Vector2(x * PixelScale, y * PixelScale),
                new Vector2(width * PixelScale, height * PixelScale)),
            color,
            true);
    }

    /// <summary>绘制选择框四个角。</summary>
    private void DrawSelectionCorners(Rect2 bounds, Color color)
    {
        const float corner = 7.0f;
        const float thickness = 2.0f;
        DrawRect(new Rect2(bounds.Position, new Vector2(corner, thickness)), color, true);
        DrawRect(new Rect2(bounds.Position, new Vector2(thickness, corner)), color, true);
        DrawRect(new Rect2(new Vector2(bounds.End.X - corner, bounds.Position.Y), new Vector2(corner, thickness)), color, true);
        DrawRect(new Rect2(new Vector2(bounds.End.X - thickness, bounds.Position.Y), new Vector2(thickness, corner)), color, true);
        DrawRect(new Rect2(new Vector2(bounds.Position.X, bounds.End.Y - thickness), new Vector2(corner, thickness)), color, true);
        DrawRect(new Rect2(new Vector2(bounds.Position.X, bounds.End.Y - corner), new Vector2(thickness, corner)), color, true);
        DrawRect(new Rect2(new Vector2(bounds.End.X - corner, bounds.End.Y - thickness), new Vector2(corner, thickness)), color, true);
        DrawRect(new Rect2(new Vector2(bounds.End.X - thickness, bounds.End.Y - corner), new Vector2(thickness, corner)), color, true);
    }
}
