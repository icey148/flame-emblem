using FlameEmblem.Game;
using FlameEmblem.Visual;
using Godot;

namespace FlameEmblem.Main;

/// <summary>
/// 当前可运行的战棋主场景。
/// 该节点只负责地图交互、回合推进和 HUD；职业、地形、装备与战斗公式均委托给独立游戏规则模块。
/// </summary>
public partial class MainGame : Node2D
{
    /// <summary>每个逻辑格子的像素尺寸。</summary>
    private const int CellSize = 52;

    /// <summary>地图左上角在窗口中的绘制偏移。</summary>
    private static readonly Vector2 BoardOrigin = new(40, 100);

    /// <summary>敌军战斗结果发布后等待横向演出真正启动的安全时间。</summary>
    private const double EnemyBattleStartGraceSeconds = 0.20;

    /// <summary>当前章节地图宽度。</summary>
    private int _gridWidth = 15;

    /// <summary>当前章节地图高度。</summary>
    private int _gridHeight = 10;

    /// <summary>当前章节标题。</summary>
    private string _chapterTitle = "Flame Emblem";

    /// <summary>当前章节胜利条件。</summary>
    private string _victoryCondition = "rout";

    /// <summary>需要击败的胜利目标实例 ID。</summary>
    private string _victoryTargetId = string.Empty;

    /// <summary>当前章节的非平地地形覆盖表。</summary>
    private readonly Dictionary<Vector2I, TerrainType> _terrain = new();

    /// <summary>当前战斗中的全部单位，包括已倒下但仍保留在集合中的单位。</summary>
    private readonly List<UnitModel> _units = new();

    /// <summary>当前选中的玩家单位；为空表示等待玩家选择。</summary>
    private UnitModel? _selectedUnit;

    /// <summary>当前战斗预测对应的敌军目标。</summary>
    private UnitModel? _pendingAttackTarget;

    /// <summary>当前选中单位是否已经完成本回合移动。</summary>
    private bool _selectedUnitHasMoved;

    /// <summary>当前选中单位可移动到的格子集合。</summary>
    private HashSet<Vector2I> _reachableCells = new();

    /// <summary>战斗命中与必杀使用的本地随机数生成器。</summary>
    private readonly Random _combatRandom = new();

    /// <summary>最近一次完整战斗交换的表现文本。</summary>
    private string _lastBattleLog = "尚未发生战斗。";

    /// <summary>地图人物表现协调器，用于敌军逐个行动时等待真实移动动画结束。</summary>
    private CharacterVisualCoordinator? _characterVisualCoordinator;

    /// <summary>本次敌军回合尚未行动的单位队列，保持章节部署顺序。</summary>
    private readonly Queue<UnitModel> _enemyTurnQueue = new();

    /// <summary>本次敌军回合累积的战斗记录，回到玩家阶段时统一显示。</summary>
    private readonly List<string> _enemyTurnLog = new();

    /// <summary>当前正在执行的敌军单位。</summary>
    private UnitModel? _activeEnemyUnit;

    /// <summary>当前敌军本次行动锁定的玩家目标。</summary>
    private UnitModel? _activeEnemyTarget;

    /// <summary>敌军逐单位行动的当前步骤。</summary>
    private EnemyTurnSequenceStep _enemyTurnStep = EnemyTurnSequenceStep.Idle;

    /// <summary>等待敌军横向战斗演出启动/结束时累计的安全时间。</summary>
    private double _enemyBattleWaitElapsed;

    /// <summary>当前敌军攻击是否已经观察到横向战斗演出真正进入播放状态。</summary>
    private bool _enemyBattlePlaybackObserved;

    /// <summary>HUD 章节标题。</summary>
    private Label? _titleLabel;

    /// <summary>HUD 顶部状态文本。</summary>
    private Label? _statusLabel;

    /// <summary>HUD 当前地形信息。</summary>
    private Label? _terrainLabel;

    /// <summary>HUD 战斗预测信息。</summary>
    private Label? _forecastLabel;

    /// <summary>HUD 最近一次战斗的逐击记录。</summary>
    private Label? _battleLogLabel;

    /// <summary>HUD 单位列表文本。</summary>
    private Label? _unitListLabel;

    /// <summary>确认当前战斗预测并执行攻击的按钮。</summary>
    private Button? _confirmAttackButton;

    /// <summary>取消当前攻击目标的按钮。</summary>
    private Button? _cancelAttackButton;

    /// <summary>让当前选中单位结束行动的按钮。</summary>
    private Button? _waitButton;

