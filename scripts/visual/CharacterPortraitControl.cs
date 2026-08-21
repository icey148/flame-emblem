using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// HUD 中使用的人物头像/半身像控件。
/// 正式 portrait.png 存在时优先显示正式原创立绘；缺少素材时使用原创修长古典像素胸像。
/// </summary>
public partial class CharacterPortraitControl : Control
{
    /// <summary>当前需要展示的单位；为空时显示中性像素占位轮廓。</summary>
    private UnitModel? _unit;

    /// <summary>正式头像与面板边框之间至少保留的像素内边距。</summary>
    private const float FormalPortraitPadding = 4.0f;

    /// <summary>节点就绪后启用最近邻过滤，保证头像素材和程序像素胸像保持硬边。</summary>
    public override void _Ready()
    {
        TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
    }

    /// <summary>设置当前人物并请求 Godot 重绘控件。</summary>
    public void SetUnit(UnitModel? unit)
    {
        _unit = unit;
        QueueRedraw();
    }

    /// <summary>绘制当前人物头像。</summary>
    public override void _Draw()
    {
        Rect2 bounds = new(Vector2.Zero, Size);
        DrawRect(bounds, new Color(0.075f, 0.082f, 0.096f, 0.97f), true);
        DrawRect(new Rect2(Vector2.Zero, new Vector2(Size.X, 3)), new Color(0.46f, 0.45f, 0.40f), true);
        DrawRect(new Rect2(new Vector2(0, Size.Y - 3), new Vector2(Size.X, 3)), new Color(0.23f, 0.24f, 0.25f), true);

        if (_unit is null)
        {
            DrawEmptyPortrait();
            return;
        }

        Texture2D? portraitTexture = CharacterAssetResolver.TryLoad(_unit, CharacterArtSlot.Portrait);
        if (portraitTexture is not null)
        {
            DrawFormalPortrait(portraitTexture, bounds);
            return;
        }

        DrawProceduralPixelPortrait(_unit);
    }

    /// <summary>
    /// 以等比整数倍率绘制正式原创头像。
    /// 64×64 标准头像在小 HUD 中保持 1×，在大详情页中使用完整整数倍，不做横向拉伸。
    /// </summary>
    private void DrawFormalPortrait(Texture2D texture, Rect2 bounds)
    {
        int sourceWidth = Math.Max(1, texture.GetWidth());
        int sourceHeight = Math.Max(1, texture.GetHeight());
        float availableWidth = Math.Max(1.0f, bounds.Size.X - FormalPortraitPadding * 2.0f);
        float availableHeight = Math.Max(1.0f, bounds.Size.Y - FormalPortraitPadding * 2.0f);
        int integerScale = Math.Max(
            1,
            (int)MathF.Floor(MathF.Min(
                availableWidth / sourceWidth,
                availableHeight / sourceHeight)));
        int targetWidth = sourceWidth * integerScale;
        int targetHeight = sourceHeight * integerScale;
        float targetX = Mathf.Round((bounds.Size.X - targetWidth) * 0.5f);
        float targetY = Mathf.Round((bounds.Size.Y - targetHeight) * 0.5f);
        Rect2 target = new(new Vector2(targetX, targetY), new Vector2(targetWidth, targetHeight));

        DrawTextureRect(texture, target, false);
    }

    /// <summary>没有选中人物时绘制中性修长胸像占位。</summary>
    private void DrawEmptyPortrait()
    {
        float pixel = ResolvePixelSize();
        Vector2 origin = ResolvePortraitOrigin(pixel);
        Color dark = new(0.18f, 0.19f, 0.22f);
        Color mid = new(0.28f, 0.29f, 0.32f);

        DrawPortraitPixel(origin, pixel, 9, 3, 6, 10, dark);
        DrawPortraitPixel(origin, pixel, 10, 4, 4, 8, mid);
        DrawPortraitPixel(origin, pixel, 10, 12, 4, 3, dark);
        DrawPortraitPixel(origin, pixel, 5, 15, 14, 8, dark);
        DrawPortraitPixel(origin, pixel, 7, 15, 10, 7, mid);
    }

