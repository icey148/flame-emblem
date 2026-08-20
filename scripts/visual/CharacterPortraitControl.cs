using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// HUD 中使用的人物头像/半身像控件。
/// 正式 portrait.png 存在时优先显示正式原创立绘；缺少素材时自动退回复古有限色块像素胸像。
/// </summary>
public partial class CharacterPortraitControl : Control
{
    /// <summary>当前需要展示的单位；为空时显示中性像素占位轮廓。</summary>
    private UnitModel? _unit;

    /// <summary>节点就绪后启用最近邻过滤，保证头像素材和程序像素胸像保持硬边。</summary>
    public override void _Ready()
    {
        TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
    }

    /// <summary>
    /// 设置当前人物并请求 Godot 重绘控件。
    /// </summary>
    public void SetUnit(UnitModel? unit)
    {
        _unit = unit;
        QueueRedraw();
    }

    /// <summary>
    /// 绘制当前人物头像。
    /// </summary>
    public override void _Draw()
    {
        Rect2 bounds = new(Vector2.Zero, Size);
        DrawRect(bounds, new Color(0.055f, 0.06f, 0.075f, 0.96f), true);
        DrawRect(new Rect2(Vector2.Zero, new Vector2(Size.X, 3)), new Color(0.34f, 0.35f, 0.39f), true);
        DrawRect(new Rect2(new Vector2(0, Size.Y - 3), new Vector2(Size.X, 3)), new Color(0.18f, 0.19f, 0.22f), true);

        if (_unit is null)
        {
            DrawEmptyPortrait();
            return;
        }

        Texture2D? portraitTexture = CharacterAssetResolver.TryLoad(_unit, CharacterArtSlot.Portrait);
        if (portraitTexture is not null)
        {
            // 正式原创立绘使用最近邻过滤；素材自身负责透明背景和构图裁切。
            DrawTextureRect(portraitTexture, bounds, false);
            return;
        }

        DrawProceduralPixelPortrait(_unit);
    }

    /// <summary>
    /// 没有选中人物时绘制中性像素占位轮廓。
    /// </summary>
    private void DrawEmptyPortrait()
    {
        float pixel = ResolvePixelSize();
        Vector2 origin = ResolvePortraitOrigin(pixel);
        Color dark = new(0.18f, 0.19f, 0.22f);
        Color mid = new(0.28f, 0.29f, 0.32f);

        DrawPortraitPixel(origin, pixel, 8, 4, 8, 8, dark);
        DrawPortraitPixel(origin, pixel, 9, 5, 6, 6, mid);
        DrawPortraitPixel(origin, pixel, 5, 13, 14, 9, dark);
        DrawPortraitPixel(origin, pixel, 7, 12, 10, 3, mid);
    }

    /// <summary>
    /// 在正式头像素材缺失时绘制原创复古像素胸像。
    /// 使用有限色阶与块状轮廓，让人物详情界面和地图/战斗人物保持同一种视觉语言。
    /// </summary>
    private void DrawProceduralPixelPortrait(UnitModel unit)
    {
        CharacterAppearanceDefinition appearance = CharacterAppearanceCatalog.Get(unit);
        float pixel = ResolvePixelSize();
        Vector2 origin = ResolvePortraitOrigin(pixel);

        Color outline = new(0.075f, 0.065f, 0.075f);
        Color outfit = appearance.OutfitColor;
        Color outfitShadow = appearance.OutfitColor.Darkened(0.32f);
        Color outfitLight = appearance.OutfitColor.Lightened(0.18f);
        Color hair = appearance.HairColor;
        Color hairShadow = appearance.HairColor.Darkened(0.22f);
        Color skin = appearance.SkinColor;
        Color accent = appearance.AccentColor;
        Color metal = new(0.60f, 0.62f, 0.64f);

        // 披风和肩部先画在后方，形成宽阔的胸像轮廓。
        if (appearance.HasCape)
        {
            DrawPortraitPixel(origin, pixel, 3, 14, 18, 9, accent.Darkened(0.25f));
        }

        DrawPortraitBody(origin, pixel, appearance, outline, outfit, outfitShadow, outfitLight, accent, metal);

        // 颈部和脸部采用方形轮廓，不使用圆形或抗锯齿曲线。
        DrawPortraitPixel(origin, pixel, 10, 11, 4, 4, skin.Darkened(0.08f));
        DrawPortraitPixel(origin, pixel, 7, 3, 10, 10, outline);
        DrawPortraitPixel(origin, pixel, 8, 4, 8, 8, skin);

        // 发型由主色和阴影块组成；不同人物沿用各自配色，因此无需复制任何既有人物造型。
        DrawPortraitPixel(origin, pixel, 6, 2, 12, 4, hair);
        DrawPortraitPixel(origin, pixel, 6, 5, 3, 7, hairShadow);
        DrawPortraitPixel(origin, pixel, 15, 4, 3, 5, hair);
        DrawPortraitPixel(origin, pixel, 10, 2, 7, 2, hair.Lightened(0.10f));

        // 只用几个深色像素表示眼睛、眉线和嘴部，保持低分辨率头像的克制细节。
        DrawPortraitPixel(origin, pixel, 9, 7, 2, 1, outline);
        DrawPortraitPixel(origin, pixel, 14, 7, 2, 1, outline);
        DrawPortraitPixel(origin, pixel, 10, 10, 5, 1, skin.Darkened(0.26f));

        // 根据职业给胸像增加一个很小的装备标记，帮助快速识别角色定位。
        DrawPortraitClassMark(origin, pixel, appearance, outline, accent);
    }

