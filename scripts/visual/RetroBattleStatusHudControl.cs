using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 横向战斗画面的左右状态框。
/// 视觉结构以当前确认的参考稿为标准：黑色身份区、粗白像素边框、阵营色数据区以及 HP/HIT/ATC/DEF 分段条。
/// 本控件只重放已经结算好的 HP，并读取单位当前属性用于表现，不参与任何真实战斗判定。
/// </summary>
public partial class RetroBattleStatusHudControl : Control
{
    /// <summary>单侧状态框宽度。</summary>
    private const float PanelWidth = 560.0f;

    /// <summary>左右状态框之间的固定间隔。</summary>
    private const float PanelGap = 60.0f;

    /// <summary>单侧状态框总高度。</summary>
    private const float PanelHeight = 286.0f;

    /// <summary>身份区高度。</summary>
    private const float IdentityHeight = 112.0f;

    /// <summary>外框像素厚度。</summary>
    private const float BorderThickness = 4.0f;

    /// <summary>数据条统一使用的分段数量。</summary>
    private const int MeterSegments = 24;

    /// <summary>左侧固定单位。</summary>
    private UnitModel? _leftUnit;

    /// <summary>右侧固定单位。</summary>
    private UnitModel? _rightUnit;

    /// <summary>左侧演出中的当前 HP。</summary>
    private int _leftHp;

    /// <summary>右侧演出中的当前 HP。</summary>
    private int _rightHp;

    /// <summary>左侧开战时最大 HP。</summary>
    private int _leftMaxHp = 1;

    /// <summary>右侧开战时最大 HP。</summary>
    private int _rightMaxHp = 1;

    /// <summary>本场实际获得经验的玩家单位。</summary>
    private UnitModel? _experienceUnit;

    /// <summary>玩家获得经验前等级。</summary>
    private int _experienceLevelBefore = 1;

    /// <summary>玩家获得经验前 EXP。</summary>
    private int _experienceBefore;

    /// <summary>左侧姓名。</summary>
    private Label? _leftNameLabel;

    /// <summary>左侧职业。</summary>
    private Label? _leftClassLabel;

    /// <summary>左侧等级。</summary>
    private Label? _leftLevelLabel;

    /// <summary>右侧姓名。</summary>
    private Label? _rightNameLabel;

    /// <summary>右侧职业。</summary>
    private Label? _rightClassLabel;

    /// <summary>右侧等级。</summary>
    private Label? _rightLevelLabel;

