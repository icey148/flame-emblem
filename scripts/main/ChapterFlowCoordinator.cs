using FlameEmblem.Game;
using FlameEmblem.Visual;
using Godot;
using System.Reflection;

namespace FlameEmblem.Main;

/// <summary>
/// 连接统一战斗场景与世界地图。
/// 世界地图选择的章节会在第一帧替换 MainGame 默认章节；随后恢复跨场景队伍，并在胜利后记录战果。
/// </summary>
public partial class ChapterFlowCoordinator : Node
{
    /// <summary>当前 MainGame 节点。</summary>
    private Node? _battleHost;

    /// <summary>地图人物表现协调器，用于返回世界地图前等待移动动画结束。</summary>
    private CharacterVisualCoordinator? _visualCoordinator;

    /// <summary>MainGame 全部单位字段。</summary>
    private FieldInfo? _unitsField;

    /// <summary>MainGame 章节地形字段。</summary>
    private FieldInfo? _terrainField;

    /// <summary>MainGame 当前章节标题字段。</summary>
    private FieldInfo? _chapterTitleField;

    /// <summary>MainGame 胜利条件字段。</summary>
    private FieldInfo? _victoryConditionField;

    /// <summary>MainGame 指定胜利目标 ID 字段。</summary>
    private FieldInfo? _victoryTargetIdField;

    /// <summary>MainGame 地图宽度字段。</summary>
    private FieldInfo? _gridWidthField;

    /// <summary>MainGame 地图高度字段。</summary>
    private FieldInfo? _gridHeightField;

    /// <summary>MainGame 当前回合数字段。</summary>
    private FieldInfo? _roundField;

    /// <summary>MainGame 最近战斗记录字段。</summary>
    private FieldInfo? _lastBattleLogField;

    /// <summary>MainGame 当前战斗阶段字段。</summary>
    private FieldInfo? _phaseField;

    /// <summary>MainGame 结束回合按钮字段，用于定位右侧 VBox。</summary>
    private FieldInfo? _endTurnButtonField;

    /// <summary>MainGame 清空选中状态的方法。</summary>
    private MethodInfo? _clearSelectionMethod;

    /// <summary>MainGame HUD 刷新方法。</summary>
    private MethodInfo? _updateHudMethod;

    /// <summary>世界地图选择的章节是否已经替换 MainGame 默认章节。</summary>
    private bool _chapterPrepared;

    /// <summary>跨场景玩家状态是否已经应用到本场战斗。</summary>
    private bool _rosterApplied;

    /// <summary>当前章节胜利是否已经写入战役状态。</summary>
    private bool _victoryRecorded;

    /// <summary>胜利后出现的世界地图按钮。</summary>
    private Button? _worldMapButton;

    /// <summary>缓存 MainGame 必要成员；真正逻辑在第一帧等父节点完成 _Ready 后执行。</summary>
    public override void _Ready()
    {
        // 必须早于人物层、HUD 和批量指令的常规 Process，保证它们第一帧就读取到正确章节。
        ProcessPriority = -500;
        _battleHost = GetParent();
        _visualCoordinator = GetNodeOrNull<CharacterVisualCoordinator>("../CharacterVisualCoordinator");

        if (_battleHost is null)
        {
            GD.PushWarning("ChapterFlowCoordinator 找不到 MainGame，章节流程不会启动。");
            SetProcess(false);
            return;
        }

        BindingFlags members = BindingFlags.Instance | BindingFlags.NonPublic;
        Type hostType = _battleHost.GetType();
        _unitsField = hostType.GetField("_units", members);
        _terrainField = hostType.GetField("_terrain", members);
        _chapterTitleField = hostType.GetField("_chapterTitle", members);
        _victoryConditionField = hostType.GetField("_victoryCondition", members);
        _victoryTargetIdField = hostType.GetField("_victoryTargetId", members);
        _gridWidthField = hostType.GetField("_gridWidth", members);
        _gridHeightField = hostType.GetField("_gridHeight", members);
        _roundField = hostType.GetField("_round", members);
        _lastBattleLogField = hostType.GetField("_lastBattleLog", members);
        _phaseField = hostType.GetField("_phase", members);
        _endTurnButtonField = hostType.GetField("_endTurnButton", members);
        _clearSelectionMethod = hostType.GetMethod("ClearSelection", members);
        _updateHudMethod = hostType.GetMethod("UpdateHud", members);

        if (_unitsField is null ||
            _terrainField is null ||
            _chapterTitleField is null ||
            _victoryConditionField is null ||
            _victoryTargetIdField is null ||
            _gridWidthField is null ||
            _gridHeightField is null ||
            _roundField is null ||
            _lastBattleLogField is null ||
            _phaseField is null ||
            _endTurnButtonField is null ||
            _clearSelectionMethod is null ||
            _updateHudMethod is null)
        {
            GD.PushWarning("ChapterFlowCoordinator 无法读取 MainGame 的章节状态，章节流程已停用。");
            SetProcess(false);
        }
    }

    /// <summary>准备章节、恢复队伍、观察胜利状态并刷新世界地图按钮。</summary>
    public override void _Process(double delta)
    {
        if (!_chapterPrepared)
        {
            if (!TryPrepareSelectedChapter())
            {
                return;
            }
        }

        if (!_rosterApplied)
        {
            TryApplyCampaignRoster();
        }

        bool victory = CurrentPhaseName().Equals("Victory", StringComparison.Ordinal);
        if (victory && !_victoryRecorded)
        {
            RecordVictory();
        }

        if (_worldMapButton is not null)
        {
            bool busy = BattleAnimationBus.IsPlaybackActive ||
                        (_visualCoordinator?.IsMovementAnimating ?? false);
            _worldMapButton.Disabled = !victory || busy;
        }
    }

