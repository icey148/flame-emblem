using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Main;

/// <summary>
/// 第一版可运行战棋场景。
/// 该节点负责绘制地图、处理鼠标操作、推进玩家/敌军回合，并把核心规则委托给独立规则类。
/// </summary>
public partial class MainGame : Node2D
{
    /// <summary>战斗地图横向格子数量。</summary>
    private const int GridWidth = 15;

    /// <summary>战斗地图纵向格子数量。</summary>
    private const int GridHeight = 10;

    /// <summary>每个逻辑格子的像素尺寸。</summary>
    private const int CellSize = 52;

    /// <summary>地图左上角在窗口中的绘制偏移。</summary>
    private static readonly Vector2 BoardOrigin = new(40, 100);

    /// <summary>当前战斗中的全部单位，包括已经倒下但尚未从集合中移除的单位。</summary>
    private readonly List<UnitModel> _units = new();

    /// <summary>当前选中的玩家单位；为空表示正在等待玩家选择单位。</summary>
    private UnitModel? _selectedUnit;

    /// <summary>当前选中单位可移动到的格子集合。</summary>
    private HashSet<Vector2I> _reachableCells = new();

    /// <summary>HUD 顶部状态文本，用于提示当前回合和操作。</summary>
    private Label? _statusLabel;

    /// <summary>HUD 的单位列表文本。</summary>
    private Label? _unitListLabel;

    /// <summary>让当前选中单位结束行动的按钮。</summary>
    private Button? _waitButton;

    /// <summary>手动结束玩家回合的按钮。</summary>
    private Button? _endTurnButton;

    /// <summary>当前进行到的玩家回合数。</summary>
    private int _round = 1;

    /// <summary>战斗当前所处阶段。</summary>
    private BattlePhase _phase = BattlePhase.Player;

    /// <summary>
    /// Godot 节点进入场景树后初始化第一张测试地图和 HUD。
    /// </summary>
    public override void _Ready()
    {
        CreatePrototypeUnits();
        CreateHud();
        UpdateHud("选择一个蓝色单位开始行动。移动后可以攻击相邻敌军，或点击“等待”。");
        QueueRedraw();
    }

    /// <summary>
    /// 接收鼠标点击并把屏幕坐标转换为战棋格子操作。
    /// </summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        // 非玩家阶段不接受地图操作，防止敌军回合期间修改战斗状态。
        if (_phase != BattlePhase.Player)
        {
            return;
        }

        if (@event is not InputEventMouseButton mouseButton ||
            mouseButton.ButtonIndex != MouseButton.Left ||
            !mouseButton.Pressed)
        {
            return;
        }

        Vector2I? clickedCell = ScreenToGrid(mouseButton.Position);
        if (clickedCell is null)
        {
            return;
        }

