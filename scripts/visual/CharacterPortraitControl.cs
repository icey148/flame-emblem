using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// HUD 中使用的人物头像/半身像控件。
/// 正式 portrait.png 存在时优先显示正式立绘；缺少素材时自动退回程序绘制占位头像。
/// </summary>
public partial class CharacterPortraitControl : Control
{
    /// <summary>当前需要展示的单位；为空时显示中性占位轮廓。</summary>
    private UnitModel? _unit;

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
        DrawRect(bounds, new Color(0.30f, 0.32f, 0.38f), false, 2.0f);

        if (_unit is null)
        {
            DrawEmptyPortrait();
            return;
        }

        Texture2D? portraitTexture = CharacterAssetResolver.TryLoad(_unit, CharacterArtSlot.Portrait);
        if (portraitTexture is not null)
        {
            // 正式立绘使用控件完整区域，素材自身应带透明背景或已经完成构图裁切。
            DrawTextureRect(portraitTexture, bounds, false);
            return;
        }

        DrawProceduralPortrait(_unit);
    }

    /// <summary>
    /// 没有选中人物时绘制中性占位轮廓。
    /// </summary>
    private void DrawEmptyPortrait()
    {
        DrawCircle(new Vector2(Size.X * 0.5f, Size.Y * 0.38f), 16.0f, new Color(0.28f, 0.29f, 0.32f));
        DrawRect(
            new Rect2(new Vector2(Size.X * 0.28f, Size.Y * 0.58f), new Vector2(Size.X * 0.44f, Size.Y * 0.34f)),
            new Color(0.24f, 0.25f, 0.28f),
            true);
    }

    /// <summary>
    /// 在正式头像素材缺失时绘制程序化半身像。
    /// </summary>
    private void DrawProceduralPortrait(UnitModel unit)
    {
        CharacterAppearanceDefinition appearance = CharacterAppearanceCatalog.Get(unit);
        Vector2 headCenter = new(Size.X * 0.5f, Size.Y * 0.33f);

        if (appearance.HasCape)
        {
            DrawRect(
                new Rect2(new Vector2(Size.X * 0.16f, Size.Y * 0.48f), new Vector2(Size.X * 0.68f, Size.Y * 0.47f)),
                appearance.AccentColor.Darkened(0.22f),
                true);
        }

        // 胸像先画服装，再叠加颈部、面部和头发，使其和地图小人的配色保持一致。
        DrawRect(
            new Rect2(new Vector2(Size.X * 0.23f, Size.Y * 0.55f), new Vector2(Size.X * 0.54f, Size.Y * 0.42f)),
            appearance.OutfitColor,
            true);
        DrawRect(
            new Rect2(new Vector2(Size.X * 0.43f, Size.Y * 0.45f), new Vector2(Size.X * 0.14f, Size.Y * 0.17f)),
            appearance.SkinColor,
            true);
        DrawCircle(headCenter, Size.X * 0.19f, appearance.SkinColor);
        DrawCircle(headCenter + new Vector2(0, -Size.Y * 0.055f), Size.X * 0.205f, appearance.HairColor);
        DrawRect(
            new Rect2(new Vector2(headCenter.X - Size.X * 0.17f, headCenter.Y), new Vector2(Size.X * 0.34f, Size.Y * 0.15f)),
            appearance.SkinColor,
            true);

        // 极简五官和服装饰边用于保持不同人物的可读性，不代表最终美术风格。
        DrawCircle(headCenter + new Vector2(-Size.X * 0.065f, Size.Y * 0.035f), 2.2f, new Color(0.12f, 0.10f, 0.10f));
        DrawCircle(headCenter + new Vector2(Size.X * 0.065f, Size.Y * 0.035f), 2.2f, new Color(0.12f, 0.10f, 0.10f));
        DrawRect(
            new Rect2(new Vector2(Size.X * 0.23f, Size.Y * 0.70f), new Vector2(Size.X * 0.54f, 5.0f)),
            appearance.AccentColor,
            true);
    }
}
