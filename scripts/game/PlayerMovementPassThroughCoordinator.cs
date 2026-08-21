using Godot;
using System.Reflection;

namespace FlameEmblem.Game;

/// <summary>
/// 修正玩家移动范围中的单位阻挡规则。
/// 同阵营单位可以作为路径中的中间格经过，但不能作为最终停留格；敌对单位仍然完全阻挡移动。
/// 该过渡协调器通过现有 MainGame 私有状态工作，等主场景移动 API 独立后可移除反射。
/// </summary>
public partial class PlayerMovementPassThroughCoordinator : Node
{
    /// <summary>当前战斗主节点。</summary>
    private Node? _battleHost;

    /// <summary>MainGame 当前选中单位字段。</summary>
    private FieldInfo? _selectedUnitField;

    /// <summary>MainGame 当前选中单位是否已经移动字段。</summary>
    private FieldInfo? _selectedUnitHasMovedField;

    /// <summary>MainGame 当前可移动格集合字段。</summary>
    private FieldInfo? _reachableCellsField;

    /// <summary>MainGame 全部单位集合字段。</summary>
    private FieldInfo? _unitsField;

    /// <summary>MainGame 当前章节地形字段。</summary>
    private FieldInfo? _terrainField;

    /// <summary>MainGame 地图宽度字段。</summary>
    private FieldInfo? _gridWidthField;

    /// <summary>MainGame 地图高度字段。</summary>
    private FieldInfo? _gridHeightField;

    /// <summary>最近一次写入移动范围的状态签名，避免每帧重复分配集合。</summary>
    private string _lastState = string.Empty;

    /// <summary>启动时缓存 MainGame 字段。</summary>
    public override void _Ready()
    {
        // 提前于大部分表现层刷新移动范围，让同一帧绘制拿到修正后的蓝色区域。
        ProcessPriority = -200;
        _battleHost = GetParent();
        if (_battleHost is null)
        {
            GD.PushWarning("PlayerMovementPassThroughCoordinator 找不到 MainGame。友军穿越规则不会启动。");
            SetProcess(false);
            return;
        }

        Type hostType = _battleHost.GetType();
        BindingFlags members = BindingFlags.Instance | BindingFlags.NonPublic;
        _selectedUnitField = hostType.GetField("_selectedUnit", members);
        _selectedUnitHasMovedField = hostType.GetField("_selectedUnitHasMoved", members);
        _reachableCellsField = hostType.GetField("_reachableCells", members);
        _unitsField = hostType.GetField("_units", members);
        _terrainField = hostType.GetField("_terrain", members);
        _gridWidthField = hostType.GetField("_gridWidth", members);
        _gridHeightField = hostType.GetField("_gridHeight", members);

        if (_selectedUnitField is null ||
            _selectedUnitHasMovedField is null ||
            _reachableCellsField is null ||
            _unitsField is null ||
            _terrainField is null ||
            _gridWidthField is null ||
            _gridHeightField is null)
        {
            GD.PushWarning("PlayerMovementPassThroughCoordinator 无法读取 MainGame 移动状态字段。");
            SetProcess(false);
        }
    }

    /// <summary>
    /// 玩家选中尚未移动的单位时，按“友军可穿越、敌军阻挡、友军格不可停留”重新计算移动范围。
    /// </summary>
    public override void _Process(double delta)
    {
        if (_battleHost is null ||
            _selectedUnitField is null ||
            _selectedUnitHasMovedField is null ||
            _reachableCellsField is null)
        {
            return;
        }

        UnitModel? selectedUnit = _selectedUnitField.GetValue(_battleHost) as UnitModel;
        bool hasMoved = _selectedUnitHasMovedField.GetValue(_battleHost) is bool moved && moved;
        if (selectedUnit is not { Team: UnitTeam.Player } || hasMoved || selectedUnit.HasActed)
        {
            _lastState = string.Empty;
            return;
        }

        IReadOnlyList<UnitModel> units = ReadUnits();
        string occupancySignature = string.Join(",", units
            .Where(unit => unit.IsAlive)
            .OrderBy(unit => unit.Id, StringComparer.Ordinal)
            .Select(unit => $"{unit.Id}:{unit.GridPosition.X}:{unit.GridPosition.Y}"));
        string state = $"{selectedUnit.Id}:{selectedUnit.GridPosition}:{selectedUnit.Move}:{occupancySignature}";
        if (state == _lastState)
        {
            return;
        }

        _lastState = state;
        HashSet<Vector2I> reachable = CalculateReachableCells(selectedUnit, units);
        _reachableCellsField.SetValue(_battleHost, reachable);

        if (_battleHost is CanvasItem canvasItem)
        {
            canvasItem.QueueRedraw();
        }
    }

