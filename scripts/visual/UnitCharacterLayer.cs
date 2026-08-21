using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 在战棋地图上绘制 2D 人物小人，并把逻辑格移动表现成连续的逐格行走动画。
/// 地图人物优先使用方向化多帧像素动画；素材不完整时依次回退到单张 map.png 和原创复古像素职业模板。
/// </summary>
public partial class UnitCharacterLayer : Node2D
{
    /// <summary>当前需要绘制的全部战斗单位。</summary>
    public IReadOnlyList<UnitModel> Units { get; set; } = Array.Empty<UnitModel>();

    /// <summary>当前选中的单位，用于显示选中状态。</summary>
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

    /// <summary>程序像素模板中一个逻辑像素对应的屏幕像素尺寸。</summary>
    private const float PixelScale = 2.0f;

    /// <summary>
    /// 正式地图人物允许占用的最大整数像素边长。
    /// 32×32 原图保持 1× 原生尺寸，24×24/16×16 等更小素材最多使用 2×，绝不拉成非整数比例。
    /// </summary>
    private const int FormalMapMaxFootprint = 48;

    /// <summary>正式地图人物脚底相对格子中心的像素偏移。</summary>
    private const float FormalMapFeetOffsetY = 20.0f;

    /// <summary>
    /// 节点进入场景后使用最近邻纹理过滤。
    /// 低分辨率人物帧被放大时保持清晰像素边缘，不使用线性平滑。
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

    /// <summary>绘制所有存活单位。</summary>
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

