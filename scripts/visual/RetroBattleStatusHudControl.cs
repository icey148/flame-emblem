using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 最终战斗界面的左右人物状态面板。
/// 结构以用户确认的定稿图为准：外侧正式头像、内侧姓名/职业/武器/LV、暗红/深蓝底色、
/// 金色细边以及 HP/HIT/ATC/DEF 分段条。本控件只表现已结算数据，不参与战斗判定。
/// </summary>
public partial class RetroBattleStatusHudControl : Control
{
    /// <summary>头像区域宽度。</summary>
    private const float PortraitWidth = 180.0f;

    /// <summary>信息区与头像之间的金色分隔线宽度。</summary>
    private const float SeparatorWidth = 2.0f;

    /// <summary>分段条使用的格数。</summary>
    private const int MeterSegments = 18;

    /// <summary>面板金色主边框。</summary>
    private static readonly Color Gold = new("c9a66a");

    /// <summary>面板金色高光。</summary>
    private static readonly Color GoldLight = new("e3c98d");

    /// <summary>正文使用的浅色。</summary>
    private static readonly Color TextColor = new("f3efe7");

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

    /// <summary>左侧所有文字控件。</summary>
    private SideLabels? _leftLabels;

    /// <summary>右侧所有文字控件。</summary>
    private SideLabels? _rightLabels;

