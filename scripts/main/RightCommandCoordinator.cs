using FlameEmblem.Game;
using FlameEmblem.Visual;
using Godot;
using System.Reflection;

namespace FlameEmblem.Main;

/// <summary>
/// 整理右侧战斗 HUD，并提供“全体进攻 / 全体待机”两种批量指令。
/// 全体进攻只使用固定规则：依次朝最近敌军移动，进入武器射程后立即攻击；没有可攻击目标时结束该单位行动。
/// </summary>
public partial class RightCommandCoordinator : Node
{
    /// <summary>当前战斗主节点。</summary>
    private Node? _battleHost;

    /// <summary>人物表现层，用于等待地图移动动画结束。</summary>
    private CharacterVisualCoordinator? _visualCoordinator;

    /// <summary>人物表现层旧战斗预测控件字段；用户不需要预测，因此本协调器持续隐藏它。</summary>
    private FieldInfo? _duelPreviewField;

    /// <summary>MainGame 当前选中人物字段。</summary>
    private FieldInfo? _selectedUnitField;

    /// <summary>MainGame 当前攻击目标字段。</summary>
    private FieldInfo? _pendingTargetField;

    /// <summary>MainGame 当前人物是否已经移动字段。</summary>
    private FieldInfo? _selectedUnitHasMovedField;

    /// <summary>MainGame 当前可达格集合字段。</summary>
    private FieldInfo? _reachableCellsField;

    /// <summary>MainGame 全部单位字段。</summary>
    private FieldInfo? _unitsField;

    /// <summary>MainGame 当前回合数字段。</summary>
    private FieldInfo? _roundField;

    /// <summary>MainGame 当前阶段字段。</summary>
    private FieldInfo? _phaseField;

    /// <summary>MainGame 右侧状态文本字段。</summary>
    private FieldInfo? _statusLabelField;

    /// <summary>MainGame 不再需要的战斗预测文本字段。</summary>
    private FieldInfo? _forecastLabelField;

    /// <summary>MainGame 战斗记录文本字段。</summary>
    private FieldInfo? _battleLogLabelField;

    /// <summary>MainGame 单位列表文本字段。</summary>
    private FieldInfo? _unitListLabelField;

    /// <summary>MainGame 确认战斗按钮字段。</summary>
    private FieldInfo? _confirmButtonField;

    /// <summary>MainGame 取消目标按钮字段。</summary>
    private FieldInfo? _cancelButtonField;

    /// <summary>MainGame 当前单位待机按钮字段。</summary>
    private FieldInfo? _waitButtonField;

    /// <summary>MainGame 结束我方回合按钮字段。</summary>
    private FieldInfo? _endTurnButtonField;

    /// <summary>MainGame 选择我方单位的方法。</summary>
    private MethodInfo? _selectPlayerUnitMethod;

    /// <summary>MainGame 设置攻击目标的方法。</summary>
    private MethodInfo? _setPendingTargetMethod;

    /// <summary>MainGame 确认攻击的方法。</summary>
    private MethodInfo? _confirmAttackMethod;

    /// <summary>MainGame 当前单位等待的方法。</summary>
    private MethodInfo? _waitMethod;

    /// <summary>MainGame 清空当前选择的方法。</summary>
    private MethodInfo? _clearSelectionMethod;

    /// <summary>MainGame 刷新 HUD 文本的方法。</summary>
    private MethodInfo? _updateHudMethod;

    /// <summary>MainGame 进入敌军回合的方法。</summary>
    private MethodInfo? _runEnemyTurnMethod;

    /// <summary>新增的全体进攻按钮。</summary>
    private Button? _groupAttackButton;

    /// <summary>新增的全体待机按钮。</summary>
    private Button? _groupWaitButton;

    /// <summary>右侧旧 HUD 是否已经完成一次性压缩。</summary>
    private bool _hudInitialized;

    /// <summary>当前是否正在执行全体进攻。</summary>
    private bool _groupAttackActive;

    /// <summary>全体进攻启动时的玩家回合号；进入下一回合后自动停止。</summary>
    private int _groupAttackRound;

    /// <summary>
    /// 刚被批量模式选中的单位。
    /// 等一帧让 PlayerMovementPassThroughCoordinator 把“友军可穿越”的真实移动范围写回 MainGame。
    /// </summary>
    private UnitModel? _preparedGroupUnit;

