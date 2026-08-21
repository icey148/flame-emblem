using FlameEmblem.Game;
using FlameEmblem.Visual;
using Godot;
using System.Reflection;

namespace FlameEmblem.Main;

/// <summary>
/// 连接统一战斗场景与世界地图。
/// 进入战斗时恢复跨场景玩家队伍；观察到胜利后记录章节完成并提供返回世界地图入口。
/// </summary>
public partial class ChapterFlowCoordinator : Node
{
    /// <summary>当前 MainGame 节点。</summary>
    private Node? _battleHost;

    /// <summary>地图人物表现协调器，用于返回世界地图前等待移动动画结束。</summary>
    private CharacterVisualCoordinator? _visualCoordinator;

    /// <summary>MainGame 全部单位字段。</summary>
    private FieldInfo? _unitsField;

    /// <summary>MainGame 当前战斗阶段字段。</summary>
    private FieldInfo? _phaseField;

    /// <summary>MainGame 结束回合按钮字段，用于定位右侧 VBox。</summary>
    private FieldInfo? _endTurnButtonField;

    /// <summary>MainGame HUD 刷新方法。</summary>
    private MethodInfo? _updateHudMethod;

    /// <summary>跨场景玩家状态是否已经应用到本场战斗。</summary>
    private bool _rosterApplied;

    /// <summary>当前章节胜利是否已经写入战役状态。</summary>
    private bool _victoryRecorded;

    /// <summary>胜利后出现的世界地图按钮。</summary>
    private Button? _worldMapButton;

    /// <summary>缓存 MainGame 必要成员；真正逻辑在后续 _Process 等父节点完成 _Ready 后执行。</summary>
    public override void _Ready()
    {
        ProcessPriority = 420;
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
        _phaseField = hostType.GetField("_phase", members);
        _endTurnButtonField = hostType.GetField("_endTurnButton", members);
        _updateHudMethod = hostType.GetMethod("UpdateHud", members);

        if (_unitsField is null || _phaseField is null || _endTurnButtonField is null || _updateHudMethod is null)
        {
            GD.PushWarning("ChapterFlowCoordinator 无法读取 MainGame 的章节状态，章节流程已停用。");
            SetProcess(false);
        }
    }

    /// <summary>恢复队伍、观察胜利状态并刷新世界地图按钮。</summary>
    public override void _Process(double delta)
    {
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
    /// MainGame 父节点完成 _Ready 并生成单位以后，套用上一章节留下的玩家成长状态。
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
        InvokeHud("队伍已从世界地图进入战场。等级、EXP、职业、装备和 HP 已继承。请选择我方单位行动。");

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
