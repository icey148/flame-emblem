using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 给 CharacterPortraitControl 叠加最终定稿使用的金色外框和阵营色内线。
/// 头像本体由 ApprovedCharacterArtCatalog 提供；本控件只做轻量阵营识别，不再使用旧版 4px 粉/蓝粗边。
/// </summary>
public partial class TeamPortraitFrameControl : Control
{
    /// <summary>被装饰的头像控件。</summary>
    private CharacterPortraitControl? _portrait;

    /// <summary>最终界面使用的主金色。</summary>
    private static readonly Color Gold = new("b8833f");

    /// <summary>绑定头像并开始逐帧刷新边框。</summary>
    public void Bind(CharacterPortraitControl portrait)
    {
        _portrait = portrait;
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        SetProcess(true);
        QueueRedraw();
    }

    /// <summary>头像角色会随对话切换，因此边框每帧刷新一次。</summary>
    public override void _Process(double delta)
    {
        QueueRedraw();
    }

    /// <summary>绘制 3px 金色外框、1px 暗金内框和 2px 阵营色底线。</summary>
    public override void _Draw()
    {
        Rect2 bounds = new(Vector2.Zero, Size);
        DrawRect(bounds, Gold, false, 3.0f);
        DrawRect(bounds.Grow(-5), Gold.Darkened(0.50f), false, 1.0f);

        UnitModel? unit = _portrait?.DisplayedUnit;
        if (unit is null)
        {
            return;
        }

        Color accent = TeamVisualPalette.Highlight(unit.Team).Darkened(0.12f);
        DrawRect(
            new Rect2(
                new Vector2(8, bounds.Size.Y - 7),
                new Vector2(Mathf.Max(0.0f, bounds.Size.X - 16.0f), 2)),
            accent,
            true);
    }
}
