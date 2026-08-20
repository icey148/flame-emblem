using FlameEmblem.Game;
using Godot;
using System.Reflection;

namespace FlameEmblem.Visual;

/// <summary>
/// 覆盖 MainGame 早期原型绘制，负责显示复古像素战场、范围高亮和像素生命条。
/// 规则层仍然由 MainGame / TerrainRules / CombatRules 管理；本节点只读取状态，不修改单位位置、地形或战斗数值。
/// </summary>
public partial class RetroBattlefieldLayer : Node2D
{
    /// <summary>战棋地图左上角与 MainGame 保持一致。</summary>
    private static readonly Vector2 BoardOrigin = new(40, 100);

    /// <summary>逻辑格尺寸与 MainGame 保持一致。</summary>
    private const int CellSize = 52;

    /// <summary>当前战斗主节点。</summary>
    private Node? _battleHost;

    /// <summary>MainGame 地形字典字段。</summary>
    private FieldInfo? _terrainField;

    /// <summary>MainGame 全部单位字段。</summary>
    private FieldInfo? _unitsField;

    /// <summary>MainGame 当前选中单位字段。</summary>
    private FieldInfo? _selectedUnitField;

    /// <summary>MainGame 当前攻击目标字段。</summary>
    private FieldInfo? _pendingAttackTargetField;

    /// <summary>MainGame 当前可移动格集合字段。</summary>
    private FieldInfo? _reachableCellsField;

    /// <summary>MainGame 地图宽度字段。</summary>
    private FieldInfo? _gridWidthField;

    /// <summary>MainGame 地图高度字段。</summary>
    private FieldInfo? _gridHeightField;

    /// <summary>河流像素波纹使用的表现计时，不影响任何游戏规则。</summary>
    private double _elapsed;

    /// <summary>
    /// 缓存主战斗节点字段，并把本层放在旧原型绘制之上、人物层之下。
    /// </summary>
    public override void _Ready()
    {
        ZIndex = 8;
        _battleHost = GetParent();
        if (_battleHost is null)
        {
            GD.PushWarning("RetroBattlefieldLayer 找不到战斗主节点，像素战场不会启动。");
            SetProcess(false);
            return;
        }

        Type hostType = _battleHost.GetType();
        BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        _terrainField = hostType.GetField("_terrain", fields);
        _unitsField = hostType.GetField("_units", fields);
        _selectedUnitField = hostType.GetField("_selectedUnit", fields);
        _pendingAttackTargetField = hostType.GetField("_pendingAttackTarget", fields);
        _reachableCellsField = hostType.GetField("_reachableCells", fields);
        _gridWidthField = hostType.GetField("_gridWidth", fields);
        _gridHeightField = hostType.GetField("_gridHeight", fields);

        if (_terrainField is null || _unitsField is null || _selectedUnitField is null ||
            _pendingAttackTargetField is null || _reachableCellsField is null ||
            _gridWidthField is null || _gridHeightField is null)
        {
            GD.PushWarning("RetroBattlefieldLayer 无法读取 MainGame 战场状态，请检查字段名称是否变化。");
            SetProcess(false);
        }
    }

    /// <summary>
    /// 只推进水面等轻量像素动画并重绘，不写入逻辑层。
    /// </summary>
    public override void _Process(double delta)
    {
        _elapsed += delta;
        QueueRedraw();
    }

    /// <summary>
    /// 依次绘制战场底图、移动/攻击范围和像素 HP 条。
    /// 本层使用不透明地形覆盖 MainGame 的旧色块地图和圆形单位占位，因此不会出现双重人物。
    /// </summary>
    public override void _Draw()
    {
        int width = ReadInt(_gridWidthField, 15);
        int height = ReadInt(_gridHeightField, 10);
        IReadOnlyDictionary<Vector2I, TerrainType> terrain = ReadTerrain();

        DrawOuterFrame(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2I cell = new(x, y);
                TerrainType type = terrain.TryGetValue(cell, out TerrainType explicitType)
                    ? explicitType
                    : TerrainType.Plain;
                DrawTerrainCell(cell, type);
            }
        }

