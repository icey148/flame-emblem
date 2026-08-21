using FlameEmblem.Game;
using FlameEmblem.Visual;
using Godot;
using System.Reflection;

namespace FlameEmblem.Main;

/// <summary>
/// 为当前战斗章节提供本地“保存进度 / 读取进度”入口。
/// v3 存档同时记录战斗/世界地图位置；战斗存档可以跨章节精确恢复，世界地图存档则直接返回世界地图。
/// </summary>
public partial class SaveGameCoordinator : Node
{
    /// <summary>当前战斗主节点。</summary>
    private Node? _battleHost;

    /// <summary>地图人物表现层，用于判断移动状态和读档后清除旧移动缓存。</summary>
    private CharacterVisualCoordinator? _visualCoordinator;

    /// <summary>右侧批量指令协调器；全体进攻期间禁止读写存档。</summary>
    private RightCommandCoordinator? _rightCommandCoordinator;

    /// <summary>MainGame 全部单位字段。</summary>
    private FieldInfo? _unitsField;

    /// <summary>MainGame 当前回合字段。</summary>
    private FieldInfo? _roundField;

    /// <summary>MainGame 当前阶段字段。</summary>
    private FieldInfo? _phaseField;

    /// <summary>MainGame 当前选中单位字段。</summary>
    private FieldInfo? _selectedUnitField;

    /// <summary>MainGame 最近战斗记录字段。</summary>
    private FieldInfo? _lastBattleLogField;

    /// <summary>MainGame 地图宽度字段。</summary>
    private FieldInfo? _gridWidthField;

    /// <summary>MainGame 地图高度字段。</summary>
    private FieldInfo? _gridHeightField;

    /// <summary>MainGame 结束回合按钮字段，用于定位右侧按钮列。</summary>
    private FieldInfo? _endTurnButtonField;

    /// <summary>MainGame 清空选择的方法。</summary>
    private MethodInfo? _clearSelectionMethod;

    /// <summary>MainGame 清空敌军回合状态机的方法。</summary>
    private MethodInfo? _resetEnemyTurnSequenceMethod;

    /// <summary>MainGame HUD 刷新方法。</summary>
    private MethodInfo? _updateHudMethod;

    /// <summary>RightCommandCoordinator 全体进攻状态字段。</summary>
    private FieldInfo? _groupAttackActiveField;

    /// <summary>保存按钮。</summary>
    private Button? _saveButton;

    /// <summary>读取按钮。</summary>
    private Button? _loadButton;

    /// <summary>右侧存档按钮是否已经创建。</summary>
    private bool _uiInitialized;

