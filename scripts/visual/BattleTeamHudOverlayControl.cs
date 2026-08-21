using FlameEmblem.Game;
using Godot;
using System.Reflection;

namespace FlameEmblem.Visual;

/// <summary>
/// 在现有横向战斗状态 HUD 上叠加敌我阵营识别条。
/// HP 仍然使用绿/黄/红表达生命危险程度；深蓝/粉红只用于面板边框和标题带，避免语义混淆。
/// </summary>
public partial class BattleTeamHudOverlayControl : Control
{
    /// <summary>被装饰的原战斗状态 HUD。</summary>
    private RetroBattleStatusHudControl? _source;

    /// <summary>读取左侧固定单位的私有字段。</summary>
    private FieldInfo? _leftUnitField;

    /// <summary>读取右侧固定单位的私有字段。</summary>
    private FieldInfo? _rightUnitField;

    /// <summary>绑定原状态 HUD；本层只绘制阵营边框，不修改 HP/EXP 数值。</summary>
    public void Bind(RetroBattleStatusHudControl source)
    {
        _source = source;
        BindingFlags members = BindingFlags.Instance | BindingFlags.NonPublic;
        Type type = typeof(RetroBattleStatusHudControl);
        _leftUnitField = type.GetField("_leftUnit", members);
        _rightUnitField = type.GetField("_rightUnit", members);

        Position = Vector2.Zero;
        Size = source.Size;
        MouseFilter = MouseFilterEnum.Ignore;
        SetProcess(true);
        QueueRedraw();
    }

    /// <summary>战斗双方会在每场交换开始时变化，因此逐帧同步尺寸并刷新阵营色。</summary>
    public override void _Process(double delta)
    {
        if (_source is not null && Size != _source.Size)
        {
            Size = _source.Size;
        }

        QueueRedraw();
    }

    /// <summary>给左右状态框分别绘制深蓝/粉红上沿、内高光与硬边框。</summary>
    public override void _Draw()
    {
        UnitModel? leftUnit = ReadUnit(_leftUnitField);
        UnitModel? rightUnit = ReadUnit(_rightUnitField);

        if (leftUnit is not null)
        {
            DrawTeamPanelAccent(
                new Rect2(new Vector2(0, 0), new Vector2(400, 116)),
                leftUnit.Team);
        }

        if (rightUnit is not null)
        {
            DrawTeamPanelAccent(
                new Rect2(new Vector2(630, 0), new Vector2(400, 116)),
                rightUnit.Team);
        }
    }

    /// <summary>绘制单侧状态框阵营识别，不遮挡原 HUD 的文字与 HP 条。</summary>
    private void DrawTeamPanelAccent(Rect2 panel, UnitTeam team)
    {
        Color primary = TeamVisualPalette.Primary(team);
        Color highlight = TeamVisualPalette.Highlight(team);

        // 5px 顶部主色条即使在快速战斗中也能立即建立阵营归属。
        DrawRect(
            new Rect2(panel.Position + new Vector2(3, 3), new Vector2(panel.Size.X - 6, 5)),
            primary,
            true);

        // 主色边框保持 2px，不覆盖原有青铜框的中心区域。
        DrawRect(panel.Grow(-2), new Color(primary.R, primary.G, primary.B, 0.96f), false, 2.0f);

        // 上沿高光只占 1px，增强像素层次但不做现代渐变。
        DrawRect(
            new Rect2(panel.Position + new Vector2(8, 9), new Vector2(panel.Size.X - 16, 1)),
            new Color(highlight.R, highlight.G, highlight.B, 0.88f),
            true);
    }

    /// <summary>从原状态 HUD 读取固定单位。</summary>
    private UnitModel? ReadUnit(FieldInfo? field)
    {
        return _source is null || field is null
            ? null
            : field.GetValue(_source) as UnitModel;
    }
}
