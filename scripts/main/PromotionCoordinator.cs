using FlameEmblem.Game;
using FlameEmblem.Visual;
using Godot;
using System.Reflection;

namespace FlameEmblem.Main;

/// <summary>
/// 在当前战斗原型中提供可测试的玩家转职入口。
/// 真正转职规则来自 PromotionCatalog；本协调器只负责按钮状态、安全时机和完成行动后的 HUD 反馈。
/// </summary>
public partial class PromotionCoordinator : Node
{
    /// <summary>当前战斗主节点。</summary>
    private Node? _battleHost;

    /// <summary>地图人物表现层，用于禁止移动动画期间转职。</summary>
    private CharacterVisualCoordinator? _visualCoordinator;

    /// <summary>右侧批量指令协调器，用于禁止全体进攻期间转职。</summary>
    private RightCommandCoordinator? _rightCommandCoordinator;

    /// <summary>MainGame 当前选中单位字段。</summary>
    private FieldInfo? _selectedUnitField;

    /// <summary>MainGame 当前选中单位是否已经移动字段。</summary>
    private FieldInfo? _selectedUnitHasMovedField;

    /// <summary>MainGame 当前回合阶段字段。</summary>
    private FieldInfo? _phaseField;

    /// <summary>MainGame 结束回合按钮字段，用来找到右侧 VBox。</summary>
    private FieldInfo? _endTurnButtonField;

    /// <summary>MainGame 完成当前单位行动的方法。</summary>
    private MethodInfo? _finishSelectedUnitActionMethod;

    /// <summary>MainGame 更新 HUD 的方法。</summary>
    private MethodInfo? _updateHudMethod;

    /// <summary>RightCommandCoordinator 当前是否正在全体进攻字段。</summary>
    private FieldInfo? _groupAttackActiveField;

    /// <summary>右侧转职按钮。</summary>
    private Button? _promotionButton;

    /// <summary>按钮是否已经成功加入右侧 HUD。</summary>
    private bool _uiInitialized;

    /// <summary>缓存主场景接口；实际转职始终通过公开游戏规则类执行。</summary>
    public override void _Ready()
    {
        // 排在批量指令之后、存档按钮之前，使右侧命令顺序保持“行动 -> 转职 -> 存档”。
        ProcessPriority = 310;
        _battleHost = GetParent();
        _visualCoordinator = GetNodeOrNull<CharacterVisualCoordinator>("../CharacterVisualCoordinator");
        _rightCommandCoordinator = GetNodeOrNull<RightCommandCoordinator>("../RightCommandCoordinator");

        if (_battleHost is null)
        {
            GD.PushWarning("PromotionCoordinator 找不到 MainGame，转职入口不会启动。");
            SetProcess(false);
            return;
        }

        BindingFlags members = BindingFlags.Instance | BindingFlags.NonPublic;
        Type hostType = _battleHost.GetType();
        _selectedUnitField = hostType.GetField("_selectedUnit", members);
        _selectedUnitHasMovedField = hostType.GetField("_selectedUnitHasMoved", members);
        _phaseField = hostType.GetField("_phase", members);
        _endTurnButtonField = hostType.GetField("_endTurnButton", members);
        _finishSelectedUnitActionMethod = hostType.GetMethod("FinishSelectedUnitAction", members);
        _updateHudMethod = hostType.GetMethod("UpdateHud", members);

        if (_rightCommandCoordinator is not null)
        {
            _groupAttackActiveField = typeof(RightCommandCoordinator).GetField(
                "_groupAttackActive",
                BindingFlags.Instance | BindingFlags.NonPublic);
        }

        if (_selectedUnitField is null ||
            _selectedUnitHasMovedField is null ||
            _phaseField is null ||
            _endTurnButtonField is null ||
            _finishSelectedUnitActionMethod is null ||
            _updateHudMethod is null)
        {
            GD.PushWarning("PromotionCoordinator 无法读取 MainGame 的必要状态，转职入口已停用。");
            SetProcess(false);
        }
    }

    /// <summary>等待动态 HUD 创建按钮，并持续刷新当前角色的转职条件。</summary>
    public override void _Process(double delta)
    {
        if (!_uiInitialized)
        {
            _uiInitialized = TryCreatePromotionButton();
        }

        RefreshPromotionButton();
    }