    /// <summary>创建左右文字层并使用正式设计稿需要的平滑头像过滤。</summary>
    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = CanvasItem.TextureFilterEnum.Linear;
        _leftLabels = CreateSideLabels(0.0f, true);
        _rightLabels = CreateSideLabels(ReferenceBattleLayout.PanelWidth + ReferenceBattleLayout.PanelGap, false);
        RefreshAllLabels();
        QueueRedraw();
    }

    /// <summary>开始一场战斗状态展示，并保存结算前 HP 快照。</summary>
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
        RefreshAllLabels();
        QueueRedraw();
    }

    /// <summary>按逐击结果推进演出中的 HP。</summary>
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

        RefreshAllLabels();
        QueueRedraw();
    }

    /// <summary>返回本场真实获得的经验值，实际文字仍由中央信息框展示。</summary>
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
        RefreshAllLabels();
        QueueRedraw();
    }

    /// <summary>绘制左右两块正式信息面板。</summary>
    public override void _Draw()
    {
        DrawSidePanel(Vector2.Zero, _leftUnit, _leftHp, _leftMaxHp, _rightUnit, true);
        DrawSidePanel(
            new Vector2(ReferenceBattleLayout.PanelWidth + ReferenceBattleLayout.PanelGap, 0),
            _rightUnit,
            _rightHp,
            _rightMaxHp,
            _leftUnit,
            false);
    }

    /// <summary>绘制一侧头像、底色、金边、分隔线和四条数值条。</summary>
    private void DrawSidePanel(
        Vector2 origin,
        UnitModel? unit,
        int hp,
        int maxHp,
        UnitModel? opponent,
        bool portraitOnLeft)
    {
        UnitTeam team = unit?.Team ?? UnitTeam.Player;
        Color teamPrimary = TeamVisualPalette.Primary(team);
        Color teamHighlight = TeamVisualPalette.Highlight(team);
        Color panelDark = teamPrimary.Darkened(0.62f);
        Color panelMid = teamPrimary.Darkened(0.38f);
        Color emptySegment = teamPrimary.Darkened(0.70f);

        Rect2 panel = new(origin, new Vector2(ReferenceBattleLayout.PanelWidth, ReferenceBattleLayout.PanelHeight));
        DrawRect(panel, new Color("05070b"), true);
        DrawRect(new Rect2(origin + new Vector2(4, 4), new Vector2(panel.Size.X - 8, panel.Size.Y - 8)), panelDark, true);

        float portraitX = portraitOnLeft ? origin.X + 4 : origin.X + ReferenceBattleLayout.PanelWidth - PortraitWidth - 4;
        float infoX = portraitOnLeft ? origin.X + PortraitWidth : origin.X + 4;
        float infoWidth = ReferenceBattleLayout.PanelWidth - PortraitWidth - 8;

        // 信息区用上下两档阵营色，模拟定稿图的暗色层次，而不是上一版的大块纯色。
        DrawRect(
            new Rect2(new Vector2(infoX, origin.Y + 4), new Vector2(infoWidth, 106)),
            panelDark.Darkened(0.08f),
            true);
        DrawRect(
            new Rect2(new Vector2(infoX, origin.Y + 110), new Vector2(infoWidth, ReferenceBattleLayout.PanelHeight - 114)),
            panelMid,
            true);
        DrawRect(
            new Rect2(new Vector2(infoX + 8, origin.Y + 111), new Vector2(infoWidth - 16, 2)),
            teamHighlight,
            true);

        DrawPortrait(unit, new Rect2(new Vector2(portraitX, origin.Y + 5), new Vector2(PortraitWidth - 8, ReferenceBattleLayout.PanelHeight - 10)));

        float separatorX = portraitOnLeft ? origin.X + PortraitWidth : origin.X + ReferenceBattleLayout.PanelWidth - PortraitWidth;
        DrawRect(new Rect2(new Vector2(separatorX, origin.Y + 3), new Vector2(SeparatorWidth, panel.Size.Y - 6)), Gold, true);

        DrawRect(panel, Gold, false, 3.0f);
        DrawRect(panel.Grow(-5), GoldLight.Darkened(0.36f), false, 1.0f);

        int hit = unit is null || opponent is null ? 0 : CombatRules.CalculateHitRate(unit, opponent, 0);
        int attack = unit is null
            ? 0
            : (unit.EquippedWeapon.DamageType == DamageType.Magical ? unit.Magic : unit.Strength) + unit.EquippedWeapon.Might;
        int defense = unit?.Defense ?? 0;

        DrawMetricBar(origin, portraitOnLeft, 132, hp, Math.Max(1, maxHp), TextColor, emptySegment);
        DrawMetricBar(origin, portraitOnLeft, 166, hit, 100, TextColor, emptySegment);
        DrawMetricBar(origin, portraitOnLeft, 200, attack, 40, TextColor, emptySegment);
        DrawMetricBar(origin, portraitOnLeft, 234, defense, 30, TextColor, emptySegment);
    }

    /// <summary>绘制正式头像；没有正式头像时保留干净深色空框。</summary>
    private void DrawPortrait(UnitModel? unit, Rect2 bounds)
    {
        DrawRect(bounds, new Color("07090d"), true);
        if (unit is null)
        {
            return;
        }

        Texture2D? portrait = ApprovedCharacterArtCatalog.TryLoadPortrait(unit);
        if (portrait is null)
        {
            return;
        }

        float sourceWidth = Math.Max(1, portrait.GetWidth());
        float sourceHeight = Math.Max(1, portrait.GetHeight());
        float scale = MathF.Min(bounds.Size.X / sourceWidth, bounds.Size.Y / sourceHeight);
        Vector2 targetSize = new(sourceWidth * scale, sourceHeight * scale);
        Vector2 targetPosition = bounds.Position + (bounds.Size - targetSize) * 0.5f;
        DrawTextureRect(
            portrait,
            new Rect2(
                new Vector2(Mathf.Round(targetPosition.X), Mathf.Round(targetPosition.Y)),
                new Vector2(Mathf.Round(targetSize.X), Mathf.Round(targetSize.Y))),
            false);
    }

    /// <summary>绘制一行 18 格硬边数值条。</summary>
    private void DrawMetricBar(
        Vector2 panelOrigin,
        bool portraitOnLeft,
        float y,
        int value,
        int maxValue,
        Color filled,
        Color empty)
    {
        float infoX = portraitOnLeft
            ? panelOrigin.X + PortraitWidth
            : panelOrigin.X + 4;
        Rect2 meter = new(
            new Vector2(infoX + 74, panelOrigin.Y + y),
            new Vector2(258, 18));
        DrawSegmentMeter(meter, value, maxValue, filled, empty);
    }

    /// <summary>按固定格数绘制数值条，避免使用平滑渐变。</summary>
    private void DrawSegmentMeter(Rect2 rect, int value, int maxValue, Color filled, Color empty)
    {
        float ratio = Mathf.Clamp((float)value / Math.Max(1, maxValue), 0.0f, 1.0f);
        int filledSegments = (int)MathF.Round(ratio * MeterSegments);
        float gap = 2.0f;
        float width = MathF.Floor((rect.Size.X - gap * (MeterSegments - 1)) / MeterSegments);

        for (int index = 0; index < MeterSegments; index++)
        {
            Rect2 segment = new(
                new Vector2(rect.Position.X + index * (width + gap), rect.Position.Y),
                new Vector2(width, rect.Size.Y));
            DrawRect(segment, index < filledSegments ? filled : empty, true);
            DrawRect(segment, new Color("0b0d12"), false, 1.0f);
        }
    }

    /// <summary>创建一侧姓名、职业、武器、等级和四项数值文字。</summary>
    private SideLabels CreateSideLabels(float panelX, bool portraitOnLeft)
    {
        float infoX = portraitOnLeft ? panelX + PortraitWidth : panelX + 4;
        SideLabels labels = new()
        {
            Name = CreateLabel(new Vector2(infoX + 18, 13), new Vector2(270, 35), 24, GoldLight, HorizontalAlignment.Left),
            Class = CreateLabel(new Vector2(infoX + 18, 48), new Vector2(250, 26), 16, TextColor, HorizontalAlignment.Left),
            Weapon = CreateLabel(new Vector2(infoX + 18, 76), new Vector2(250, 25), 15, new Color("e5d4b2"), HorizontalAlignment.Left),
            Level = CreateLabel(new Vector2(infoX + 334, 16), new Vector2(82, 31), 18, GoldLight, HorizontalAlignment.Right),
            HpValue = CreateLabel(new Vector2(infoX + 342, 126), new Vector2(70, 28), 18, TextColor, HorizontalAlignment.Right),
            HitValue = CreateLabel(new Vector2(infoX + 342, 160), new Vector2(70, 28), 18, TextColor, HorizontalAlignment.Right),
            AttackValue = CreateLabel(new Vector2(infoX + 342, 194), new Vector2(70, 28), 18, TextColor, HorizontalAlignment.Right),
            DefenseValue = CreateLabel(new Vector2(infoX + 342, 228), new Vector2(70, 28), 18, TextColor, HorizontalAlignment.Right)
        };

        AddChild(labels.Name);
        AddChild(labels.Class);
        AddChild(labels.Weapon);
        AddChild(labels.Level);
        AddChild(labels.HpValue);
        AddChild(labels.HitValue);
        AddChild(labels.AttackValue);
        AddChild(labels.DefenseValue);

        AddChild(CreateMetricName(new Vector2(infoX + 18, 126), "HP"));
        AddChild(CreateMetricName(new Vector2(infoX + 18, 160), "HIT"));
        AddChild(CreateMetricName(new Vector2(infoX + 18, 194), "ATC"));
        AddChild(CreateMetricName(new Vector2(infoX + 18, 228), "DEF"));
        return labels;
    }

    /// <summary>创建普通战斗 HUD 文字。</summary>
    private static Label CreateLabel(
        Vector2 position,
        Vector2 size,
        int fontSize,
        Color color,
        HorizontalAlignment alignment)
    {
        Label label = new()
        {
            Position = position,
            Size = size,
            HorizontalAlignment = alignment,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        label.AddThemeFontOverride("font", BattlePixelFontCatalog.Font);
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_shadow_color", Colors.Black);
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
        return label;
    }

    /// <summary>创建 HP/HIT/ATC/DEF 行首金色标签。</summary>
    private static Label CreateMetricName(Vector2 position, string text)
    {
        Label label = CreateLabel(position, new Vector2(54, 28), 17, GoldLight, HorizontalAlignment.Left);
        label.Text = text;
        return label;
    }

    /// <summary>刷新双方全部动态文字。</summary>
    private void RefreshAllLabels()
    {
        RefreshSideLabels(_leftLabels, _leftUnit, _leftHp, _leftMaxHp, _rightUnit);
        RefreshSideLabels(_rightLabels, _rightUnit, _rightHp, _rightMaxHp, _leftUnit);
    }

    /// <summary>刷新一侧名字、职业、武器、等级以及四项真实数值。</summary>
    private static void RefreshSideLabels(
        SideLabels? labels,
        UnitModel? unit,
        int hp,
        int maxHp,
        UnitModel? opponent)
    {
        if (labels is null)
        {
            return;
        }

        if (unit is null)
        {
            labels.Clear();
            return;
        }

        int hit = opponent is null ? 0 : CombatRules.CalculateHitRate(unit, opponent, 0);
        int attack = (unit.EquippedWeapon.DamageType == DamageType.Magical ? unit.Magic : unit.Strength) + unit.EquippedWeapon.Might;
        int defense = unit.Defense;

        labels.Name.Text = unit.DisplayName;
        labels.Class.Text = unit.ClassDefinition.DisplayName;
        labels.Weapon.Text = unit.EquippedWeapon.DisplayName;
        labels.Level.Text = $"LV {unit.Level}";
        labels.HpValue.Text = Math.Clamp(hp, 0, Math.Max(1, maxHp)).ToString();
        labels.HitValue.Text = hit.ToString();
        labels.AttackValue.Text = attack.ToString();
        labels.DefenseValue.Text = defense.ToString();
    }

    /// <summary>保存一侧 HUD 的动态文字引用。</summary>
    private sealed class SideLabels
    {
        public Label Name { get; init; } = null!;
        public Label Class { get; init; } = null!;
        public Label Weapon { get; init; } = null!;
        public Label Level { get; init; } = null!;
        public Label HpValue { get; init; } = null!;
        public Label HitValue { get; init; } = null!;
        public Label AttackValue { get; init; } = null!;
        public Label DefenseValue { get; init; } = null!;

        /// <summary>清空没有人物时的所有动态文字。</summary>
        public void Clear()
        {
            Name.Text = string.Empty;
            Class.Text = string.Empty;
            Weapon.Text = string.Empty;
            Level.Text = string.Empty;
            HpValue.Text = string.Empty;
            HitValue.Text = string.Empty;
            AttackValue.Text = string.Empty;
            DefenseValue.Text = string.Empty;
        }
    }
}
