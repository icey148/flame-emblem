using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 锁定攻击目标时显示的战斗预览。
/// 整个预览层只负责视觉展示，所有子控件都忽略鼠标输入，确保玩家仍能直接点击地图上的其他敌军切换目标。
/// </summary>
public partial class BattleDuelPreviewControl : Control
{
    /// <summary>左侧主动攻击者战斗人物图。</summary>
    private BattleCharacterArtControl? _attackerArt;

    /// <summary>右侧防守者战斗人物图。</summary>
    private BattleCharacterArtControl? _defenderArt;

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

    /// <summary>创建完全不拦截地图鼠标输入的战斗预览结构。</summary>
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;

        PanelContainer panel = new()
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(panel);

        HBoxContainer row = new()
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        panel.AddChild(row);

        VBoxContainer attackerColumn = new()
        {
            CustomMinimumSize = new Vector2(210, 210),
            MouseFilter = MouseFilterEnum.Ignore
        };
        row.AddChild(attackerColumn);

        _attackerArt = new BattleCharacterArtControl
        {
            CustomMinimumSize = new Vector2(190, 160),
            MouseFilter = MouseFilterEnum.Ignore
        };
        attackerColumn.AddChild(_attackerArt);

        _attackerLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore
        };
        attackerColumn.AddChild(_attackerLabel);

        VBoxContainer centerColumn = new()
        {
            CustomMinimumSize = new Vector2(170, 210),
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        row.AddChild(centerColumn);

        Label versus = new()
        {
            Text = "VS",
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        centerColumn.AddChild(versus);

        _centerLabel = new Label
        {
            Text = "战斗演出预览",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore
        };
        centerColumn.AddChild(_centerLabel);

        VBoxContainer defenderColumn = new()
        {
            CustomMinimumSize = new Vector2(210, 210),
            MouseFilter = MouseFilterEnum.Ignore
        };
        row.AddChild(defenderColumn);

        _defenderArt = new BattleCharacterArtControl
        {
            CustomMinimumSize = new Vector2(190, 160),
            MouseFilter = MouseFilterEnum.Ignore
        };
        defenderColumn.AddChild(_defenderArt);

        _defenderLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore
        };
        defenderColumn.AddChild(_defenderLabel);
    }

    /// <summary>设置当前演出双方；任意一方为空时自动隐藏。</summary>
    public void SetCombatants(UnitModel? attacker, UnitModel? defender)
    {
        _attacker = attacker;
        _defender = defender;
        Visible = attacker is not null && defender is not null;

        _attackerArt?.SetUnit(attacker);
        _defenderArt?.SetUnit(defender);
        RefreshLabels();
    }

    /// <summary>
    /// 让左右人物位产生轻微错相呼吸缩放。
    /// 该动画只改变视觉，不会修改输入或战斗状态。
    /// </summary>
    public override void _Process(double delta)
    {
        if (!Visible)
        {
            return;
        }

        _elapsed += delta;
        if (_attackerArt is not null)
        {
            _attackerArt.PivotOffset = _attackerArt.Size * 0.5f;
            float scale = 1.0f + Mathf.Sin((float)_elapsed * 4.2f) * 0.012f;
            _attackerArt.Scale = new Vector2(scale, scale);
        }

        if (_defenderArt is not null)
        {
            _defenderArt.PivotOffset = _defenderArt.Size * 0.5f;
            float scale = 1.0f + Mathf.Sin((float)_elapsed * 4.2f + 1.7f) * 0.012f;
            _defenderArt.Scale = new Vector2(scale, scale);
        }
    }

    /// <summary>刷新双方名称、HP 和装备信息。</summary>
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
            _centerLabel.Text = "战斗预测\n可直接点击其他红色敌军切换目标";
        }
    }
}
