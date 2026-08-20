using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Main;

/// <summary>
/// 当前可运行的战棋主场景。
/// 该节点负责地图绘制、玩家交互与回合推进，职业、地形、战斗公式和章节数据均委托给独立模块。
/// </summary>
public partial class MainGame : Node2D
{
    /// <summary>每个逻辑格子的像素尺寸。</summary>
    private const int CellSize = 52;

    /// <summary>地图左上角在窗口中的绘制偏移。</summary>
    private static readonly Vector2 BoardOrigin = new(40, 100);

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

    /// <summary>当前战斗中的全部单位，包括已经倒下但仍保留在集合中的单位。</summary>
    private readonly List<UnitModel> _units = new();

    /// <summary>当前选中的玩家单位；为空表示等待玩家选择。</summary>
    private UnitModel? _selectedUnit;

    /// <summary>当前战斗预测对应的敌军目标。</summary>
    private UnitModel? _pendingAttackTarget;

    /// <summary>当前选中单位是否已经完成了本回合移动。</summary>
    private bool _selectedUnitHasMoved;

    /// <summary>当前选中单位可移动到的格子集合。</summary>
    private HashSet<Vector2I> _reachableCells = new();

    /// <summary>HUD 标题文本。</summary>
    private Label? _titleLabel;

    /// <summary>HUD 顶部状态文本。</summary>
    private Label? _statusLabel;

    /// <summary>HUD 单位列表文本。</summary>
    private Label? _unitListLabel;

    /// <summary>HUD 当前地形信息。</summary>
    private Label? _terrainLabel;

