using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 战斗演出层的第一版骨架。
/// 当前在玩家锁定攻击目标时展示左右人物位、头像、装备和 HP；以后攻击动画、受击动画、魔法特效都在这个控件内部扩展。
/// </summary>
public partial class BattleDuelPreviewControl : Control
{
    /// <summary>左侧主动攻击者头像。</summary>
    private CharacterPortraitControl? _attackerPortrait;

    /// <summary>右侧防守者头像。</summary>
    private CharacterPortraitControl? _defenderPortrait;

    /// <summary>左侧人物信息。</summary>
    private Label? _attackerLabel;

    /// <summary>右侧人物信息。</summary>
    private Label? _defenderLabel;

    /// <summary>中央战斗状态说明。</summary>
    private Label? _centerLabel;

    /// <summary>当前主动攻击者。</summary>
    private UnitModel? _attacker;

    /// <summary>当前防守者。</summary>
    private UnitModel? _defender;

    /// <summary>人物待机呼吸动画计时。</summary>
    private double _elapsed;

    /// <summary>
    /// 创建战斗演出层的控件结构。
    /// </summary>
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;

        PanelContainer panel = new();
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(panel);

        HBoxContainer row = new()
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        panel.AddChild(row);

        VBoxContainer attackerColumn = new()
        {
            CustomMinimumSize = new Vector2(210, 210)
        };
        row.AddChild(attackerColumn);

        _attackerPortrait = new CharacterPortraitControl
        {
            CustomMinimumSize = new Vector2(190, 160)
        };
        attackerColumn.AddChild(_attackerPortrait);

        _attackerLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        attackerColumn.AddChild(_attackerLabel);

        VBoxContainer centerColumn = new()
        {
            CustomMinimumSize = new Vector2(170, 210),
            Alignment = BoxContainer.AlignmentMode.Center
        };
        row.AddChild(centerColumn);

        Label versus = new()
        {
            Text = "VS",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        centerColumn.AddChild(versus);

        _centerLabel = new Label
        {
            Text = "战斗演出预览",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        centerColumn.AddChild(_centerLabel);

        VBoxContainer defenderColumn = new()
        {
            CustomMinimumSize = new Vector2(210, 210)
        };
        row.AddChild(defenderColumn);

        _defenderPortrait = new CharacterPortraitControl
        {
            CustomMinimumSize = new Vector2(190, 160)
        };
        defenderColumn.AddChild(_defenderPortrait);

        _defenderLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        defenderColumn.AddChild(_defenderLabel);
    }

    /// <summary>
    /// 设置当前演出双方；任意一方为空时自动隐藏。
    /// </summary>
    public void SetCombatants(UnitModel? attacker, UnitModel? defender)
    {
        _attacker = attacker;
        _defender = defender;
        Visible = attacker is not null && defender is not null;

        _attackerPortrait?.SetUnit(attacker);
        _defenderPortrait?.SetUnit(defender);
        RefreshLabels();
    }

    /// <summary>
    /// 让左右人物位产生轻微错相呼吸动画，建立后续正式战斗动画状态机的基础。
    /// </summary>
    public override void _Process(double delta)
    {
        if (!Visible)
        {
            return;
        }

        _elapsed += delta;
        if (_attackerPortrait is not null)
        {
            _attackerPortrait.Position = new Vector2(0, Mathf.Sin((float)_elapsed * 4.2f) * 2.0f);
        }

        if (_defenderPortrait is not null)
        {
            _defenderPortrait.Position = new Vector2(0, Mathf.Sin((float)_elapsed * 4.2f + 1.7f) * 2.0f);
        }
    }

    /// <summary>
    /// 刷新双方名称、HP 和装备信息。
    /// </summary>
    private void RefreshLabels()
    {
        if (_attackerLabel is not null)
        {
            _attackerLabel.Text = _attacker is null
                ? string.Empty
                : $"{_attacker.DisplayName}\nHP {_attacker.CurrentHp}/{_attacker.MaxHp} | {_attacker.EquippedWeapon.DisplayName}";
        }

        if (_defenderLabel is not null)
        {
            _defenderLabel.Text = _defender is null
                ? string.Empty
                : $"{_defender.DisplayName}\nHP {_defender.CurrentHp}/{_defender.MaxHp} | {_defender.EquippedWeapon.DisplayName}";
        }

        if (_centerLabel is not null)
        {
            _centerLabel.Text = "战斗演出层\n确认攻击后由结算器决定命中 / 必杀 / 追击";
        }
    }
}