        // 死亡或离开章节的单位不再保留视觉缓存。
        HashSet<string> liveIds = Units
            .Where(unit => unit.IsAlive)
            .Select(unit => unit.Id)
            .ToHashSet(StringComparer.Ordinal);

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
    /// 有方向序列帧时真正切帧；没有多帧素材时继续兼容旧单图和原创像素职业模板。
    /// </summary>
    private void DrawUnit(UnitModel unit)
    {
        // 地图人物中心最终锁到整数屏幕像素，最近邻纹理在移动期间不会因为半像素采样产生发虚边缘。
        Vector2 visualCenter = RoundVector(ResolveVisualCenter(unit));
        bool moving = _motions.ContainsKey(unit.Id);
        CharacterFacing facing = ResolveFacing(unit);
        CharacterAnimationState animationState = moving
            ? CharacterAnimationState.Walk
            : CharacterAnimationState.Idle;

        Texture2D? animationFrame = ResolveAnimationFrame(unit, animationState, facing);
        Vector2 fallbackOffset = animationFrame is null ? AnimationOffset(unit) : Vector2.Zero;
        Vector2 characterCenter = RoundVector(visualCenter + fallbackOffset);

        Color teamColor = unit.Team == UnitTeam.Player
            ? new Color(0.20f, 0.52f, 1.0f)
            : new Color(0.93f, 0.24f, 0.24f);

        if (unit.HasActed)
        {
            teamColor = teamColor.Darkened(0.35f);
        }

        // 阵营标记使用扁平像素底座，避免大圆环破坏复古画面。
        DrawRect(
            new Rect2(visualCenter + new Vector2(-17, 20), new Vector2(34, 4)),
            new Color(0.04f, 0.05f, 0.07f, 0.92f),
            true);
        DrawRect(
            new Rect2(visualCenter + new Vector2(-14, 21), new Vector2(28, 2)),
            teamColor,
            true);

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
                DrawProceduralPixelUnit(unit, characterCenter, facing, moving);
            }
        }

        if (ReferenceEquals(unit, SelectedUnit))
        {
            // 选中单位使用四角像素框，不使用平滑圆形描边。
            Color selection = new(1.0f, 0.84f, 0.25f);
            Rect2 bounds = new(visualCenter + new Vector2(-22, -25), new Vector2(44, 50));
            DrawPixelSelectionCorners(bounds, selection);
        }
    }

    /// <summary>
    /// 根据当前动画状态、朝向和时间选择一张真正的地图序列帧。
    /// 行走帧跟随路径段进度，使移动速度改变后步伐仍与格子移动同步。
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
    /// 移动中在当前路径段起终点之间做平滑插值；DrawUnit 最终会锁到整数屏幕像素，兼顾流畅移动与像素清晰度。
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
        float t = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp(motion.Progress, 0.0f, 1.0f));
        return from.Lerp(to, t);
    }

    /// <summary>
    /// 根据地形移动消耗与单位阵营阻挡重建起点到目标格的最短合法路径。
    /// 同阵营单位允许作为中间路径经过但不能作为最终落脚格；敌军仍然完全阻挡。
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

        // 视觉路径必须与逻辑规则一致：友军可以穿过，只有敌对存活单位会完全封锁格子。
        HashSet<Vector2I> enemyBlockedCells = Units
            .Where(unit => unit.IsAlive &&
                           !ReferenceEquals(unit, movingUnit) &&
                           unit.Team != movingUnit.Team)
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
                if (!IsInsideBoard(next) || enemyBlockedCells.Contains(next))
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
    /// 绘制正式地图人物纹理。
    /// 保持源图宽高比例，只使用整数倍率，并把脚底统一对齐到格子中心下方固定基准线。
    /// </summary>
    private void DrawMapTexture(UnitModel unit, Texture2D texture, Vector2 center)
    {
        int sourceWidth = Math.Max(1, texture.GetWidth());
        int sourceHeight = Math.Max(1, texture.GetHeight());
        int largestSide = Math.Max(sourceWidth, sourceHeight);

        // 32×32 正式帧保持 1× 原生像素；更小的 16/24px 旧素材最多 2×，避免出现 1.4375× 一类模糊缩放。
        int integerScale = Math.Clamp(FormalMapMaxFootprint / largestSide, 1, 2);
        int targetWidth = sourceWidth * integerScale;
        int targetHeight = sourceHeight * integerScale;
        float targetX = Mathf.Round(center.X - targetWidth / 2.0f);
        float feetY = Mathf.Round(center.Y + FormalMapFeetOffsetY);
        float targetY = feetY - targetHeight;
        Rect2 target = new(new Vector2(targetX, targetY), new Vector2(targetWidth, targetHeight));

        DrawTextureRect(texture, target, false);

        if (unit.HasActed)
        {
            DrawRect(target, new Color(0.04f, 0.05f, 0.07f, 0.34f), true);
        }
    }

    /// <summary>
    /// 在没有正式地图帧时绘制原创复古像素职业模板。
    /// 轻装、重甲、长袍拥有不同身体轮廓，并继续使用方向化武器和两步行走循环。
    /// </summary>
    private void DrawProceduralPixelUnit(
        UnitModel unit,
        Vector2 center,
        CharacterFacing facing,
        bool moving)
    {
        CharacterAppearanceDefinition appearance = CharacterAppearanceCatalog.Get(unit);
        // 程序人物原点锁到 2px 网格，使所有逻辑像素块在移动时仍保持同一像素相位。
        Vector2 origin = SnapToGrid(center + new Vector2(-16, -20), PixelScale);
        bool alternateStep = moving && ((int)Math.Floor(_elapsed * 10.0) % 2 == 1);

        Color outline = new(0.08f, 0.07f, 0.08f);
        Color shadow = appearance.OutfitColor.Darkened(0.32f);
        Color highlight = appearance.OutfitColor.Lightened(0.18f);
        Color metal = new(0.78f, 0.80f, 0.78f);
        Color darkMetal = new(0.30f, 0.31f, 0.30f);

        if (appearance.HasCape)
        {
            DrawPixelBlock(origin, 4, 9, 8, 8, appearance.AccentColor.Darkened(0.24f));
            DrawPixelBlock(origin, 5, 16, 6, 2, appearance.AccentColor.Darkened(0.36f));
        }

        DrawPixelBody(origin, appearance, alternateStep, outline, shadow, highlight, darkMetal);

        // 头部缩小一档，让地图小人从大头模板转向更修长的古典战棋比例。
        DrawPixelBlock(origin, 6, 2, 5, 5, outline);
        DrawPixelBlock(origin, 7, 3, 3, 3, appearance.SkinColor);
        DrawPixelBlock(origin, 6, 2, 5, 2, appearance.HairColor);
        DrawPixelBlock(origin, 6, 4, 1, 3, appearance.HairColor.Darkened(0.08f));

        if (facing == CharacterFacing.Up)
        {
            DrawPixelBlock(origin, 8, 4, 3, 3, appearance.HairColor);
        }
        else
        {
            int eyeX = facing switch
            {
                CharacterFacing.Left => 7,
                CharacterFacing.Right => 9,
                _ => 8
            };
            DrawPixelBlock(origin, eyeX, 5, 1, 1, outline);
        }

        DrawPixelWeapon(origin, appearance, facing, metal, darkMetal);

        if (unit.HasActed)
        {
            // 已行动时用棋盘式暗化而不是半透明大矩形，保留像素质感。
            Color actedShade = new(0.02f, 0.03f, 0.04f, 0.34f);
            for (int y = 2; y < 19; y += 2)
            {
                DrawPixelBlock(origin, 3, y, 11, 1, actedShade);
            }
        }
    }

    /// <summary>
    /// 根据身体模板绘制地图职业体型。
    /// 重甲更宽更厚，长袍下摆覆盖腿部，轻装保持窄身和明显两步行走。
    /// </summary>
    private void DrawPixelBody(
        Vector2 origin,
        CharacterAppearanceDefinition appearance,
        bool alternateStep,
        Color outline,
        Color shadow,
        Color highlight,
        Color darkMetal)
    {
        switch (appearance.BodySilhouette)
        {
            case CharacterBodySilhouette.Armored:
                // 重甲：肩甲横向外扩，胸甲更方，腿甲和靴子更厚。
                DrawPixelBlock(origin, 3, 8, 10, 8, outline);
                DrawPixelBlock(origin, 4, 9, 8, 6, appearance.OutfitColor);
                DrawPixelBlock(origin, 3, 9, 2, 4, darkMetal);
                DrawPixelBlock(origin, 11, 9, 2, 4, darkMetal);
                DrawPixelBlock(origin, 5, 9, 2, 5, highlight);
                DrawPixelBlock(origin, 9, 12, 3, 3, shadow);
                DrawPixelBlock(origin, 4, 12, 8, 1, appearance.AccentColor);

                int armoredLeft = alternateStep ? 4 : 5;
                int armoredRight = alternateStep ? 10 : 9;
                DrawPixelBlock(origin, armoredLeft, 15, 3, 4, outline);
                DrawPixelBlock(origin, armoredRight, 15, 3, 4, outline);
                DrawPixelBlock(origin, armoredLeft, 15, 2, 3, shadow);
                DrawPixelBlock(origin, armoredRight, 15, 2, 3, shadow);
                break;

            case CharacterBodySilhouette.Robed:
                // 长袍：上身窄，下摆从腰部向外展开，移动时下摆左右交替一像素。
                DrawPixelBlock(origin, 4, 8, 8, 7, outline);
                DrawPixelBlock(origin, 5, 9, 6, 5, appearance.OutfitColor);
                DrawPixelBlock(origin, 5, 9, 2, 4, highlight);
                DrawPixelBlock(origin, 9, 11, 2, 3, shadow);
                DrawPixelBlock(origin, 5, 12, 6, 1, appearance.AccentColor);

                int robeShift = alternateStep ? 1 : 0;
                DrawPixelBlock(origin, 3 - robeShift, 14, 10 + robeShift * 2, 5, outline);
                DrawPixelBlock(origin, 4 - robeShift, 14, 8 + robeShift * 2, 4, appearance.OutfitColor);
                DrawPixelBlock(origin, 4 - robeShift, 14, 2, 3, highlight.Darkened(0.05f));
                DrawPixelBlock(origin, 10 + robeShift, 15, 2, 3, shadow);
                DrawPixelBlock(origin, 4, 18, 3, 1, darkMetal);
                DrawPixelBlock(origin, 9, 18, 3, 1, darkMetal);
                break;

            default:
                // 轻装：窄肩短上衣，两条腿清晰分离并进行经典两步循环。
                DrawPixelBlock(origin, 4, 8, 8, 8, outline);
                DrawPixelBlock(origin, 5, 8, 6, 7, appearance.OutfitColor);
                DrawPixelBlock(origin, 5, 8, 2, 5, highlight);
                DrawPixelBlock(origin, 9, 12, 2, 3, shadow);
                DrawPixelBlock(origin, 5, 11, 6, 1, appearance.AccentColor);

                int leftLegX = alternateStep ? 5 : 6;
                int rightLegX = alternateStep ? 10 : 9;
                DrawPixelBlock(origin, leftLegX, 15, 2, 4, outline);
                DrawPixelBlock(origin, rightLegX, 15, 2, 4, outline);
                DrawPixelBlock(origin, leftLegX, 15, 2, 2, shadow);
                DrawPixelBlock(origin, rightLegX, 15, 2, 2, shadow);
                break;
        }
    }

    /// <summary>
    /// 根据职业武器轮廓绘制地图像素武器。
    /// 武器位置跟随左右朝向翻转；上下方向使用较紧凑的正面/背面轮廓。
    /// </summary>
    private void DrawPixelWeapon(
        Vector2 origin,
        CharacterAppearanceDefinition appearance,
        CharacterFacing facing,
        Color metal,
        Color darkMetal)
    {
        int side = facing == CharacterFacing.Left ? -1 : 1;
        int anchorX = side < 0 ? 3 : 12;

        switch (appearance.WeaponSilhouette)
        {
            case CharacterWeaponSilhouette.Spear:
                DrawPixelBlock(origin, anchorX, 4, 1, 13, darkMetal);
                DrawPixelBlock(origin, anchorX, 2, 1, 3, metal);
                DrawPixelBlock(origin, anchorX - 1, 2, 3, 1, metal);
                break;

            case CharacterWeaponSilhouette.Bow:
                DrawPixelBlock(origin, anchorX, 5, 1, 3, appearance.AccentColor.Darkened(0.18f));
                DrawPixelBlock(origin, anchorX + side, 8, 1, 4, appearance.AccentColor.Darkened(0.18f));
                DrawPixelBlock(origin, anchorX, 12, 1, 3, appearance.AccentColor.Darkened(0.18f));
                DrawPixelBlock(origin, anchorX, 7, 1, 6, darkMetal);
                break;

            case CharacterWeaponSilhouette.Tome:
                DrawPixelBlock(origin, anchorX - (side < 0 ? 1 : 0), 10, 2, 3, darkMetal);
                DrawPixelBlock(origin, anchorX - (side < 0 ? 1 : 0), 10, 1, 2, appearance.AccentColor);
                DrawPixelBlock(origin, anchorX + side, 8, 1, 1, appearance.AccentColor.Lightened(0.28f));
                break;

            default:
                DrawPixelBlock(origin, anchorX, 6, 1, 8, darkMetal);
                DrawPixelBlock(origin, anchorX, 4, 1, 4, metal);
                DrawPixelBlock(origin, anchorX - 1, 8, 3, 1, appearance.AccentColor.Darkened(0.20f));
                break;
        }
    }

    /// <summary>
    /// 绘制一个逻辑像素矩形。
    /// 所有程序人物都通过这个方法落到整数网格，避免出现抗锯齿或半像素边缘。
    /// </summary>
    private void DrawPixelBlock(Vector2 origin, int x, int y, int width, int height, Color color)
    {
        DrawRect(
            new Rect2(
                origin + new Vector2(x * PixelScale, y * PixelScale),
                new Vector2(width * PixelScale, height * PixelScale)),
            color,
            true);
    }

    /// <summary>绘制选中人物四个角的像素框。</summary>
    private void DrawPixelSelectionCorners(Rect2 bounds, Color color)
    {
        const float corner = 7.0f;
        const float thickness = 2.0f;

        DrawRect(new Rect2(bounds.Position, new Vector2(corner, thickness)), color, true);
        DrawRect(new Rect2(bounds.Position, new Vector2(thickness, corner)), color, true);
        DrawRect(new Rect2(new Vector2(bounds.End.X - corner, bounds.Position.Y), new Vector2(corner, thickness)), color, true);
        DrawRect(new Rect2(new Vector2(bounds.End.X - thickness, bounds.Position.Y), new Vector2(thickness, corner)), color, true);
        DrawRect(new Rect2(new Vector2(bounds.Position.X, bounds.End.Y - thickness), new Vector2(corner, thickness)), color, true);
        DrawRect(new Rect2(new Vector2(bounds.Position.X, bounds.End.Y - corner), new Vector2(thickness, corner)), color, true);
        DrawRect(new Rect2(new Vector2(bounds.End.X - corner, bounds.End.Y - thickness), new Vector2(corner, thickness)), color, true);
        DrawRect(new Rect2(new Vector2(bounds.End.X - thickness, bounds.End.Y - corner), new Vector2(thickness, corner)), color, true);
    }

    /// <summary>
    /// 单张 map.png 或程序像素模板没有真正帧动画时使用的视觉补偿。
    /// 真正序列帧存在时不会再额外上下晃动。
    /// </summary>
    private Vector2 AnimationOffset(UnitModel unit)
    {
        if (_motions.ContainsKey(unit.Id))
        {
            // 程序像素模板只做整数像素级起伏，避免产生平滑漂浮感。
            int step = ((int)Math.Floor(_elapsed * 12.0) % 2 == 0) ? 0 : 2;
            return new Vector2(0, -step);
        }

        return Vector2.Zero;
    }

    /// <summary>把任意位置四舍五入到整数屏幕像素。</summary>
    private static Vector2 RoundVector(Vector2 value)
    {
        return new Vector2(Mathf.Round(value.X), Mathf.Round(value.Y));
    }

    /// <summary>把程序人物原点锁到指定逻辑像素网格。</summary>
    private static Vector2 SnapToGrid(Vector2 value, float gridSize)
    {
        return new Vector2(
            Mathf.Round(value.X / gridSize) * gridSize,
            Mathf.Round(value.Y / gridSize) * gridSize);
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

    /// <summary>把逻辑格坐标转换为当前地图格子的像素中心。</summary>
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
