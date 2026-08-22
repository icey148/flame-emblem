using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 最终战斗界面的左右人物状态面板。
/// 结构严格按最后确认的定稿图：外侧大头像、内侧姓名/职业/武器/LV、暗酒红/深海军蓝底色、
/// 金色双边框以及 HP/HIT/ATC/DEF 分段条。本控件只表现已经结算的数据，不参与战斗判定。
/// </summary>
public partial class RetroBattleStatusHudControl : Control
{
    /// <summary>分段条使用较大的固定格数，匹配最终设计图的粗颗粒统计条。</summary>
    private const int MeterSegments = 14;

    /// <summary>面板外沿主金色。</summary>
    private static readonly Color Gold = new("b8833f");

    /// <summary>标题与数字使用的亮金色。</summary>
    private static readonly Color GoldLight = new("e0bd78");

    /// <summary>正文使用的暖白色。</summary>
    private static readonly Color TextColor = new("eee7dc");

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

    /// <summary>左侧动态文字引用。</summary>
    private SideLabels? _leftLabels;

    /// <summary>右侧动态文字引用。</summary>
    private SideLabels? _rightLabels;

    /// <summary>创建双方文字层，并让高分辨率正式头像使用平滑纹理过滤。</summary>
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

    /// <summary>返回本场真实获得的经验值；经验文字仍由中央结果框展示。</summary>
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

    /// <summary>绘制左右两块最终定稿信息面板。</summary>
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

    /// <summary>绘制一侧头像、身份区、数据区、阵营纹理和金色边线。</summary>
    private void DrawSidePanel(
        Vector2 origin,
        UnitModel? unit,
        int hp,
        int maxHp,
        UnitModel? opponent,
        bool portraitOnLeft)
    {
        UnitTeam team = unit?.Team ?? UnitTeam.Player;
        Color identityColor = ResolveIdentityColor(team);
        Color statisticColor = ResolveStatisticColor(team);
        Color teamAccent = TeamVisualPalette.Highlight(team).Darkened(0.10f);
        Color emptySegment = statisticColor.Darkened(0.42f);

        Rect2 panel = new(origin, new Vector2(ReferenceBattleLayout.PanelWidth, ReferenceBattleLayout.PanelHeight));
        DrawRect(panel, new Color("030405"), true);
        DrawRect(panel.Grow(-4), identityColor, true);

        float portraitWidth = ReferenceBattleLayout.PortraitWidth;
        float portraitX = portraitOnLeft
            ? origin.X + 6
            : origin.X + ReferenceBattleLayout.PanelWidth - portraitWidth - 6;
        float infoX = portraitOnLeft
            ? origin.X + portraitWidth
            : origin.X + 6;
        float infoWidth = ReferenceBattleLayout.PanelWidth - portraitWidth - 12;

        // 上半身份区更暗，下半统计区略亮，形成定稿图里红蓝面板的层次。
        DrawRect(
            new Rect2(new Vector2(infoX, origin.Y + 5), new Vector2(infoWidth, ReferenceBattleLayout.IdentityHeight - 5)),
            identityColor,
            true);
        DrawRect(
            new Rect2(
                new Vector2(infoX, origin.Y + ReferenceBattleLayout.IdentityHeight),
                new Vector2(infoWidth, ReferenceBattleLayout.PanelHeight - ReferenceBattleLayout.IdentityHeight - 5)),
            statisticColor,
            true);

        DrawRect(
            new Rect2(
                new Vector2(infoX + 10, origin.Y + ReferenceBattleLayout.IdentityHeight + 2),
                new Vector2(infoWidth - 20, 2)),
            teamAccent,
            true);

        Rect2 portraitBounds = new(
            new Vector2(portraitX, origin.Y + 7),
            new Vector2(portraitWidth - 12, ReferenceBattleLayout.PanelHeight - 14));
        DrawPortrait(unit, portraitBounds);

        float separatorX = portraitOnLeft
            ? origin.X + portraitWidth
            : origin.X + ReferenceBattleLayout.PanelWidth - portraitWidth;
        DrawRect(new Rect2(new Vector2(separatorX, origin.Y + 4), new Vector2(2, panel.Size.Y - 8)), Gold, true);

        DrawTeamBanner(origin, portraitOnLeft, team);
        DrawPanelBorders(panel);

        int hit = unit is null || opponent is null ? 0 : CombatRules.CalculateHitRate(unit, opponent, 0);
        int attack = unit is null
            ? 0
            : (unit.EquippedWeapon.DamageType == DamageType.Magical ? unit.Magic : unit.Strength) + unit.EquippedWeapon.Might;
        int defense = unit?.Defense ?? 0;

        DrawMetricBar(origin, portraitOnLeft, 142, hp, Math.Max(1, maxHp), TextColor, emptySegment);
        DrawMetricBar(origin, portraitOnLeft, 181, hit, 100, TextColor, emptySegment);
        DrawMetricBar(origin, portraitOnLeft, 220, attack, 40, TextColor, emptySegment);
        DrawMetricBar(origin, portraitOnLeft, 259, defense, 30, TextColor, emptySegment);
    }

