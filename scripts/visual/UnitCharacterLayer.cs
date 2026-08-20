using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 在战棋地图上绘制 2D 人物小人，并负责把逻辑格移动表现成连续的逐格行走动画。
/// 地图人物优先使用方向化多帧像素动画；素材不完整时依次回退到单张 map.png 和程序绘制人物。
/// </summary>
public partial class UnitCharacterLayer : Node2D
{
    /// <summary>当前需要绘制的全部战斗单位。</summary>
    public IReadOnlyList<UnitModel> Units { get; set; } = Array.Empty<UnitModel>();

    /// <summary>当前选中的单位，用于显示选中状态和更明显的待机动画。</summary>
    public UnitModel? SelectedUnit { get; set; }

    /// <summary>当前章节地形表；未列出的格子按平地处理。</summary>
    public IReadOnlyDictionary<Vector2I, TerrainType> Terrain { get; set; } =
        new Dictionary<Vector2I, TerrainType>();

    /// <summary>当前地图宽度，单位为格。</summary>
    public int GridWidth { get; set; } = 15;

    /// <summary>当前地图高度，单位为格。</summary>
    public int GridHeight { get; set; } = 10;

    /// <summary>战棋地图左上角像素坐标。</summary>
    public Vector2 BoardOrigin { get; set; }

    /// <summary>单格像素尺寸。</summary>
    public int CellSize { get; set; } = 52;

    /// <summary>只要任意单位仍处于逐格移动动画中，就返回 true。</summary>
    public bool IsMovementAnimating => _motions.Count > 0;

    /// <summary>人物待机/行走动画累计时间。</summary>
    private double _elapsed;

    /// <summary>上一次观察到的逻辑格位置，用于检测 MainGame 已经确认的移动。</summary>
    private readonly Dictionary<string, Vector2I> _lastGridPositions = new(StringComparer.Ordinal);

    /// <summary>当前正在播放的单位移动动画。</summary>
    private readonly Dictionary<string, UnitMotion> _motions = new(StringComparer.Ordinal);

    /// <summary>保存每个单位最后一次面朝方向，静止后继续保持移动结束时的朝向。</summary>
    private readonly Dictionary<string, CharacterFacing> _facings = new(StringComparer.Ordinal);

    /// <summary>单格行走动画耗时；保持短促，避免战棋操作拖沓。</summary>
    private const float SecondsPerTile = 0.14f;

    /// <summary>真正序列帧行走动画的默认播放速度。</summary>
    private const float WalkFramesPerSecond = 9.0f;

    /// <summary>真正序列帧待机动画的默认播放速度。</summary>
    private const float IdleFramesPerSecond = 2.5f;

    /// <summary>
    /// 节点进入场景后使用最近邻纹理过滤。
    /// 这样低分辨率人物帧被放大时保持清晰像素边缘，不会被线性采样抹糊。
    /// </summary>
    public override void _Ready()
    {
        TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
    }

    /// <summary>
    /// 每帧检测逻辑位置变化、推进逐格移动和待机动画，并请求重绘。
    /// 这里只改变视觉位置，不回写 UnitModel.GridPosition，因此不会影响战斗规则。
    /// </summary>
    public override void _Process(double delta)
    {
        _elapsed += delta;
        DetectGridPositionChanges();
        AdvanceMotions((float)delta);
        QueueRedraw();
    }

    /// <summary>
    /// 绘制所有存活单位。
    /// </summary>
    public override void _Draw()
    {
        foreach (UnitModel unit in Units.Where(unit => unit.IsAlive))
        {
            DrawUnit(unit);
        }
    }

