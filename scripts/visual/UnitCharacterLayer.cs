using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 在战棋地图上绘制 2D 人物小人。
/// 正式 map.png 存在时优先绘制纹理；没有素材时继续使用程序绘制人物，因此开发阶段不会被美术进度阻塞。
/// </summary>
public partial class UnitCharacterLayer : Node2D
{
    /// <summary>当前需要绘制的全部战斗单位。</summary>
    public IReadOnlyList<UnitModel> Units { get; set; } = Array.Empty<UnitModel>();

    /// <summary>当前选中的单位，用于显示选中状态和更明显的待机动画。</summary>
    public UnitModel? SelectedUnit { get; set; }

    /// <summary>战棋地图左上角像素坐标。</summary>
    public Vector2 BoardOrigin { get; set; }

    /// <summary>单格像素尺寸。</summary>
    public int CellSize { get; set; } = 52;

    /// <summary>人物待机动画累计时间。</summary>
    private double _elapsed;

    /// <summary>
    /// 每帧推进轻量待机动画并请求重绘。
    /// 这里只改变视觉偏移，不修改任何单位逻辑坐标。
    /// </summary>
    public override void _Process(double delta)
    {
        _elapsed += delta;
        QueueRedraw();
    }

    /// <summary>
    /// 绘制所有存活单位。
    /// </summary>
    public override void _Draw()
    {
        foreach (UnitModel unit in Units.Where(unit => unit.IsAlive))
        {
            DrawUnit(unit);
        }
    }

    /// <summary>
    /// 绘制单个地图人物。
    /// 正式纹理和程序占位模型共享同一动画状态，因此替换美术后选中/行动反馈仍然保留。
    /// </summary>
    private void DrawUnit(UnitModel unit)
    {
        Vector2 gridCenter = GridCenter(unit.GridPosition);
        Vector2 animationOffset = AnimationOffset(unit);
        Vector2 characterCenter = gridCenter + animationOffset;
        Color teamColor = unit.Team == UnitTeam.Player
            ? new Color(0.20f, 0.52f, 1.0f)
            : new Color(0.93f, 0.24f, 0.24f);

        if (unit.HasActed)
        {
            teamColor = teamColor.Darkened(0.35f);
        }

        // 阵营底圈固定在格子中心，人物本体上下浮动时不会造成位置判断上的视觉误解。
        DrawCircle(gridCenter + new Vector2(0, 11), 20.0f, new Color(0.04f, 0.05f, 0.07f, 0.92f));
        DrawCircle(gridCenter + new Vector2(0, 11), 20.5f, teamColor, false, 3.0f);

        Texture2D? mapTexture = CharacterAssetResolver.TryLoad(unit, CharacterArtSlot.Map);
        if (mapTexture is not null)
        {
            DrawMapTexture(unit, mapTexture, characterCenter);
        }
        else
        {
            DrawProceduralUnit(unit, characterCenter);
        }

        if (ReferenceEquals(unit, SelectedUnit))
        {
            // 选中单位增加金色外环，与主地图选择框形成双重反馈。
            DrawCircle(gridCenter + new Vector2(0, 11), 24.0f, new Color(1.0f, 0.84f, 0.25f), false, 2.5f);
        }
    }

    /// <summary>
    /// 绘制正式地图人物纹理。
    /// 统一把素材缩放到单格范围内，因此未来美术只需要保证透明背景和人物完整即可。
    /// </summary>
    private void DrawMapTexture(UnitModel unit, Texture2D texture, Vector2 center)
    {
        Rect2 target = new(center + new Vector2(-23, -29), new Vector2(46, 54));
        DrawTextureRect(texture, target, false);

        if (unit.HasActed)
        {
            // 已行动状态用半透明遮罩降低亮度，不需要额外准备一套灰色贴图。
            DrawRect(target, new Color(0.04f, 0.05f, 0.07f, 0.38f), true);
        }
    }

    /// <summary>
    /// 绘制无正式素材时使用的程序化小比例人物。
    /// </summary>
    private void DrawProceduralUnit(UnitModel unit, Vector2 center)
    {
        CharacterAppearanceDefinition appearance = CharacterAppearanceCatalog.Get(unit);

        if (appearance.HasCape)
        {
            Rect2 cape = new(center + new Vector2(-14, -1), new Vector2(28, 28));
            DrawRect(cape, appearance.AccentColor.Darkened(0.18f), true);
        }

        // 身体采用短矩形，头部和头发用圆形叠加，形成清晰的人物轮廓。
        Rect2 body = new(center + new Vector2(-11, 2), new Vector2(22, 25));
        DrawRect(body, appearance.OutfitColor, true);
        DrawRect(body, appearance.AccentColor.Darkened(0.45f), false, 2.0f);

        Vector2 headCenter = center + new Vector2(0, -8);
        DrawCircle(headCenter, 10.0f, appearance.SkinColor);
        DrawCircle(headCenter + new Vector2(0, -4), 10.5f, appearance.HairColor);
        DrawRect(new Rect2(headCenter + new Vector2(-9, 0), new Vector2(18, 8)), appearance.SkinColor, true);

        DrawWeaponSilhouette(center, appearance);

        if (unit.HasActed)
        {
            DrawRect(new Rect2(center + new Vector2(-16, -19), new Vector2(32, 48)), new Color(0.02f, 0.03f, 0.04f, 0.26f), true);
        }
    }

    /// <summary>
    /// 根据人物当前地图状态计算轻量动画偏移。
    /// 选中人物呼吸幅度更明显；已行动人物停止动画，用静止感强化回合状态。
    /// </summary>
    private Vector2 AnimationOffset(UnitModel unit)
    {
        if (unit.HasActed)
        {
            return Vector2.Zero;
        }

        float phase = StableVisualPhase(unit.Id);
        bool selected = ReferenceEquals(unit, SelectedUnit);
        float speed = selected ? 6.0f : 3.2f;
        float amplitude = selected ? 2.2f : 1.0f;
        float y = Mathf.Sin((float)_elapsed * speed + phase) * amplitude;
        return new Vector2(0, y);
    }

    /// <summary>
    /// 根据人物 ID 生成稳定的视觉相位，避免所有单位同时上下移动像整齐机械振动。
    /// </summary>
    private static float StableVisualPhase(string id)
    {
        int sum = 0;
        foreach (char character in id)
        {
            sum += character;
        }

        return (sum % 31) * 0.17f;
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