    /// <summary>HUD 战斗预测信息。</summary>
    private Label? _forecastLabel;

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
    /// Godot 节点进入场景树后创建 HUD，并从 JSON 加载第一张正式测试关卡。
    /// </summary>
    public override void _Ready()
    {
        CreateHud();

        try
        {
            LoadChapter();
            UpdateHud("选择一个蓝色单位开始行动。移动后选择攻击范围内的敌军即可查看战斗预测。");
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
    /// 后续 UI 稳定后再拆成独立 .tscn 场景，当前保持工程最小可运行依赖。
    /// </summary>
    private void CreateHud()
    {
        CanvasLayer canvasLayer = new();
        AddChild(canvasLayer);

        PanelContainer panel = new()
        {
            Position = new Vector2(850, 45),
            Size = new Vector2(390, 635)
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
            CustomMinimumSize = new Vector2(350, 85)
        };
        column.AddChild(_statusLabel);

        _terrainLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(350, 45)
        };
        column.AddChild(_terrainLabel);

        _forecastLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(350, 105)
        };
        column.AddChild(_forecastLabel);

        _unitListLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(350, 210)
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

        // 点击攻击范围内的敌军只进入“预测”状态，不立即结算伤害。
        if (clickedUnit is { Team: UnitTeam.Enemy } enemy &&
            CombatRules.IsInAttackRange(_selectedUnit, enemy))
        {
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
            UpdateHud($"{_selectedUnit.DisplayName} 已移动。请选择攻击范围内敌军查看预测，或点击“等待”。");
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
        UpdateHud($"已选择 {unit.DisplayName}（{unit.ClassDefinition.DisplayName}）。蓝色区域为可移动范围。");
        QueueRedraw();
    }

    /// <summary>
    /// 设置当前攻击目标并刷新战斗预测窗口。
    /// </summary>
    private void SetPendingAttackTarget(UnitModel enemy)
    {
        if (_selectedUnit is null || !CombatRules.IsInAttackRange(_selectedUnit, enemy))
        {
            return;
        }

        _pendingAttackTarget = enemy;
        UpdateHud($"已锁定 {enemy.DisplayName}。确认预测无误后点击“确认攻击”。");
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
    /// 平地/桥面消耗 1，森林消耗 2，河流不可进入，其他存活单位视为阻挡。
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
                if (!IsInsideBoard(next) || IsOccupiedByOtherUnit(next, unit))
                {
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

        return bestCosts.Keys.ToHashSet();
    }

    /// <summary>
    /// 玩家确认战斗预测后执行攻击、可能的反击、经验获取和升级。
    /// </summary>
    private void OnConfirmAttackPressed()
    {
        if (_phase != BattlePhase.Player ||
            _selectedUnit is null ||
            _pendingAttackTarget is null ||
            !_pendingAttackTarget.IsAlive ||
            !CombatRules.IsInAttackRange(_selectedUnit, _pendingAttackTarget))
        {
            UpdateHud("当前没有可以确认的攻击目标。");
            return;
        }

        UnitModel attacker = _selectedUnit;
        UnitModel defender = _pendingAttackTarget;
        TerrainDefinition attackerTerrain = TerrainRules.Get(TerrainAt(attacker.GridPosition));
        TerrainDefinition defenderTerrain = TerrainRules.Get(TerrainAt(defender.GridPosition));
        CombatForecast forecast = CombatRules.CreateForecast(
            attacker,
            defender,
            attackerTerrain.DefenseBonus,
            defenderTerrain.DefenseBonus);

        int damage = CombatRules.ResolveAttack(attacker, defender, defenderTerrain.DefenseBonus);
        bool defenderDefeated = !defender.IsAlive;
        string result = $"{attacker.DisplayName} 对 {defender.DisplayName} 造成 {damage} 点伤害。";

        // 只有预测确认目标能存活并具备射程时才会发生反击，确保实际结算与预测一致。
        if (!defenderDefeated && forecast.DefenderCanCounter)
        {
            int counterDamage = CombatRules.ResolveAttack(defender, attacker, attackerTerrain.DefenseBonus);
            result += $" {defender.DisplayName} 反击造成 {counterDamage} 点伤害。";
        }

        result += GrantCombatExperience(attacker, defender, defenderDefeated);
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
    /// 执行一整个规则驱动的敌军回合。
    /// 每个敌人寻找最近玩家、按地形移动到更近位置，并在进入射程后执行一次攻击交换。
    /// </summary>
    private void RunEnemyTurn()
    {
        if (_phase is BattlePhase.Victory or BattlePhase.Defeat)
        {
            return;
        }

        _phase = BattlePhase.Enemy;
        ClearSelection();
        List<string> turnLog = new() { "敌军按照固定规则行动；游戏没有使用 LLM 或生成式 AI。" };

        foreach (UnitModel enemy in _units.Where(unit => unit.IsAlive && unit.Team == UnitTeam.Enemy).ToList())
        {
            UnitModel? target = EnemyTurnController.FindNearestPlayer(enemy, _units);
            if (target is null)
            {
                break;
            }

            // 如果当前位置不能攻击，就先在自己的可达范围中选择更接近目标的格子。
            if (!CombatRules.IsInAttackRange(enemy, target))
            {
                HashSet<Vector2I> reachable = CalculateReachableCells(enemy);
                Vector2I destination = EnemyTurnController.ChooseMoveDestination(enemy, target, reachable);
                enemy.GridPosition = destination;
            }

            if (target.IsAlive && CombatRules.IsInAttackRange(enemy, target))
            {
                TerrainDefinition enemyTerrain = TerrainRules.Get(TerrainAt(enemy.GridPosition));
                TerrainDefinition targetTerrain = TerrainRules.Get(TerrainAt(target.GridPosition));
                CombatForecast forecast = CombatRules.CreateForecast(
                    enemy,
                    target,
                    enemyTerrain.DefenseBonus,
                    targetTerrain.DefenseBonus);

                int damage = CombatRules.ResolveAttack(enemy, target, targetTerrain.DefenseBonus);
                turnLog.Add($"{enemy.DisplayName} 攻击 {target.DisplayName}，造成 {damage} 点伤害。");

                // 玩家单位作为防守方也可以正常反击，而且反击造成伤害后同样获得经验。
                if (target.IsAlive && forecast.DefenderCanCounter)
                {
                    int counterDamage = CombatRules.ResolveAttack(target, enemy, enemyTerrain.DefenseBonus);
                    bool enemyDefeated = !enemy.IsAlive;
                    turnLog.Add($"{target.DisplayName} 反击，造成 {counterDamage} 点伤害。" +
                                GrantCombatExperience(target, enemy, enemyDefeated));
                }
            }

            CheckBattleOutcome();
            if (_phase is BattlePhase.Victory or BattlePhase.Defeat)
            {
                break;
            }
        }

        if (_phase is BattlePhase.Victory or BattlePhase.Defeat)
        {
            return;
        }

        StartNextPlayerTurn(string.Join("\n", turnLog));
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
        string text = $" {attacker.DisplayName} 获得 {gainedExperience} EXP。";

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
            UnitModel? target = _units.FirstOrDefault(unit => unit.Id.Equals(_victoryTargetId, StringComparison.OrdinalIgnoreCase));
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
            UpdateHud($"胜利：{_chapterTitle} 的主要目标已经完成。当前第一关数据驱动战斗结束。");
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
    /// 刷新 HUD 的章节、状态、地形、战斗预测和单位信息。
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
        _terrainLabel.Text = $"地形：{terrain.DisplayName} | 移动消耗 {terrain.MoveCost} | 防御 +{terrain.DefenseBonus} | 回避 +{terrain.AvoidBonus} | {passableText}";
    }

    /// <summary>
    /// 根据当前攻击目标刷新预测窗口。
    /// </summary>
    private void UpdateForecastHud()
    {
        if (_forecastLabel is null)
        {
            return;
        }

        if (_selectedUnit is null || _pendingAttackTarget is null)
        {
            _forecastLabel.Text = "战斗预测：选择攻击范围内敌军后显示。";
            return;
        }

        TerrainDefinition attackerTerrain = TerrainRules.Get(TerrainAt(_selectedUnit.GridPosition));
        TerrainDefinition defenderTerrain = TerrainRules.Get(TerrainAt(_pendingAttackTarget.GridPosition));
        CombatForecast forecast = CombatRules.CreateForecast(
            _selectedUnit,
            _pendingAttackTarget,
            attackerTerrain.DefenseBonus,
            defenderTerrain.DefenseBonus);

        int defenderAfter = Mathf.Max(0, _pendingAttackTarget.CurrentHp - forecast.AttackerDamage);
        int attackerAfter = forecast.DefenderCanCounter
            ? Mathf.Max(0, _selectedUnit.CurrentHp - forecast.DefenderCounterDamage)
            : _selectedUnit.CurrentHp;
        string counterText = forecast.DefenderCanCounter
            ? $"反击 {forecast.DefenderCounterDamage} → 我方 HP {attackerAfter}/{_selectedUnit.MaxHp}"
            : "敌方无法反击";

        _forecastLabel.Text =
            $"战斗预测\n{_selectedUnit.DisplayName} → {_pendingAttackTarget.DisplayName}\n" +
            $"伤害 {forecast.AttackerDamage} → 敌方 HP {defenderAfter}/{_pendingAttackTarget.MaxHp}\n{counterText}\n" +
            $"目标地形：{defenderTerrain.DisplayName}（防御 +{defenderTerrain.DefenseBonus}）";
    }

    /// <summary>
    /// 刷新玩家与敌军的等级、职业、经验和生命值列表。
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
                $"HP {unit.CurrentHp}/{unit.MaxHp} EXP {unit.Experience}/100" +
                (unit.HasActed ? " [已行动]" : string.Empty));

        IEnumerable<string> enemyLines = _units
            .Where(unit => unit.Team == UnitTeam.Enemy)
            .Select(unit =>
                $"{(unit.IsAlive ? "●" : "×")} {unit.DisplayName} Lv.{unit.Level} {unit.ClassDefinition.DisplayName} " +
                $"HP {unit.CurrentHp}/{unit.MaxHp}");

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
            _confirmAttackButton.Disabled = !playerControlsEnabled || _selectedUnit is null || _pendingAttackTarget is null;
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
