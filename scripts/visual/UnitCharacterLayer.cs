using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 在战棋地图上绘制程序化 2D 人物小人。
/// 当前实现是无外部素材的原创占位模型；正式美术接入后可替换为 Sprite2D/AnimatedSprite2D，而不影响单位和战斗规则。
/// </summary>
public partial class UnitCharacterLayer : Node2D
{
    /// <summary>当前需要绘制的全部战斗单位。</summary>
    public IReadOnlyList<UnitModel> Units { get; set; } = Array.Empty<UnitModel>();

    /// <summary>当前选中的单位，用于额外绘制选中光环。</summary>
    public UnitModel? SelectedUnit { get; set; }

    /// <summary>战棋地图左上角像素坐标。</summary>
    public Vector2 BoardOrigin { get; set; }

    /// <summary>单格像素尺寸。</summary>
    public int CellSize { get; set; } = 52;

    /// <summary>
    /// 绘制所有存活单位的程序化战棋小人。
    /// </summary>
    public override void _Draw()
    {
        foreach (UnitModel unit in Units.Where(unit => unit.IsAlive))
        {
            DrawUnit(unit);
        }
    }

    /// <summary>
    /// 绘制一个小比例人物：阵营底圈、披风、身体、头部、头发和武器轮廓。
    /// 这种结构比原先单色圆点更接近最终 2D 小人的层级，也方便未来逐层替换美术。
    /// </summary>
    private void DrawUnit(UnitModel unit)
    {
        CharacterAppearanceDefinition appearance = CharacterAppearanceCatalog.Get(unit);
        Vector2 center = GridCenter(unit.GridPosition);
        Color teamColor = unit.Team == UnitTeam.Player
            ? new Color(0.20f, 0.52f, 1.0f)
            : new Color(0.93f, 0.24f, 0.24f);

        if (unit.HasActed)
        {
            // 已行动单位整体降低亮度，但仍保留个人配色，避免重新退化成“只有蓝红两个圆点”。
            teamColor = teamColor.Darkened(0.35f);
        }

        // 先盖住旧原型圆点的大部分区域，再在最底层保留阵营识别环。
        DrawCircle(center + new Vector2(0, 11), 20.0f, new Color(0.04f, 0.05f, 0.07f, 0.92f));
        DrawCircle(center + new Vector2(0, 11), 20.5f, teamColor, false, 3.0f);

        if (appearance.HasCape)
        {
            Rect2 cape = new(center + new Vector2(-14, -1), new Vector2(28, 28));
            DrawRect(cape, appearance.AccentColor.Darkened(0.18f), true);
        }

        // 身体采用短矩形，头部和头发用圆形叠加，形成清晰的“人物”轮廓。
        Rect2 body = new(center + new Vector2(-11, 2), new Vector2(22, 25));
        DrawRect(body, appearance.OutfitColor, true);
        DrawRect(body, appearance.AccentColor.Darkened(0.45f), false, 2.0f);

        Vector2 headCenter = center + new Vector2(0, -8);
        DrawCircle(headCenter, 10.0f, appearance.SkinColor);
        DrawCircle(headCenter + new Vector2(0, -4), 10.5f, appearance.HairColor);
        DrawRect(new Rect2(headCenter + new Vector2(-9, 0), new Vector2(18, 8)), appearance.SkinColor, true);

        DrawWeaponSilhouette(center, appearance);

        if (ReferenceEquals(unit, SelectedUnit))
        {
            // 选中单位增加金色外环，与原地图选择框形成双重反馈。
            DrawCircle(center + new Vector2(0, 11), 24.0f, new Color(1.0f, 0.84f, 0.25f), false, 2.5f);
        }
    }

    /// <summary>
    /// 根据职业/武器类型绘制不同的简化武器轮廓。
    /// </summary>
    private void DrawWeaponSilhouette(Vector2 center, CharacterAppearanceDefinition appearance)
    {
        Color weaponColor = new(0.88f, 0.88f, 0.90f);
        Color darkWeaponColor = new(0.28f, 0.24f, 0.20f);

        switch (appearance.WeaponSilhouette)
        {
            case CharacterWeaponSilhouette.Spear:
                DrawLine(center + new Vector2(12, 20), center + new Vector2(24, -17), darkWeaponColor, 4.0f);
                DrawLine(center + new Vector2(23, -16), center + new Vector2(27, -23), weaponColor, 3.0f);
                break;
            case CharacterWeaponSilhouette.Bow:
                DrawLine(center + new Vector2(15, -11), center + new Vector2(21, 21), darkWeaponColor, 3.0f);
                DrawLine(center + new Vector2(15, -11), center + new Vector2(26, 5), appearance.AccentColor, 2.0f);
                DrawLine(center + new Vector2(26, 5), center + new Vector2(21, 21), appearance.AccentColor, 2.0f);
                break;
            case CharacterWeaponSilhouette.Tome:
                DrawRect(new Rect2(center + new Vector2(13, 6), new Vector2(13, 17)), appearance.AccentColor, true);
                DrawCircle(center + new Vector2(19.5f, 14.5f), 3.0f, new Color(0.92f, 0.72f, 1.0f));
                break;
            default:
                DrawLine(center + new Vector2(13, 19), center + new Vector2(23, -14), darkWeaponColor, 5.0f);
                DrawLine(center + new Vector2(22, -13), center + new Vector2(26, -21), weaponColor, 4.0f);
                break;
        }
    }

    /// <summary>
    /// 把逻辑格坐标转换为当前地图格子的像素中心。
    /// </summary>
    private Vector2 GridCenter(Vector2I cell)
    {
        return BoardOrigin + new Vector2(
            cell.X * CellSize + CellSize / 2.0f,
            cell.Y * CellSize + CellSize / 2.0f);
    }
}
