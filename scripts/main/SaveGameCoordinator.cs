using FlameEmblem.Game;
using FlameEmblem.Visual;
using Godot;
using System.Reflection;

namespace FlameEmblem.Main;

/// <summary>
/// 为当前单章节原型提供本地“保存进度 / 读取进度”入口。
/// 存档本身由 SaveGameService 负责；本协调器只负责从 MainGame 取得运行时快照、恢复状态和创建右侧按钮。
/// </summary>
public partial class SaveGameCoordinator : Node
{
    /// <summary>当前战斗主节点。</summary>
    private Node? _battleHost;

    /// <summary>地图人物表现协调器，用于判断移动忙碌状态和读档后清除视觉移动缓存。</summary>
    private CharacterVisualCoordinator? _visualCoordinator;

    /// <summary>右侧批量指令协调器；全体进攻期间禁止保存/读取。</summary>
    private RightCommandCoordinator? _rightCommandCoordinator;

    /// <summary>MainGame 全部单位字段。</summary>
    private FieldInfo? _unitsField;

    /// <summary>MainGame 当前回合数字段。</summary>
    private FieldInfo? _roundField;

    /// <summary>MainGame 当前阶段字段。</summary>
    private FieldInfo? _phaseField;

    /// <summary>MainGame 当前选中单位字段；保存只允许发生在完整行动边界。</summary>
    private FieldInfo? _selectedUnitField;

    /// <summary>MainGame 最近战斗记录字段。</summary>
    private FieldInfo? _lastBattleLogField;

    /// <summary>MainGame 地图宽度字段，用于验证损坏存档中的坐标。</summary>
    private FieldInfo? _gridWidthField;

    /// <summary>MainGame 地图高度字段，用于验证损坏存档中的坐标。</summary>
    private FieldInfo? _gridHeightField;

    /// <summary>MainGame 结束回合按钮字段，用来定位右侧 VBox。</summary>
    private FieldInfo? _endTurnButtonField;

    /// <summary>MainGame 清除当前选择的方法。</summary>
    private MethodInfo? _clearSelectionMethod;

    /// <summary>MainGame 清理敌军逐单位状态机临时数据的方法。</summary>
    private MethodInfo? _resetEnemyTurnSequenceMethod;

    /// <summary>MainGame 统一刷新 HUD 的方法。</summary>
    private MethodInfo? _updateHudMethod;

    /// <summary>RightCommandCoordinator 的全体进攻状态字段。</summary>
    private FieldInfo? _groupAttackActiveField;

    /// <summary>保存按钮。</summary>
    private Button? _saveButton;

    /// <summary>读取按钮。</summary>
    private Button? _loadButton;

    /// <summary>右侧保存/读取行是否已经成功创建。</summary>
    private bool _uiInitialized;

    /// <summary>当前基础版本只支持这一张章节存档；多章节系统加入后改为从章节管理器读取。</summary>
    private const string CurrentChapterId = "chapter_01";

    /// <summary>缓存 MainGame 的必要接口，并等待右侧 HUD 完成创建。</summary>
    public override void _Ready()
    {
        // 晚于 RightCommandCoordinator 的 HUD 整理，保证保存按钮永远追加在全体指令之后。
        ProcessPriority = 320;
        _battleHost = GetParent();
        _visualCoordinator = GetNodeOrNull<CharacterVisualCoordinator>("../CharacterVisualCoordinator");
        _rightCommandCoordinator = GetNodeOrNull<RightCommandCoordinator>("../RightCommandCoordinator");

        if (_battleHost is null)
        {
            GD.PushWarning("SaveGameCoordinator 找不到 MainGame，本地存档功能不会启动。");
            SetProcess(false);
            return;
        }

        Type hostType = _battleHost.GetType();
        BindingFlags members = BindingFlags.Instance | BindingFlags.NonPublic;
        _unitsField = hostType.GetField("_units", members);
        _roundField = hostType.GetField("_round", members);
        _phaseField = hostType.GetField("_phase", members);
        _selectedUnitField = hostType.GetField("_selectedUnit", members);
        _lastBattleLogField = hostType.GetField("_lastBattleLog", members);
        _gridWidthField = hostType.GetField("_gridWidth", members);
        _gridHeightField = hostType.GetField("_gridHeight", members);
        _endTurnButtonField = hostType.GetField("_endTurnButton", members);
        _clearSelectionMethod = hostType.GetMethod("ClearSelection", members);
        _resetEnemyTurnSequenceMethod = hostType.GetMethod("ResetEnemyTurnSequence", members);
        _updateHudMethod = hostType.GetMethod("UpdateHud", members);

        if (_rightCommandCoordinator is not null)
        {
            _groupAttackActiveField = typeof(RightCommandCoordinator).GetField(
                "_groupAttackActive",
                BindingFlags.Instance | BindingFlags.NonPublic);
        }

        if (_unitsField is null ||
            _roundField is null ||
            _phaseField is null ||
            _selectedUnitField is null ||
            _lastBattleLogField is null ||
            _gridWidthField is null ||
            _gridHeightField is null ||
            _endTurnButtonField is null ||
            _clearSelectionMethod is null ||
            _resetEnemyTurnSequenceMethod is null ||
            _updateHudMethod is null)
        {
            GD.PushWarning("SaveGameCoordinator 无法读取 MainGame 的必要成员，本地存档功能已停用。");
            SetProcess(false);
        }
    }