        HandleGridClick(clickedCell.Value);
    }

    /// <summary>
    /// 每次 Godot 请求重绘时绘制地图、高亮范围和单位占位图形。
    /// 第一版故意不依赖外部素材，确保 clone 后即可验证玩法。
    /// </summary>
    public override void _Draw()
    {
        DrawBoard();
        DrawSelectionOverlay();
        DrawUnits();
    }

    /// <summary>
    /// 创建第一张测试关卡的玩家与敌军单位。
    /// 后续会把这些数据迁移到 JSON/Resource，使关卡完全数据驱动。
    /// </summary>
    private void CreatePrototypeUnits()
    {
        // 玩家侧先放置 4 人，属性刻意保持简单，方便观察移动和伤害结果。
        _units.Add(new UnitModel("adrian", "Adrian", UnitTeam.Player, new Vector2I(2, 3), 24, 8, 5, 5, 1, 4));
        _units.Add(new UnitModel("celine", "Celine", UnitTeam.Player, new Vector2I(2, 5), 20, 7, 4, 5, 1, 3));
        _units.Add(new UnitModel("rowan", "Rowan", UnitTeam.Player, new Vector2I(3, 4), 27, 9, 6, 4, 1, 4));
        _units.Add(new UnitModel("mira", "Mira", UnitTeam.Player, new Vector2I(1, 4), 19, 6, 3, 6, 1, 3));

        // 敌军侧第一版使用 5 个普通单位；规则控制器会让它们向最近玩家单位移动。
        _units.Add(new UnitModel("enemy_01", "Raider A", UnitTeam.Enemy, new Vector2I(11, 2), 18, 6, 3, 4, 1, 3));
        _units.Add(new UnitModel("enemy_02", "Raider B", UnitTeam.Enemy, new Vector2I(12, 4), 18, 6, 3, 4, 1, 3));
        _units.Add(new UnitModel("enemy_03", "Raider C", UnitTeam.Enemy, new Vector2I(11, 6), 18, 6, 3, 4, 1, 3));
        _units.Add(new UnitModel("enemy_04", "Guard", UnitTeam.Enemy, new Vector2I(13, 7), 22, 7, 5, 3, 1, 4));
        _units.Add(new UnitModel("boss", "Bridge Captain", UnitTeam.Enemy, new Vector2I(13, 4), 28, 9, 6, 3, 1, 5));
    }

    /// <summary>
    /// 通过 Godot 控件动态创建右侧 HUD。
    /// 使用代码创建可以让第一版只依赖一个主场景文件，后续 UI 稳定后再拆成独立场景。
    /// </summary>
    private void CreateHud()
    {
        CanvasLayer canvasLayer = new();
        AddChild(canvasLayer);

        PanelContainer panel = new()
        {
            Position = new Vector2(860, 70),
            Size = new Vector2(380, 590)
        };
        canvasLayer.AddChild(panel);

        VBoxContainer column = new();
        panel.AddChild(column);

        Label title = new()
        {
            Text = "FLAME EMBLEM — PLAYABLE FOUNDATION",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        column.AddChild(title);

        _statusLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(340, 100)
        };
        column.AddChild(_statusLabel);

        _unitListLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(340, 300)
        };
        column.AddChild(_unitListLabel);

        _waitButton = new Button { Text = "等待（结束当前单位行动）" };
        _waitButton.Pressed += OnWaitPressed;
        column.AddChild(_waitButton);

        _endTurnButton = new Button { Text = "结束玩家回合" };
        _endTurnButton.Pressed += OnEndTurnPressed;
        column.AddChild(_endTurnButton);
    }

    /// <summary>
    /// 处理玩家点击某个地图格子的行为：选人、移动或攻击。
    /// </summary>
    private void HandleGridClick(Vector2I cell)
    {
        UnitModel? clickedUnit = FindLivingUnitAt(cell);

        // 未选择单位时，只允许选择尚未行动的玩家单位。
        if (_selectedUnit is null)
        {
            TrySelectPlayerUnit(clickedUnit);
            return;
        }

        // 点击敌军且处于攻击范围时，立即执行攻击并结束当前单位行动。
        if (clickedUnit is { Team: UnitTeam.Enemy } enemy &&
            CombatRules.IsInAttackRange(_selectedUnit, enemy))
        {
            int damage = CombatRules.ResolveAttack(_selectedUnit, enemy);
            string result = $"{_selectedUnit.DisplayName} 对 {enemy.DisplayName} 造成 {damage} 点伤害。";
            FinishSelectedUnitAction(result);
            CheckBattleOutcome();
            return;
        }

        // 点击另一个可行动玩家单位时，直接切换选中对象。
        if (clickedUnit is { Team: UnitTeam.Player } player && !player.HasActed)
        {
            SelectPlayerUnit(player);
            return;
        }

        // 可移动格必须为空；单位不会与友军或敌军重叠。
        if (clickedUnit is null && _reachableCells.Contains(cell))
        {
            _selectedUnit.GridPosition = cell;
            RefreshSelectedMovementRange();
            UpdateHud($"{_selectedUnit.DisplayName} 已移动。现在可攻击相邻敌军，或点击“等待”结束行动。");
            QueueRedraw();
        }
    }

    /// <summary>
    /// 尝试选择一个玩家单位；无效点击只更新提示，不改变状态。
    /// </summary>
    private void TrySelectPlayerUnit(UnitModel? unit)
    {
        if (unit is not { Team: UnitTeam.Player } || unit.HasActed)
        {
            UpdateHud("请选择一个本回合尚未行动的蓝色单位。");
            return;
        }

        SelectPlayerUnit(unit);
    }

    /// <summary>
    /// 设置当前玩家单位，并重新计算它的移动范围。
    /// </summary>
    private void SelectPlayerUnit(UnitModel unit)
    {
        _selectedUnit = unit;
        RefreshSelectedMovementRange();
        UpdateHud($"已选择 {unit.DisplayName}。蓝色高亮为可移动范围，红色格表示当前可攻击目标。");
        QueueRedraw();
    }

    /// <summary>
    /// 重新计算当前单位的移动范围。
    /// </summary>
    private void RefreshSelectedMovementRange()
    {
        _reachableCells = _selectedUnit is null
            ? new HashSet<Vector2I>()
            : CalculateReachableCells(_selectedUnit);
    }

    /// <summary>
    /// 使用广度优先搜索计算单位在当前地图上的可达格。
    /// 第一版所有地形移动消耗均为 1；其他存活单位会作为不可穿越障碍。
    /// </summary>
    private HashSet<Vector2I> CalculateReachableCells(UnitModel unit)
    {
        HashSet<Vector2I> reachable = new() { unit.GridPosition };
        Queue<(Vector2I Cell, int Cost)> frontier = new();
        frontier.Enqueue((unit.GridPosition, 0));

        while (frontier.Count > 0)
        {
            (Vector2I current, int cost) = frontier.Dequeue();
            if (cost >= unit.Move)
            {
                continue;
            }

            foreach (Vector2I direction in CardinalDirections())
            {
                Vector2I next = current + direction;

                // 越界格、已经访问过的格以及被其他单位占据的格都不能继续扩展。
                if (!IsInsideBoard(next) || reachable.Contains(next) || IsOccupiedByOtherUnit(next, unit))
                {
                    continue;
                }

                reachable.Add(next);
                frontier.Enqueue((next, cost + 1));
            }
        }

        return reachable;
    }

    /// <summary>
    /// 玩家点击“等待”时，让当前单位结束本回合行动而不攻击。
    /// </summary>
    private void OnWaitPressed()
    {
        if (_phase != BattlePhase.Player || _selectedUnit is null)
        {
            UpdateHud("当前没有可结束行动的玩家单位。");
            return;
        }

        FinishSelectedUnitAction($"{_selectedUnit.DisplayName} 结束行动。");
    }

    /// <summary>
    /// 玩家手动结束整个己方回合。
    /// 尚未行动的单位会直接放弃本回合行动。
    /// </summary>
    private void OnEndTurnPressed()
    {
        if (_phase != BattlePhase.Player)
        {
            return;
        }

        foreach (UnitModel player in _units.Where(unit => unit.IsAlive && unit.Team == UnitTeam.Player))
        {
            player.HasActed = true;
        }

        ClearSelection();
        RunEnemyTurn();
    }

    /// <summary>
    /// 标记当前单位行动完成，并在所有玩家都行动后自动进入敌军回合。
    /// </summary>
    private void FinishSelectedUnitAction(string message)
    {
        if (_selectedUnit is not null)
        {
            _selectedUnit.HasActed = true;
        }

        ClearSelection();
        UpdateHud(message);
        QueueRedraw();

        // 当所有存活玩家单位都已行动时，不需要额外点击按钮即可进入敌军回合。
        if (_phase == BattlePhase.Player &&
            _units.Where(unit => unit.IsAlive && unit.Team == UnitTeam.Player).All(unit => unit.HasActed))
        {
            RunEnemyTurn();
        }
    }

    /// <summary>
    /// 执行一整个规则驱动的敌军回合。
    /// 每个敌人依次寻找最近玩家、移动到更近的位置，并在进入攻击距离后攻击一次。
    /// </summary>
    private void RunEnemyTurn()
    {
        if (_phase is BattlePhase.Victory or BattlePhase.Defeat)
        {
            return;
        }

        _phase = BattlePhase.Enemy;
        UpdateHud("敌军回合：敌军正在按照固定规则行动。游戏没有使用生成式 AI。\n");

        foreach (UnitModel enemy in _units.Where(unit => unit.IsAlive && unit.Team == UnitTeam.Enemy))
        {
            UnitModel? target = EnemyTurnController.FindNearestPlayer(enemy, _units);
            if (target is null)
            {
                break;
            }

            // 如果当前位置不能攻击，就先在自己的可达范围中选择最接近目标的格子。
            if (!CombatRules.IsInAttackRange(enemy, target))
            {
                HashSet<Vector2I> reachable = CalculateReachableCells(enemy);
                Vector2I destination = EnemyTurnController.ChooseMoveDestination(enemy, target, reachable);
                enemy.GridPosition = destination;
            }

            // 移动结束后重新检查攻击距离；第一版每个敌人最多攻击一次。
            if (target.IsAlive && CombatRules.IsInAttackRange(enemy, target))
            {
                CombatRules.ResolveAttack(enemy, target);
            }
        }

        CheckBattleOutcome();
        if (_phase is BattlePhase.Victory or BattlePhase.Defeat)
        {
            return;
        }

        StartNextPlayerTurn();
    }

    /// <summary>
    /// 开始下一个玩家回合并清除玩家单位的行动标记。
    /// </summary>
    private void StartNextPlayerTurn()
    {
        _round++;
        _phase = BattlePhase.Player;

        foreach (UnitModel player in _units.Where(unit => unit.IsAlive && unit.Team == UnitTeam.Player))
        {
            player.ResetForNewTurn();
        }

        ClearSelection();
        UpdateHud($"第 {_round} 回合开始。请选择一个蓝色单位。\n");
        QueueRedraw();
    }

    /// <summary>
    /// 检查双方是否已经没有存活单位，并切换到胜利或失败状态。
    /// </summary>
    private void CheckBattleOutcome()
    {
        bool hasPlayers = _units.Any(unit => unit.IsAlive && unit.Team == UnitTeam.Player);
        bool hasEnemies = _units.Any(unit => unit.IsAlive && unit.Team == UnitTeam.Enemy);

        if (!hasEnemies)
        {
            _phase = BattlePhase.Victory;
            ClearSelection();
            UpdateHud("胜利：测试关卡中的敌军已全部被击败。下一阶段会接入正式胜利条件和章节结算。");
        }
        else if (!hasPlayers)
        {
            _phase = BattlePhase.Defeat;
            ClearSelection();
            UpdateHud("战斗结束：我方已经没有可行动单位。可以重新运行场景再次测试。");
        }

        QueueRedraw();
    }

    /// <summary>
    /// 清空当前选择和移动高亮。
    /// </summary>
    private void ClearSelection()
    {
        _selectedUnit = null;
        _reachableCells.Clear();
    }

    /// <summary>
    /// 刷新 HUD 的状态文本与所有存活单位的生命值。
    /// </summary>
    private void UpdateHud(string message)
    {
        if (_statusLabel is not null)
        {
            _statusLabel.Text = $"回合：{_round}\n阶段：{PhaseDisplayName()}\n\n{message}";
        }

        if (_unitListLabel is not null)
        {
            IEnumerable<string> playerLines = _units
                .Where(unit => unit.Team == UnitTeam.Player)
                .Select(unit => $"{(unit.IsAlive ? "●" : "×")} {unit.DisplayName}: {unit.CurrentHp}/{unit.MaxHp} HP{(unit.HasActed ? " [已行动]" : string.Empty)}");

            IEnumerable<string> enemyLines = _units
                .Where(unit => unit.Team == UnitTeam.Enemy)
                .Select(unit => $"{(unit.IsAlive ? "●" : "×")} {unit.DisplayName}: {unit.CurrentHp}/{unit.MaxHp} HP");

            _unitListLabel.Text = "我方\n" + string.Join("\n", playerLines) + "\n\n敌方\n" + string.Join("\n", enemyLines);
        }

        // 非玩家阶段禁用行动按钮，避免状态机被 UI 误触打断。
        bool playerControlsEnabled = _phase == BattlePhase.Player;
        if (_waitButton is not null)
        {
            _waitButton.Disabled = !playerControlsEnabled || _selectedUnit is null;
        }

        if (_endTurnButton is not null)
        {
            _endTurnButton.Disabled = !playerControlsEnabled;
        }
    }

    /// <summary>
    /// 绘制 15×10 的基础草地棋盘和网格线。
    /// </summary>
    private void DrawBoard()
    {
        Color tileA = new(0.22f, 0.40f, 0.25f);
        Color tileB = new(0.25f, 0.45f, 0.28f);
        Color gridLine = new(0.08f, 0.12f, 0.09f, 0.75f);

        for (int y = 0; y < GridHeight; y++)
        {
            for (int x = 0; x < GridWidth; x++)
            {
                Vector2 topLeft = BoardOrigin + new Vector2(x * CellSize, y * CellSize);
                Rect2 rect = new(topLeft, new Vector2(CellSize, CellSize));
                Color tileColor = (x + y) % 2 == 0 ? tileA : tileB;

                DrawRect(rect, tileColor, true);
                DrawRect(rect, gridLine, false, 1.0f);
            }
        }
    }

    /// <summary>
    /// 绘制当前选中单位的移动范围、当前位置和可攻击敌军高亮。
    /// </summary>
    private void DrawSelectionOverlay()
    {
        if (_selectedUnit is null)
        {
            return;
        }

        Color moveColor = new(0.15f, 0.45f, 0.95f, 0.32f);
        Color attackColor = new(0.95f, 0.20f, 0.18f, 0.42f);
        Color selectedBorder = new(1.0f, 0.86f, 0.18f, 1.0f);

        foreach (Vector2I cell in _reachableCells)
        {
            DrawRect(GridRect(cell), moveColor, true);
        }

        foreach (UnitModel enemy in _units.Where(unit => unit.IsAlive && unit.Team == UnitTeam.Enemy))
        {
            if (CombatRules.IsInAttackRange(_selectedUnit, enemy))
            {
                DrawRect(GridRect(enemy.GridPosition), attackColor, true);
            }
        }

        DrawRect(GridRect(_selectedUnit.GridPosition), selectedBorder, false, 4.0f);
    }

    /// <summary>
    /// 以简单圆形和生命条绘制单位。
    /// 蓝色代表玩家、红色代表敌军；已行动玩家会变暗。
    /// </summary>
    private void DrawUnits()
    {
        foreach (UnitModel unit in _units.Where(unit => unit.IsAlive))
        {
            Vector2 center = GridCenter(unit.GridPosition);
            Color baseColor = unit.Team == UnitTeam.Player
                ? new Color(0.18f, 0.48f, 0.95f)
                : new Color(0.78f, 0.18f, 0.20f);

            if (unit.HasActed)
            {
                // 已行动单位降低亮度，给玩家明确的回合反馈。
                baseColor = baseColor.Darkened(0.45f);
            }

            DrawCircle(center, 17.0f, baseColor);
            DrawCircle(center, 18.5f, new Color(0.05f, 0.05f, 0.06f), false, 3.0f);

            // 生命条放在单位圆形底部，长度按当前生命比例缩放。
            float hpRatio = (float)unit.CurrentHp / unit.MaxHp;
            Vector2 hpOrigin = center + new Vector2(-18, 22);
            DrawRect(new Rect2(hpOrigin, new Vector2(36, 5)), new Color(0.08f, 0.08f, 0.08f), true);
            DrawRect(new Rect2(hpOrigin, new Vector2(36 * hpRatio, 5)), new Color(0.25f, 0.85f, 0.35f), true);
        }
    }

    /// <summary>
    /// 把鼠标屏幕坐标转换为合法地图格；地图外返回空值。
    /// </summary>
    private Vector2I? ScreenToGrid(Vector2 screenPosition)
    {
        Vector2 local = screenPosition - BoardOrigin;
        if (local.X < 0 || local.Y < 0)
        {
            return null;
        }

        Vector2I cell = new((int)(local.X / CellSize), (int)(local.Y / CellSize));
        return IsInsideBoard(cell) ? cell : null;
    }

    /// <summary>
    /// 返回指定格子的像素矩形。
    /// </summary>
    private static Rect2 GridRect(Vector2I cell)
    {
        Vector2 topLeft = BoardOrigin + new Vector2(cell.X * CellSize, cell.Y * CellSize);
        return new Rect2(topLeft, new Vector2(CellSize, CellSize));
    }

    /// <summary>
    /// 返回指定格子的像素中心位置。
    /// </summary>
    private static Vector2 GridCenter(Vector2I cell)
    {
        return BoardOrigin + new Vector2(cell.X * CellSize + CellSize / 2.0f, cell.Y * CellSize + CellSize / 2.0f);
    }

    /// <summary>
    /// 查找某格上的存活单位。
    /// </summary>
    private UnitModel? FindLivingUnitAt(Vector2I cell)
    {
        return _units.FirstOrDefault(unit => unit.IsAlive && unit.GridPosition == cell);
    }

    /// <summary>
    /// 判断指定格是否被当前单位之外的存活单位占据。
    /// </summary>
    private bool IsOccupiedByOtherUnit(Vector2I cell, UnitModel movingUnit)
    {
        return _units.Any(unit => unit.IsAlive && unit != movingUnit && unit.GridPosition == cell);
    }

    /// <summary>
    /// 判断逻辑坐标是否位于地图范围内。
    /// </summary>
    private static bool IsInsideBoard(Vector2I cell)
    {
        return cell.X >= 0 && cell.X < GridWidth && cell.Y >= 0 && cell.Y < GridHeight;
    }

    /// <summary>
    /// 返回战棋移动所需的四个正交方向。
    /// </summary>
    private static IEnumerable<Vector2I> CardinalDirections()
    {
        yield return Vector2I.Up;
        yield return Vector2I.Down;
        yield return Vector2I.Left;
        yield return Vector2I.Right;
    }

    /// <summary>
    /// 把内部阶段枚举转换成人类可读的中文文本。
    /// </summary>
    private string PhaseDisplayName()
    {
        return _phase switch
        {
            BattlePhase.Player => "玩家回合",
            BattlePhase.Enemy => "敌军回合",
            BattlePhase.Victory => "胜利",
            BattlePhase.Defeat => "结束",
            _ => "未知"
        };
    }

    /// <summary>
    /// 第一版战斗阶段状态机。
    /// </summary>
    private enum BattlePhase
    {
        Player,
        Enemy,
        Victory,
        Defeat
    }
}