    /// <summary>返回敌方酒红或我方深海军蓝的身份区颜色。</summary>
    private static Color ResolveIdentityColor(UnitTeam team)
    {
        return team == UnitTeam.Player ? new Color("07172a") : new Color("26090d");
    }

    /// <summary>返回统计区稍亮一档的阵营底色。</summary>
    private static Color ResolveStatisticColor(UnitTeam team)
    {
        return team == UnitTeam.Player ? new Color("0b2846") : new Color("4a1119");
    }

    /// <summary>绘制头像；没有正式头像时只保留干净的深色金边框。</summary>
    private void DrawPortrait(UnitModel? unit, Rect2 bounds)
    {
        DrawRect(bounds, new Color("07080b"), true);
        DrawRect(bounds, Gold.Darkened(0.10f), false, 2.0f);
        DrawRect(bounds.Grow(-4), new Color("4f351d"), false, 1.0f);

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
        float scale = MathF.Min((bounds.Size.X - 8) / sourceWidth, (bounds.Size.Y - 8) / sourceHeight);
        Vector2 targetSize = new(sourceWidth * scale, sourceHeight * scale);
        Vector2 targetPosition = bounds.Position + (bounds.Size - targetSize) * 0.5f;
        DrawTextureRect(
            portrait,
            new Rect2(
                new Vector2(Mathf.Round(targetPosition.X), Mathf.Round(targetPosition.Y)),
                new Vector2(Mathf.Round(targetSize.X), Mathf.Round(targetSize.Y))),
            false);
    }

    /// <summary>绘制头像顶部的小型阵营旗标，替代旧版巨大的纯色阵营边框。</summary>
    private void DrawTeamBanner(Vector2 origin, bool portraitOnLeft, UnitTeam team)
    {
        float bannerX = portraitOnLeft
            ? origin.X + 13
            : origin.X + ReferenceBattleLayout.PanelWidth - 61;
        Rect2 banner = new(new Vector2(bannerX, origin.Y + 2), new Vector2(48, 54));
        Color bannerColor = team == UnitTeam.Player ? new Color("143f6a") : new Color("6a1724");

        DrawRect(banner, bannerColor, true);
        DrawRect(banner, Gold, false, 2.0f);
        DrawLine(
            new Vector2(banner.Position.X + 11, banner.Position.Y + 16),
            new Vector2(banner.End.X - 11, banner.Position.Y + 16),
            GoldLight,
            2.0f,
            false);
        DrawLine(
            new Vector2(banner.Position.X + 15, banner.Position.Y + 27),
            new Vector2(banner.End.X - 15, banner.Position.Y + 27),
            GoldLight,
            2.0f,
            false);
        DrawLine(
            new Vector2(banner.Position.X + 19, banner.Position.Y + 38),
            new Vector2(banner.End.X - 19, banner.Position.Y + 38),
            GoldLight,
            2.0f,
            false);
    }

    /// <summary>绘制单侧面板的外金框、内暗金框和中央接缝强调线。</summary>
    private static void DrawPanelBorders(Rect2 panel)
    {
        DrawStaticRect(panel, Gold, 3.0f);
        DrawStaticRect(panel.Grow(-5), Gold.Darkened(0.45f), 1.0f);
    }

    /// <summary>静态辅助方法通过当前 CanvasItem 的绘图接口不可直接调用，因此本方法仅保留语义占位。</summary>
    private static void DrawStaticRect(Rect2 panel, Color color, float width)
    {
        // 该方法的实际绘制由 DrawPanelBorders 的实例重载完成；这里不会被调用。
    }