    /// <summary>
    /// 正式头像缺失时绘制原创修长古典像素胸像。
    /// 重点使用更窄脸型、明确下颌、细颈和个人发型，避免旧版正方形“大头胸像”。
    /// </summary>
    private void DrawProceduralPixelPortrait(UnitModel unit)
    {
        CharacterAppearanceDefinition appearance = CharacterAppearanceCatalog.Get(unit);
        float pixel = ResolvePixelSize();
        Vector2 origin = ResolvePortraitOrigin(pixel);

        Color outline = new("19171e");
        Color outfit = appearance.OutfitColor;
        Color outfitShadow = appearance.OutfitColor.Darkened(0.32f);
        Color outfitLight = appearance.OutfitColor.Lightened(0.18f);
        Color hair = appearance.HairColor;
        Color hairShadow = appearance.HairColor.Darkened(0.24f);
        Color hairLight = appearance.HairColor.Lightened(0.13f);
        Color skin = appearance.SkinColor;
        Color skinShadow = appearance.SkinColor.Darkened(0.15f);
        Color accent = appearance.AccentColor;
        Color metal = new("9fa6a8");
        Color leather = new("4c3429");

        // 披风只从肩后露出，不再占满整张胸像背景。
        if (appearance.HasCape)
        {
            DrawPortraitPixel(origin, pixel, 5, 16, 14, 7, outline);
            DrawPortraitPixel(origin, pixel, 6, 16, 12, 6, accent.Darkened(0.25f));
        }

        DrawPortraitBody(origin, pixel, appearance, outline, outfit, outfitShadow, outfitLight, accent, metal, leather);

        // 细颈把脸与肩部真正分开。
        DrawPortraitPixel(origin, pixel, 10, 12, 4, 4, outline);
        DrawPortraitPixel(origin, pixel, 11, 12, 2, 4, skinShadow);

        DrawPortraitFace(origin, pixel, outline, skin, skinShadow);
        DrawPortraitHair(origin, pixel, unit, hair, hairShadow, hairLight, outline);
        DrawPortraitClassMark(origin, pixel, appearance, outline, accent);
    }

    /// <summary>绘制更窄、更长并带下颌收束的脸型。</summary>
    private void DrawPortraitFace(
        Vector2 origin,
        float pixel,
        Color outline,
        Color skin,
        Color skinShadow)
    {
        // 外轮廓从额头 8px 宽逐渐收成 5px 下颌。
        DrawPortraitPixel(origin, pixel, 8, 3, 8, 9, outline);
        DrawPortraitPixel(origin, pixel, 9, 4, 6, 8, skin);
        DrawPortraitPixel(origin, pixel, 9, 10, 6, 3, outline);
        DrawPortraitPixel(origin, pixel, 10, 10, 4, 2, skinShadow);
        DrawPortraitPixel(origin, pixel, 11, 12, 2, 1, skinShadow);

        // 眉眼使用更小的单像素信息，鼻梁和嘴部错开，避免左右完全镜像的娃娃脸。
        DrawPortraitPixel(origin, pixel, 10, 6, 2, 1, outline);
        DrawPortraitPixel(origin, pixel, 13, 6, 1, 1, outline);
        DrawPortraitPixel(origin, pixel, 13, 8, 1, 2, skinShadow);
        DrawPortraitPixel(origin, pixel, 11, 10, 3, 1, skinShadow.Darkened(0.18f));
    }

    /// <summary>按角色 ID 绘制原创个人发型，使四名主角不再共用同一个发块。</summary>
    private void DrawPortraitHair(
        Vector2 origin,
        float pixel,
        UnitModel unit,
        Color hair,
        Color shadow,
        Color light,
        Color outline)
    {
        string id = unit.Id.ToLowerInvariant();

        // 通用发冠保持前方轻、后脑较深的层次。
        DrawPortraitPixel(origin, pixel, 7, 2, 10, 4, outline);
        DrawPortraitPixel(origin, pixel, 8, 2, 8, 3, hair);
        DrawPortraitPixel(origin, pixel, 9, 2, 4, 1, light);
        DrawPortraitPixel(origin, pixel, 7, 5, 3, 6, shadow);

        if (id == "adrian")
        {
            // Adrian：短而不规则的发束，形成年轻剑士轮廓。
            DrawPortraitPixel(origin, pixel, 7, 1, 3, 3, outline);
            DrawPortraitPixel(origin, pixel, 13, 1, 3, 3, outline);
            DrawPortraitPixel(origin, pixel, 8, 1, 2, 2, hair);
            DrawPortraitPixel(origin, pixel, 13, 2, 2, 2, hair);
            DrawPortraitPixel(origin, pixel, 15, 4, 2, 5, hair);
            return;
        }

        if (id == "celine")
        {
            // Celine：侧后长发束和轻薄前发，保持弓手的轻盈感。
            DrawPortraitPixel(origin, pixel, 15, 4, 3, 8, hair);
            DrawPortraitPixel(origin, pixel, 16, 10, 3, 8, shadow);
            DrawPortraitPixel(origin, pixel, 17, 16, 2, 5, hair);
            DrawPortraitPixel(origin, pixel, 12, 3, 4, 3, hair);
            return;
        }

        if (id == "mira")
        {
            // Mira：两侧长发向肩部下垂，与长袍职业形成纵向节奏。
            DrawPortraitPixel(origin, pixel, 6, 4, 3, 10, shadow);
            DrawPortraitPixel(origin, pixel, 15, 4, 3, 10, hair);
            DrawPortraitPixel(origin, pixel, 6, 12, 3, 8, shadow);
            DrawPortraitPixel(origin, pixel, 15, 12, 3, 8, hair);
            DrawPortraitPixel(origin, pixel, 12, 3, 4, 3, hair);
            return;
        }

        if (id == "rowan")
        {
            // Rowan：短发露出耳侧和颈部，和厚重铠甲形成反差。
            DrawPortraitPixel(origin, pixel, 15, 4, 2, 5, shadow);
            DrawPortraitPixel(origin, pixel, 8, 3, 7, 2, hair);
            return;
        }

        // 通用敌军保持较短后发，避免每个人都像同一顶头盔。
        DrawPortraitPixel(origin, pixel, 15, 4, 2, 6, shadow);
    }