    /// <summary>把转职按钮追加到右侧主操作列。</summary>
    private bool TryCreatePromotionButton()
    {
        if (_battleHost is null ||
            _endTurnButtonField?.GetValue(_battleHost) is not Button endTurnButton ||
            endTurnButton.GetParent() is not VBoxContainer column)
        {
            return false;
        }

        _promotionButton = new Button
        {
            Name = "PromotionButton",
            Text = "转职",
            CustomMinimumSize = new Vector2(360, 32),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _promotionButton.Pressed += ExecutePromotion;
        column.AddChild(_promotionButton);
        return true;
    }

    /// <summary>按当前选择、等级和动画状态刷新按钮文案与可用状态。</summary>
    private void RefreshPromotionButton()
    {
        if (_promotionButton is null)
        {
            return;
        }

        UnitModel? unit = ReadSelectedUnit();
        if (unit is null)
        {
            _promotionButton.Text = "转职（先选择角色）";
            _promotionButton.Disabled = true;
            return;
        }

        PromotionRule? rule = PromotionCatalog.TryGetRule(unit);
        if (rule is null)
        {
            _promotionButton.Text = $"{unit.ClassDefinition.DisplayName}：无后续转职";
            _promotionButton.Disabled = true;
            return;
        }

        bool hasMoved = ReadSelectedUnitHasMoved();
        bool busy = BattleAnimationBus.IsPlaybackActive ||
                    (_visualCoordinator?.IsMovementAnimating ?? false) ||
                    IsGroupAttackActive();
        bool eligible = IsPlayerPhase() &&
                        !unit.HasActed &&
                        !hasMoved &&
                        rule.CanPromote(unit) &&
                        !busy;

        _promotionButton.Text = unit.Level >= rule.MinimumLevel
            ? $"转职：{rule.TargetClass.DisplayName}"
            : $"{rule.TargetClass.DisplayName} 需要 Lv.{rule.MinimumLevel}";
        _promotionButton.Disabled = !eligible;
    }

    /// <summary>执行当前角色转职，并把它作为本回合的一次完整行动结束。</summary>
    private void ExecutePromotion()
    {
        UnitModel? unit = ReadSelectedUnit();
        if (unit is null || !CanPromoteNow(unit, out PromotionRule? rule, out string reason) || rule is null)
        {
            ShowMessage(reason);
            return;
        }

        PromotionResult result = unit.PromoteTo(
            rule.TargetClass,
            rule.StatFloor,
            rule.ResetLevel);

        string statText = result.StatChanges.Count == 0
            ? "属性均已达到新职业基准"
            : string.Join("、", result.StatChanges);
        string levelText = rule.ResetLevel ? "等级重置为 Lv.1，EXP 归零" : $"保持 Lv.{unit.Level}";
        string message =
            $"{unit.DisplayName}：{result.PreviousClassName} -> {result.NewClassName}。" +
            $"{levelText}。{statText}。";

        // 战斗中的测试入口把转职视为一次完整行动；未来祠堂场景会直接复用规则层而不调用这个回合方法。
        _finishSelectedUnitActionMethod?.Invoke(_battleHost, new object[] { message });
    }

    /// <summary>完整检查一次点击时的转职安全条件。</summary>
    private bool CanPromoteNow(UnitModel unit, out PromotionRule? rule, out string reason)
    {
        rule = PromotionCatalog.TryGetRule(unit);
        if (!IsPlayerPhase())
        {
            reason = "只能在我方回合执行转职。";
            return false;
        }

        if (unit.HasActed)
        {
            reason = $"{unit.DisplayName} 本回合已经行动。";
            return false;
        }

        if (ReadSelectedUnitHasMoved())
        {
            reason = "转职必须在当前角色移动前进行。";
            return false;
        }

        if (BattleAnimationBus.IsPlaybackActive || (_visualCoordinator?.IsMovementAnimating ?? false))
        {
            reason = "人物移动或战斗演出进行中，暂时不能转职。";
            return false;
        }

        if (IsGroupAttackActive())
        {
            reason = "全体进攻执行中，暂时不能转职。";
            return false;
        }

        if (rule is null)
        {
            reason = PromotionCatalog.RequirementText(unit);
            return false;
        }

        if (!rule.CanPromote(unit))
        {
            reason = PromotionCatalog.RequirementText(unit);
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>读取当前选中的玩家单位。</summary>
    private UnitModel? ReadSelectedUnit()
    {
        return _battleHost is not null
            ? _selectedUnitField?.GetValue(_battleHost) as UnitModel
            : null;
    }

    /// <summary>读取当前选择是否已经完成移动。</summary>
    private bool ReadSelectedUnitHasMoved()
    {
        return _battleHost is not null &&
               _selectedUnitHasMovedField?.GetValue(_battleHost) is bool moved &&
               moved;
    }

    /// <summary>读取当前是否处于玩家阶段。</summary>
    private bool IsPlayerPhase()
    {
        return _battleHost is not null &&
               (_phaseField?.GetValue(_battleHost)?.ToString() ?? string.Empty).Equals(
                   "Player",
                   StringComparison.Ordinal);
    }

    /// <summary>读取全体进攻状态。</summary>
    private bool IsGroupAttackActive()
    {
        return _rightCommandCoordinator is not null &&
               _groupAttackActiveField?.GetValue(_rightCommandCoordinator) is bool active &&
               active;
    }

    /// <summary>通过 MainGame 现有 HUD 更新入口显示无法转职的原因。</summary>
    private void ShowMessage(string message)
    {
        if (_battleHost is null || _updateHudMethod is null || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        _updateHudMethod.Invoke(_battleHost, new object[] { message });
    }
}