        DrawRangeOverlay();
        DrawPixelHpBars();
    }

    /// <summary>绘制战场外围的暗色像素边框。</summary>
    private void DrawOuterFrame(int width, int height)
    {
        Rect2 board = new(
            BoardOrigin - new Vector2(4, 4),
            new Vector2(width * CellSize + 8, height * CellSize + 8));
        DrawRect(board, new Color(0.055f, 0.06f, 0.055f), true);
        DrawRect(
            new Rect2(board.Position + new Vector2(2, 2), board.Size - new Vector2(4, 4)),
            new Color(0.24f, 0.20f, 0.14f),
            false,
            2.0f);
    }

    /// <summary>根据地形类型绘制一个完整像素格。</summary>
    private void DrawTerrainCell(Vector2I cell, TerrainType type)
    {
        Rect2 rect = GridRect(cell);
        switch (type)
        {
            case TerrainType.Forest:
                DrawForest(rect, cell);
                break;
            case TerrainType.Bridge:
                DrawBridge(rect, cell);
                break;
            case TerrainType.River:
                DrawRiver(rect, cell);
                break;
            case TerrainType.Fort:
                DrawFort(rect, cell);
                break;
            default:
                DrawPlain(rect, cell);
                break;
        }

        // 网格线保持很弱，只帮助读格，不让地图重新变成棋盘表格。
        DrawRect(rect, new Color(0.07f, 0.09f, 0.065f, 0.48f), false, 1.0f);
    }

    /// <summary>
    /// 绘制草地。使用少量确定性草簇、裸土和裂纹，形成经历过战事的明亮草原，而不是纯色方块。
    /// </summary>
    private void DrawPlain(Rect2 rect, Vector2I cell)
    {
        Color grass = (cell.X + cell.Y) % 2 == 0
            ? new Color(0.27f, 0.48f, 0.23f)
            : new Color(0.30f, 0.52f, 0.25f);
        DrawRect(rect, grass, true);

        int seed = StableCellSeed(cell, 17);
        for (int i = 0; i < 4; i++)
        {
            int x = 5 + PositiveMod(seed + i * 13, 39);
            int y = 6 + PositiveMod(seed / 7 + i * 19, 36);
            Color tuft = i % 2 == 0 ? grass.Darkened(0.16f) : grass.Lightened(0.10f);
            DrawPixel(rect, x, y, 2, 5, tuft);
            DrawPixel(rect, x + 3, y + 2, 2, 3, tuft);
        }

        // 一部分平地加入小块裸土和裂纹，让序章战场带有轻微战争痕迹。
        if (PositiveMod(seed, 5) == 0)
        {
            Color dirt = new(0.43f, 0.34f, 0.20f);
            DrawPixel(rect, 18, 31, 13, 5, dirt);
            DrawPixel(rect, 22, 27, 7, 4, dirt.Darkened(0.08f));
            DrawPixel(rect, 29, 34, 7, 2, dirt.Darkened(0.18f));
        }
    }

    /// <summary>绘制森林：深草底色、树干与三簇方形树冠。</summary>
    private void DrawForest(Rect2 rect, Vector2I cell)
    {
        Color ground = new(0.16f, 0.34f, 0.16f);
        Color trunk = new(0.31f, 0.22f, 0.13f);
        Color leafDark = new(0.075f, 0.25f, 0.11f);
        Color leaf = new(0.10f, 0.38f, 0.15f);
        Color leafLight = new(0.18f, 0.48f, 0.20f);
        DrawRect(rect, ground, true);

        int sway = ((int)(_elapsed * 2.0) + PositiveMod(StableCellSeed(cell, 5), 3)) % 2;
        DrawPixel(rect, 10, 29, 5, 16, trunk);
        DrawPixel(rect, 34, 27, 5, 18, trunk.Darkened(0.06f));
        DrawPixel(rect, 21, 31, 5, 14, trunk);

        DrawTreeCrown(rect, 4 + sway, 8, leafDark, leaf, leafLight);
        DrawTreeCrown(rect, 25 - sway, 5, leafDark, leaf, leafLight);
        DrawTreeCrown(rect, 14, 14, leafDark, leaf, leafLight);
    }

    /// <summary>绘制一簇块状树冠。</summary>
    private void DrawTreeCrown(Rect2 rect, int x, int y, Color dark, Color main, Color light)
    {
        DrawPixel(rect, x + 4, y, 14, 5, dark);
        DrawPixel(rect, x, y + 5, 22, 11, dark);
        DrawPixel(rect, x + 3, y + 4, 16, 10, main);
        DrawPixel(rect, x + 6, y + 5, 7, 4, light);
    }

    /// <summary>绘制河流：蓝色底面、岸边暗线和缓慢移动的水平像素浪花。</summary>
    private void DrawRiver(Rect2 rect, Vector2I cell)
    {
        Color water = new(0.16f, 0.43f, 0.66f);
        Color deep = new(0.10f, 0.31f, 0.54f);
        Color foam = new(0.43f, 0.70f, 0.82f);
        DrawRect(rect, water, true);
        DrawPixel(rect, 0, 0, 4, CellSize, deep);
        DrawPixel(rect, CellSize - 4, 0, 4, CellSize, deep);

        int phase = ((int)(_elapsed * 8.0) + cell.Y * 5) % 16;
        for (int row = 0; row < 3; row++)
        {
            int y = 10 + row * 14;
            int x = PositiveMod(phase + row * 17, 30);
            DrawPixel(rect, x, y, 14, 2, foam);
            DrawPixel(rect, PositiveMod(x + 24, 40), y + 4, 9, 2, deep.Lightened(0.12f));
        }
    }

    /// <summary>
    /// 绘制横跨河面的旧石桥。桥下仍能看见水色，桥面带有断裂石缝和磨损边缘。
    /// </summary>
    private void DrawBridge(Rect2 rect, Vector2I cell)
    {
        DrawRiver(rect, cell);
        Color stoneDark = new(0.34f, 0.30f, 0.24f);
        Color stone = new(0.57f, 0.50f, 0.38f);
        Color stoneLight = new(0.69f, 0.62f, 0.48f);

        DrawPixel(rect, 0, 12, CellSize, 30, stoneDark);
        DrawPixel(rect, 0, 15, CellSize, 24, stone);
        for (int x = 2; x < CellSize; x += 13)
        {
            DrawPixel(rect, x, 17, 9, 8, stoneLight);
            DrawPixel(rect, x + 5, 27, 8, 8, stone.Darkened(0.09f));
        }

        // 几条不规则裂纹强化“残破石桥”主题，但不影响通行规则。
        DrawPixel(rect, 18, 17, 2, 7, stoneDark);
        DrawPixel(rect, 19, 23, 7, 2, stoneDark);
        DrawPixel(rect, 35, 31, 2, 7, stoneDark);
        DrawPixel(rect, 31, 30, 6, 2, stoneDark);
    }

    /// <summary>绘制据点：石质平台、墙垛和中央阵营旗座。</summary>
    private void DrawFort(Rect2 rect, Vector2I cell)
    {
        Color dirt = new(0.35f, 0.31f, 0.22f);
        Color stoneDark = new(0.25f, 0.26f, 0.24f);
        Color stone = new(0.48f, 0.48f, 0.43f);
        Color stoneLight = new(0.62f, 0.61f, 0.54f);
        DrawRect(rect, dirt, true);

        DrawPixel(rect, 4, 10, 44, 38, stoneDark);
        DrawPixel(rect, 7, 13, 38, 32, stone);
        for (int x = 7; x <= 39; x += 8)
        {
            DrawPixel(rect, x, 8, 5, 8, stoneLight);
        }
        DrawPixel(rect, 13, 22, 26, 4, stoneLight);
        DrawPixel(rect, 22, 26, 8, 19, stoneDark);
        DrawPixel(rect, 24, 28, 4, 17, new Color(0.20f, 0.17f, 0.14f));
    }

    /// <summary>
    /// 绘制移动范围、可攻击敌军、当前选择和锁定目标。
    /// 范围使用棋盘式像素网点，而不是整格半透明色块，让底下地形仍保持可读。
    /// </summary>
    private void DrawRangeOverlay()
    {
        UnitModel? selected = ReadSelectedUnit();
        if (selected is null)
        {
            return;
        }

        IReadOnlyCollection<Vector2I> reachable = ReadReachableCells();
        foreach (Vector2I cell in reachable)
        {
            DrawDitherOverlay(GridRect(cell), new Color(0.18f, 0.43f, 0.92f, 0.50f));
        }

        foreach (UnitModel enemy in ReadUnits().Where(unit => unit.IsAlive && unit.Team == UnitTeam.Enemy))
        {
            if (CombatRules.IsInAttackRange(selected, enemy))
            {
                DrawDitherOverlay(GridRect(enemy.GridPosition), new Color(0.86f, 0.16f, 0.18f, 0.58f));
                DrawPixelCorners(GridRect(enemy.GridPosition).Grow(-3), new Color(0.94f, 0.26f, 0.18f), 6);
            }
        }

        DrawPixelCorners(GridRect(selected.GridPosition).Grow(-2), new Color(0.98f, 0.82f, 0.20f), 9);

        UnitModel? target = ReadPendingTarget();
        if (target is not null)
        {
            Rect2 targetRect = GridRect(target.GridPosition).Grow(-1);
            DrawPixelCorners(targetRect, new Color(1.0f, 0.43f, 0.10f), 12);
            DrawPixelCorners(targetRect.Grow(-4), new Color(1.0f, 0.76f, 0.20f), 7);
        }
    }

    /// <summary>用规则排列的小色块绘制半透明范围覆盖。</summary>
    private void DrawDitherOverlay(Rect2 rect, Color color)
    {
        for (int y = 3; y < CellSize - 3; y += 8)
        {
            for (int x = 3; x < CellSize - 3; x += 8)
            {
                int offset = ((x / 8) + (y / 8)) % 2 == 0 ? 0 : 3;
                DrawPixel(rect, x + offset, y, 4, 4, color);
            }
        }
    }

    /// <summary>绘制像素四角框。</summary>
    private void DrawPixelCorners(Rect2 rect, Color color, float cornerLength)
    {
        const float thickness = 2.0f;
        Vector2 p = rect.Position;
        Vector2 e = rect.End;
        DrawRect(new Rect2(p, new Vector2(cornerLength, thickness)), color, true);
        DrawRect(new Rect2(p, new Vector2(thickness, cornerLength)), color, true);
        DrawRect(new Rect2(new Vector2(e.X - cornerLength, p.Y), new Vector2(cornerLength, thickness)), color, true);
        DrawRect(new Rect2(new Vector2(e.X - thickness, p.Y), new Vector2(thickness, cornerLength)), color, true);
        DrawRect(new Rect2(new Vector2(p.X, e.Y - thickness), new Vector2(cornerLength, thickness)), color, true);
        DrawRect(new Rect2(new Vector2(p.X, e.Y - cornerLength), new Vector2(thickness, cornerLength)), color, true);
        DrawRect(new Rect2(new Vector2(e.X - cornerLength, e.Y - thickness), new Vector2(cornerLength, thickness)), color, true);
        DrawRect(new Rect2(new Vector2(e.X - thickness, e.Y - cornerLength), new Vector2(thickness, cornerLength)), color, true);
    }

    /// <summary>
    /// 为所有存活单位绘制短像素 HP 条。
    /// 人物本体由更高 ZIndex 的 UnitCharacterLayer 绘制，因此 HP 条会稳定贴在脚下而不覆盖角色主体。
    /// </summary>
    private void DrawPixelHpBars()
    {
        foreach (UnitModel unit in ReadUnits().Where(unit => unit.IsAlive))
        {
            Vector2 center = GridCenter(unit.GridPosition);
            float ratio = unit.MaxHp <= 0 ? 0.0f : Mathf.Clamp((float)unit.CurrentHp / unit.MaxHp, 0.0f, 1.0f);
            Color hp = ratio > 0.55f
                ? new Color(0.30f, 0.80f, 0.30f)
                : ratio > 0.25f
                    ? new Color(0.86f, 0.68f, 0.18f)
                    : new Color(0.82f, 0.22f, 0.19f);

            Rect2 back = new(center + new Vector2(-18, 19), new Vector2(36, 6));
            DrawRect(back, new Color(0.04f, 0.045f, 0.04f), true);
            DrawRect(new Rect2(back.Position + Vector2.One, new Vector2(34 * ratio, 4)), hp, true);
        }
    }

    /// <summary>读取 MainGame 地形表。</summary>
    private IReadOnlyDictionary<Vector2I, TerrainType> ReadTerrain()
    {
        return _battleHost is not null && _terrainField?.GetValue(_battleHost) is IReadOnlyDictionary<Vector2I, TerrainType> terrain
            ? terrain
            : new Dictionary<Vector2I, TerrainType>();
    }

    /// <summary>读取全部战斗单位。</summary>
    private IReadOnlyList<UnitModel> ReadUnits()
    {
        return _battleHost is not null && _unitsField?.GetValue(_battleHost) is IReadOnlyList<UnitModel> units
            ? units
            : Array.Empty<UnitModel>();
    }

    /// <summary>读取当前选中单位。</summary>
    private UnitModel? ReadSelectedUnit()
    {
        return _battleHost is null || _selectedUnitField is null
            ? null
            : _selectedUnitField.GetValue(_battleHost) as UnitModel;
    }

    /// <summary>读取当前攻击目标。</summary>
    private UnitModel? ReadPendingTarget()
    {
        return _battleHost is null || _pendingAttackTargetField is null
            ? null
            : _pendingAttackTargetField.GetValue(_battleHost) as UnitModel;
    }

    /// <summary>读取当前移动范围。</summary>
    private IReadOnlyCollection<Vector2I> ReadReachableCells()
    {
        return _battleHost is not null && _reachableCellsField?.GetValue(_battleHost) is IReadOnlyCollection<Vector2I> cells
            ? cells
            : Array.Empty<Vector2I>();
    }

    /// <summary>安全读取一个整数私有字段。</summary>
    private int ReadInt(FieldInfo? field, int fallback)
    {
        return _battleHost is not null && field?.GetValue(_battleHost) is int value
            ? value
            : fallback;
    }

    /// <summary>返回指定格子的像素矩形。</summary>
    private static Rect2 GridRect(Vector2I cell)
    {
        return new Rect2(
            BoardOrigin + new Vector2(cell.X * CellSize, cell.Y * CellSize),
            new Vector2(CellSize, CellSize));
    }

    /// <summary>返回指定格子的像素中心。</summary>
    private static Vector2 GridCenter(Vector2I cell)
    {
        return BoardOrigin + new Vector2(
            cell.X * CellSize + CellSize / 2.0f,
            cell.Y * CellSize + CellSize / 2.0f);
    }

    /// <summary>在格子内部绘制硬边像素矩形。</summary>
    private void DrawPixel(Rect2 cellRect, int x, int y, int width, int height, Color color)
    {
        DrawRect(
            new Rect2(cellRect.Position + new Vector2(x, y), new Vector2(width, height)),
            color,
            true);
    }

    /// <summary>生成只依赖格坐标的稳定伪随机种子，重进关卡不会改变地表装饰。</summary>
    private static int StableCellSeed(Vector2I cell, int salt)
    {
        return unchecked(cell.X * 73856093 ^ cell.Y * 19349663 ^ salt * 83492791);
    }

    /// <summary>返回始终非负的取模结果，便于把稳定种子映射到格内坐标。</summary>
    private static int PositiveMod(int value, int modulus)
    {
        int result = value % modulus;
        return result < 0 ? result + modulus : result;
    }
}
