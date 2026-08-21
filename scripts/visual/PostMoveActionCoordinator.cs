using FlameEmblem.Game;
using Godot;
using System.Reflection;

namespace FlameEmblem.Visual;

/// <summary>
/// 为当前选中的玩家单位显示明确的行动菜单。
/// 单位不需要先移动；攻击范围内存在多个敌人时可以用按钮循环切换，也可以直接点击地图敌军选择目标。
/// </summary>
public partial class PostMoveActionCoordinator : Node
{
    /// <summary>当前战斗主节点。</summary>
    private Node? _battleHost;

    /// <summary>人物表现协调器，用于判断逐格移动动画是否结束。</summary>
    private CharacterVisualCoordinator? _visualCoordinator;

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

    /// <summary>当前单位行动菜单。</summary>
    private PanelContainer? _panel;

    /// <summary>提示当前可以执行什么行动以及当前目标。</summary>
    private Label? _messageLabel;

    /// <summary>进入目标选择的攻击按钮。</summary>
    private Button? _attackButton;

    /// <summary>切换到上一个可攻击目标。</summary>
    private Button? _previousTargetButton;

    /// <summary>切换到下一个可攻击目标。</summary>
    private Button? _nextTargetButton;

    /// <summary>结束当前单位行动的等待按钮。</summary>
    private Button? _waitButton;

    /// <summary>上一次用于刷新面板的状态签名。</summary>
    private string _lastState = string.Empty;

    /// <summary>缓存 MainGame 所需成员并创建行动菜单。</summary>
    public override void _Ready()
    {
        _battleHost = GetParent();
        if (_battleHost is null)
        {
            GD.PushWarning("PostMoveActionCoordinator 找不到战斗主节点。行动菜单不会启动。");
            SetProcess(false);
            return;
        }

        _visualCoordinator = GetNodeOrNull<CharacterVisualCoordinator>("../CharacterVisualCoordinator");

        Type hostType = _battleHost.GetType();
        BindingFlags members = BindingFlags.Instance | BindingFlags.NonPublic;
        _selectedUnitField = hostType.GetField("_selectedUnit", members);
        _selectedUnitHasMovedField = hostType.GetField("_selectedUnitHasMoved", members);
        _pendingAttackTargetField = hostType.GetField("_pendingAttackTarget", members);
        _unitsField = hostType.GetField("_units", members);
        _setPendingAttackTargetMethod = hostType.GetMethod("SetPendingAttackTarget", members);
        _waitMethod = hostType.GetMethod("OnWaitPressed", members);

        if (_selectedUnitField is null ||
            _selectedUnitHasMovedField is null ||
            _pendingAttackTargetField is null ||
            _unitsField is null ||
            _setPendingAttackTargetMethod is null ||
            _waitMethod is null)
        {
            GD.PushWarning("PostMoveActionCoordinator 无法读取 MainGame 的行动状态。请同步检查字段/方法名称。");
            SetProcess(false);
            return;
        }

        CreatePanel();
    }

    /// <summary>
    /// 每帧检查当前选中单位并显示行动菜单。
    /// 锁定攻击目标后菜单仍然保留，这样多个敌人时可以继续切换目标，而不是被锁死在第一个目标上。
    /// </summary>
    public override void _Process(double delta)
    {
        UnitModel? selectedUnit = ReadSelectedUnit();
        bool hasMoved = ReadSelectedUnitHasMoved();
        UnitModel? pendingTarget = ReadPendingTarget();
        bool movementAnimating = _visualCoordinator?.IsMovementAnimating ?? false;

        bool shouldShow = selectedUnit is { Team: UnitTeam.Player } &&
                          !selectedUnit.HasActed &&
                          !movementAnimating &&
                          !BattleAnimationBus.IsPlaybackActive;

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
        string targetSignature = string.Join(",", targets.Select(target => target.Id));
        string state =
            $"{selectedUnit.Id}:{selectedUnit.GridPosition}:{selectedUnit.EquippedWeapon.Id}:{hasMoved}:" +
            $"pending={pendingTarget?.Id ?? "none"}:targets={targetSignature}";
        if (state == _lastState)
        {
            return;
        }

        _lastState = state;
        RefreshPanel(selectedUnit, targets, pendingTarget, hasMoved);
    }