    /// <summary>
    /// 检测 MainGame 已确认的格子变化，并为该变化重建一条合法地形路径。
    /// 第一次看见单位时只记录当前位置，不播放从地图外飞入的动画。
    /// </summary>
    private void DetectGridPositionChanges()
    {
        foreach (UnitModel unit in Units.Where(unit => unit.IsAlive))
        {
            if (!_lastGridPositions.TryGetValue(unit.Id, out Vector2I previous))
            {
                _lastGridPositions[unit.Id] = unit.GridPosition;
                _facings.TryAdd(unit.Id, CharacterFacing.Down);
                continue;
            }

            if (previous == unit.GridPosition)
            {
                continue;
            }

            List<Vector2I> path = BuildVisualPath(unit, previous, unit.GridPosition);
            _motions[unit.Id] = new UnitMotion(unit, path);
            _lastGridPositions[unit.Id] = unit.GridPosition;
        }

        // 已经从章节中移除或死亡的单位不再保留视觉缓存，避免长期章节中无意义增长。
        HashSet<string> liveIds = Units.Where(unit => unit.IsAlive).Select(unit => unit.Id).ToHashSet(StringComparer.Ordinal);
        foreach (string staleId in _lastGridPositions.Keys.Where(id => !liveIds.Contains(id)).ToList())
        {
            _lastGridPositions.Remove(staleId);
            _motions.Remove(staleId);
            _facings.Remove(staleId);
        }
    }

