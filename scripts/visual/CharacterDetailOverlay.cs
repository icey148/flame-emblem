using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 人物完整详情页。
/// 展示身份、装备、八维属性和基础战斗派生值，并提供稳定的顶部按钮、Esc 和右键关闭入口。
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

    /// <summary>固定在详情页顶部的关闭按钮，避免内容过高时底部按钮被挤出可视区域。</summary>
    private Button? _closeButton;

    /// <summary>当前详情页人物。</summary>
    private UnitModel? _unit;

    /// <summary>
    /// 创建详情页布局并默认隐藏。
    /// </summary>
    public override void _Ready()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        ProcessMode = ProcessModeEnum.Always;

        VBoxContainer root = new()
        {
            CustomMinimumSize = new Vector2(820, 500)
        };
        AddChild(root);

        // 顶部工具栏始终位于第一行，关闭按钮不会被长属性文本挤到底部屏幕之外。
        HBoxContainer toolbar = new()
        {
            CustomMinimumSize = new Vector2(820, 42)
        };
        root.AddChild(toolbar);

        _titleLabel = new Label
        {
            Text = "人物详情",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        toolbar.AddChild(_titleLabel);

        _closeButton = new Button
        {
            Text = "关闭",
            CustomMinimumSize = new Vector2(96, 36),
            FocusMode = FocusModeEnum.All
        };
        _closeButton.Pressed += HideOverlay;
        toolbar.AddChild(_closeButton);

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

        Label closeHint = new()
        {
            Text = "Esc / 右键也可以关闭人物详情",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        root.AddChild(closeHint);
    }

    /// <summary>
    /// 打开指定人物的详情页，并把键盘焦点交给关闭按钮，保证键盘取消输入稳定。
    /// </summary>
    public void ShowUnit(UnitModel unit)
    {
        _unit = unit;
        Visible = true;
        RefreshContent();
        _closeButton?.GrabFocus();
    }

    /// <summary>
    /// 关闭详情页并清理当前人物表现引用。
    /// 清理引用可防止隐藏后的旧头像/文字继续作为下一次打开时的瞬时残留。
    /// </summary>
    public void HideOverlay()
    {
        Visible = false;
        _unit = null;
        _portrait?.SetUnit(null);
        _closeButton?.ReleaseFocus();
    }

    /// <summary>
    /// GUI 内部的右键会先到这里，因此必须在控件层直接处理，不能只依赖 _UnhandledInput。
    /// </summary>
    public override void _GuiInput(InputEvent @event)
    {
        if (!Visible || !IsCloseInput(@event))
        {
            return;
        }

        HideOverlay();
        AcceptEvent();
    }

    /// <summary>
    /// 详情页可见时允许 Esc 或未被 GUI 消耗的右键立即关闭。
    /// </summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible || !IsCloseInput(@event))
        {
            return;
        }

        HideOverlay();
        GetViewport().SetInputAsHandled();
    }

    /// <summary>判断输入是否属于详情页关闭操作。</summary>
    private static bool IsCloseInput(InputEvent @event)
    {
        bool cancelPressed = @event.IsActionPressed("ui_cancel");
        bool rightClickPressed = @event is InputEventMouseButton mouseButton &&
                                 mouseButton.ButtonIndex == MouseButton.Right &&
                                 mouseButton.Pressed;
        return cancelPressed || rightClickPressed;
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