    /// <summary>
    /// 根据身体模板绘制更窄的肩线和多层领口。
    /// 重甲只扩大肩甲，不把整个胸腔做成方形；长袍也保留颈肩结构。
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
        Color metal,
        Color leather)
    {
        // 所有职业共享收窄胸腔，肩部略宽、腰部再收窄。
        DrawPortraitPixel(origin, pixel, 5, 15, 14, 4, outline);
        DrawPortraitPixel(origin, pixel, 6, 16, 12, 7, outline);
        DrawPortraitPixel(origin, pixel, 7, 16, 10, 7, outfit);
        DrawPortraitPixel(origin, pixel, 7, 16, 3, 5, light);
        DrawPortraitPixel(origin, pixel, 15, 18, 2, 5, shadow);
        DrawPortraitPixel(origin, pixel, 9, 15, 6, 2, leather);
        DrawPortraitPixel(origin, pixel, 11, 16, 2, 2, accent);

        switch (appearance.BodySilhouette)
        {
            case CharacterBodySilhouette.Armored:
                // 肩甲向外扩，但胸甲本身仍然保留窄腰。
                DrawPortraitPixel(origin, pixel, 3, 15, 4, 5, outline);
                DrawPortraitPixel(origin, pixel, 17, 15, 4, 5, outline);
                DrawPortraitPixel(origin, pixel, 4, 16, 3, 3, metal.Darkened(0.08f));
                DrawPortraitPixel(origin, pixel, 17, 16, 3, 3, metal.Darkened(0.14f));
                DrawPortraitPixel(origin, pixel, 9, 16, 6, 3, metal);
                DrawPortraitPixel(origin, pixel, 10, 16, 2, 2, metal.Lightened(0.12f));
                DrawPortraitPixel(origin, pixel, 8, 21, 8, 2, accent.Darkened(0.12f));
                break;

            case CharacterBodySilhouette.Robed:
                // 高领与 V 型饰边突出柔软层次，而不是宽大的长方形肩膀。
                DrawPortraitPixel(origin, pixel, 9, 14, 6, 2, accent.Darkened(0.12f));
                DrawPortraitPixel(origin, pixel, 9, 16, 2, 4, accent);
                DrawPortraitPixel(origin, pixel, 13, 16, 2, 4, accent);
                DrawPortraitPixel(origin, pixel, 8, 21, 8, 2, accent.Darkened(0.18f));
                break;

            default:
                // 轻装增加斜胸带，减少大片纯色衣服。
                DrawPortraitPixel(origin, pixel, 8, 16, 2, 3, leather);
                DrawPortraitPixel(origin, pixel, 10, 18, 2, 3, leather);
                DrawPortraitPixel(origin, pixel, 12, 20, 2, 3, leather);
                break;
        }
    }

    /// <summary>在胸像边缘绘制尺寸更克制的职业装备标记。</summary>
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
                DrawPortraitPixel(origin, pixel, 20, 8, 1, 13, outline);
                DrawPortraitPixel(origin, pixel, 19, 6, 3, 3, new Color("c3c7c5"));
                break;
            case CharacterWeaponSilhouette.Bow:
                DrawPortraitPixel(origin, pixel, 20, 9, 1, 4, accent.Darkened(0.22f));
                DrawPortraitPixel(origin, pixel, 21, 13, 1, 4, accent.Darkened(0.22f));
                DrawPortraitPixel(origin, pixel, 20, 17, 1, 4, accent.Darkened(0.22f));
                break;
            case CharacterWeaponSilhouette.Tome:
                DrawPortraitPixel(origin, pixel, 19, 16, 4, 4, outline);
                DrawPortraitPixel(origin, pixel, 20, 17, 2, 2, accent);
                break;
            default:
                DrawPortraitPixel(origin, pixel, 20, 9, 1, 10, outline);
                DrawPortraitPixel(origin, pixel, 20, 6, 1, 5, new Color("d0d4d1"));
                break;
        }
    }

    /// <summary>根据控件尺寸计算整数像素放大倍率。</summary>
    private float ResolvePixelSize()
    {
        float raw = Mathf.Min(Size.X / 24.0f, Size.Y / 24.0f);
        return Mathf.Max(2.0f, Mathf.Floor(raw));
    }

    /// <summary>把 24×24 逻辑头像网格居中到当前控件，并锁到整数屏幕像素。</summary>
    private Vector2 ResolvePortraitOrigin(float pixel)
    {
        Vector2 renderedSize = new(24.0f * pixel, 24.0f * pixel);
        Vector2 centered = (Size - renderedSize) * 0.5f;
        return new Vector2(Mathf.Round(centered.X), Mathf.Round(centered.Y));
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
