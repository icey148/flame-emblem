using FlameEmblem.Game;
using Godot;
using System.Reflection;

namespace FlameEmblem.Visual;

/// <summary>
/// 在玩家单位完成移动后显示明确的行动菜单。
/// 这个过渡层只负责交互提示与按钮，不修改战斗公式；攻击和等待仍然调用 MainGame 现有逻辑。
/// </summary>
public partial class PostMoveActionCoordinator : Node
{
    /// <summary>当前战斗主节点。</summary>
    private Node? _battleHost;

    /// <summary>MainGame 当前选中单位字段。</summary>
    private FieldInfo? _selectedUnitField;

    /// <summary>MainGame 当前单位是否已经移动字段。</summary>
    private FieldInfo? _selectedUnitHasMovedField;

    /// <summary>MainGame 当前锁定攻击目标字段。</summary>
    private FieldInfo? _pendingAttackTargetField;

    /// <summary>MainGame 全部单位集合字段。</summary>
    private FieldInfo? _unitsField;

    /// <summary>MainGame 设置攻击目标的私有方法。</summary>
    private MethodInfo? _setPendingAttackTargetMethod;

    /// <summary>MainGame 等待按钮对应的私有方法。</summary>
    private MethodInfo? _waitMethod;

    /// <summary>移动结束后显示的浮动面板。</summary>
    private PanelContainer? _panel;

    /// <summary>提示当前可以执行什么行动。</summary>
    private Label? _messageLabel;

    /// <summary>自动锁定一个可攻击敌人的按钮。</summary>
    private Button? _attackButton;

    /// <summary>结束当前单位行动的等待按钮。</summary>
    private Button? _waitButton;

    /// <summary>上一次用于刷新面板的状态签名。</summary>
    private string _lastState = string.Empty;

    /// <summary>
    /// 缓存 MainGame 所需成员并创建移动后行动菜单。
    /// </summary>
    public override void _Ready()
    {
        _battleHost = GetParent();
        if (_battleHost is null)
        {
            GD.PushWarning("PostMoveActionCoordinator 找不到战斗主节点。移动后行动菜单不会启动。");
            SetProcess(false);
            return;
        }

        Type hostType = _battleHost.GetType();
        BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        _selectedUnitField = hostType.GetField("_selectedUnit", fields);
        _selectedUnitHasMovedField = hostType.GetField("_selectedUnitHasMoved", fields);
        _pendingAttackTargetField = hostType.GetField("_pendingAttackTarget", fields);
        _unitsField = hostType.GetField("_units", fields);
        _setPendingAttackTargetMethod = hostType.GetMethod("SetPendingAttackTarget", fields);
        _waitMethod = hostType.GetMethod("OnWaitPressed", fields);

        if (_selectedUnitField is null ||
            _selectedUnitHasMovedField is null ||
            _pendingAttackTargetField is null ||
            _unitsField is null ||
            _setPendingAttackTargetMethod is null ||
            _waitMethod is null)
        {
            GD.PushWarning("PostMoveActionCoordinator 无法读取 MainGame 的移动后状态。请同步检查字段/方法名称。");
            SetProcess(false);
            return;
        }

        CreatePanel();
    }

    /// <summary>
    /// 每帧检查单位是否已经完成移动，并在需要时显示行动菜单。
    /// </summary>
    public override void _Process(double delta)
    {
        UnitModel? selectedUnit = ReadSelectedUnit();
        bool hasMoved = ReadSelectedUnitHasMoved();
        UnitModel? pendingTarget = ReadPendingTarget();

        // 只有“我方单位已移动且尚未锁定攻击目标”时显示菜单。
        bool shouldShow = selectedUnit is { Team: UnitTeam.Player } &&
                          hasMoved &&
                          pendingTarget is null &&
                          !selectedUnit.HasActed;

        if (_panel is not null)
        {
            _panel.Visible = shouldShow;
        }

        if (!shouldShow || selectedUnit is null)
        {
            _lastState = string.Empty;
            return;
        }

        IReadOnlyList<UnitModel> targets = FindAttackableTargets(selectedUnit);
        string state = $"{selectedUnit.Id}:{selectedUnit.GridPosition}:{selectedUnit.EquippedWeapon.Id}:{targets.Count}";
        if (state == _lastState)
        {
            return;
        }

        _lastState = state;
        RefreshPanel(selectedUnit, targets);
    }