    /// <summary>计算允许穿越友军的带权移动范围。</summary>
    private HashSet<Vector2I> CalculateReachableCells(UnitModel movingUnit, IReadOnlyList<UnitModel> units)
    {
        Dictionary<Vector2I, int> bestCosts = new()
        {
            [movingUnit.GridPosition] = 0
        };
        PriorityQueue<Vector2I, int> frontier = new();
        frontier.Enqueue(movingUnit.GridPosition, 0);

        while (frontier.TryDequeue(out Vector2I current, out int currentCost))
        {
            if (bestCosts.TryGetValue(current, out int bestKnown) && currentCost > bestKnown)
            {
                continue;
            }

            foreach (Vector2I direction in CardinalDirections())
            {
                Vector2I next = current + direction;
                if (!IsInsideBoard(next))
                {
                    continue;
                }

                UnitModel? occupant = FindOtherLivingUnitAt(next, movingUnit, units);
                if (occupant is not null && occupant.Team != movingUnit.Team)
                {
                    // 敌对单位的格子不能进入，也不能作为中间路径穿越。
                    continue;
                }

                TerrainDefinition terrain = TerrainRules.Get(TerrainAt(next));
                if (!terrain.Passable)
                {
                    continue;
                }

                int nextCost = currentCost + terrain.MoveCost;
                if (nextCost > movingUnit.Move)
                {
                    continue;
                }

                if (bestCosts.TryGetValue(next, out int previousCost) && previousCost <= nextCost)
                {
                    continue;
                }

                // 即使 next 上有友军，也记录并继续扩展；这样路径可以从友军身后继续搜索。
                bestCosts[next] = nextCost;
                frontier.Enqueue(next, nextCost);
            }
        }

        // 最终可点击的落脚格必须没有其他存活单位；友军格只用于路径中转。
        return bestCosts.Keys
            .Where(cell => FindOtherLivingUnitAt(cell, movingUnit, units) is null)
            .ToHashSet();
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

    /// <summary>查找指定格上的其他存活单位。</summary>
    private static UnitModel? FindOtherLivingUnitAt(
        Vector2I cell,
        UnitModel movingUnit,
        IReadOnlyList<UnitModel> units)
    {
        return units.FirstOrDefault(unit =>
            unit.IsAlive &&
            !ReferenceEquals(unit, movingUnit) &&
            unit.GridPosition == cell);
    }

    /// <summary>读取指定格地形；章节未设置的格默认平地。</summary>
    private TerrainType TerrainAt(Vector2I cell)
    {
        if (_battleHost is null || _terrainField is null)
        {
            return TerrainType.Plain;
        }

        IReadOnlyDictionary<Vector2I, TerrainType> terrain =
            _terrainField.GetValue(_battleHost) as IReadOnlyDictionary<Vector2I, TerrainType>
            ?? new Dictionary<Vector2I, TerrainType>();
        return terrain.TryGetValue(cell, out TerrainType type) ? type : TerrainType.Plain;
    }

    /// <summary>判断格子是否在当前章节地图范围内。</summary>
    private bool IsInsideBoard(Vector2I cell)
    {
        int width = _battleHost is not null && _gridWidthField?.GetValue(_battleHost) is int w ? w : 15;
        int height = _battleHost is not null && _gridHeightField?.GetValue(_battleHost) is int h ? h : 10;
        return cell.X >= 0 && cell.X < width && cell.Y >= 0 && cell.Y < height;
    }

    /// <summary>返回四个正交移动方向。</summary>
    private static IEnumerable<Vector2I> CardinalDirections()
    {
        yield return Vector2I.Up;
        yield return Vector2I.Down;
        yield return Vector2I.Left;
        yield return Vector2I.Right;
    }
}