    /// <summary>已经移动、正在等待地图行走动画结束的批量单位。</summary>
    private UnitModel? _movingGroupUnit;

    /// <summary>
    /// 缓存 MainGame 私有成员。旧主场景尚未暴露正式只读/指令接口，因此这里继续使用过渡反射层。
    /// </summary>
    public override void _Ready()
    {
        // 晚于人物表现和复古 HUD 执行，确保本协调器最后完成隐藏预测、文本清理和批量状态推进。
        ProcessPriority = 300;
        _battleHost = GetParent();
        if (_battleHost is null)
        {
            GD.PushWarning("RightCommandCoordinator 找不到 MainGame，右侧指令不会启动。");
            SetProcess(false);
            return;
        }

        _visualCoordinator = GetNodeOrNull<CharacterVisualCoordinator>("../CharacterVisualCoordinator");
        if (_visualCoordinator is not null)
        {
            _duelPreviewField = typeof(CharacterVisualCoordinator).GetField(
                "_duelPreview",
                BindingFlags.Instance | BindingFlags.NonPublic);
        }

        Type hostType = _battleHost.GetType();
        BindingFlags members = BindingFlags.Instance | BindingFlags.NonPublic;
        _selectedUnitField = hostType.GetField("_selectedUnit", members);
        _pendingTargetField = hostType.GetField("_pendingAttackTarget", members);
        _selectedUnitHasMovedField = hostType.GetField("_selectedUnitHasMoved", members);
        _reachableCellsField = hostType.GetField("_reachableCells", members);
        _unitsField = hostType.GetField("_units", members);
        _roundField = hostType.GetField("_round", members);
        _phaseField = hostType.GetField("_phase", members);
        _statusLabelField = hostType.GetField("_statusLabel", members);
        _forecastLabelField = hostType.GetField("_forecastLabel", members);
        _battleLogLabelField = hostType.GetField("_battleLogLabel", members);
        _unitListLabelField = hostType.GetField("_unitListLabel", members);
        _confirmButtonField = hostType.GetField("_confirmAttackButton", members);
        _cancelButtonField = hostType.GetField("_cancelAttackButton", members);
        _waitButtonField = hostType.GetField("_waitButton", members);
        _endTurnButtonField = hostType.GetField("_endTurnButton", members);
        _selectPlayerUnitMethod = hostType.GetMethod("SelectPlayerUnit", members);
        _setPendingTargetMethod = hostType.GetMethod("SetPendingAttackTarget", members);
        _confirmAttackMethod = hostType.GetMethod("OnConfirmAttackPressed", members);
        _waitMethod = hostType.GetMethod("OnWaitPressed", members);
        _clearSelectionMethod = hostType.GetMethod("ClearSelection", members);
        _updateHudMethod = hostType.GetMethod("UpdateHud", members);
        _runEnemyTurnMethod = hostType.GetMethod("RunEnemyTurn", members);

        if (_selectedUnitField is null ||
            _pendingTargetField is null ||
            _selectedUnitHasMovedField is null ||
            _reachableCellsField is null ||
            _unitsField is null ||
            _roundField is null ||
            _phaseField is null ||
            _selectPlayerUnitMethod is null ||
            _setPendingTargetMethod is null ||
            _confirmAttackMethod is null ||
            _waitMethod is null ||
            _clearSelectionMethod is null ||
            _runEnemyTurnMethod is null)
        {
            GD.PushWarning("RightCommandCoordinator 无法读取 MainGame 的必要状态，右侧批量指令已停用。");
            SetProcess(false);
        }
    }

    /// <summary>整理右侧 HUD、隐藏旧预测，并逐帧推进全体进攻状态机。</summary>
    public override void _Process(double delta)
    {
        if (!_hudInitialized)
        {
            _hudInitialized = TryInitializeRightHud();
        }

        SuppressLegacyBattlePreview();
        CleanRightHudText();
        RefreshGroupButtons();
        AdvanceGroupAttack();
    }