    /// <summary>手动结束玩家回合的按钮。</summary>
    private Button? _endTurnButton;

    /// <summary>当前进行到的玩家回合数。</summary>
    private int _round = 1;

    /// <summary>战斗当前所处阶段。</summary>
    private BattlePhase _phase = BattlePhase.Player;

    /// <summary>
    /// Godot 节点进入场景树后创建 HUD，并从 JSON 加载第一张测试关卡。
    /// </summary>
    public override void _Ready()
    {
        CreateHud();
        _characterVisualCoordinator = GetNodeOrNull<CharacterVisualCoordinator>("CharacterVisualCoordinator");

        try
        {
            LoadChapter();
            UpdateHud("选择一个蓝色单位开始行动。可以直接攻击，也可以移动后再行动。");
        }
        catch (Exception exception)
        {
            // 数据加载失败时把详细异常写入 Godot 输出，同时在 HUD 显示可读错误，避免黑屏难以排查。
            GD.PushError(exception.ToString());
            _phase = BattlePhase.Defeat;
            UpdateHud($"章节数据加载失败：{exception.Message}");
        }

        QueueRedraw();
    }

    /// <summary>
    /// 敌军阶段按帧推进一个很小的状态机。
    /// 每名敌军必须完成“移动动画 -> 战斗演出 -> 下一名敌军”，不会再在同一 C# 调用中把整回合全部结算完。
    /// </summary>
    public override void _Process(double delta)
    {
        if (_phase != BattlePhase.Enemy || _enemyTurnStep == EnemyTurnSequenceStep.Idle)
        {
            return;
        }

        // 战斗结果发布后，横向演出对敌军攻击会先做一次移动门控；这里单独等待它真正开始并结束。
        if (_enemyTurnStep == EnemyTurnSequenceStep.WaitForBattlePresentation)
        {
            AdvanceEnemyBattlePresentationWait(delta);
            return;
        }

        // 玩家最后一次攻击的演出也可能延续到敌军阶段开始以后；任何演出期间都不推进下一名敌军。
        if (BattleAnimationBus.IsPlaybackActive)
        {
            return;
        }

        // 逻辑坐标先变化，地图人物层随后逐格补动画；敌军必须等人物真正走到终点再判断攻击。
        if (_characterVisualCoordinator?.IsMovementAnimating ?? false)
        {
            return;
        }

        AdvanceEnemyTurnSequence();
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
    /// 每次 Godot 请求重绘时绘制地形、高亮范围和单位占位图形。
    /// 当前阶段不依赖外部商业素材，确保仓库 clone 后即可测试玩法。
    /// </summary>
    public override void _Draw()
    {
        DrawBoard();
        DrawSelectionOverlay();
        DrawUnits();
    }

    /// <summary>
    /// 从 data/chapter_01.json 加载地图、胜利条件和单位部署。
    /// </summary>
    private void LoadChapter()
    {
        LoadedChapter chapter = ChapterDataLoader.Load("res://data/chapter_01.json");

        _chapterTitle = chapter.Title;
        _gridWidth = chapter.Width;
        _gridHeight = chapter.Height;
        _victoryCondition = chapter.VictoryCondition;
        _victoryTargetId = chapter.VictoryTargetId;

        _terrain.Clear();
        foreach ((Vector2I cell, TerrainType type) in chapter.Terrain)
        {
            _terrain[cell] = type;
        }

        _units.Clear();
        _units.AddRange(chapter.Units);
    }

    /// <summary>
    /// 通过 Godot 控件动态创建右侧 HUD。
    /// 后续美术稳定后会拆成独立 .tscn；当前保持工程最小可运行依赖。
    /// </summary>
    private void CreateHud()
    {
        CanvasLayer canvasLayer = new();
        AddChild(canvasLayer);

        PanelContainer panel = new()
        {
            Position = new Vector2(850, 25),
            Size = new Vector2(400, 670)
        };
        canvasLayer.AddChild(panel);

        VBoxContainer column = new();
        panel.AddChild(column);

        _titleLabel = new Label
        {
            Text = "FLAME EMBLEM",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        column.AddChild(_titleLabel);

        _statusLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(360, 72)
        };
        column.AddChild(_statusLabel);

        _terrainLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(360, 42)
        };
        column.AddChild(_terrainLabel);

        _forecastLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(360, 110)
        };
        column.AddChild(_forecastLabel);