    /// <summary>
    /// 推进所有正在播放的逐格移动动画。
    /// 一条路径可能包含多格；每完成一格就进入下一段，直到抵达逻辑目标格。
    /// </summary>
    private void AdvanceMotions(float delta)
    {
        foreach ((string unitId, UnitMotion motion) in _motions.ToList())
        {
            motion.Progress += delta / SecondsPerTile;

            while (motion.Progress >= 1.0f)
            {
                motion.Progress -= 1.0f;
                motion.SegmentIndex++;

                if (motion.SegmentIndex >= motion.Path.Count - 1)
                {
                    _motions.Remove(unitId);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// 绘制单个地图人物。
    /// 有方向序列帧时真正切帧；没有多帧素材时继续兼容旧单图和程序占位模型。
    /// </summary>
    private void DrawUnit(UnitModel unit)
    {
        Vector2 visualCenter = ResolveVisualCenter(unit);
        bool moving = _motions.ContainsKey(unit.Id);
        CharacterFacing facing = ResolveFacing(unit);
        CharacterAnimationState animationState = moving
            ? CharacterAnimationState.Walk
            : CharacterAnimationState.Idle;

        Texture2D? animationFrame = ResolveAnimationFrame(unit, animationState, facing);
        Vector2 fallbackOffset = animationFrame is null ? AnimationOffset(unit) : Vector2.Zero;
        Vector2 characterCenter = visualCenter + fallbackOffset;

        Color teamColor = unit.Team == UnitTeam.Player
            ? new Color(0.20f, 0.52f, 1.0f)
            : new Color(0.93f, 0.24f, 0.24f);

        if (unit.HasActed)
        {
            teamColor = teamColor.Darkened(0.35f);
        }

        // 阵营底圈跟随视觉位置移动，让逐格行走时人物不会与阵营标记脱离。
        DrawCircle(visualCenter + new Vector2(0, 11), 20.0f, new Color(0.04f, 0.05f, 0.07f, 0.92f));
        DrawCircle(visualCenter + new Vector2(0, 11), 20.5f, teamColor, false, 3.0f);

        if (animationFrame is not null)
        {
            DrawMapTexture(unit, animationFrame, characterCenter);
        }
        else
        {
            Texture2D? mapTexture = CharacterAssetResolver.TryLoad(unit, CharacterArtSlot.Map);
            if (mapTexture is not null)
            {
                DrawMapTexture(unit, mapTexture, characterCenter);
            }
            else
            {
                DrawProceduralUnit(unit, characterCenter);
            }
        }

        if (ReferenceEquals(unit, SelectedUnit))
        {
            // 选中单位增加金色外环；移动时外环也跟随人物走完整条路径。
            DrawCircle(visualCenter + new Vector2(0, 11), 24.0f, new Color(1.0f, 0.84f, 0.25f), false, 2.5f);
        }
    }

    /// <summary>
    /// 根据当前动画状态、朝向和时间选择一张真正的地图序列帧。
    /// 行走帧优先跟随当前路径段进度，使移动速度改变后步伐仍与格子移动同步。
    /// </summary>
    private Texture2D? ResolveAnimationFrame(
        UnitModel unit,
        CharacterAnimationState state,
        CharacterFacing facing)
    {
        int frameCount = CharacterAssetResolver.GetMapFrameCount(unit, state, facing);
        if (frameCount <= 0)
        {
            return null;
        }

        int frameIndex;
        if (state == CharacterAnimationState.Walk && _motions.TryGetValue(unit.Id, out UnitMotion? motion))
        {
            float pathPhase = motion.SegmentIndex + Mathf.Clamp(motion.Progress, 0.0f, 1.0f);
            frameIndex = (int)MathF.Floor(pathPhase * WalkFramesPerSecond * SecondsPerTile) % frameCount;
        }
        else
        {
            frameIndex = (int)Math.Floor(_elapsed * IdleFramesPerSecond) % frameCount;
        }

        return CharacterAssetResolver.TryLoadMapFrame(unit, state, facing, frameIndex);
    }

    /// <summary>
    /// 返回单位当前面朝方向。
    /// 移动期间根据正在播放的路径段更新朝向；静止时保持最后一次方向。
    /// </summary>
    private CharacterFacing ResolveFacing(UnitModel unit)
    {
        if (_motions.TryGetValue(unit.Id, out UnitMotion? motion) && motion.Path.Count >= 2)
        {
            int segmentIndex = Mathf.Clamp(motion.SegmentIndex, 0, motion.Path.Count - 2);
            Vector2I delta = motion.Path[segmentIndex + 1] - motion.Path[segmentIndex];
            CharacterFacing facing = delta switch
            {
                { X: < 0 } => CharacterFacing.Left,
                { X: > 0 } => CharacterFacing.Right,
                { Y: < 0 } => CharacterFacing.Up,
                _ => CharacterFacing.Down
            };

            _facings[unit.Id] = facing;
            return facing;
        }

        return _facings.TryGetValue(unit.Id, out CharacterFacing cached)
            ? cached
            : CharacterFacing.Down;
    }

    /// <summary>
    /// 返回单位当前用于绘制的像素中心。
    /// 移动中在当前路径段起终点之间做平滑插值；静止时直接使用逻辑格中心。
    /// </summary>
    private Vector2 ResolveVisualCenter(UnitModel unit)
    {
        if (!_motions.TryGetValue(unit.Id, out UnitMotion? motion) || motion.Path.Count < 2)
        {
            return GridCenter(unit.GridPosition);
        }

        int fromIndex = Mathf.Clamp(motion.SegmentIndex, 0, motion.Path.Count - 2);
        Vector2 from = GridCenter(motion.Path[fromIndex]);
        Vector2 to = GridCenter(motion.Path[fromIndex + 1]);

        // SmoothStep 让每格起步和停步更自然，避免机械匀速滑动。
        float t = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp(motion.Progress, 0.0f, 1.0f));
        return from.Lerp(to, t);
    }

    /// <summary>
    /// 根据地形移动消耗与当前其他单位阻挡，重建起点到目标格的最短合法路径。
    /// 目标已经由 MainGame 验证过可达；如果表现层仍无法重建路径，则使用直达两点作为安全回退。
    /// </summary>
    private List<Vector2I> BuildVisualPath(UnitModel movingUnit, Vector2I start, Vector2I destination)
    {
        if (start == destination)
        {
            return new List<Vector2I> { start };
        }

        Dictionary<Vector2I, int> bestCosts = new() { [start] = 0 };
        Dictionary<Vector2I, Vector2I> previous = new();
        PriorityQueue<Vector2I, int> frontier = new();
        frontier.Enqueue(start, 0);

        HashSet<Vector2I> occupied = Units
            .Where(unit => unit.IsAlive && !ReferenceEquals(unit, movingUnit))
            .Select(unit => unit.GridPosition)
            .ToHashSet();

        while (frontier.TryDequeue(out Vector2I current, out int currentCost))
        {
            if (current == destination)
            {
                break;
            }

            if (bestCosts.TryGetValue(current, out int bestKnown) && currentCost > bestKnown)
            {
                continue;
            }

            foreach (Vector2I direction in CardinalDirections())
            {
                Vector2I next = current + direction;
                if (!IsInsideBoard(next) || (occupied.Contains(next) && next != destination))
                {
                    continue;
                }

                TerrainDefinition terrain = TerrainRules.Get(TerrainAt(next));
                if (!terrain.Passable)
                {
                    continue;
                }

                int nextCost = currentCost + terrain.MoveCost;
                if (bestCosts.TryGetValue(next, out int existingCost) && existingCost <= nextCost)
                {
                    continue;
                }

                bestCosts[next] = nextCost;
                previous[next] = current;
                frontier.Enqueue(next, nextCost);
            }
        }

        if (!bestCosts.ContainsKey(destination))
        {
            // 逻辑层已经允许该移动时，表现层不应阻断游戏；极端情况下至少做起点到终点的视觉移动。
            return new List<Vector2I> { start, destination };
        }

        List<Vector2I> path = new() { destination };
        Vector2I cursor = destination;
        while (cursor != start && previous.TryGetValue(cursor, out Vector2I parent))
        {
            cursor = parent;
            path.Add(cursor);
        }

        path.Reverse();
        return path;
    }

    /// <summary>
    /// 绘制地图人物纹理。
    /// 纹理使用最近邻过滤并缩放到单格范围，保持像素边缘清晰。
    /// </summary>
    private void DrawMapTexture(UnitModel unit, Texture2D texture, Vector2 center)
    {
        Rect2 target = new(center + new Vector2(-23, -29), new Vector2(46, 54));
        DrawTextureRect(texture, target, false);

        if (unit.HasActed)
        {
            DrawRect(target, new Color(0.04f, 0.05f, 0.07f, 0.38f), true);
        }
    }

    /// <summary>
    /// 绘制无正式素材时使用的程序化小比例人物。
    /// </summary>
    private void DrawProceduralUnit(UnitModel unit, Vector2 center)
    {
        CharacterAppearanceDefinition appearance = CharacterAppearanceCatalog.Get(unit);

        if (appearance.HasCape)
        {
            Rect2 cape = new(center + new Vector2(-14, -1), new Vector2(28, 28));
            DrawRect(cape, appearance.AccentColor.Darkened(0.18f), true);
        }

        Rect2 body = new(center + new Vector2(-11, 2), new Vector2(22, 25));
        DrawRect(body, appearance.OutfitColor, true);
        DrawRect(body, appearance.AccentColor.Darkened(0.45f), false, 2.0f);

        Vector2 headCenter = center + new Vector2(0, -8);
        DrawCircle(headCenter, 10.0f, appearance.SkinColor);
        DrawCircle(headCenter + new Vector2(0, -4), 10.5f, appearance.HairColor);
        DrawRect(new Rect2(headCenter + new Vector2(-9, 0), new Vector2(18, 8)), appearance.SkinColor, true);

        DrawWeaponSilhouette(center, appearance);

        if (unit.HasActed)
        {
            DrawRect(new Rect2(center + new Vector2(-16, -19), new Vector2(32, 48)), new Color(0.02f, 0.03f, 0.04f, 0.26f), true);
        }
    }

    /// <summary>
    /// 单张 map.png 或程序占位人物没有真正帧动画时使用的视觉补偿。
    /// 真正序列帧存在时不会再额外上下晃动，避免出现“切帧同时漂浮”的重复动画。
    /// </summary>
    private Vector2 AnimationOffset(UnitModel unit)
    {
        if (_motions.ContainsKey(unit.Id))
        {
            float walkBob = Mathf.Abs(Mathf.Sin((float)_elapsed * 22.0f)) * 3.0f;
            return new Vector2(0, -walkBob);
        }

        if (unit.HasActed)
        {
            return Vector2.Zero;
        }

        float phase = StableVisualPhase(unit.Id);
        bool selected = ReferenceEquals(unit, SelectedUnit);
        float speed = selected ? 6.0f : 3.2f;
        float amplitude = selected ? 2.2f : 1.0f;
        float y = Mathf.Sin((float)_elapsed * speed + phase) * amplitude;
        return new Vector2(0, y);
    }

    /// <summary>
    /// 根据人物 ID 生成稳定视觉相位，避免所有单位同时上下移动。
    /// </summary>
    private static float StableVisualPhase(string id)
    {
        int sum = 0;
        foreach (char character in id)
        {
            sum += character;
        }

        return (sum % 31) * 0.17f;
    }

    /// <summary>
    /// 根据职业/武器类型绘制不同的简化武器轮廓。
    /// </summary>
    private void DrawWeaponSilhouette(Vector2 center, CharacterAppearanceDefinition appearance)
    {
        Color weaponColor = new(0.88f, 0.88f, 0.90f);
        Color darkWeaponColor = new(0.28f, 0.24f, 0.20f);

        switch (appearance.WeaponSilhouette)
        {
            case CharacterWeaponSilhouette.Spear:
                DrawLine(center + new Vector2(12, 20), center + new Vector2(24, -17), darkWeaponColor, 4.0f);
                DrawLine(center + new Vector2(23, -16), center + new Vector2(27, -23), weaponColor, 3.0f);
                break;
            case CharacterWeaponSilhouette.Bow:
                DrawLine(center + new Vector2(15, -11), center + new Vector2(21, 21), darkWeaponColor, 3.0f);
                DrawLine(center + new Vector2(15, -11), center + new Vector2(26, 5), appearance.AccentColor, 2.0f);
                DrawLine(center + new Vector2(26, 5), center + new Vector2(21, 21), appearance.AccentColor, 2.0f);
                break;
            case CharacterWeaponSilhouette.Tome:
                DrawRect(new Rect2(center + new Vector2(13, 6), new Vector2(13, 17)), appearance.AccentColor, true);
                DrawCircle(center + new Vector2(19.5f, 14.5f), 3.0f, new Color(0.92f, 0.72f, 1.0f));
                break;
            default:
                DrawLine(center + new Vector2(13, 19), center + new Vector2(23, -14), darkWeaponColor, 5.0f);
                DrawLine(center + new Vector2(22, -13), center + new Vector2(26, -21), weaponColor, 4.0f);
                break;
        }
    }

    /// <summary>获取指定格地形；未配置的格子默认是平地。</summary>
    private TerrainType TerrainAt(Vector2I cell)
    {
        return Terrain.TryGetValue(cell, out TerrainType terrain) ? terrain : TerrainType.Plain;
    }

    /// <summary>判断格子是否位于当前地图范围内。</summary>
    private bool IsInsideBoard(Vector2I cell)
    {
        return cell.X >= 0 && cell.X < GridWidth && cell.Y >= 0 && cell.Y < GridHeight;
    }

    /// <summary>返回战棋寻路使用的四个正交方向。</summary>
    private static IEnumerable<Vector2I> CardinalDirections()
    {
        yield return Vector2I.Up;
        yield return Vector2I.Down;
        yield return Vector2I.Left;
        yield return Vector2I.Right;
    }

    /// <summary>
    /// 把逻辑格坐标转换为当前地图格子的像素中心。
    /// </summary>
    private Vector2 GridCenter(Vector2I cell)
    {
        return BoardOrigin + new Vector2(
            cell.X * CellSize + CellSize / 2.0f,
            cell.Y * CellSize + CellSize / 2.0f);
    }

    /// <summary>
    /// 保存单个单位当前的视觉移动状态。
    /// Path 包含起点与终点；SegmentIndex 指向正在播放的路径段起点。
    /// </summary>
    private sealed class UnitMotion
    {
        /// <summary>创建一条单位移动动画。</summary>
        public UnitMotion(UnitModel unit, List<Vector2I> path)
        {
            Unit = unit;
            Path = path;
        }

        /// <summary>正在移动的单位。</summary>
        public UnitModel Unit { get; }

        /// <summary>完整逐格路径。</summary>
        public List<Vector2I> Path { get; }

        /// <summary>当前路径段起点索引。</summary>
        public int SegmentIndex { get; set; }

        /// <summary>当前路径段 0~1 的动画进度。</summary>
        public float Progress { get; set; }
    }
}
