using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 锁定攻击目标时使用的战斗预览人物控件。
/// 加载优先级为 battle.png → portrait.png → 原创侧视像素职业模板，保证预览与正式战斗演出视觉一致。
/// </summary>
public partial class BattleCharacterArtControl : Control
{
    /// <summary>当前需要展示的人物。</summary>
    private UnitModel? _unit;

    /// <summary>程序预览模板的逻辑像素放大倍率。</summary>
    private const float PixelScale = 4.0f;

    /// <summary>启用最近邻过滤，让低分辨率战斗素材保持像素硬边。</summary>
    public override void _Ready()
    {
        TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
    }

    /// <summary>
    /// 设置人物并请求重绘。
    /// </summary>
    public void SetUnit(UnitModel? unit)
    {
        _unit = unit;
        QueueRedraw();
    }

    /// <summary>
    /// 绘制战斗人物图。
    /// </summary>
    public override void _Draw()
    {
        Rect2 bounds = new(Vector2.Zero, Size);
        DrawRect(bounds, new Color(0.035f, 0.04f, 0.055f, 0.97f), true);

        if (_unit is null)
        {
            return;
        }

        Texture2D? texture = CharacterAssetResolver.TryLoad(_unit, CharacterArtSlot.Battle)
                             ?? CharacterAssetResolver.TryLoad(_unit, CharacterArtSlot.Portrait);
        if (texture is not null)
        {
            DrawTextureRect(texture, bounds, false);
            return;
        }

        DrawProceduralPixelPreview(_unit);
    }

    /// <summary>
    /// 正式战斗素材缺失时绘制原创侧视像素职业预览。
    /// 玩家默认朝右，敌军默认朝左，因此双方在预测面板中自然相向。
    /// </summary>
    private void DrawProceduralPixelPreview(UnitModel unit)
    {
        CharacterAppearanceDefinition appearance = CharacterAppearanceCatalog.Get(unit);
        int side = unit.Team == UnitTeam.Player ? 1 : -1;
        Vector2 origin = new Vector2(Size.X * 0.5f - 64.0f, Size.Y * 0.05f);

        Color outline = new(0.065f, 0.055f, 0.065f);
        Color outfit = appearance.OutfitColor;
        Color shadow = appearance.OutfitColor.Darkened(0.34f);
        Color light = appearance.OutfitColor.Lightened(0.18f);
        Color skin = appearance.SkinColor;
        Color hair = appearance.HairColor;
        Color accent = appearance.AccentColor;
        Color metal = new(0.82f, 0.84f, 0.82f);
        Color darkMetal = new(0.28f, 0.29f, 0.30f);

        if (appearance.HasCape)
        {
            // 披风先画在职业本体后方，避免覆盖肩甲、长袍边缘等识别轮廓。
            DrawPixel(origin, 7, 15, 15, 16, accent.Darkened(0.24f));
        }

        DrawPreviewBody(origin, appearance, outline, outfit, shadow, light, accent, darkMetal);

        // 头部所有职业共用同一低分辨率比例，职业差异主要留给身体、袖口和装备。
        DrawPixel(origin, 10, 4, 10, 10, outline);
        DrawPixel(origin, 11, 5, 8, 8, skin);
        DrawPixel(origin, 9, 3, 11, 4, hair);
        DrawPixel(origin, side > 0 ? 10 : 17, 6, 3, 6, hair.Darkened(0.22f));
        DrawPixel(origin, side > 0 ? 17 : 11, 9, 1, 1, outline);

        DrawPreviewArms(origin, appearance, side, shadow);
        DrawPreviewWeapon(origin, appearance, side, accent, metal, darkMetal);
    }