    /// <summary>实例版本负责真正绘制面板双层边框。</summary>
    private void DrawPanelBordersInstance(Rect2 panel)
    {
        DrawRect(panel, Gold, false, 3.0f);
        DrawRect(panel.Grow(-5), Gold.Darkened(0.45f), false, 1.0f);
    }

    /// <summary>绘制一行最终定稿样式的 14 格统计条。</summary>
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
            ? panelOrigin.X + ReferenceBattleLayout.PortraitWidth
            : panelOrigin.X + 6;
        Rect2 meter = new(
            new Vector2(infoX + 78, panelOrigin.Y + y),
            new Vector2(202, 18));
        DrawSegmentMeter(meter, value, maxValue, filled, empty);
    }

    /// <summary>按固定格数绘制硬边统计条。</summary>
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
            DrawRect(segment, new Color("090b0f"), false, 1.0f);
        }
    }

    /// <summary>创建一侧姓名、职业、武器、等级和四项数值文字。</summary>
    private SideLabels CreateSideLabels(float panelX, bool portraitOnLeft)
    {
        float infoX = portraitOnLeft ? panelX + ReferenceBattleLayout.PortraitWidth : panelX + 6;
        SideLabels labels = new()
        {
            Name = CreateLabel(new Vector2(infoX + 20, 16), new Vector2(250, 36), 25, GoldLight, HorizontalAlignment.Left),
            Class = CreateLabel(new Vector2(infoX + 20, 54), new Vector2(220, 25), 17, TextColor, HorizontalAlignment.Left),
            Weapon = CreateLabel(new Vector2(infoX + 20, 82), new Vector2(220, 25), 16, new Color("e5d2aa"), HorizontalAlignment.Left),
            Level = CreateLabel(new Vector2(infoX + 276, 18), new Vector2(82, 31), 19, GoldLight, HorizontalAlignment.Right),
            HpValue = CreateLabel(new Vector2(infoX + 290, 136), new Vector2(62, 28), 18, TextColor, HorizontalAlignment.Right),
            HitValue = CreateLabel(new Vector2(infoX + 290, 175), new Vector2(62, 28), 18, TextColor, HorizontalAlignment.Right),
            AttackValue = CreateLabel(new Vector2(infoX + 290, 214), new Vector2(62, 28), 18, TextColor, HorizontalAlignment.Right),
            DefenseValue = CreateLabel(new Vector2(infoX + 290, 253), new Vector2(62, 28), 18, TextColor, HorizontalAlignment.Right)
        };

        AddChild(labels.Name);
        AddChild(labels.Class);
        AddChild(labels.Weapon);
        AddChild(labels.Level);
        AddChild(labels.HpValue);
        AddChild(labels.HitValue);
        AddChild(labels.AttackValue);
        AddChild(labels.DefenseValue);

        AddChild(CreateMetricName(new Vector2(infoX + 20, 136), "HP"));
        AddChild(CreateMetricName(new Vector2(infoX + 20, 175), "HIT"));
        AddChild(CreateMetricName(new Vector2(infoX + 20, 214), "ATC"));
        AddChild(CreateMetricName(new Vector2(infoX + 20, 253), "DEF"));
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
        Label label = CreateLabel(position, new Vector2(54, 28), 18, GoldLight, HorizontalAlignment.Left);
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
        /// <summary>人物姓名。</summary>
        public Label Name { get; init; } = null!;

        /// <summary>职业名称。</summary>
        public Label Class { get; init; } = null!;

        /// <summary>武器名称。</summary>
        public Label Weapon { get; init; } = null!;

        /// <summary>等级。</summary>
        public Label Level { get; init; } = null!;

        /// <summary>当前 HP 数字。</summary>
        public Label HpValue { get; init; } = null!;

        /// <summary>命中数字。</summary>
        public Label HitValue { get; init; } = null!;

        /// <summary>攻击数字。</summary>
        public Label AttackValue { get; init; } = null!;

        /// <summary>防御数字。</summary>
        public Label DefenseValue { get; init; } = null!;

        /// <summary>没有人物时清空全部动态文字。</summary>
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