    /// <summary>创建地图右下方的行动菜单。</summary>
    private void CreatePanel()
    {
        CanvasLayer layer = new()
        {
            Layer = 25
        };
        AddChild(layer);

        _panel = new PanelContainer
        {
            Position = new Vector2(600, 390),
            Size = new Vector2(240, 195),
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
            Text = "行动 / 目标",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        column.AddChild(title);

        _messageLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(220, 72),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        column.AddChild(_messageLabel);

        _attackButton = new Button
        {
            Text = "选择攻击目标"
        };
        _attackButton.Pressed += OnAttackPressed;
        column.AddChild(_attackButton);

        HBoxContainer targetRow = new()
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        column.AddChild(targetRow);

        _previousTargetButton = new Button
        {
            Text = "◀ 上一目标",
            CustomMinimumSize = new Vector2(110, 34)
        };
        _previousTargetButton.Pressed += () => SelectTargetByOffset(-1);
        targetRow.AddChild(_previousTargetButton);

        _nextTargetButton = new Button
        {
            Text = "下一目标 ▶",
            CustomMinimumSize = new Vector2(110, 34)
        };
        _nextTargetButton.Pressed += () => SelectTargetByOffset(1);
        targetRow.AddChild(_nextTargetButton);

        _waitButton = new Button
        {
            Text = "等待"
        };
        _waitButton.Pressed += OnWaitPressed;
        column.AddChild(_waitButton);
    }

    /// <summary>
    /// 刷新行动菜单和目标切换按钮。
    /// 多目标时明确显示当前目标编号，避免玩家不知道预测对应的是哪一个敌人。
    /// </summary>
    private void RefreshPanel(
        UnitModel selectedUnit,
        IReadOnlyList<UnitModel> targets,
        UnitModel? pendingTarget,
        bool hasMoved)
    {
        int currentIndex = pendingTarget is null
            ? -1
            : targets.ToList().FindIndex(target => ReferenceEquals(target, pendingTarget));

        if (_messageLabel is not null)
        {
            if (pendingTarget is not null && currentIndex >= 0)
            {
                _messageLabel.Text =
                    $"目标 {currentIndex + 1}/{targets.Count}：{pendingTarget.DisplayName}\n" +
                    "可直接点其他红色敌军，或用下方按钮切换。";
            }
            else if (targets.Count > 1)
            {
                _messageLabel.Text =
                    $"射程内有 {targets.Count} 个敌人。\n请点敌军，或用上一/下一目标选择。";
            }
            else if (targets.Count == 1)
            {
                _messageLabel.Text = hasMoved
                    ? $"{selectedUnit.DisplayName} 已到达。\n射程内有 1 个敌人。"
                    : "当前有 1 个可攻击目标。\n也可以先移动。";
            }
            else
            {
                _messageLabel.Text = hasMoved
                    ? $"{selectedUnit.DisplayName} 已到达。\n当前射程内没有敌人。"
                    : "当前没有攻击目标。\n可以移动，或原地等待。";
            }
        }

        bool canAttack = targets.Count > 0 && selectedUnit.CanUseEquippedWeapon;
        if (_attackButton is not null)
        {
            _attackButton.Disabled = !canAttack;
            _attackButton.Text = pendingTarget is not null
                ? $"当前目标：{pendingTarget.DisplayName}"
                : targets.Count > 0
                    ? $"选择攻击目标（{targets.Count}）"
                    : "攻击（无目标）";
        }

        bool canCycle = canAttack && targets.Count > 1;
        if (_previousTargetButton is not null)
        {
            _previousTargetButton.Visible = targets.Count > 1;
            _previousTargetButton.Disabled = !canCycle;
        }

        if (_nextTargetButton is not null)
        {
            _nextTargetButton.Visible = targets.Count > 1;
            _nextTargetButton.Disabled = !canCycle;
        }

        if (_waitButton is not null)
        {
            _waitButton.Disabled = false;
        }
    }

    /// <summary>
    /// 单目标时直接锁定；多目标时第一次点击只进入目标选择，并允许随后循环切换。
    /// 不再把“距离最近”当作玩家不可更改的最终选择。
    /// </summary>
    private void OnAttackPressed()
    {
        if (!CanChangeTarget())
        {
            return;
        }

        UnitModel? selectedUnit = ReadSelectedUnit();
        if (selectedUnit is null)
        {
            return;
        }

        IReadOnlyList<UnitModel> targets = FindAttackableTargets(selectedUnit);
        if (targets.Count == 0)
        {
            return;
        }

        UnitModel? pendingTarget = ReadPendingTarget();
        if (pendingTarget is not null && targets.Any(target => ReferenceEquals(target, pendingTarget)))
        {
            // 已经有合法目标时不强制改成最近目标；让玩家自行确认或继续切换。
            return;
        }

        SetPendingTarget(targets[0]);
    }

    /// <summary>
    /// 从当前目标向前/向后循环选择攻击范围内敌人。
    /// 当前尚未锁定目标时，“下一目标”从第一个开始，“上一目标”从最后一个开始。
    /// </summary>
    private void SelectTargetByOffset(int offset)
    {
        if (!CanChangeTarget())
        {
            return;
        }

        UnitModel? selectedUnit = ReadSelectedUnit();
        if (selectedUnit is null)
        {
            return;
        }

        IReadOnlyList<UnitModel> targets = FindAttackableTargets(selectedUnit);
        if (targets.Count == 0)
        {
            return;
        }

        UnitModel? current = ReadPendingTarget();
        int currentIndex = current is null
            ? -1
            : targets.ToList().FindIndex(target => ReferenceEquals(target, current));

        int nextIndex;
        if (currentIndex < 0)
        {
            nextIndex = offset < 0 ? targets.Count - 1 : 0;
        }
        else
        {
            nextIndex = (currentIndex + offset) % targets.Count;
            if (nextIndex < 0)
            {
                nextIndex += targets.Count;
            }
        }

        SetPendingTarget(targets[nextIndex]);
    }

    /// <summary>调用 MainGame 的目标锁定方法，使地图边框、预测和确认按钮同步刷新。</summary>
    private void SetPendingTarget(UnitModel target)
    {
        if (_battleHost is null || _setPendingAttackTargetMethod is null)
        {
            return;
        }

        _setPendingAttackTargetMethod.Invoke(_battleHost, new object[] { target });
        _lastState = string.Empty;
    }

    /// <summary>目标选择只允许在地图人物停止移动且横向战斗演出未播放时执行。</summary>
    private bool CanChangeTarget()
    {
        return _battleHost is not null &&
               _setPendingAttackTargetMethod is not null &&
               !(_visualCoordinator?.IsMovementAnimating ?? false) &&
               !BattleAnimationBus.IsPlaybackActive;
    }

    /// <summary>
    /// 点击等待按钮时复用 MainGame 原有等待逻辑。
    /// 因此原地不移动也可以直接结束当前单位行动。
    /// </summary>
    private void OnWaitPressed()
    {
        if (_battleHost is null ||
            _waitMethod is null ||
            (_visualCoordinator?.IsMovementAnimating ?? false) ||
            BattleAnimationBus.IsPlaybackActive)
        {
            return;
        }

        _waitMethod.Invoke(_battleHost, null);
    }

    /// <summary>
    /// 查找当前单位实际可以攻击的全部存活敌人。
    /// 固定按距离、坐标和 ID 排序，使上一/下一目标的循环顺序稳定可预测。
    /// </summary>
    private IReadOnlyList<UnitModel> FindAttackableTargets(UnitModel selectedUnit)
    {
        return ReadUnits()
            .Where(unit => unit.IsAlive &&
                           unit.Team == UnitTeam.Enemy &&
                           CombatRules.CanAttack(selectedUnit, unit))
            .OrderBy(unit => CombatRules.GridDistance(selectedUnit.GridPosition, unit.GridPosition))
            .ThenBy(unit => unit.GridPosition.Y)
            .ThenBy(unit => unit.GridPosition.X)
            .ThenBy(unit => unit.Id, StringComparer.Ordinal)
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