    /// <summary>等待 HUD 创建保存按钮，并持续刷新按钮可用状态。</summary>
    public override void _Process(double delta)
    {
        if (!_uiInitialized)
        {
            _uiInitialized = TryCreateSaveButtons();
        }

        RefreshButtonStates();
    }

    /// <summary>在右侧指令列底部加入单槽位保存/读取按钮。</summary>
    private bool TryCreateSaveButtons()
    {
        if (_battleHost is null ||
            _endTurnButtonField?.GetValue(_battleHost) is not Button endTurnButton ||
            endTurnButton.GetParent() is not VBoxContainer column)
        {
            // MainGame 的动态 HUD 还没创建完成，下一帧继续等待。
            return false;
        }

        HBoxContainer saveRow = new()
        {
            Name = "SaveLoadRow",
            CustomMinimumSize = new Vector2(360, 36),
            Alignment = BoxContainer.AlignmentMode.Center
        };
        column.AddChild(saveRow);

        _saveButton = new Button
        {
            Text = "保存进度",
            CustomMinimumSize = new Vector2(174, 32),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _saveButton.Pressed += SaveCurrentProgress;
        saveRow.AddChild(_saveButton);

        _loadButton = new Button
        {
            Text = "读取进度",
            CustomMinimumSize = new Vector2(174, 32),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _loadButton.Pressed += LoadCurrentProgress;
        saveRow.AddChild(_loadButton);
        return true;
    }

    /// <summary>根据当前回合、动画和批量指令状态决定能否保存/读取。</summary>
    private void RefreshButtonStates()
    {
        if (_saveButton is null || _loadButton is null)
        {
            return;
        }

        bool playerPhase = IsPlayerPhase();
        bool busy = BattleAnimationBus.IsPlaybackActive ||
                    (_visualCoordinator?.IsMovementAnimating ?? false) ||
                    IsGroupAttackActive();
        bool hasSelection = _battleHost is not null && _selectedUnitField?.GetValue(_battleHost) is UnitModel;

        // 保存必须位于一次完整单位行动的边界，否则不保存“已经移动但尚未待机/攻击”的半行动状态。
        _saveButton.Disabled = !playerPhase || busy || hasSelection;
        _loadButton.Disabled = !playerPhase || busy || !SaveGameService.HasSave;
        _loadButton.Text = SaveGameService.HasSave ? "读取进度" : "读取进度（无存档）";
    }

    /// <summary>创建当前战场快照并写入 user:// 单槽位。</summary>
    private void SaveCurrentProgress()
    {
        if (!CanOperateSaveSystem(requireActionBoundary: true, out string reason))
        {
            ShowMessage(reason);
            return;
        }

        SaveGameData data = new()
        {
            ChapterId = CurrentChapterId,
            Round = ReadRound(),
            LastBattleLog = ReadLastBattleLog(),
            Units = ReadUnits().Select(CreateUnitSnapshot).ToList()
        };

        SaveGameService.TrySave(data, out string message);
        ShowMessage(message);
    }

    /// <summary>读取本地单槽位，完整验证后一次性恢复全部单位状态。</summary>
    private void LoadCurrentProgress()
    {
        if (!CanOperateSaveSystem(requireActionBoundary: false, out string reason))
        {
            ShowMessage(reason);
            return;
        }

        if (!SaveGameService.TryLoad(out SaveGameData? data, out string readMessage) || data is null)
        {
            ShowMessage(readMessage);
            return;
        }

        if (!TryValidateSave(data, out List<ValidatedUnitRestore> restores, out string validationMessage))
        {
            ShowMessage(validationMessage);
            return;
        }

        // 所有 ID、职业、装备和坐标已经先验证完，这里才开始真正修改运行时对象，避免半读档状态。
        foreach (ValidatedUnitRestore restore in restores)
        {
            UnitSaveData state = restore.State;
            restore.Unit.RestoreRuntimeState(
                new Vector2I(state.X, state.Y),
                restore.Weapon,
                state.Level,
                state.Experience,
                state.MaxHp,
                state.CurrentHp,
                state.Strength,
                state.Magic,
                state.Skill,
                state.Speed,
                state.Luck,
                state.Defense,
                state.Resistance,
                state.HasActed);
        }

        if (_battleHost is not null)
        {
            _roundField?.SetValue(_battleHost, Math.Max(1, data.Round));
            _lastBattleLogField?.SetValue(
                _battleHost,
                string.IsNullOrWhiteSpace(data.LastBattleLog) ? "尚未发生战斗。" : data.LastBattleLog);
            _resetEnemyTurnSequenceMethod?.Invoke(_battleHost, null);
            _clearSelectionMethod?.Invoke(_battleHost, null);
        }

        SnapMapCharactersToLogicalPositions();
        ShowMessage($"已读取槽位 1。第 {Math.Max(1, data.Round)} 回合继续。");

        if (_battleHost is CanvasItem canvasItem)
        {
            canvasItem.QueueRedraw();
        }
    }

    /// <summary>把一个运行时单位转换成纯数据存档快照。</summary>
    private static UnitSaveData CreateUnitSnapshot(UnitModel unit)
    {
        return new UnitSaveData
        {
            Id = unit.Id,
            ClassId = unit.ClassDefinition.Id,
            EquippedWeaponId = unit.EquippedWeapon.Id,
            X = unit.GridPosition.X,
            Y = unit.GridPosition.Y,
            Level = unit.Level,
            Experience = unit.Experience,
            MaxHp = unit.MaxHp,
            CurrentHp = unit.CurrentHp,
            Strength = unit.Strength,
            Magic = unit.Magic,
            Skill = unit.Skill,
            Speed = unit.Speed,
            Luck = unit.Luck,
            Defense = unit.Defense,
            Resistance = unit.Resistance,
            HasActed = unit.HasActed
        };
    }

    /// <summary>
    /// 在恢复前验证整份存档，包含章节、单位集合、职业、装备、坐标与重复格。
    /// 任何一项失败都不会修改现有游戏状态。
    /// </summary>
    private bool TryValidateSave(
        SaveGameData data,
        out List<ValidatedUnitRestore> restores,
        out string message)
    {
        restores = new List<ValidatedUnitRestore>();
        if (!data.ChapterId.Equals(CurrentChapterId, StringComparison.OrdinalIgnoreCase))
        {
            message = $"这个存档属于章节 {data.ChapterId}，当前基础版本只能读取 {CurrentChapterId}。";
            return false;
        }

        IReadOnlyList<UnitModel> units = ReadUnits();
        if (data.Units.Count != units.Count)
        {
            message = "存档中的单位数量与当前章节不一致，已拒绝读取。";
            return false;
        }

        Dictionary<string, UnitModel> currentById = units.ToDictionary(
            unit => unit.Id,
            StringComparer.OrdinalIgnoreCase);
        HashSet<string> seenIds = new(StringComparer.OrdinalIgnoreCase);
        HashSet<Vector2I> occupiedCells = new();
        int width = ReadGridWidth();
        int height = ReadGridHeight();

        foreach (UnitSaveData state in data.Units)
        {
            if (string.IsNullOrWhiteSpace(state.Id) || !seenIds.Add(state.Id))
            {
                message = "存档包含空单位 ID 或重复单位 ID，已拒绝读取。";
                return false;
            }

            if (!currentById.TryGetValue(state.Id, out UnitModel? unit))
            {
                message = $"当前章节找不到存档单位：{state.Id}。";
                return false;
            }

            if (!unit.ClassDefinition.Id.Equals(state.ClassId, StringComparison.OrdinalIgnoreCase))
            {
                message = $"{unit.DisplayName} 的职业数据与当前版本不一致，已拒绝读取。";
                return false;
            }

            WeaponDefinition? weapon = UnitLoadoutCatalog.TryGetWeapon(state.EquippedWeaponId);
            if (weapon is null)
            {
                message = $"存档引用了不存在的装备：{state.EquippedWeaponId}。";
                return false;
            }

            Vector2I cell = new(state.X, state.Y);
            if (cell.X < 0 || cell.X >= width || cell.Y < 0 || cell.Y >= height)
            {
                message = $"{unit.DisplayName} 的存档坐标超出地图范围。";
                return false;
            }

            // 已倒下单位仍保留最后位置；它们不参与当前地图占位，因此只检查存活单位之间是否重叠。
            if (state.CurrentHp > 0 && !occupiedCells.Add(cell))
            {
                message = "存档中有两个存活单位占据同一格，已拒绝读取。";
                return false;
            }

            restores.Add(new ValidatedUnitRestore(unit, state, weapon));
        }

        message = "存档验证通过。";
        return true;
    }

    /// <summary>
    /// 清空 UnitCharacterLayer 的旧位置和移动动画缓存。
    /// 下一帧人物层会把存档坐标当作初始位置记录，因此不会从读档前的位置播放长距离移动动画。
    /// </summary>
    private void SnapMapCharactersToLogicalPositions()
    {
        if (_visualCoordinator is null)
        {
            return;
        }

        FieldInfo? layerField = typeof(CharacterVisualCoordinator).GetField(
            "_unitCharacterLayer",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (layerField?.GetValue(_visualCoordinator) is not UnitCharacterLayer layer)
        {
            return;
        }

        BindingFlags members = BindingFlags.Instance | BindingFlags.NonPublic;
        object? lastPositions = typeof(UnitCharacterLayer)
            .GetField("_lastGridPositions", members)
            ?.GetValue(layer);
        object? motions = typeof(UnitCharacterLayer)
            .GetField("_motions", members)
            ?.GetValue(layer);

        // 两个对象都是 Dictionary；通过公开 Clear 方法清空即可，不依赖其私有 UnitMotion 泛型类型。
        lastPositions?.GetType().GetMethod("Clear")?.Invoke(lastPositions, null);
        motions?.GetType().GetMethod("Clear")?.Invoke(motions, null);
        layer.QueueRedraw();
    }

    /// <summary>判断当前是否处于允许读写存档的安全时刻。</summary>
    private bool CanOperateSaveSystem(bool requireActionBoundary, out string reason)
    {
        if (!IsPlayerPhase())
        {
            reason = "只能在我方回合保存或读取进度。";
            return false;
        }

        if (BattleAnimationBus.IsPlaybackActive || (_visualCoordinator?.IsMovementAnimating ?? false))
        {
            reason = "人物移动或战斗演出进行中，暂时不能保存或读取。";
            return false;
        }

        if (IsGroupAttackActive())
        {
            reason = "全体进攻执行中，暂时不能保存或读取。";
            return false;
        }

        if (requireActionBoundary &&
            _battleHost is not null &&
            _selectedUnitField?.GetValue(_battleHost) is UnitModel)
        {
            reason = "请先让当前选中单位完成攻击或待机，再保存进度。";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>读取当前是否为玩家阶段；私有枚举通过名称比较避免依赖其内部类型。</summary>
    private bool IsPlayerPhase()
    {
        if (_battleHost is null || _phaseField is null)
        {
            return false;
        }

        return (_phaseField.GetValue(_battleHost)?.ToString() ?? string.Empty).Equals(
            "Player",
            StringComparison.Ordinal);
    }

    /// <summary>读取全体进攻是否正在执行。</summary>
    private bool IsGroupAttackActive()
    {
        return _rightCommandCoordinator is not null &&
               _groupAttackActiveField?.GetValue(_rightCommandCoordinator) is bool active &&
               active;
    }

    /// <summary>读取当前全部单位。</summary>
    private IReadOnlyList<UnitModel> ReadUnits()
    {
        if (_battleHost is null || _unitsField is null)
        {
            return Array.Empty<UnitModel>();
        }

        return _unitsField.GetValue(_battleHost) as IReadOnlyList<UnitModel>
               ?? Array.Empty<UnitModel>();
    }

    /// <summary>读取当前回合数。</summary>
    private int ReadRound()
    {
        return _battleHost is not null && _roundField?.GetValue(_battleHost) is int round
            ? Math.Max(1, round)
            : 1;
    }

    /// <summary>读取最近一次战斗日志。</summary>
    private string ReadLastBattleLog()
    {
        return _battleHost is not null && _lastBattleLogField?.GetValue(_battleHost) is string text
            ? text
            : "尚未发生战斗。";
    }

    /// <summary>读取当前地图宽度。</summary>
    private int ReadGridWidth()
    {
        return _battleHost is not null && _gridWidthField?.GetValue(_battleHost) is int width
            ? Math.Max(1, width)
            : 15;
    }

    /// <summary>读取当前地图高度。</summary>
    private int ReadGridHeight()
    {
        return _battleHost is not null && _gridHeightField?.GetValue(_battleHost) is int height
            ? Math.Max(1, height)
            : 10;
    }

    /// <summary>通过 MainGame 自己的 HUD 刷新入口显示存档反馈。</summary>
    private void ShowMessage(string message)
    {
        if (_battleHost is null || _updateHudMethod is null)
        {
            return;
        }

        _updateHudMethod.Invoke(_battleHost, new object[] { message });
        if (_battleHost is CanvasItem canvasItem)
        {
            canvasItem.QueueRedraw();
        }
    }

    /// <summary>保存经过验证的一条单位恢复计划。</summary>
    private sealed record ValidatedUnitRestore(
        UnitModel Unit,
        UnitSaveData State,
        WeaponDefinition Weapon);
}