    /// <summary>创建文字层并强制使用最近邻纹理过滤。</summary>
    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        CreateIdentityLabels();
        QueueRedraw();
    }

    /// <summary>
    /// 开始一场战斗状态展示。
    /// HP 使用结算前快照，避免真实结算已经完成后直接跳到最终血量。
    /// </summary>
    public void BeginBattle(
        UnitModel leftUnit,
        int leftHp,
        int leftMaxHp,
        UnitModel rightUnit,
        int rightHp,
        int rightMaxHp,
        UnitModel? experienceUnit,
        int experienceLevelBefore,
        int experienceBefore)
    {
        _leftUnit = leftUnit;
        _rightUnit = rightUnit;
        _leftHp = Math.Clamp(leftHp, 0, Math.Max(1, leftMaxHp));
        _rightHp = Math.Clamp(rightHp, 0, Math.Max(1, rightMaxHp));
        _leftMaxHp = Math.Max(1, leftMaxHp);
        _rightMaxHp = Math.Max(1, rightMaxHp);
        _experienceUnit = experienceUnit;
        _experienceLevelBefore = Math.Max(1, experienceLevelBefore);
        _experienceBefore = Math.Clamp(experienceBefore, 0, 99);

        RefreshIdentityLabels();
        QueueRedraw();
    }

    /// <summary>
    /// 按逐击结果推进双方 HP。
    /// 法术 HP 成本先扣攻击者，命中后再扣防守者伤害，顺序和真实结算保持一致。
    /// </summary>
    public void ApplyStrike(CombatStrikeResult strike)
    {
        if (_leftUnit is null || _rightUnit is null)
        {
            return;
        }

        if (ReferenceEquals(strike.Attacker, _leftUnit))
        {
            _leftHp = Math.Max(0, _leftHp - strike.HpCostPaid);
        }
        else if (ReferenceEquals(strike.Attacker, _rightUnit))
        {
            _rightHp = Math.Max(0, _rightHp - strike.HpCostPaid);
        }

        if (strike.Hit)
        {
            if (ReferenceEquals(strike.Defender, _leftUnit))
            {
                _leftHp = Math.Max(0, _leftHp - strike.Damage);
            }
            else if (ReferenceEquals(strike.Defender, _rightUnit))
            {
                _rightHp = Math.Max(0, _rightHp - strike.Damage);
            }
        }

        QueueRedraw();
    }

    /// <summary>
    /// 返回本场真实获得的经验值。
    /// 参考稿战斗框不再额外塞入 EXP 小面板，EXP/升级结果由中央信息框展示。
    /// </summary>
    public int ShowExperienceResult()
    {
        if (_experienceUnit is null)
        {
            return 0;
        }

        int levelDifference = Math.Max(0, _experienceUnit.Level - _experienceLevelBefore);
        int gained = levelDifference * 100 + _experienceUnit.Experience - _experienceBefore;
        return Math.Max(0, gained);
    }

    /// <summary>清空当前战斗引用与显示。</summary>
    public void ResetBattle()
    {
        _leftUnit = null;
        _rightUnit = null;
        _experienceUnit = null;
        _leftHp = 0;
        _rightHp = 0;
        _leftMaxHp = 1;
        _rightMaxHp = 1;
        RefreshIdentityLabels();
        QueueRedraw();
    }

    /// <summary>绘制左右两个参考稿式状态框。</summary>
    public override void _Draw()
    {
        DrawSidePanel(new Vector2(0, 0), _leftUnit, _leftHp, _leftMaxHp, _rightUnit);
        DrawSidePanel(new Vector2(PanelWidth + PanelGap, 0), _rightUnit, _rightHp, _rightMaxHp, _leftUnit);
    }

    /// <summary>绘制单侧状态框、阵营色数据区和四条分段计量条。</summary>
    private void DrawSidePanel(
        Vector2 origin,
        UnitModel? unit,
        int hp,
        int maxHp,
        UnitModel? opponent)
    {
        UnitTeam team = unit?.Team ?? UnitTeam.Player;
        Color primary = TeamVisualPalette.Primary(team);
        Color highlight = TeamVisualPalette.Highlight(team);
        Color dataBackground = primary.Darkened(0.08f);
        Color emptySegment = primary.Darkened(0.46f);
        Color white = new("f3f3ef");
        Color black = new("050608");

        Rect2 fullPanel = new(origin, new Vector2(PanelWidth, PanelHeight));
        Rect2 identityPanel = new(origin, new Vector2(PanelWidth, IdentityHeight));
        Rect2 dataPanel = new(
            origin + new Vector2(0, IdentityHeight),
            new Vector2(PanelWidth, PanelHeight - IdentityHeight));

        // 参考稿使用纯黑身份区和高对比白色硬边框，不再使用青铜/灰蓝框架。
        DrawRect(fullPanel, black, true);
        DrawRect(dataPanel, dataBackground, true);
        DrawRect(fullPanel, white, false, BorderThickness);
        DrawLine(
            origin + new Vector2(0, IdentityHeight),
            origin + new Vector2(PanelWidth, IdentityHeight),
            white,
            BorderThickness,
            false);

        // 数据区顶部与底部各加一条更深阵营色，形成参考稿那种双层像素框感。
        DrawRect(
            new Rect2(dataPanel.Position + new Vector2(7, 7), new Vector2(dataPanel.Size.X - 14, 3)),
            highlight,
            true);
        DrawRect(
            new Rect2(dataPanel.Position + new Vector2(7, dataPanel.Size.Y - 10), new Vector2(dataPanel.Size.X - 14, 3)),
            primary.Darkened(0.36f),
            true);

        int hit = unit is null || opponent is null
            ? 0
            : CombatRules.CalculateHitRate(unit, opponent, 0);
        int attack = unit is null
            ? 0
            : (unit.EquippedWeapon.DamageType == DamageType.Magical ? unit.Magic : unit.Strength) + unit.EquippedWeapon.Might;
        int defense = unit?.Defense ?? 0;

        DrawMetricRow(origin, "HP", 128, hp, Math.Max(1, maxHp), white, emptySegment, true);
        DrawMetricRow(origin, "HIT", 169, hit, 100, white, emptySegment, false);
        DrawMetricRow(origin, "ATC", 210, attack, 40, white, emptySegment, false);
        DrawMetricRow(origin, "DEF", 251, defense, 30, white, emptySegment, false);
    }

    /// <summary>绘制一行标签与固定 24 格像素计量条。</summary>
    private void DrawMetricRow(
        Vector2 origin,
        string label,
        float y,
        int value,
        int maxValue,
        Color filled,
        Color empty,
        bool hpRow)
    {
        // 文字由 DrawString 绘制会依赖系统字体抗锯齿，因此标签仍交给独立 Label 风格；这里仅绘制条本体。
        // 行首留出 90px 给 HP/HIT/ATC/DEF 标签，条本体全部锁到整数坐标。
        Rect2 meterRect = new(
            origin + new Vector2(108, y),
            new Vector2(414, hpRow ? 20 : 18));
        DrawSegmentMeter(meterRect, value, maxValue, filled, empty);
    }

    /// <summary>绘制固定数量的硬边分段条，任何缩放下都保持独立方块而不是平滑渐变。</summary>
    private void DrawSegmentMeter(Rect2 rect, int value, int maxValue, Color filled, Color empty)
    {
        float safeRatio = Mathf.Clamp((float)value / Math.Max(1, maxValue), 0.0f, 1.0f);
        int filledSegments = (int)MathF.Round(safeRatio * MeterSegments);
        float segmentGap = 2.0f;
        float segmentWidth = MathF.Floor((rect.Size.X - segmentGap * (MeterSegments - 1)) / MeterSegments);
        float usedWidth = segmentWidth * MeterSegments + segmentGap * (MeterSegments - 1);
        float startX = rect.Position.X + MathF.Floor((rect.Size.X - usedWidth) * 0.5f);

        for (int index = 0; index < MeterSegments; index++)
        {
            Rect2 segment = new(
                new Vector2(startX + index * (segmentWidth + segmentGap), rect.Position.Y),
                new Vector2(segmentWidth, rect.Size.Y));
            Color color = index < filledSegments ? filled : empty;
            DrawRect(segment, color, true);
            DrawRect(segment, new Color("101116"), false, 1.0f);
        }
    }

    /// <summary>创建身份区和数据区所需的全部文字标签。</summary>
    private void CreateIdentityLabels()
    {
        _leftNameLabel = CreatePixelLabel(new Vector2(24, 16), new Vector2(350, 42), HorizontalAlignment.Left, 27);
        _leftClassLabel = CreatePixelLabel(new Vector2(24, 57), new Vector2(320, 36), HorizontalAlignment.Left, 22);
        _leftLevelLabel = CreatePixelLabel(new Vector2(398, 22), new Vector2(132, 36), HorizontalAlignment.Right, 23);

        float rightX = PanelWidth + PanelGap;
        _rightNameLabel = CreatePixelLabel(new Vector2(rightX + 24, 16), new Vector2(350, 42), HorizontalAlignment.Left, 27);
        _rightClassLabel = CreatePixelLabel(new Vector2(rightX + 24, 57), new Vector2(320, 36), HorizontalAlignment.Left, 22);
        _rightLevelLabel = CreatePixelLabel(new Vector2(rightX + 398, 22), new Vector2(132, 36), HorizontalAlignment.Right, 23);

        foreach (Label label in new[]
                 {
                     _leftNameLabel,
                     _leftClassLabel,
                     _leftLevelLabel,
                     _rightNameLabel,
                     _rightClassLabel,
                     _rightLevelLabel
                 })
        {
            AddChild(label);
        }

        // 数据区标签单独创建，位置与分段条严格对应。
        AddChild(CreateMetricLabel(new Vector2(24, 122), "HP"));
        AddChild(CreateMetricLabel(new Vector2(24, 163), "HIT"));
        AddChild(CreateMetricLabel(new Vector2(24, 204), "ATC"));
        AddChild(CreateMetricLabel(new Vector2(24, 245), "DEF"));
        AddChild(CreateMetricLabel(new Vector2(rightX + 24, 122), "HP"));
        AddChild(CreateMetricLabel(new Vector2(rightX + 24, 163), "HIT"));
        AddChild(CreateMetricLabel(new Vector2(rightX + 24, 204), "ATC"));
        AddChild(CreateMetricLabel(new Vector2(rightX + 24, 245), "DEF"));
    }

    /// <summary>创建身份文字，使用粗白字和黑色像素阴影提高黑底可读性。</summary>
    private static Label CreatePixelLabel(
        Vector2 position,
        Vector2 size,
        HorizontalAlignment alignment,
        int fontSize)
    {
        Label label = new()
        {
            Position = position,
            Size = size,
            HorizontalAlignment = alignment,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        label.AddThemeColorOverride("font_color", new Color("f3f3ef"));
        label.AddThemeColorOverride("font_shadow_color", Colors.Black);
        label.AddThemeConstantOverride("shadow_offset_x", 2);
        label.AddThemeConstantOverride("shadow_offset_y", 2);
        label.AddThemeFontSizeOverride("font_size", fontSize);
        return label;
    }

    /// <summary>创建 HP/HIT/ATC/DEF 行首标签。</summary>
    private static Label CreateMetricLabel(Vector2 position, string text)
    {
        Label label = CreatePixelLabel(position, new Vector2(78, 28), HorizontalAlignment.Left, 22);
        label.Text = text;
        return label;
    }

    /// <summary>把当前双方中文姓名、职业和等级写入身份区。</summary>
    private void RefreshIdentityLabels()
    {
        if (_leftNameLabel is not null)
        {
            _leftNameLabel.Text = _leftUnit?.DisplayName ?? string.Empty;
        }

        if (_leftClassLabel is not null)
        {
            _leftClassLabel.Text = _leftUnit?.ClassDefinition.DisplayName ?? string.Empty;
        }

        if (_leftLevelLabel is not null)
        {
            _leftLevelLabel.Text = _leftUnit is null ? string.Empty : $"LV{_leftUnit.Level}";
        }

        if (_rightNameLabel is not null)
        {
            _rightNameLabel.Text = _rightUnit?.DisplayName ?? string.Empty;
        }

        if (_rightClassLabel is not null)
        {
            _rightClassLabel.Text = _rightUnit?.ClassDefinition.DisplayName ?? string.Empty;
        }

        if (_rightLevelLabel is not null)
        {
            _rightLevelLabel.Text = _rightUnit is null ? string.Empty : $"LV{_rightUnit.Level}";
        }
    }
}