    /// <summary>缓存主场景接口并等待 MainGame 动态 HUD 创建完成。</summary>
    public override void _Ready()
    {
        // ChapterFlowCoordinator 使用 -500 优先级先准备正确章节；这里随后才能安全套用跨场景待恢复战斗快照。
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

        BindingFlags members = BindingFlags.Instance | BindingFlags.NonPublic;
        Type hostType = _battleHost.GetType();
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

    /// <summary>等待右侧 HUD 完成创建，处理跨场景待恢复存档，并持续刷新保存/读取按钮状态。</summary>
    public override void _Process(double delta)
    {
        if (!_uiInitialized)
        {
            _uiInitialized = TryCreateButtons();
        }

        TryApplyPendingSceneRestore();
        RefreshButtonStates();
    }

    /// <summary>在右侧按钮列底部创建单槽位保存和读取按钮。</summary>
    private bool TryCreateButtons()
    {
        if (_battleHost is null ||
            _endTurnButtonField?.GetValue(_battleHost) is not Button endTurnButton ||
            endTurnButton.GetParent() is not VBoxContainer column)
        {
            return false;
        }

        HBoxContainer row = new()
        {
            Name = "SaveLoadRow",
            CustomMinimumSize = new Vector2(360, 36),
            Alignment = BoxContainer.AlignmentMode.Center
        };
        column.AddChild(row);

        _saveButton = new Button
        {
            Text = "保存进度",
            CustomMinimumSize = new Vector2(174, 32),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _saveButton.Pressed += SaveCurrentProgress;
        row.AddChild(_saveButton);

        _loadButton = new Button
        {
            Text = "读取进度",
            CustomMinimumSize = new Vector2(174, 32),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _loadButton.Pressed += LoadCurrentProgress;
        row.AddChild(_loadButton);
        return true;
    }

    /// <summary>根据回合、动画、选择和批量指令状态决定按钮是否可用。</summary>
    private void RefreshButtonStates()
    {
        if (_saveButton is null || _loadButton is null)
        {
            return;
        }

        bool playerPhase = IsPlayerPhase();
        bool busy = BattleAnimationBus.IsPlaybackActive ||
                    (_visualCoordinator?.IsMovementAnimating ?? false) ||
                    IsGroupAttackActive() ||
                    SaveGameService.HasPendingSceneRestore;
        bool hasSelection = _battleHost is not null && _selectedUnitField?.GetValue(_battleHost) is UnitModel;

        // 保存必须位于完整单位行动边界，避免保存“已经移动但还没攻击/待机”的半行动状态。
        _saveButton.Disabled = !playerPhase || busy || hasSelection;
        _loadButton.Disabled = !playerPhase || busy || !SaveGameService.HasSave;
        _loadButton.Text = SaveGameService.HasSave ? "读取进度" : "读取进度（无存档）";
    }

    /// <summary>把当前章节完整运行时状态和世界地图战役状态写入本地单槽位。</summary>
    private void SaveCurrentProgress()
    {
        if (!CanOperate(requireActionBoundary: true, out string reason))
        {
            ShowMessage(reason);
            return;
        }

        IReadOnlyList<UnitModel> units = ReadUnits();
        // 存档瞬间同步一次长期队伍，确保本回合刚获得的 EXP、转职和受伤状态也进入战役快照。
        CampaignState.CapturePlayerRoster(units);

        SaveGameData data = new()
        {
            Location = SaveLocation.Battle,
            ChapterId = CampaignState.CurrentChapterId,
            ChapterPath = CampaignState.CurrentChapterPath,
            Round = ReadRound(),
            LastBattleLog = ReadLastBattleLog(),
            Units = units.Select(CreateSnapshot).ToList(),
            Campaign = CampaignState.CreateSaveSnapshot()
        };

        SaveGameService.TrySave(data, out string message);
        ShowMessage(message);
    }

    /// <summary>
    /// 读取本地单槽位。
    /// 世界地图存档直接回世界地图；战斗存档同章节直接恢复，不同章节先切换后再精确恢复。
    /// </summary>
    private void LoadCurrentProgress()
    {
        if (!CanOperate(requireActionBoundary: false, out string reason))
        {
            ShowMessage(reason);
            return;
        }

        if (!SaveGameService.TryLoad(out SaveGameData? data, out string readMessage) || data is null)
        {
            ShowMessage(readMessage);
            return;
        }

        CampaignState.RestoreSaveSnapshot(data.Campaign, data.ChapterId, data.ChapterPath);

        if (data.Location == SaveLocation.WorldMap)
        {
            ShowMessage("正在返回世界地图存档……");
            Error worldMapError = GetTree().ChangeSceneToFile("res://scenes/world/WorldMap.tscn");
            if (worldMapError != Error.Ok)
            {
                ShowMessage($"无法切换到世界地图：{worldMapError}。");
            }

            return;
        }

        if (!data.ChapterId.Equals(CampaignState.CurrentChapterId, StringComparison.OrdinalIgnoreCase))
        {
            CampaignState.BeginChapter(data.ChapterId, data.ChapterPath);
            SaveGameService.QueuePendingSceneRestore(data);
            ShowMessage($"正在切换到存档章节 {data.ChapterId}……");

            Error error = GetTree().ChangeSceneToFile("res://scenes/main/Main.tscn");
            if (error != Error.Ok)
            {
                // 场景切换失败时消费待恢复对象，避免下一次进入战斗意外自动读档。
                SaveGameService.TakePendingSceneRestore();
                ShowMessage($"无法切换到存档章节：{error}。");
            }

            return;
        }

        ApplyLoadedData(data);
    }

    /// <summary>
    /// 场景切换后自动套用之前暂存的战斗快照。
    /// ChapterFlowCoordinator 已经更早把 MainGame 替换成正确章节，因此这里可以做完整单位集合校验。
    /// </summary>
    private void TryApplyPendingSceneRestore()
    {
        if (!SaveGameService.HasPendingSceneRestore)
        {
            return;
        }

        SaveGameData? data = SaveGameService.TakePendingSceneRestore();
        if (data is null)
        {
            return;
        }

        if (data.Location != SaveLocation.Battle)
        {
            GD.PushWarning("SaveGameCoordinator 收到非战斗待恢复存档，已忽略该临时对象。");
            return;
        }

        if (!data.ChapterId.Equals(CampaignState.CurrentChapterId, StringComparison.OrdinalIgnoreCase))
        {
            // 理论上不应发生；重新排队让下一帧/正确场景继续处理，而不是丢掉快照。
            SaveGameService.QueuePendingSceneRestore(data);
            return;
        }

        ApplyLoadedData(data);
    }

    /// <summary>验证并一次性恢复一份已经解析完成的战斗存档。</summary>
    private void ApplyLoadedData(SaveGameData data)
    {
        if (data.Location != SaveLocation.Battle)
        {
            ShowMessage("这不是战斗内存档，不能套用到当前战场。");
            return;
        }

        if (!TryValidateSave(data, out List<ValidatedRestore> restores, out string validationMessage))
        {
            ShowMessage(validationMessage);
            return;
        }

        CampaignState.RestoreSaveSnapshot(data.Campaign, data.ChapterId, data.ChapterPath);
        CampaignState.BeginChapter(data.ChapterId, data.ChapterPath);

        foreach (ValidatedRestore restore in restores)
        {
            UnitSaveData state = restore.State;
            restore.Unit.RestoreRuntimeState(
                new Vector2I(state.X, state.Y),
                restore.ClassDefinition,
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

        // 精确战斗快照恢复后再次覆盖长期队伍，使 v1 旧存档即使没有 Campaign.PlayerRoster 也能自动迁移出长期角色状态。
        CampaignState.CapturePlayerRoster(ReadUnits());
        SnapMapCharactersToLogicalPositions();
        ShowMessage($"已读取槽位 1：{CampaignState.CurrentChapterId}，第 {Math.Max(1, data.Round)} 回合继续。");

        if (_battleHost is CanvasItem canvasItem)
        {
            canvasItem.QueueRedraw();
        }
    }

    /// <summary>把一个运行时单位转换为纯数据单场战斗快照。</summary>
    private static UnitSaveData CreateSnapshot(UnitModel unit)
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
    /// 恢复前验证整份存档：单位集合、职业、装备、地图坐标和存活单位重叠必须全部合法。
    /// </summary>
    private bool TryValidateSave(
        SaveGameData data,
        out List<ValidatedRestore> restores,
        out string message)
    {
        restores = new List<ValidatedRestore>();
        IReadOnlyList<UnitModel> units = ReadUnits();
        if (data.Units.Count != units.Count)
        {
            message = "存档单位数量与当前章节不一致，已拒绝读取。";
            return false;
        }

        Dictionary<string, UnitModel> currentById = units.ToDictionary(
            unit => unit.Id,
            StringComparer.OrdinalIgnoreCase);
        HashSet<string> seenIds = new(StringComparer.OrdinalIgnoreCase);
        HashSet<Vector2I> occupied = new();
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

            UnitClassDefinition? classDefinition = UnitClassCatalog.TryGet(state.ClassId);
            if (classDefinition is null)
            {
                message = $"存档引用了不存在的职业：{state.ClassId}。";
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
                message = $"{unit.DisplayName} 的存档坐标超出当前地图范围。";
                return false;
            }

            // 已倒下单位不占地图格；只阻止两个存活单位被损坏存档叠在同一格。
            if (state.CurrentHp > 0 && !occupied.Add(cell))
            {
                message = "存档中有两个存活单位占据同一格，已拒绝读取。";
                return false;
            }

            restores.Add(new ValidatedRestore(unit, state, classDefinition, weapon));
        }

        message = "存档验证通过。";
        return true;
    }

    /// <summary>清空地图人物层的旧位置缓存，使读档后角色瞬间同步到存档坐标。</summary>
    private void SnapMapCharactersToLogicalPositions()
    {
        if (_visualCoordinator is null)
        {
            return;
        }

        BindingFlags members = BindingFlags.Instance | BindingFlags.NonPublic;
        FieldInfo? layerField = typeof(CharacterVisualCoordinator).GetField("_unitCharacterLayer", members);
        if (layerField?.GetValue(_visualCoordinator) is not UnitCharacterLayer layer)
        {
            return;
        }

        object? lastPositions = typeof(UnitCharacterLayer)
            .GetField("_lastGridPositions", members)
            ?.GetValue(layer);
        object? motions = typeof(UnitCharacterLayer)
            .GetField("_motions", members)
            ?.GetValue(layer);

        lastPositions?.GetType().GetMethod("Clear")?.Invoke(lastPositions, null);
        motions?.GetType().GetMethod("Clear")?.Invoke(motions, null);
        layer.QueueRedraw();
    }

    /// <summary>判断当前是否处于允许保存或读取的安全时刻。</summary>
    private bool CanOperate(bool requireActionBoundary, out string reason)
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
            reason = "请先让当前选中单位完成攻击、转职或待机，再保存进度。";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>读取当前是否为玩家阶段。</summary>
    private bool IsPlayerPhase()
    {
        return _battleHost is not null &&
               (_phaseField?.GetValue(_battleHost)?.ToString() ?? string.Empty).Equals(
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

    /// <summary>读取当前回合。</summary>
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

    /// <summary>通过 MainGame 的 HUD 入口显示存档反馈。</summary>
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

    /// <summary>一条已经验证好的单位恢复计划。</summary>
    private sealed record ValidatedRestore(
        UnitModel Unit,
        UnitSaveData State,
        UnitClassDefinition ClassDefinition,
        WeaponDefinition Weapon);
}