    /// <summary>
    /// 根据轻装、重甲、长袍三种身体模板绘制预览职业体型。
    /// 三种模板刻意改变肩宽、腿部和下摆，而不是只改颜色。
    /// </summary>
    private void DrawPreviewBody(
        Vector2 origin,
        CharacterAppearanceDefinition appearance,
        Color outline,
        Color outfit,
        Color shadow,
        Color light,
        Color accent,
        Color darkMetal)
    {
        switch (appearance.BodySilhouette)
        {
            case CharacterBodySilhouette.Armored:
                // 重甲：厚肩甲、宽胸甲、分离腿甲和更大的金属靴。
                DrawPixel(origin, 6, 12, 18, 18, outline);
                DrawPixel(origin, 8, 14, 14, 15, outfit);
                DrawPixel(origin, 5, 13, 5, 6, darkMetal);
                DrawPixel(origin, 21, 13, 5, 6, darkMetal);
                DrawPixel(origin, 8, 15, 4, 10, light);
                DrawPixel(origin, 19, 19, 3, 10, shadow);
                DrawPixel(origin, 7, 22, 16, 3, accent);
                DrawPixel(origin, 8, 28, 5, 9, outline);
                DrawPixel(origin, 18, 28, 5, 9, outline);
                DrawPixel(origin, 9, 28, 4, 7, shadow);
                DrawPixel(origin, 18, 28, 4, 7, shadow);
                DrawPixel(origin, 6, 35, 8, 3, darkMetal);
                DrawPixel(origin, 17, 35, 8, 3, darkMetal);
                break;

            case CharacterBodySilhouette.Robed:
                // 长袍：上身较窄，下摆逐渐展开，脚部只露出少量深色鞋尖。
                DrawPixel(origin, 8, 13, 14, 13, outline);
                DrawPixel(origin, 9, 14, 12, 11, outfit);
                DrawPixel(origin, 9, 14, 4, 8, light);
                DrawPixel(origin, 18, 18, 3, 7, shadow);
                DrawPixel(origin, 8, 22, 14, 3, accent);
                DrawPixel(origin, 7, 25, 16, 11, outline);
                DrawPixel(origin, 8, 25, 14, 10, outfit);
                DrawPixel(origin, 8, 25, 4, 8, light.Darkened(0.06f));
                DrawPixel(origin, 19, 27, 3, 8, shadow);
                DrawPixel(origin, 8, 34, 5, 3, darkMetal);
                DrawPixel(origin, 18, 34, 5, 3, darkMetal);
                break;

            default:
                // 轻装：窄肩、短上衣、两条清晰分开的腿，突出灵活感。
                DrawPixel(origin, 8, 13, 14, 17, outline);
                DrawPixel(origin, 9, 14, 12, 15, outfit);
                DrawPixel(origin, 9, 14, 4, 11, light);
                DrawPixel(origin, 18, 20, 3, 9, shadow);
                DrawPixel(origin, 8, 24, 14, 3, accent);
                DrawPixel(origin, 10, 28, 4, 9, outline);
                DrawPixel(origin, 17, 28, 4, 9, outline);
                DrawPixel(origin, 10, 28, 3, 6, shadow);
                DrawPixel(origin, 17, 28, 3, 6, shadow);
                DrawPixel(origin, 8, 35, 7, 3, darkMetal);
                DrawPixel(origin, 16, 35, 7, 3, darkMetal);
                break;
        }
    }

    /// <summary>
    /// 根据身体模板绘制待机手臂。
    /// 重甲使用厚护臂，长袍使用宽袖，轻装保持细窄手臂。
    /// </summary>
    private void DrawPreviewArms(
        Vector2 origin,
        CharacterAppearanceDefinition appearance,
        int side,
        Color shadow)
    {
        int frontX = side > 0 ? 22 : 5;
        int backX = side > 0 ? 5 : 22;

        switch (appearance.BodySilhouette)
        {
            case CharacterBodySilhouette.Armored:
                DrawPixel(origin, frontX, 17, 4, 9, shadow);
                DrawPixel(origin, backX - 1, 17, 4, 9, shadow);
                break;
            case CharacterBodySilhouette.Robed:
                DrawPixel(origin, frontX - 1, 16, 5, 10, shadow);
                DrawPixel(origin, backX - 1, 16, 5, 10, shadow);
                break;
            default:
                DrawPixel(origin, frontX, 17, 3, 8, shadow);
                DrawPixel(origin, backX, 17, 3, 8, shadow);
                break;
        }
    }

    /// <summary>按职业绘制预览中的武器/法书。</summary>
    private void DrawPreviewWeapon(
        Vector2 origin,
        CharacterAppearanceDefinition appearance,
        int side,
        Color accent,
        Color metal,
        Color darkMetal)
    {
        int front = side > 0 ? 26 : 3;

        switch (appearance.WeaponSilhouette)
        {
            case CharacterWeaponSilhouette.Spear:
                DrawPixel(origin, front, 7, 2, 26, darkMetal);
                DrawPixel(origin, front - 1, 4, 4, 5, metal);
                break;
            case CharacterWeaponSilhouette.Bow:
                DrawPixel(origin, front, 10, 2, 6, accent.Darkened(0.16f));
                DrawPixel(origin, front + side * 2, 15, 2, 8, accent.Darkened(0.16f));
                DrawPixel(origin, front, 22, 2, 6, accent.Darkened(0.16f));
                DrawPixel(origin, front, 14, 1, 10, darkMetal);
                break;
            case CharacterWeaponSilhouette.Tome:
                DrawPixel(origin, side > 0 ? 24 : 4, 18, 5, 6, darkMetal);
                DrawPixel(origin, side > 0 ? 25 : 5, 19, 2, 4, accent);
                DrawPixel(origin, side > 0 ? 27 : 7, 19, 1, 4, accent.Lightened(0.24f));
                break;
            default:
                DrawPixel(origin, front, 9, 2, 18, darkMetal);
                DrawPixel(origin, front, 5, 2, 8, metal);
                DrawPixel(origin, front - 2, 14, 6, 2, accent.Darkened(0.18f));
                break;
        }
    }

    /// <summary>绘制一个预览逻辑像素块。</summary>
    private void DrawPixel(Vector2 origin, int x, int y, int width, int height, Color color)
    {
        DrawRect(
            new Rect2(
                origin + new Vector2(x * PixelScale, y * PixelScale),
                new Vector2(width * PixelScale, height * PixelScale)),
            color,
            true);
    }
}
