using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 人物完整详情页。
/// 当前展示身份、装备、八维属性和基础战斗派生值；后续转职、背包、技能、支援等分页都会从这里继续扩展。
/// </summary>
public partial class CharacterDetailOverlay : PanelContainer
{
    /// <summary>大尺寸人物头像。</summary>
    private CharacterPortraitControl? _portrait;

    /// <summary>角色身份标题。</summary>
    private Label? _titleLabel;

    /// <summary>基础属性文本。</summary>
    private Label? _statsLabel;

    /// <summary>装备详细参数文本。</summary>
    private Label? _weaponLabel;

    /// <summary>派生战斗能力文本。</summary>
    private Label? _derivedLabel;

    /// <summary>当前详情页人物。</summary>
    private UnitModel? _unit;

    /// <summary>
    /// 创建详情页布局并默认隐藏。
    /// </summary>
    public override void _Ready()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;

        VBoxContainer root = new()
        {
            CustomMinimumSize = new Vector2(820, 500)
        };
        AddChild(root);

        HBoxContainer header = new();
        root.AddChild(header);

        _portrait = new CharacterPortraitControl
        {
            CustomMinimumSize = new Vector2(230, 240)
        };
        header.AddChild(_portrait);

        VBoxContainer summary = new()
        {
            CustomMinimumSize = new Vector2(560, 240)
        };
        header.AddChild(summary);

        _titleLabel = new Label
        {
            Text = "人物详情",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        summary.AddChild(_titleLabel);

        _statsLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        summary.AddChild(_statsLabel);

        _weaponLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        summary.AddChild(_weaponLabel);

        _derivedLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        summary.AddChild(_derivedLabel);

        Label futureTabs = new()
        {
            Text = "后续分页：装备背包 / 转职 / 技能 / 支援 / 成长记录",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        root.AddChild(futureTabs);

        Button closeButton = new()
        {
            Text = "关闭人物详情"
        };
        closeButton.Pressed += HideOverlay;
        root.AddChild(closeButton);
    }

    /// <summary>
    /// 打开指定人物的详情页。
    /// </summary>
    public void ShowUnit(UnitModel unit)
    {
        _unit = unit;
        Visible = true;
        RefreshContent();
    }

    /// <summary>
    /// 关闭详情页，不修改任何人物状态。
    /// </summary>
    public void HideOverlay()
    {
        Visible = false;
    }

    /// <summary>
    /// 根据当前人物刷新所有详情文本。
    /// </summary>
    private void RefreshContent()
    {
        if (_unit is null || _titleLabel is null || _statsLabel is null || _weaponLabel is null || _derivedLabel is null)
        {
            return;
        }

        _portrait?.SetUnit(_unit);
        WeaponDefinition weapon = _unit.EquippedWeapon;
        string damageType = weapon.DamageType == DamageType.Magical ? "魔法" : "物理";

        _titleLabel.Text = $"{_unit.DisplayName} — Lv.{_unit.Level} {_unit.ClassDefinition.DisplayName}";
        _statsLabel.Text =
            $"HP {_unit.CurrentHp}/{_unit.MaxHp}    EXP {_unit.Experience}/100\n" +
            $"力量 {_unit.Strength}    魔力 {_unit.Magic}    技巧 {_unit.Skill}    速度 {_unit.Speed}\n" +
            $"幸运 {_unit.Luck}    防御 {_unit.Defense}    魔防 {_unit.Resistance}    移动力 {_unit.Move}";

        _weaponLabel.Text =
            $"当前装备：{weapon.DisplayName}（{damageType}）\n" +
            $"威力 {weapon.Might}    命中 {weapon.Hit}    必杀 {weapon.Critical}    射程 {weapon.MinRange}-{weapon.MaxRange}" +
            (weapon.HpCost > 0 ? $"    施放消耗 {weapon.HpCost} HP" : string.Empty);

        // 这些派生值与 CombatRules 使用相同的基础思路，用于角色详情页快速比较人物战斗倾向。
        int attackPower = (weapon.DamageType == DamageType.Magical ? _unit.Magic : _unit.Strength) + weapon.Might;
        int baseHit = Math.Clamp(weapon.Hit + _unit.Skill * 2 + _unit.Luck, 0, 100);
        int baseCritical = Math.Clamp(weapon.Critical + _unit.Skill / 2, 0, 100);
        int baseAvoid = Math.Max(0, _unit.Speed * 2 + _unit.Luck);

        _derivedLabel.Text =
            $"派生能力：攻击 {attackPower}    基础命中 {baseHit}    基础必杀 {baseCritical}    基础回避 {baseAvoid}\n" +
            "实际战斗还会受到目标幸运、地形回避、防御/魔防以及追击规则影响。";
    }
}