    /// <summary>
    /// 根据身体模板绘制胸像肩部与服装结构。
    /// 重甲强调肩甲/胸甲，长袍强调领口与披肩，轻装保持简洁窄肩。
    /// </summary>
    private void DrawPortraitBody(
        Vector2 origin,
        float pixel,
        CharacterAppearanceDefinition appearance,
        Color outline,
        Color outfit,
        Color shadow,
        Color light,
        Color accent,
        Color metal)
    {
        switch (appearance.BodySilhouette)
        {
            case CharacterBodySilhouette.Armored:
                // 重甲胸像：肩部横向更宽，并增加金属肩甲和中央胸甲高光。
                DrawPortraitPixel(origin, pixel, 2, 13, 20, 10, outline);
                DrawPortraitPixel(origin, pixel, 4, 14, 16, 9, outfit);
                DrawPortraitPixel(origin, pixel, 2, 14, 5, 5, metal.Darkened(0.12f));
                DrawPortraitPixel(origin, pixel, 17, 14, 5, 5, metal.Darkened(0.12f));
                DrawPortraitPixel(origin, pixel, 6, 14, 4, 7, light);
                DrawPortraitPixel(origin, pixel, 16, 17, 4, 6, shadow);
                DrawPortraitPixel(origin, pixel, 8, 15, 8, 2, metal.Lightened(0.10f));
                DrawPortraitPixel(origin, pixel, 4, 20, 16, 2, accent);
                break;

            case CharacterBodySilhouette.Robed:
                // 长袍胸像：窄肩、宽袖与 V 形领口，突出施法职业的柔软服装结构。
                DrawPortraitPixel(origin, pixel, 4, 13, 16, 10, outline);
                DrawPortraitPixel(origin, pixel, 5, 14, 14, 9, outfit);
                DrawPortraitPixel(origin, pixel, 3, 15, 4, 7, shadow);
                DrawPortraitPixel(origin, pixel, 17, 15, 4, 7, shadow);
                DrawPortraitPixel(origin, pixel, 6, 14, 4, 7, light);
                DrawPortraitPixel(origin, pixel, 9, 14, 2, 4, accent);
                DrawPortraitPixel(origin, pixel, 13, 14, 2, 4, accent);
                DrawPortraitPixel(origin, pixel, 7, 20, 10, 2, accent.Darkened(0.15f));
                break;

            default:
                // 轻装胸像：窄肩与短上衣，保留更多人物脸部空间。
                DrawPortraitPixel(origin, pixel, 4, 13, 16, 10, outline);
                DrawPortraitPixel(origin, pixel, 5, 14, 14, 9, outfit);
                DrawPortraitPixel(origin, pixel, 5, 14, 5, 7, light);
                DrawPortraitPixel(origin, pixel, 16, 17, 3, 6, shadow);
                DrawPortraitPixel(origin, pixel, 5, 19, 14, 2, accent);
                break;
        }
    }

    /// <summary>
    /// 在胸像边缘绘制职业装备标记。
    /// 该标记只用于辨识，不承担战斗碰撞或规则逻辑。
    /// </summary>
    private void DrawPortraitClassMark(
        Vector2 origin,
        float pixel,
        CharacterAppearanceDefinition appearance,
        Color outline,
        Color accent)
    {
        switch (appearance.WeaponSilhouette)
        {
            case CharacterWeaponSilhouette.Spear:
                DrawPortraitPixel(origin, pixel, 20, 7, 1, 13, outline);
                DrawPortraitPixel(origin, pixel, 19, 5, 3, 3, new Color(0.78f, 0.80f, 0.78f));
                break;
            case CharacterWeaponSilhouette.Bow:
                DrawPortraitPixel(origin, pixel, 20, 8, 1, 4, accent.Darkened(0.18f));
                DrawPortraitPixel(origin, pixel, 21, 12, 1, 5, accent.Darkened(0.18f));
                DrawPortraitPixel(origin, pixel, 20, 17, 1, 4, accent.Darkened(0.18f));
                break;
            case CharacterWeaponSilhouette.Tome:
                DrawPortraitPixel(origin, pixel, 19, 15, 4, 5, outline);
                DrawPortraitPixel(origin, pixel, 20, 16, 2, 3, accent);
                break;
            default:
                DrawPortraitPixel(origin, pixel, 20, 8, 1, 11, outline);
                DrawPortraitPixel(origin, pixel, 20, 6, 1, 5, new Color(0.80f, 0.82f, 0.80f));
                break;
        }
    }

    /// <summary>根据控件尺寸计算整数像素放大倍率。</summary>
    private float ResolvePixelSize()
    {
        float raw = Mathf.Min(Size.X / 24.0f, Size.Y / 24.0f);
        return Mathf.Max(2.0f, Mathf.Floor(raw));
    }

    /// <summary>把 24×24 逻辑头像网格居中到当前控件。</summary>
    private Vector2 ResolvePortraitOrigin(float pixel)
    {
        Vector2 renderedSize = new(24.0f * pixel, 24.0f * pixel);
        return (Size - renderedSize) * 0.5f;
    }

    /// <summary>绘制一个头像逻辑像素块。</summary>
    private void DrawPortraitPixel(Vector2 origin, float pixel, int x, int y, int width, int height, Color color)
    {
        DrawRect(
            new Rect2(
                origin + new Vector2(x * pixel, y * pixel),
                new Vector2(width * pixel, height * pixel)),
            color,
            true);
    }
}
