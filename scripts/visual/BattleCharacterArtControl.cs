using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 独立战斗演出使用的大尺寸人物图控件。
/// 加载优先级为 battle.png → portrait.png → 程序绘制，因此正式战斗立绘可以晚于头像素材逐步接入。
/// </summary>
public partial class BattleCharacterArtControl : Control
{
    /// <summary>当前需要展示的人物。</summary>
    private UnitModel? _unit;

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

        DrawProceduralBattleFigure(_unit);
    }

    /// <summary>
    /// 正式战斗立绘缺失时绘制更大的程序化半身人物。
    /// </summary>
    private void DrawProceduralBattleFigure(UnitModel unit)
    {
        CharacterAppearanceDefinition appearance = CharacterAppearanceCatalog.Get(unit);
        Vector2 center = new(Size.X * 0.5f, Size.Y * 0.43f);

        if (appearance.HasCape)
        {
            DrawRect(
                new Rect2(new Vector2(Size.X * 0.12f, Size.Y * 0.40f), new Vector2(Size.X * 0.76f, Size.Y * 0.55f)),
                appearance.AccentColor.Darkened(0.22f),
                true);
        }

        DrawRect(
            new Rect2(new Vector2(Size.X * 0.20f, Size.Y * 0.48f), new Vector2(Size.X * 0.60f, Size.Y * 0.47f)),
            appearance.OutfitColor,
            true);

        DrawCircle(center, Size.X * 0.19f, appearance.SkinColor);
        DrawCircle(center + new Vector2(0, -Size.Y * 0.06f), Size.X * 0.205f, appearance.HairColor);
        DrawRect(
            new Rect2(new Vector2(center.X - Size.X * 0.17f, center.Y), new Vector2(Size.X * 0.34f, Size.Y * 0.15f)),
            appearance.SkinColor,
            true);

        // 简单绘制武器/法术强调线，让战斗位在没有正式素材时仍能区分职业倾向。
        DrawRect(
            new Rect2(new Vector2(Size.X * 0.20f, Size.Y * 0.76f), new Vector2(Size.X * 0.60f, 7.0f)),
            appearance.AccentColor,
            true);
    }
}