    /// <summary>
    /// 创建地图右下方的移动后行动菜单。
    /// </summary>
    private void CreatePanel()
    {
        CanvasLayer layer = new()
        {
            Layer = 25
        };
        AddChild(layer);

        _panel = new PanelContainer
        {
            Position = new Vector2(610, 430),
            Size = new Vector2(210, 145),
            Visible = false
        };
        layer.AddChild(_panel);

        VBoxContainer column = new()
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        _panel.AddChild(column);

        Label title = new()
        {
            Text = "行动",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        column.AddChild(title);

        _messageLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(190, 55),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        column.AddChild(_messageLabel);

        _attackButton = new Button
        {
            Text = "攻击"
        };
        _attackButton.Pressed += OnAttackPressed;
        column.AddChild(_attackButton);

        _waitButton = new Button
        {
            Text = "等待"
        };
        _waitButton.Pressed += OnWaitPressed;
        column.AddChild(_waitButton);
    }

    /// <summary>
    /// 根据当前可攻击敌人数刷新提示和按钮状态。
    /// </summary>
    private void RefreshPanel(UnitModel selectedUnit, IReadOnlyList<UnitModel> targets)
    {
        if (_messageLabel is not null)
        {
            _messageLabel.Text = targets.Count > 0
                ? $"{selectedUnit.DisplayName} 已移动。\n射程内有 {targets.Count} 个敌人。"
                : $"{selectedUnit.DisplayName} 已移动。\n当前射程内没有敌人。";
        }

        if (_attackButton is not null)
        {
            _attackButton.Disabled = targets.Count == 0 || !selectedUnit.CanUseEquippedWeapon;
            _attackButton.Text = targets.Count > 0 ? $"攻击（{targets.Count}）" : "攻击（无目标）";
        }

        if (_waitButton is not null)
        {
            _waitButton.Disabled = false;
        }
    }

    /// <summary>
    /// 点击攻击按钮时自动锁定距离最近的一个可攻击敌人。
    /// 玩家仍然可以直接点击地图上的红色敌人选择具体目标。
    /// </summary>
    private void OnAttackPressed()
    {
        if (_battleHost is null || _setPendingAttackTargetMethod is null)
        {
            return;
        }

        UnitModel? selectedUnit = ReadSelectedUnit();
        if (selectedUnit is null)
        {
            return;
        }

        UnitModel? target = FindAttackableTargets(selectedUnit)
            .OrderBy(enemy => CombatRules.GridDistance(selectedUnit.GridPosition, enemy.GridPosition))
            .ThenBy(enemy => enemy.Id, StringComparer.Ordinal)
            .FirstOrDefault();

        if (target is null)
        {
            return;
        }

        _setPendingAttackTargetMethod.Invoke(_battleHost, new object[] { target });
    }

    /// <summary>
    /// 点击等待按钮时复用 MainGame 原有等待逻辑，正常结束当前单位行动。
    /// </summary>
    private void OnWaitPressed()
    {
        if (_battleHost is null || _waitMethod is null)
        {
            return;
        }

        _waitMethod.Invoke(_battleHost, null);
    }

    /// <summary>
    /// 查找当前单位实际可以攻击的全部存活敌人。
    /// </summary>
    private IReadOnlyList<UnitModel> FindAttackableTargets(UnitModel selectedUnit)
    {
        return ReadUnits()
            .Where(unit => unit.IsAlive && unit.Team == UnitTeam.Enemy && CombatRules.CanAttack(selectedUnit, unit))
            .ToList();
    }

    /// <summary>读取 MainGame 当前选中的单位。</summary>
    private UnitModel? ReadSelectedUnit()
    {
        return _battleHost is null || _selectedUnitField is null
            ? null
            : _selectedUnitField.GetValue(_battleHost) as UnitModel;
    }

    /// <summary>读取 MainGame 当前单位是否已经移动。</summary>
    private bool ReadSelectedUnitHasMoved()
    {
        return _battleHost is not null &&
               _selectedUnitHasMovedField?.GetValue(_battleHost) is bool moved &&
               moved;
    }

    /// <summary>读取 MainGame 当前锁定的攻击目标。</summary>
    private UnitModel? ReadPendingTarget()
    {
        return _battleHost is null || _pendingAttackTargetField is null
            ? null
            : _pendingAttackTargetField.GetValue(_battleHost) as UnitModel;
    }

    /// <summary>读取 MainGame 当前全部单位。</summary>
    private IReadOnlyList<UnitModel> ReadUnits()
    {
        if (_battleHost is null || _unitsField is null)
        {
            return Array.Empty<UnitModel>();
        }

        return _unitsField.GetValue(_battleHost) as IReadOnlyList<UnitModel>
               ?? Array.Empty<UnitModel>();
    }
}