    /// <summary>
    /// 隐藏战斗预测、压缩日志/单位列表，并把批量指令放进右侧面板可视范围。
    /// </summary>
    private bool TryInitializeRightHud()
    {
        Label? forecast = ReadControl<Label>(_forecastLabelField);
        Label? battleLog = ReadControl<Label>(_battleLogLabelField);
        Label? unitList = ReadControl<Label>(_unitListLabelField);
        Button? confirm = ReadControl<Button>(_confirmButtonField);
        Button? cancel = ReadControl<Button>(_cancelButtonField);
        Button? wait = ReadControl<Button>(_waitButtonField);
        Button? endTurn = ReadControl<Button>(_endTurnButtonField);

        // MainGame 在自己的 _Ready 中动态创建这些控件；还没创建时下一帧继续等待。
        if (forecast is null || battleLog is null || unitList is null ||
            confirm is null || cancel is null || wait is null || endTurn is null)
        {
            return false;
        }

        // 用户不需要战斗预测，所以完全隐藏并释放它强制占用的 110px 高度。
        forecast.Visible = false;
        forecast.CustomMinimumSize = Vector2.Zero;

        // 保留战斗记录和单位状态，但收紧固定高度，避免确认战斗下方按钮被 VBox 挤出 720p 画面。
        battleLog.CustomMinimumSize = new Vector2(360, 72);
        unitList.CustomMinimumSize = new Vector2(360, 112);
        confirm.CustomMinimumSize = new Vector2(0, 32);
        cancel.CustomMinimumSize = new Vector2(0, 32);
        wait.CustomMinimumSize = new Vector2(0, 32);
        endTurn.CustomMinimumSize = new Vector2(0, 32);

        // 右侧全部使用普通中文和 ASCII，避免系统字体缺少装饰字形时出现方框/乱码。
        confirm.Text = "确认战斗";
        cancel.Text = "取消目标";
        wait.Text = "当前单位待机";
        endTurn.Text = "结束我方回合";

        if (endTurn.GetParent() is not VBoxContainer column)
        {
            GD.PushWarning("RightCommandCoordinator 找不到右侧按钮列，无法加入全体指令。");
            return false;
        }

        HBoxContainer groupRow = new()
        {
            CustomMinimumSize = new Vector2(360, 36),
            Alignment = BoxContainer.AlignmentMode.Center
        };
        column.AddChild(groupRow);

        _groupAttackButton = new Button
        {
            Text = "全体进攻",
            CustomMinimumSize = new Vector2(174, 32),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _groupAttackButton.Pressed += StartGroupAttack;
        StyleGroupButton(
            _groupAttackButton,
            new Color(0.38f, 0.28f, 0.17f),
            new Color(0.78f, 0.58f, 0.27f));
        groupRow.AddChild(_groupAttackButton);

        _groupWaitButton = new Button
        {
            Text = "全体待机",
            CustomMinimumSize = new Vector2(174, 32),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _groupWaitButton.Pressed += ExecuteGroupWait;
        StyleGroupButton(
            _groupWaitButton,
            new Color(0.22f, 0.34f, 0.31f),
            new Color(0.44f, 0.64f, 0.50f));
        groupRow.AddChild(_groupWaitButton);

        return true;
    }

    /// <summary>旧的地图战斗预测面板不再显示，也不再阻挡地图输入。</summary>
    private void SuppressLegacyBattlePreview()
    {
        if (_visualCoordinator is null || _duelPreviewField is null)
        {
            return;
        }

        if (_duelPreviewField.GetValue(_visualCoordinator) is not BattleDuelPreviewControl preview)
        {
            return;
        }

        preview.Visible = false;
        preview.MouseFilter = Control.MouseFilterEnum.Ignore;
    }

    /// <summary>清理右侧标签中的装饰字符和旧“战斗预测”用词。</summary>
    private void CleanRightHudText()
    {
        CleanLabel(ReadControl<Label>(_statusLabelField));
        CleanLabel(ReadControl<Label>(_battleLogLabelField));
        CleanLabel(ReadControl<Label>(_unitListLabelField));
    }

    /// <summary>只使用普通中文/ASCII 字符，降低缺字体时出现乱码方框的概率。</summary>
    private static void CleanLabel(Label? label)
    {
        if (label is null || string.IsNullOrEmpty(label.Text))
        {
            return;
        }

        string cleaned = label.Text
            .Replace("●", "存活")
            .Replace("×", "倒下")
            .Replace("→", "攻击")
            .Replace("—", "-")
            .Replace("·", " ")
            .Replace("确认预测后点击“确认攻击”", "点击“确认战斗”开始战斗")
            .Replace("确认攻击", "确认战斗")
            .Replace("战斗预测", "战斗目标");

        if (!cleaned.Equals(label.Text, StringComparison.Ordinal))
        {
            label.Text = cleaned;
        }
    }

    /// <summary>刷新全体按钮，并在批量进攻期间锁住手动操作按钮防止状态互相覆盖。</summary>
    private void RefreshGroupButtons()
    {
        if (_groupAttackButton is null || _groupWaitButton is null)
        {
            return;
        }

        bool playerPhase = IsPlayerPhase();
        bool busy = BattleAnimationBus.IsPlaybackActive ||
                    (_visualCoordinator?.IsMovementAnimating ?? false);
        int remaining = ReadUnits().Count(unit =>
            unit.IsAlive && unit.Team == UnitTeam.Player && !unit.HasActed);

        _groupAttackButton.Disabled = !playerPhase || busy || remaining == 0 || _groupAttackActive;
        _groupWaitButton.Disabled = !playerPhase || busy || remaining == 0 || _groupAttackActive;
        _groupAttackButton.Text = _groupAttackActive ? "全体进攻中" : $"全体进攻 ({remaining})";
        _groupWaitButton.Text = $"全体待机 ({remaining})";

        if (_groupAttackActive)
        {
            SetDisabled(_confirmButtonField, true);
            SetDisabled(_cancelButtonField, true);
            SetDisabled(_waitButtonField, true);
            SetDisabled(_endTurnButtonField, true);
        }
    }

    /// <summary>启动全体进攻：清除手动选择，从第一名未行动我方开始依次执行。</summary>
    private void StartGroupAttack()
    {
        if (_battleHost is null ||
            _clearSelectionMethod is null ||
            !IsPlayerPhase() ||
            BattleAnimationBus.IsPlaybackActive ||
            (_visualCoordinator?.IsMovementAnimating ?? false))
        {
            return;
        }

        _clearSelectionMethod.Invoke(_battleHost, null);
        _groupAttackRound = ReadRound();
        _preparedGroupUnit = null;
        _movingGroupUnit = null;
        _groupAttackActive = true;
        InvokeHudMessage("全体进攻：我方将依次朝敌军推进，进入射程后自动攻击。");
    }

    /// <summary>全体待机：所有剩余我方结束行动，然后按原规则进入敌军回合。</summary>
    private void ExecuteGroupWait()
    {
        if (_battleHost is null ||
            _clearSelectionMethod is null ||
            _runEnemyTurnMethod is null ||
            !IsPlayerPhase() ||
            BattleAnimationBus.IsPlaybackActive ||
            (_visualCoordinator?.IsMovementAnimating ?? false))
        {
            return;
        }

        StopGroupAttack();
        _clearSelectionMethod.Invoke(_battleHost, null);

        foreach (UnitModel player in ReadUnits().Where(unit =>
                     unit.IsAlive && unit.Team == UnitTeam.Player && !unit.HasActed))
        {
            player.HasActed = true;
        }

        InvokeHudMessage("全体待机：我方剩余单位结束本回合行动。");
        _runEnemyTurnMethod.Invoke(_battleHost, null);
    }

    /// <summary>
    /// 逐人推进全体进攻：选人 -> 等真实移动范围 -> 移动 -> 等行走动画 -> 攻击/待机 -> 下一人。
    /// </summary>
    private void AdvanceGroupAttack()
    {
        if (!_groupAttackActive || _battleHost is null)
        {
            return;
        }

        // 最后一名玩家行动可能触发 MainGame 自动进入敌军回合；阶段或回合变化后立即退出批量模式。
        if (!IsPlayerPhase() || ReadRound() != _groupAttackRound)
        {
            StopGroupAttack();
            return;
        }

        if (BattleAnimationBus.IsPlaybackActive || (_visualCoordinator?.IsMovementAnimating ?? false))
        {
            return;
        }

        // 地图移动完成后，再判断移动终点有没有敌军进入射程。
        if (_movingGroupUnit is not null)
        {
            UnitModel movedUnit = _movingGroupUnit;
            _movingGroupUnit = null;
            FinishGroupUnitAction(movedUnit);
            return;
        }

        // 选人后的下一帧，友军穿越协调器已经把正确移动范围写回 MainGame，此时再决定移动终点。
        if (_preparedGroupUnit is not null)
        {
            UnitModel preparedUnit = _preparedGroupUnit;
            _preparedGroupUnit = null;
            PrepareMovementOrAttack(preparedUnit);
            return;
        }

        UnitModel? nextPlayer = ReadUnits()
            .Where(unit => unit.IsAlive && unit.Team == UnitTeam.Player && !unit.HasActed)
            .OrderBy(unit => unit.Id, StringComparer.Ordinal)
            .FirstOrDefault();

        if (nextPlayer is null)
        {
            StopGroupAttack();
            return;
        }

        _selectPlayerUnitMethod?.Invoke(_battleHost, new object[] { nextPlayer });
        _preparedGroupUnit = nextPlayer;
    }

    /// <summary>
    /// 如果当前已经有敌人在射程就直接攻击；否则从可达格中选择最接近敌军的合法终点并移动。
    /// </summary>
    private void PrepareMovementOrAttack(UnitModel unit)
    {
        if (!unit.IsAlive || unit.HasActed || !IsPlayerPhase())
        {
            return;
        }

        if (TryAttackFromCurrentPosition(unit))
        {
            return;
        }

        IReadOnlyList<UnitModel> enemies = LivingEnemies();
        if (enemies.Count == 0)
        {
            StopGroupAttack();
            return;
        }

        HashSet<Vector2I> reachable = ReadReachableCells();
        Vector2I destination = ChooseAdvanceDestination(unit, reachable, enemies);
        if (destination == unit.GridPosition)
        {
            FinishGroupUnitAction(unit);
            return;
        }

        unit.GridPosition = destination;
        _selectedUnitHasMovedField?.SetValue(_battleHost, true);
        _pendingTargetField?.SetValue(_battleHost, null);
        _reachableCellsField?.SetValue(_battleHost, new HashSet<Vector2I> { destination });
        _movingGroupUnit = unit;

        if (_battleHost is CanvasItem canvasItem)
        {
            canvasItem.QueueRedraw();
        }

        InvokeHudMessage($"全体进攻：{unit.DisplayName} 向敌军推进。");
    }

    /// <summary>移动结束以后，有敌军进入射程就攻击，否则该单位待机。</summary>
    private void FinishGroupUnitAction(UnitModel unit)
    {
        if (!unit.IsAlive || unit.HasActed || !IsPlayerPhase())
        {
            return;
        }

        if (TryAttackFromCurrentPosition(unit))
        {
            return;
        }

        _waitMethod?.Invoke(_battleHost, null);
    }

    /// <summary>按距离、剩余 HP、ID 的固定顺序选择射程内敌军并立即确认战斗。</summary>
    private bool TryAttackFromCurrentPosition(UnitModel unit)
    {
        if (_battleHost is null || !unit.CanUseEquippedWeapon)
        {
            return false;
        }

        UnitModel? target = ReadUnits()
            .Where(enemy => enemy.IsAlive &&
                            enemy.Team == UnitTeam.Enemy &&
                            CombatRules.CanAttack(unit, enemy))
            .OrderBy(enemy => CombatRules.GridDistance(unit.GridPosition, enemy.GridPosition))
            .ThenBy(enemy => enemy.CurrentHp)
            .ThenBy(enemy => enemy.Id, StringComparer.Ordinal)
            .FirstOrDefault();

        if (target is null)
        {
            return false;
        }

        _setPendingTargetMethod?.Invoke(_battleHost, new object[] { target });
        _confirmAttackMethod?.Invoke(_battleHost, null);
        return true;
    }

    /// <summary>
    /// 选择“向敌军方向移动”的终点。
    /// 优先能在移动后进入任意敌军射程的格；否则选择到最近敌军曼哈顿距离最小的格。
    /// </summary>
    private static Vector2I ChooseAdvanceDestination(
        UnitModel unit,
        IEnumerable<Vector2I> reachable,
        IReadOnlyList<UnitModel> enemies)
    {
        List<Vector2I> candidates = reachable.Distinct().ToList();
        if (!candidates.Contains(unit.GridPosition))
        {
            candidates.Add(unit.GridPosition);
        }

        int minRange = unit.EquippedWeapon.MinRange;
        int maxRange = unit.EquippedWeapon.MaxRange;

        return candidates
            .OrderBy(cell => CanAttackAnyFromCell(cell, enemies, minRange, maxRange) ? 0 : 1)
            .ThenBy(cell => enemies.Min(enemy => CombatRules.GridDistance(cell, enemy.GridPosition)))
            .ThenByDescending(cell => CombatRules.GridDistance(unit.GridPosition, cell))
            .ThenBy(cell => cell.Y)
            .ThenBy(cell => cell.X)
            .First();
    }

    /// <summary>判断站在指定格时是否有任意敌军处于当前武器射程。</summary>
    private static bool CanAttackAnyFromCell(
        Vector2I cell,
        IReadOnlyList<UnitModel> enemies,
        int minRange,
        int maxRange)
    {
        return enemies.Any(enemy =>
        {
            int distance = CombatRules.GridDistance(cell, enemy.GridPosition);
            return distance >= minRange && distance <= maxRange;
        });
    }

    /// <summary>停止批量模式并清理尚未完成的阶段引用。</summary>
    private void StopGroupAttack()
    {
        _groupAttackActive = false;
        _preparedGroupUnit = null;
        _movingGroupUnit = null;
    }

    /// <summary>读取全部单位。</summary>
    private IReadOnlyList<UnitModel> ReadUnits()
    {
        if (_battleHost is null || _unitsField is null)
        {
            return Array.Empty<UnitModel>();
        }

        return _unitsField.GetValue(_battleHost) as IReadOnlyList<UnitModel>
               ?? Array.Empty<UnitModel>();
    }

    /// <summary>读取全部存活敌军。</summary>
    private IReadOnlyList<UnitModel> LivingEnemies()
    {
        return ReadUnits()
            .Where(unit => unit.IsAlive && unit.Team == UnitTeam.Enemy)
            .ToList();
    }

    /// <summary>读取友军穿越规则已经计算好的当前可达格。</summary>
    private HashSet<Vector2I> ReadReachableCells()
    {
        if (_battleHost is null || _reachableCellsField is null)
        {
            return new HashSet<Vector2I>();
        }

        return _reachableCellsField.GetValue(_battleHost) as HashSet<Vector2I>
               ?? new HashSet<Vector2I>();
    }

    /// <summary>判断当前是否仍处于玩家阶段。</summary>
    private bool IsPlayerPhase()
    {
        return _battleHost is not null &&
               (_phaseField?.GetValue(_battleHost)?.ToString() ?? string.Empty) == "Player";
    }

    /// <summary>读取当前玩家回合号。</summary>
    private int ReadRound()
    {
        return _battleHost is not null && _roundField?.GetValue(_battleHost) is int round
            ? round
            : 0;
    }

    /// <summary>让 MainGame 使用原有 HUD 刷新流程显示批量指令提示。</summary>
    private void InvokeHudMessage(string message)
    {
        if (_battleHost is not null && _updateHudMethod is not null)
        {
            _updateHudMethod.Invoke(_battleHost, new object[] { message });
        }
    }

    /// <summary>读取一个 MainGame Control 字段。</summary>
    private T? ReadControl<T>(FieldInfo? field) where T : Control
    {
        return _battleHost is null || field is null
            ? null
            : field.GetValue(_battleHost) as T;
    }

    /// <summary>需要时覆盖旧按钮 Disabled 状态。</summary>
    private void SetDisabled(FieldInfo? field, bool disabled)
    {
        Button? button = ReadControl<Button>(field);
        if (button is not null)
        {
            button.Disabled = disabled;
        }
    }

    /// <summary>给批量按钮应用与现有复古 HUD 一致的无圆角样式。</summary>
    private static void StyleGroupButton(Button button, Color background, Color border)
    {
        button.AddThemeStyleboxOverride("normal", MakeButtonStyle(background, border));
        button.AddThemeStyleboxOverride("hover", MakeButtonStyle(background.Lightened(0.10f), border.Lightened(0.12f)));
        button.AddThemeStyleboxOverride("pressed", MakeButtonStyle(background.Darkened(0.12f), border.Lightened(0.18f)));
        button.AddThemeStyleboxOverride("disabled", MakeButtonStyle(background.Darkened(0.46f), border.Darkened(0.38f)));
        button.AddThemeColorOverride("font_color", new Color(0.96f, 0.93f, 0.83f));
        button.AddThemeColorOverride("font_disabled_color", new Color(0.48f, 0.50f, 0.48f));
        button.AddThemeFontSizeOverride("font_size", 13);
    }

    /// <summary>创建批量按钮的硬边背景/边框。</summary>
    private static StyleBoxFlat MakeButtonStyle(Color background, Color border)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2
        };
    }
}