    /// <summary>
    /// 读取 CampaignState 当前章节路径，并替换 MainGame 在 _Ready 中加载的默认序章数据。
    /// 统一战斗场景因此可以承载任意符合 ChapterDataLoader 格式的章节 JSON。
    /// </summary>
    private bool TryPrepareSelectedChapter()
    {
        if (_battleHost is null)
        {
            return false;
        }

        try
        {
            string chapterPath = CampaignState.CurrentChapterPath;
            LoadedChapter chapter = ChapterDataLoader.Load(chapterPath);

            if (_unitsField?.GetValue(_battleHost) is not List<UnitModel> units ||
                _terrainField?.GetValue(_battleHost) is not Dictionary<Vector2I, TerrainType> terrain)
            {
                return false;
            }

            units.Clear();
            units.AddRange(chapter.Units);
            terrain.Clear();
            foreach ((Vector2I cell, TerrainType type) in chapter.Terrain)
            {
                terrain[cell] = type;
            }

            _chapterTitleField?.SetValue(_battleHost, chapter.Title);
            _victoryConditionField?.SetValue(_battleHost, chapter.VictoryCondition);
            _victoryTargetIdField?.SetValue(_battleHost, chapter.VictoryTargetId);
            _gridWidthField?.SetValue(_battleHost, chapter.Width);
            _gridHeightField?.SetValue(_battleHost, chapter.Height);
            _roundField?.SetValue(_battleHost, 1);
            _lastBattleLogField?.SetValue(_battleHost, "尚未发生战斗。");
            _clearSelectionMethod?.Invoke(_battleHost, null);

            CampaignState.ConfirmLoadedChapter(chapter.Id, chapterPath);
            _chapterPrepared = true;
            InvokeHud($"{chapter.Title} 开始。请选择一个我方单位行动。");

            if (_battleHost is CanvasItem canvasItem)
            {
                canvasItem.QueueRedraw();
            }

            return true;
        }
        catch (Exception exception)
        {
            GD.PushError($"准备世界地图章节失败：{exception}");
            InvokeHud($"章节切换失败：{exception.Message}");
            SetProcess(false);
            return false;
        }
    }

    /// <summary>
    /// 章节准备完成以后，套用上一章节留下的玩家成长状态。
    /// </summary>
    private void TryApplyCampaignRoster()
    {
        IReadOnlyList<UnitModel> units = ReadUnits();
        if (units.Count == 0)
        {
            return;
        }

        _rosterApplied = true;
        if (!CampaignState.HasPlayerRoster)
        {
            return;
        }

        CampaignState.ApplyPlayerRoster(units);
        InvokeHud("队伍已进入战场。等级、EXP、职业、装备和 HP 已继承。请选择我方单位行动。");

        if (_battleHost is CanvasItem canvasItem)
        {
            canvasItem.QueueRedraw();
        }
    }

    /// <summary>首次观察到胜利时保存玩家长期状态并创建返回地图入口。</summary>
    private void RecordVictory()
    {
        IReadOnlyList<UnitModel> units = ReadUnits();
        if (units.Count == 0)
        {
            return;
        }

        _victoryRecorded = true;
        CampaignState.CompleteChapter(CampaignState.CurrentChapterId, units);
        CreateWorldMapButton();
        InvokeHud("章节目标完成。战果已经记录，可以返回世界地图继续前进。");
    }

    /// <summary>在右侧命令列底部创建“返回世界地图”。</summary>
    private void CreateWorldMapButton()
    {
        if (_worldMapButton is not null ||
            _battleHost is null ||
            _endTurnButtonField?.GetValue(_battleHost) is not Button endTurnButton ||
            endTurnButton.GetParent() is not VBoxContainer column)
        {
            return;
        }

        _worldMapButton = new Button
        {
            Name = "ReturnToWorldMapButton",
            Text = "返回世界地图",
            CustomMinimumSize = new Vector2(360, 38),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _worldMapButton.Pressed += ReturnToWorldMap;
        column.AddChild(_worldMapButton);
    }

    /// <summary>切换到世界地图场景。</summary>
    private void ReturnToWorldMap()
    {
        if (_worldMapButton is null || _worldMapButton.Disabled)
        {
            return;
        }

        Error error = GetTree().ChangeSceneToFile("res://scenes/world/WorldMap.tscn");
        if (error != Error.Ok)
        {
            InvokeHud($"无法进入世界地图：{error}。");
        }
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

    /// <summary>读取私有 BattlePhase 的枚举名称，避免把协调器耦合到 MainGame 内部枚举类型。</summary>
    private string CurrentPhaseName()
    {
        return _battleHost is not null
            ? _phaseField?.GetValue(_battleHost)?.ToString() ?? string.Empty
            : string.Empty;
    }

    /// <summary>通过 MainGame 自己的 HUD 入口显示章节流程提示。</summary>
    private void InvokeHud(string message)
    {
        if (_battleHost is null || _updateHudMethod is null)
        {
            return;
        }

        _updateHudMethod.Invoke(_battleHost, new object[] { message });
    }
}
