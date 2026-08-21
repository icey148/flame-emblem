using FlameEmblem.Game;
using Godot;
using System.Reflection;

namespace FlameEmblem.Visual;

/// <summary>
/// 给 CharacterPortraitControl 叠加阵营色硬边框。
/// 不修改头像本身的正式素材/程序回退逻辑，只读取当前 UnitModel 并用深蓝或粉红标记阵营。
/// </summary>
public partial class TeamPortraitFrameControl : Control
{
    /// <summary>被装饰的头像控件。</summary>
    private CharacterPortraitControl? _portrait;

    /// <summary>读取头像当前单位的私有字段。</summary>
    private FieldInfo? _unitField;

    /// <summary>绑定头像并开始逐帧刷新边框。</summary>
    public void Bind(CharacterPortraitControl portrait)
    {
        _portrait = portrait;
        _unitField = typeof(CharacterPortraitControl).GetField(
            "_unit",
            BindingFlags.Instance | BindingFlags.NonPublic);
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        SetProcess(true);
        QueueRedraw();
    }

    /// <summary>头像角色会随对话切换，因此边框每帧同步一次。</summary>
    public override void _Process(double delta)
    {
        QueueRedraw();
    }

    /// <summary>绘制 4px 主色边框与 1px 高亮内线。</summary>
    public override void _Draw()
    {
        UnitModel? unit = ReadUnit();
        if (unit is null)
        {
            return;
        }

        Color primary = TeamVisualPalette.Primary(unit.Team);
        Color highlight = TeamVisualPalette.Highlight(unit.Team);
        Rect2 bounds = new(Vector2.Zero, Size);

        // 四边分别绘制，保持硬边像素风并避免圆角。
        DrawRect(new Rect2(0, 0, bounds.Size.X, 4), primary, true);
        DrawRect(new Rect2(0, bounds.Size.Y - 4, bounds.Size.X, 4), primary, true);
        DrawRect(new Rect2(0, 0, 4, bounds.Size.Y), primary, true);
        DrawRect(new Rect2(bounds.Size.X - 4, 0, 4, bounds.Size.Y), primary, true);

        DrawRect(new Rect2(4, 4, bounds.Size.X - 8, 1), highlight, true);
        DrawRect(new Rect2(4, bounds.Size.Y - 5, bounds.Size.X - 8, 1), highlight.Darkened(0.12f), true);
    }

    /// <summary>读取当前头像绑定的单位。</summary>
    private UnitModel? ReadUnit()
    {
        return _portrait is null ? null : _unitField?.GetValue(_portrait) as UnitModel;
    }
}