        _battleLogLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(360, 105)
        };
        column.AddChild(_battleLogLabel);

        _unitListLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(360, 150)
        };
        column.AddChild(_unitListLabel);

        _confirmAttackButton = new Button { Text = "确认攻击" };
        _confirmAttackButton.Pressed += OnConfirmAttackPressed;
        column.AddChild(_confirmAttackButton);

        _cancelAttackButton = new Button { Text = "取消攻击目标" };
        _cancelAttackButton.Pressed += OnCancelAttackPressed;
        column.AddChild(_cancelAttackButton);

        _waitButton = new Button { Text = "等待（结束当前单位行动）" };
        _waitButton.Pressed += OnWaitPressed;
        column.AddChild(_waitButton);

        _endTurnButton = new Button { Text = "结束玩家回合" };
        _endTurnButton.Pressed += OnEndTurnPressed;
        column.AddChild(_endTurnButton);
    }

    /// <summary>
    /// 处理玩家点击某个地图格子的行为：选人、移动或选择攻击目标。
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

        // 点击敌军时先验证装备使用条件，再进入预测状态。
        if (clickedUnit is { Team: UnitTeam.Enemy } enemy && CombatRules.IsInAttackRange(_selectedUnit, enemy))
        {
            if (!_selectedUnit.CanUseEquippedWeapon)
            {
                UpdateHud($"{_selectedUnit.DisplayName} 当前 HP 不足，无法使用 {_selectedUnit.EquippedWeapon.DisplayName}。");
                return;
            }

            SetPendingAttackTarget(enemy);
            return;
        }

        // 单位尚未移动时允许切换到另一个可行动玩家；移动后必须先攻击或等待，避免免费多次改位置。
        if (!_selectedUnitHasMoved && clickedUnit is { Team: UnitTeam.Player } player && !player.HasActed)
        {
            SelectPlayerUnit(player);
            return;
        }

        // 可移动格必须为空；移动后锁定当前位置并清除攻击目标。
        if (!_selectedUnitHasMoved && clickedUnit is null && _reachableCells.Contains(cell))
        {
            _selectedUnit.GridPosition = cell;
            _selectedUnitHasMoved = true;
            _pendingAttackTarget = null;
            _reachableCells = new HashSet<Vector2I> { cell };
            UpdateHud($"{_selectedUnit.DisplayName} 已移动。请选择攻击范围内敌军，或点击“等待”。");
            QueueRedraw();
        }
    }

    /// <summary>
    /// 尝试选择一个玩家单位；无效点击只更新提示，不改变战斗状态。
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
    /// 设置当前玩家单位，并根据地形移动消耗重新计算可达范围。
    /// </summary>
    private void SelectPlayerUnit(UnitModel unit)
    {
        _selectedUnit = unit;
        _pendingAttackTarget = null;
        _selectedUnitHasMoved = false;
        RefreshSelectedMovementRange();
        UpdateHud(
            $"已选择 {unit.DisplayName}（{unit.ClassDefinition.DisplayName}）。" +
            $"装备：{unit.EquippedWeapon.DisplayName}。蓝色区域为可移动范围。");
        QueueRedraw();
    }

    /// <summary>
    /// 设置当前攻击目标并刷新 HUD。
    /// </summary>
    private void SetPendingAttackTarget(UnitModel enemy)
    {
        if (_selectedUnit is null || !CombatRules.CanAttack(_selectedUnit, enemy))
        {
            return;
        }

        _pendingAttackTarget = enemy;
        UpdateHud($"已锁定 {enemy.DisplayName}。点击“确认攻击”开始战斗。");
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
    /// 使用带权最短路计算单位在当前地图上的可达格。
    /// 同阵营单位可以作为路径中间格经过但不能作为终点；敌对单位完全阻挡。
    /// </summary>
    private HashSet<Vector2I> CalculateReachableCells(UnitModel unit)
    {
        Dictionary<Vector2I, int> bestCosts = new()
        {
            [unit.GridPosition] = 0
        };
        PriorityQueue<Vector2I, int> frontier = new();
        frontier.Enqueue(unit.GridPosition, 0);

        while (frontier.TryDequeue(out Vector2I current, out int currentCost))
        {
            // 如果队列中这是一个已经被更短路径替代的旧条目，直接跳过。
            if (bestCosts.TryGetValue(current, out int bestKnownCost) && currentCost > bestKnownCost)
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

                UnitModel? occupant = FindLivingUnitAt(next);
                if (occupant is not null &&
                    !ReferenceEquals(occupant, unit) &&
                    occupant.Team != unit.Team)
                {
                    // 敌对单位不能被穿过，也不能把它所在格作为移动终点。
                    continue;
                }

                TerrainDefinition terrain = TerrainRules.Get(TerrainAt(next));
                if (!terrain.Passable)
                {
                    continue;
                }

                int nextCost = currentCost + terrain.MoveCost;
                if (nextCost > unit.Move)
                {
                    continue;
                }

                if (bestCosts.TryGetValue(next, out int previousCost) && previousCost <= nextCost)
                {
                    continue;
                }

                bestCosts[next] = nextCost;
                frontier.Enqueue(next, nextCost);
            }
        }

        // 同阵营单位所在格只允许“经过”，最终返回的蓝色/敌军候选终点必须为空或是单位自己的起点。
        return bestCosts.Keys
            .Where(cell => cell == unit.GridPosition || FindLivingUnitAt(cell) is null)
            .ToHashSet();
    }

    /// <summary>
    /// 玩家确认目标后执行带命中、必杀、反击、追击与魔法 HP 消耗的完整战斗交换。
    /// </summary>
    private void OnConfirmAttackPressed()
    {
        if (_phase != BattlePhase.Player ||
            _selectedUnit is null ||
            _pendingAttackTarget is null ||
            !CombatRules.CanAttack(_selectedUnit, _pendingAttackTarget))
        {
            UpdateHud("当前没有可以确认的攻击目标，或装备已经无法使用。");
            return;
        }

        UnitModel attacker = _selectedUnit;
        UnitModel defender = _pendingAttackTarget;
        TerrainDefinition attackerTerrain = TerrainRules.Get(TerrainAt(attacker.GridPosition));
        TerrainDefinition defenderTerrain = TerrainRules.Get(TerrainAt(defender.GridPosition));

        CombatExchangeResult exchange = CombatResolver.ResolveExchange(
            attacker,
            defender,
            attackerTerrain,
            defenderTerrain,
            _combatRandom);

        _lastBattleLog = BattlePresentationFormatter.FormatExchange(exchange);
        bool defenderDefeated = !defender.IsAlive;
        string result = _lastBattleLog;

        // 主动发起战斗的玩家单位获得一次战斗经验；击倒目标时按现有经验公式追加奖励。
        if (exchange.Strikes.Any(strike => ReferenceEquals(strike.Attacker, attacker)))
        {
            result += GrantCombatExperience(attacker, defender, defenderDefeated);
        }

        FinishSelectedUnitAction(result);
    }

    /// <summary>
    /// 玩家取消当前攻击目标，但保留已经选择/移动的单位。
    /// </summary>
    private void OnCancelAttackPressed()
    {
        if (_phase != BattlePhase.Player || _selectedUnit is null)
        {
            return;
        }

        _pendingAttackTarget = null;
        UpdateHud("已取消攻击目标。可以重新选择敌军，或点击“等待”。");
        QueueRedraw();
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
    /// 标记当前单位行动完成，检查胜负，并在所有玩家都行动后自动进入敌军回合。
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
        CheckBattleOutcome();

        // 当所有存活玩家单位都已行动且战斗仍未结束时，自动进入敌军回合。
        if (_phase == BattlePhase.Player &&
            _units.Where(unit => unit.IsAlive && unit.Team == UnitTeam.Player).All(unit => unit.HasActed))
        {
            RunEnemyTurn();
        }
    }

    /// <summary>
    /// 初始化逐单位敌军回合。
    /// 这里只建立队列，不在当前调用栈里移动或结算任何敌军，真正执行交给 _Process 状态机。
    /// </summary>
    private void RunEnemyTurn()
    {
        if (_phase is BattlePhase.Victory or BattlePhase.Defeat or BattlePhase.Enemy)
        {
            return;
        }

        _phase = BattlePhase.Enemy;
        ClearSelection();
        ResetEnemyTurnSequence();

        foreach (UnitModel enemy in _units.Where(unit => unit.IsAlive && unit.Team == UnitTeam.Enemy))
        {
            _enemyTurnQueue.Enqueue(enemy);
        }

        _enemyTurnStep = EnemyTurnSequenceStep.SelectNextUnit;
        UpdateHud("敌军回合开始。敌军将依次移动并完成各自战斗。");
        QueueRedraw();
    }

    /// <summary>推进一次敌军状态机；每次调用最多只进入一个新步骤。</summary>
    private void AdvanceEnemyTurnSequence()
    {
        switch (_enemyTurnStep)
        {
            case EnemyTurnSequenceStep.SelectNextUnit:
                SelectNextEnemyUnit();
                break;
            case EnemyTurnSequenceStep.WaitForMovement:
                _enemyTurnStep = EnemyTurnSequenceStep.ResolveAttack;
                break;
            case EnemyTurnSequenceStep.ResolveAttack:
                ResolveActiveEnemyAttack();
                break;
            default:
                break;
        }
    }

    /// <summary>从敌军队列选择下一名仍然存活的单位，并决定是否需要先移动。</summary>
    private void SelectNextEnemyUnit()
    {
        _activeEnemyUnit = null;
        _activeEnemyTarget = null;

        while (_enemyTurnQueue.Count > 0)
        {
            UnitModel candidate = _enemyTurnQueue.Dequeue();
            if (!candidate.IsAlive)
            {
                continue;
            }

            UnitModel? target = EnemyTurnController.FindNearestPlayer(candidate, _units);
            if (target is null)
            {
                FinishEnemyTurnSequence();
                return;
            }

            _activeEnemyUnit = candidate;
            _activeEnemyTarget = target;

            if (!CombatRules.IsInAttackRange(candidate, target))
            {
                HashSet<Vector2I> reachable = CalculateReachableCells(candidate);
                Vector2I destination = EnemyTurnController.ChooseMoveDestination(candidate, target, reachable);
                if (destination != candidate.GridPosition)
                {
                    candidate.GridPosition = destination;
                    _enemyTurnStep = EnemyTurnSequenceStep.WaitForMovement;
                    UpdateHud($"敌军行动：{candidate.DisplayName} 向 {target.DisplayName} 推进。");
                    QueueRedraw();
                    return;
                }
            }

            _enemyTurnStep = EnemyTurnSequenceStep.ResolveAttack;
            UpdateHud($"敌军行动：{candidate.DisplayName} 准备行动。");
            QueueRedraw();
            return;
        }

        FinishEnemyTurnSequence();
    }

    /// <summary>移动结束后判断当前敌军是否可以攻击，并只结算这一场战斗。</summary>
    private void ResolveActiveEnemyAttack()
    {
        UnitModel? enemy = _activeEnemyUnit;
        if (enemy is null || !enemy.IsAlive)
        {
            CompleteActiveEnemyUnit();
            return;
        }

        UnitModel? target = _activeEnemyTarget;
        if (target is null || !target.IsAlive)
        {
            target = EnemyTurnController.FindNearestPlayer(enemy, _units);
            _activeEnemyTarget = target;
        }

        if (target is null)
        {
            FinishEnemyTurnSequence();
            return;
        }

        if (!CombatRules.CanAttack(enemy, target))
        {
            CompleteActiveEnemyUnit();
            return;
        }

        TerrainDefinition enemyTerrain = TerrainRules.Get(TerrainAt(enemy.GridPosition));
        TerrainDefinition targetTerrain = TerrainRules.Get(TerrainAt(target.GridPosition));
        CombatExchangeResult exchange = CombatResolver.ResolveExchange(
            enemy,
            target,
            enemyTerrain,
            targetTerrain,
            _combatRandom);

        string exchangeText = BattlePresentationFormatter.FormatExchange(exchange);
        _enemyTurnLog.Add(exchangeText);
        _lastBattleLog = exchangeText;

        // 玩家作为防守方只要实际完成了至少一次反击，也可以获得经验与击倒奖励。
        if (exchange.Strikes.Any(strike => ReferenceEquals(strike.Attacker, target)))
        {
            bool enemyDefeated = !enemy.IsAlive;
            string experienceText = GrantCombatExperience(target, enemy, enemyDefeated).Trim();
            if (!string.IsNullOrWhiteSpace(experienceText))
            {
                _enemyTurnLog.Add(experienceText);
            }
        }

        CheckBattleOutcome();
        if (_phase is BattlePhase.Victory or BattlePhase.Defeat)
        {
            // 胜负已经锁定时不再安排其他敌军，但当前横向战斗演出仍可自行播放完成。
            ResetEnemyTurnSequence();
            return;
        }

        _enemyBattleWaitElapsed = 0.0;
        _enemyBattlePlaybackObserved = false;
        _enemyTurnStep = EnemyTurnSequenceStep.WaitForBattlePresentation;
    }

    /// <summary>
    /// 等待敌军战斗演出真正启动并结束。
    /// 演出协调器对敌军攻击有一个很短的移动门控，因此不能在 ResolveExchange 返回后立刻进入下一名敌军。
    /// </summary>
    private void AdvanceEnemyBattlePresentationWait(double delta)
    {
        if (BattleAnimationBus.IsPlaybackActive)
        {
            _enemyBattlePlaybackObserved = true;
            return;
        }

        _enemyBattleWaitElapsed += Math.Max(0.0, delta);

        // 只要曾经观察到播放状态，当前 false 就表示这一场演出已经完整结束。
        if (_enemyBattlePlaybackObserved)
        {
            CompleteActiveEnemyUnit();
            return;
        }

        // 正常情况下约 0.05 秒就会开始；安全门限防止表现层异常时敌军回合永久卡死。
        if (_enemyBattleWaitElapsed >= EnemyBattleStartGraceSeconds)
        {
            CompleteActiveEnemyUnit();
        }
    }

    /// <summary>结束当前敌军的行动并在下一帧选择下一名敌军。</summary>
    private void CompleteActiveEnemyUnit()
    {
        CheckBattleOutcome();
        if (_phase is BattlePhase.Victory or BattlePhase.Defeat)
        {
            ResetEnemyTurnSequence();
            return;
        }

        _activeEnemyUnit = null;
        _activeEnemyTarget = null;
        _enemyBattleWaitElapsed = 0.0;
        _enemyBattlePlaybackObserved = false;
        _enemyTurnStep = EnemyTurnSequenceStep.SelectNextUnit;
    }

    /// <summary>所有敌军完成行动以后汇总日志并进入下一玩家回合。</summary>
    private void FinishEnemyTurnSequence()
    {
        if (_phase is BattlePhase.Victory or BattlePhase.Defeat)
        {
            ResetEnemyTurnSequence();
            return;
        }

        string previousEnemyTurnLog = _enemyTurnLog.Count == 0
            ? "敌军本回合没有发生有效战斗。"
            : string.Join("\n", _enemyTurnLog);

        ResetEnemyTurnSequence();
        StartNextPlayerTurn(previousEnemyTurnLog);
    }

    /// <summary>清空敌军状态机临时数据；不会改变当前 BattlePhase。</summary>
    private void ResetEnemyTurnSequence()
    {
        _enemyTurnQueue.Clear();
        _enemyTurnLog.Clear();
        _activeEnemyUnit = null;
        _activeEnemyTarget = null;
        _enemyBattleWaitElapsed = 0.0;
        _enemyBattlePlaybackObserved = false;
        _enemyTurnStep = EnemyTurnSequenceStep.Idle;
    }

    /// <summary>
    /// 为玩家单位结算一次战斗经验，并把升级结果转换成 HUD 文本。
    /// </summary>
    private static string GrantCombatExperience(UnitModel attacker, UnitModel defender, bool defenderDefeated)
    {
        if (attacker.Team != UnitTeam.Player || !attacker.IsAlive)
        {
            return string.Empty;
        }

        int gainedExperience = CombatRules.CalculateExperienceGain(attacker, defender, defenderDefeated);
        IReadOnlyList<LevelUpResult> levelUps = attacker.GainExperience(gainedExperience);
        string text = $"\n{attacker.DisplayName} 获得 {gainedExperience} EXP。";

        foreach (LevelUpResult levelUp in levelUps)
        {
            string changes = levelUp.StatChanges.Count == 0
                ? "本级无属性提升"
                : string.Join("、", levelUp.StatChanges);
            text += $" 升到 Lv.{levelUp.NewLevel}：{changes}。";
        }

        return text;
    }

    /// <summary>
    /// 开始下一个玩家回合并清除玩家单位的行动标记。
    /// </summary>
    private void StartNextPlayerTurn(string previousEnemyTurnLog)
    {
        _round++;
        _phase = BattlePhase.Player;

        foreach (UnitModel player in _units.Where(unit => unit.IsAlive && unit.Team == UnitTeam.Player))
        {
            player.ResetForNewTurn();
        }

        ClearSelection();
        UpdateHud($"{previousEnemyTurnLog}\n\n第 {_round} 回合开始。请选择一个蓝色单位。");
        QueueRedraw();
    }

    /// <summary>
    /// 根据章节配置检查胜利条件，同时检查玩家是否已经全灭。
    /// </summary>
    private void CheckBattleOutcome()
    {
        bool hasPlayers = _units.Any(unit => unit.IsAlive && unit.Team == UnitTeam.Player);
        if (!hasPlayers)
        {
            _phase = BattlePhase.Defeat;
            ClearSelection();
            UpdateHud("战斗结束：我方已经没有存活单位。可以重新运行场景再次测试。");
            QueueRedraw();
            return;
        }

        bool playerHasWon;
        if (_victoryCondition.Equals("defeat_target", StringComparison.OrdinalIgnoreCase))
        {
            UnitModel? target = _units.FirstOrDefault(unit =>
                unit.Id.Equals(_victoryTargetId, StringComparison.OrdinalIgnoreCase));
            playerHasWon = target is not null && !target.IsAlive;
        }
        else
        {
            // rout 代表击败所有敌军，也是未识别胜利条件时的安全默认行为。
            playerHasWon = !_units.Any(unit => unit.IsAlive && unit.Team == UnitTeam.Enemy);
        }

        if (playerHasWon)
        {
            _phase = BattlePhase.Victory;
            ClearSelection();
            UpdateHud($"胜利：{_chapterTitle} 的主要目标已经完成。");
            QueueRedraw();
        }
    }

    /// <summary>
    /// 清空当前选择、移动状态和攻击目标。
    /// </summary>
    private void ClearSelection()
    {
        _selectedUnit = null;
        _pendingAttackTarget = null;
        _selectedUnitHasMoved = false;
        _reachableCells.Clear();
    }

    /// <summary>
    /// 刷新 HUD 的章节、状态、地形、战斗预测、战斗记录和单位信息。
    /// </summary>
    private void UpdateHud(string message)
    {
        if (_titleLabel is not null)
        {
            _titleLabel.Text = _chapterTitle;
        }

        if (_statusLabel is not null)
        {
            string objective = _victoryCondition.Equals("defeat_target", StringComparison.OrdinalIgnoreCase)
                ? "胜利条件：击败桥头队长"
                : "胜利条件：击败全部敌军";
            _statusLabel.Text = $"回合：{_round}  |  阶段：{PhaseDisplayName()}\n{objective}\n{message}";
        }

        UpdateTerrainHud();
        UpdateForecastHud();
        UpdateBattleLogHud();
        UpdateUnitListHud();
        UpdateActionButtons();
    }

    /// <summary>
    /// 刷新当前选中单位所处地形的说明。
    /// </summary>
    private void UpdateTerrainHud()
    {
        if (_terrainLabel is null)
        {
            return;
        }

        if (_selectedUnit is null)
        {
            _terrainLabel.Text = "地形：选择单位后显示当前地形效果。";
            return;
        }

        TerrainDefinition terrain = TerrainRules.Get(TerrainAt(_selectedUnit.GridPosition));
        string passableText = terrain.Passable ? "可通行" : "不可通行";
        _terrainLabel.Text =
            $"地形：{terrain.DisplayName} | 移动 {terrain.MoveCost} | 防御 +{terrain.DefenseBonus} | " +
            $"回避 +{terrain.AvoidBonus} | {passableText}";
    }

    /// <summary>
    /// 战斗预测已经从最终右侧界面移除；保留该控件引用只为兼容旧 HUD 创建顺序。
    /// </summary>
    private void UpdateForecastHud()
    {
        if (_forecastLabel is not null)
        {
            _forecastLabel.Text = string.Empty;
        }
    }

    /// <summary>
    /// 刷新最近一次战斗的逐击演出文本。
    /// </summary>
    private void UpdateBattleLogHud()
    {
        if (_battleLogLabel is not null)
        {
            _battleLogLabel.Text = "战斗记录\n" + _lastBattleLog;
        }
    }

    /// <summary>
    /// 刷新玩家与敌军的等级、职业、装备、经验和生命值列表。
    /// </summary>
    private void UpdateUnitListHud()
    {
        if (_unitListLabel is null)
        {
            return;
        }

        IEnumerable<string> playerLines = _units
            .Where(unit => unit.Team == UnitTeam.Player)
            .Select(unit =>
                $"{(unit.IsAlive ? "●" : "×")} {unit.DisplayName} Lv.{unit.Level} {unit.ClassDefinition.DisplayName} " +
                $"HP {unit.CurrentHp}/{unit.MaxHp} EXP {unit.Experience}/100 | {unit.EquippedWeapon.DisplayName}" +
                (unit.HasActed ? " [已行动]" : string.Empty));

        IEnumerable<string> enemyLines = _units
            .Where(unit => unit.Team == UnitTeam.Enemy)
            .Select(unit =>
                $"{(unit.IsAlive ? "●" : "×")} {unit.DisplayName} Lv.{unit.Level} " +
                $"HP {unit.CurrentHp}/{unit.MaxHp} | {unit.EquippedWeapon.DisplayName}");

        _unitListLabel.Text = "我方\n" + string.Join("\n", playerLines) + "\n\n敌方\n" + string.Join("\n", enemyLines);
    }

    /// <summary>
    /// 根据当前阶段与选择状态启用/禁用 HUD 操作按钮。
    /// </summary>
    private void UpdateActionButtons()
    {
        bool playerControlsEnabled = _phase == BattlePhase.Player;

        if (_confirmAttackButton is not null)
        {
            _confirmAttackButton.Disabled =
                !playerControlsEnabled ||
                _selectedUnit is null ||
                _pendingAttackTarget is null ||
                !CombatRules.CanAttack(_selectedUnit, _pendingAttackTarget);
        }

        if (_cancelAttackButton is not null)
        {
            _cancelAttackButton.Disabled = !playerControlsEnabled || _pendingAttackTarget is null;
        }

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
    /// 绘制当前章节地形和网格线。
    /// </summary>
    private void DrawBoard()
    {
        Color gridLine = new(0.08f, 0.10f, 0.09f, 0.8f);

        for (int y = 0; y < _gridHeight; y++)
        {
            for (int x = 0; x < _gridWidth; x++)
            {
                Vector2I cell = new(x, y);
                Rect2 rect = GridRect(cell);
                Color tileColor = TerrainColor(cell, TerrainAt(cell));
                DrawRect(rect, tileColor, true);
                DrawRect(rect, gridLine, false, 1.0f);
            }
        }
    }

    /// <summary>
    /// 返回地形对应的原型颜色；平地继续使用轻微棋盘明暗差增强格子可读性。
    /// </summary>
    private static Color TerrainColor(Vector2I cell, TerrainType terrain)
    {
        return terrain switch
        {
            TerrainType.Forest => new Color(0.13f, 0.31f, 0.16f),
            TerrainType.Bridge => new Color(0.53f, 0.43f, 0.29f),
            TerrainType.River => new Color(0.18f, 0.43f, 0.62f),
            TerrainType.Fort => new Color(0.43f, 0.38f, 0.33f),
            _ => (cell.X + cell.Y) % 2 == 0
                ? new Color(0.22f, 0.40f, 0.25f)
                : new Color(0.25f, 0.45f, 0.28f)
        };
    }

    /// <summary>
    /// 绘制当前选中单位的移动范围、攻击目标高亮与预测目标边框。
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
        Color targetBorder = new(1.0f, 0.38f, 0.12f, 1.0f);

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

        if (_pendingAttackTarget is not null)
        {
            DrawRect(GridRect(_pendingAttackTarget.GridPosition), targetBorder, false, 5.0f);
        }
    }

    /// <summary>
    /// 以简单圆形和生命条绘制单位。
    /// 蓝色代表玩家、红色代表敌军；魔法使用者额外绘制一个紫色内点作为临时识别标记。
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

            if (unit.EquippedWeapon.DamageType == DamageType.Magical)
            {
                DrawCircle(center, 6.0f, new Color(0.72f, 0.32f, 0.92f));
            }

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
    /// 获取指定格子的地形；章节没有显式配置的格子默认是平地。
    /// </summary>
    private TerrainType TerrainAt(Vector2I cell)
    {
        return _terrain.TryGetValue(cell, out TerrainType terrain)
            ? terrain
            : TerrainType.Plain;
    }

    /// <summary>
    /// 查找某格上的存活单位。
    /// </summary>
    private UnitModel? FindLivingUnitAt(Vector2I cell)
    {
        return _units.FirstOrDefault(unit => unit.IsAlive && unit.GridPosition == cell);
    }

    /// <summary>
    /// 判断指定格是否被当前移动单位之外的存活单位占据。
    /// </summary>
    private bool IsOccupiedByOtherUnit(Vector2I cell, UnitModel movingUnit)
    {
        return _units.Any(unit => unit.IsAlive && unit != movingUnit && unit.GridPosition == cell);
    }

    /// <summary>
    /// 判断逻辑坐标是否位于当前章节地图范围内。
    /// </summary>
    private bool IsInsideBoard(Vector2I cell)
    {
        return cell.X >= 0 && cell.X < _gridWidth && cell.Y >= 0 && cell.Y < _gridHeight;
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

    /// <summary>敌军逐单位行动状态。</summary>
    private enum EnemyTurnSequenceStep
    {
        Idle,
        SelectNextUnit,
        WaitForMovement,
        ResolveAttack,
        WaitForBattlePresentation
    }

    /// <summary>
    /// 战斗阶段状态机。
    /// </summary>
    private enum BattlePhase
    {
        Player,
        Enemy,
        Victory,
        Defeat
    }
}
