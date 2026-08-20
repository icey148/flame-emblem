using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 把真实 CombatExchangeResult 按攻击顺序播放成独立复古战斗演出。
/// 时间线完全由 Godot Timer 驱动；玩家主动攻击优先播放，敌军攻击先等待地图移动动画结束。
/// </summary>
public partial class RetroBattleAnimationCoordinator : Node
{
    /// <summary>
    /// 等待播放的战斗交换及其表现快照。
    /// EXP 必须在 BattleAnimationBus 事件触发瞬间记录，因为 MainGame 会在 ResolveExchange 返回以后才发放经验。
    /// </summary>
    private readonly Queue<QueuedBattlePresentation> _queue = new();

    /// <summary>地图人物表现协调器，用于等待敌军地图移动完成。</summary>
    private CharacterVisualCoordinator? _visualCoordinator;

    /// <summary>全屏输入拦截层。</summary>
    private Control? _blocker;

    /// <summary>左侧战斗人物。</summary>
    private AnimatedBattleCharacterControl? _leftCharacter;

    /// <summary>右侧战斗人物。</summary>
    private AnimatedBattleCharacterControl? _rightCharacter;

    /// <summary>左侧人物顶部名称。</summary>
    private Label? _leftLabel;

    /// <summary>右侧人物顶部名称。</summary>
    private Label? _rightLabel;

    /// <summary>中央逐击结果文本。</summary>
    private Label? _resultLabel;

    /// <summary>双方 HP、伤害和玩家 EXP 的底部战斗 HUD。</summary>
    private RetroBattleStatusHudControl? _statusHud;

    /// <summary>命中、闪避、必杀和魔法的程序特效层。</summary>
    private RetroBattleEffectControl? _effectControl;

    /// <summary>驱动 Intro/Windup/Impact/Recovery/Outro 的阶段计时器。</summary>
    private Timer? _phaseTimer;

    /// <summary>整场战斗安全超时。</summary>
    private Timer? _watchdogTimer;

    /// <summary>等待地图移动结束的轮询计时器。</summary>
    private Timer? _movementGateTimer;

    /// <summary>当前正在播放的表现快照。</summary>
    private QueuedBattlePresentation? _currentPresentation;

    /// <summary>当前交换左侧固定人物。</summary>
    private UnitModel? _leftUnit;

    /// <summary>当前交换右侧固定人物。</summary>
    private UnitModel? _rightUnit;

    /// <summary>当前正在播放第几次攻击。</summary>
    private int _strikeIndex;

    /// <summary>当前演出阶段。</summary>
    private PlaybackPhase _phase = PlaybackPhase.Hidden;

    /// <summary>每一击允许的最大整场预算。</summary>
    private const double MaximumSecondsPerStrike = 2.2;

    /// <summary>整场演出最短安全超时。</summary>
    private const double MinimumExchangeTimeoutSeconds = 6.0;

    /// <summary>敌军移动等待轮询间隔。</summary>
    private const double MovementGateIntervalSeconds = 0.05;

    /// <summary>进入场景树时订阅战斗结算事件。</summary>
    public override void _EnterTree()
    {
        BattleAnimationBus.ExchangeResolved += OnExchangeResolved;
    }

    /// <summary>离开场景树时释放订阅、计时器和全局播放状态。</summary>
    public override void _ExitTree()
    {
        BattleAnimationBus.ExchangeResolved -= OnExchangeResolved;
        StopAllTimers();
        BattleAnimationBus.EndPlayback();
        _blocker?.Hide();
    }

    /// <summary>创建 Timer 时间线和战斗 UI。</summary>
    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _visualCoordinator = GetNodeOrNull<CharacterVisualCoordinator>("../CharacterVisualCoordinator");
        CreateTimers();
        CreateBattleOverlay();
    }

    /// <summary>
    /// 收到真实战斗结果后保存表现所需快照并入队。
    /// 这里发生在 MainGame 发放 EXP 之前，因此可以安全记录经验增长前的等级和经验值。
    /// </summary>
    private void OnExchangeResolved(CombatExchangeResult exchange)
    {
        if (exchange.Strikes.Count == 0)
        {
            return;
        }

        UnitModel leftUnit = exchange.InitiatingAttacker;
        UnitModel rightUnit = exchange.InitiatingDefender;
        UnitModel? experienceUnit = exchange.Strikes
            .Select(strike => strike.Attacker)
            .FirstOrDefault(unit => unit.Team == UnitTeam.Player);

        QueuedBattlePresentation presentation = new(
            exchange,
            leftUnit.MaxHp,
            rightUnit.MaxHp,
            experienceUnit,
            experienceUnit?.Level ?? 0,
            experienceUnit?.Experience ?? 0);

        _queue.Enqueue(presentation);
        RequestStartForQueueHead();
    }

    /// <summary>根据队首攻击方决定立即播放还是先等待地图移动。</summary>
    private void RequestStartForQueueHead()
    {
        if (_currentPresentation is not null || _queue.Count == 0)
        {
            return;
        }

        QueuedBattlePresentation next = _queue.Peek();
        bool playerInitiated = next.Exchange.InitiatingAttacker.Team == UnitTeam.Player;
        if (playerInitiated)
        {
            StartNextExchangeNow();
            return;
        }

        RequestStartAfterMovement();
    }

    /// <summary>创建阶段、超时、移动等待三个独立一次性 Timer。</summary>
    private void CreateTimers()
    {
        _phaseTimer = new Timer
        {
            OneShot = true,
            ProcessMode = ProcessModeEnum.Always
        };
        _phaseTimer.Timeout += () => RunSafely(AdvancePlaybackPhase);
        AddChild(_phaseTimer);

        _watchdogTimer = new Timer
        {
            OneShot = true,
            ProcessMode = ProcessModeEnum.Always
        };
        _watchdogTimer.Timeout += () => RunSafely(() =>
            EmergencyFinishExchange("战斗演出超过安全时间上限"));
        AddChild(_watchdogTimer);

        _movementGateTimer = new Timer
        {
            OneShot = true,
            ProcessMode = ProcessModeEnum.Always
        };
        _movementGateTimer.Timeout += () => RunSafely(CheckMovementAndStartExchange);
        AddChild(_movementGateTimer);
    }

    /// <summary>
    /// 敌军战斗至少延后一个短计时周期，让地图人物层先检测 GridPosition 改变并创建移动动画。
    /// </summary>
    private void RequestStartAfterMovement()
    {
        if (_currentPresentation is not null || _queue.Count == 0 || _movementGateTimer is null)
        {
            return;
        }

        if (_movementGateTimer.IsStopped())
        {
            _movementGateTimer.Start(MovementGateIntervalSeconds);
        }
    }

    /// <summary>地图仍在移动时继续等待；全部到达后才打开敌军横向战斗界面。</summary>
    private void CheckMovementAndStartExchange()
    {
        if (_currentPresentation is not null || _queue.Count == 0)
        {
            return;
        }

        if (_visualCoordinator?.IsMovementAnimating ?? false)
        {
            _movementGateTimer?.Start(MovementGateIntervalSeconds);
            return;
        }

        StartNextExchangeNow();
    }

    /// <summary>从队列取出下一场战斗并正式打开演出。</summary>
    private void StartNextExchangeNow()
    {
        if (_currentPresentation is not null || _queue.Count == 0)
        {
            return;
        }

        QueuedBattlePresentation presentation = _queue.Dequeue();
        CombatExchangeResult exchange = presentation.Exchange;
        if (exchange.Strikes.Count == 0)
        {
            RequestStartForQueueHead();
            return;
        }

        _movementGateTimer?.Stop();
        _currentPresentation = presentation;
        _strikeIndex = 0;
        _leftUnit = exchange.InitiatingAttacker;
        _rightUnit = exchange.InitiatingDefender;

        // 先标记全局表现状态，再显示遮罩；地图移动层会据此暂停后台人物动画。
        BattleAnimationBus.BeginPlayback();
        _blocker?.Show();
        _leftCharacter?.SetUnit(_leftUnit);
        _rightCharacter?.SetUnit(_rightUnit);
        _leftCharacter?.Play(CharacterAnimationState.Idle);
        _rightCharacter?.Play(CharacterAnimationState.Idle);
        _effectControl?.Clear();

        if (_leftLabel is not null)
        {
            _leftLabel.Text = _leftUnit.DisplayName;
        }

        if (_rightLabel is not null)
        {
            _rightLabel.Text = _rightUnit.DisplayName;
        }

        if (_resultLabel is not null)
        {
            _resultLabel.Text = "战斗开始";
        }

        _statusHud?.BeginBattle(
            _leftUnit,
            exchange.InitiatingAttackerHpBefore,
            presentation.LeftMaxHpBefore,
            _rightUnit,
            exchange.InitiatingDefenderHpBefore,
            presentation.RightMaxHpBefore,
            presentation.ExperienceUnit,
            presentation.ExperienceLevelBefore,
            presentation.ExperienceBefore);

        double timeout = Math.Max(
            MinimumExchangeTimeoutSeconds,
            exchange.Strikes.Count * MaximumSecondsPerStrike + 3.0);
        _watchdogTimer?.Start(timeout);
        SchedulePhase(PlaybackPhase.Intro, 0.28);
    }

    /// <summary>切换到指定演出阶段并启动 Timer。</summary>
    private void SchedulePhase(PlaybackPhase phase, double seconds)
    {
        _phase = phase;
        _phaseTimer?.Start(Math.Max(0.01, seconds));
    }

    /// <summary>阶段 Timer 到点后推进一次状态。</summary>
    private void AdvancePlaybackPhase()
    {
        if (_currentPresentation is null)
        {
            ResetCurrentExchangeState();
            RequestStartForQueueHead();
            return;
        }

        switch (_phase)
        {
            case PlaybackPhase.Intro:
                StartCurrentStrike();
                break;
            case PlaybackPhase.Windup:
                StartImpact();
                break;
            case PlaybackPhase.Impact:
                StartRecovery();
                break;
            case PlaybackPhase.Recovery:
                AdvanceStrikeOrFinish();
                break;
            case PlaybackPhase.Outro:
                FinishExchange();
                break;
            default:
                EmergencyFinishExchange("战斗演出进入了无效阶段");
                break;
        }
    }

    /// <summary>开始当前一击的蓄力/攻击动作。</summary>
    private void StartCurrentStrike()
    {
        CombatStrikeResult? strike = CurrentStrike();
        if (strike is null)
        {
            EmergencyFinishExchange("当前攻击记录不存在");
            return;
        }

        _leftCharacter?.Play(CharacterAnimationState.Idle);
        _rightCharacter?.Play(CharacterAnimationState.Idle);
        _effectControl?.Clear();

        AnimatedBattleCharacterControl? attackerControl = ControlFor(strike.Attacker);
        bool magical = strike.Attacker.EquippedWeapon.DamageType == DamageType.Magical;
        attackerControl?.Play(magical ? CharacterAnimationState.Cast : CharacterAnimationState.Attack);

        if (magical)
        {
            _effectControl?.Play(RetroBattleEffectKind.Magic, IsLeftUnit(strike.Attacker));
        }

        if (_resultLabel is not null)
        {
            string costText = strike.HpCostPaid > 0 ? $"  HP -{strike.HpCostPaid}" : string.Empty;
            _resultLabel.Text = $"{strike.Attacker.DisplayName}  {strike.Attacker.EquippedWeapon.DisplayName}{costText}";
        }

        SchedulePhase(PlaybackPhase.Windup, CurrentWindupDuration());
    }

    /// <summary>
    /// 显示命中、闪避、必杀或倒下结果，并在同一时刻推进底部 HP/伤害 HUD。
    /// </summary>
    private void StartImpact()
    {
        CombatStrikeResult? strike = CurrentStrike();
        if (strike is null)
        {
            EmergencyFinishExchange("命中阶段找不到当前攻击记录");
            return;
        }

        AnimatedBattleCharacterControl? attackerControl = ControlFor(strike.Attacker);
        AnimatedBattleCharacterControl? defenderControl = ControlFor(strike.Defender);
        attackerControl?.Play(CharacterAnimationState.Idle);

        // 血条在真正的命中时刻变化，法术 HP 成本和伤害都按真实逐击顺序重放。
        _statusHud?.ApplyStrike(strike);

        if (!strike.Hit)
        {
            defenderControl?.Play(CharacterAnimationState.Dodge);
            _effectControl?.Play(RetroBattleEffectKind.Dodge, IsLeftUnit(strike.Attacker));
            if (_resultLabel is not null)
            {
                _resultLabel.Text = $"MISS  ·  命中率 {strike.HitRate}%";
            }
        }
        else
        {
            defenderControl?.Play(strike.DefenderDefeated
                ? CharacterAnimationState.Defeat
                : CharacterAnimationState.Hit);
            _effectControl?.Play(
                strike.Critical ? RetroBattleEffectKind.Critical : RetroBattleEffectKind.Hit,
                IsLeftUnit(strike.Attacker));

            if (_resultLabel is not null)
            {
                string criticalText = strike.Critical ? "必杀！  " : string.Empty;
                string defeatText = strike.DefenderDefeated ? "  ·  击倒" : string.Empty;
                _resultLabel.Text = $"{criticalText}{strike.Damage} DAMAGE{defeatText}";
            }
        }

        SchedulePhase(PlaybackPhase.Impact, 0.42);
    }

    /// <summary>结果展示完成后恢复待机。</summary>
    private void StartRecovery()
    {
        CombatStrikeResult? strike = CurrentStrike();
        if (strike is null)
        {
            EmergencyFinishExchange("恢复阶段找不到当前攻击记录");
            return;
        }

        ControlFor(strike.Attacker)?.Play(CharacterAnimationState.Idle);
        if (!strike.DefenderDefeated)
        {
            ControlFor(strike.Defender)?.Play(CharacterAnimationState.Idle);
        }

        _effectControl?.Clear();
        SchedulePhase(PlaybackPhase.Recovery, 0.22);
    }

    /// <summary>进入下一次反击/追击，或进入战后 EXP 展示。</summary>
    private void AdvanceStrikeOrFinish()
    {
        if (_currentPresentation is null)
        {
            EmergencyFinishExchange("恢复阶段失去当前战斗交换");
            return;
        }

        _strikeIndex++;
        if (_strikeIndex < _currentPresentation.Exchange.Strikes.Count)
        {
            StartCurrentStrike();
            return;
        }

        _effectControl?.Clear();
        int gainedExperience = _statusHud?.ShowExperienceResult() ?? 0;
        if (_resultLabel is not null)
        {
            _resultLabel.Text = gainedExperience > 0
                ? $"获得 {gainedExperience} EXP"
                : "战斗结束";
        }

        // 有玩家实际参与攻击时多停留一点时间，让 EXP 与升级信息可读。
        double outroDuration = _currentPresentation.ExperienceUnit is null ? 0.55 : 1.15;
        SchedulePhase(PlaybackPhase.Outro, outroDuration);
    }

    /// <summary>正常结束当前演出并准备队列下一场。</summary>
    private void FinishExchange()
    {
        ResetCurrentExchangeState();
        RequestStartForQueueHead();
    }

    /// <summary>手动跳过当前与排队中的全部演出。</summary>
    private void SkipAllBattleAnimations()
    {
        _queue.Clear();
        ResetCurrentExchangeState();
    }

    /// <summary>异常或超时时强制释放战斗窗口。</summary>
    private void EmergencyFinishExchange(string reason)
    {
        GD.PushWarning($"Battle animation aborted safely: {reason}");
        _queue.Clear();
        ResetCurrentExchangeState();
    }

    /// <summary>统一释放当前演出状态和全屏遮罩。</summary>
    private void ResetCurrentExchangeState()
    {
        _phaseTimer?.Stop();
        _watchdogTimer?.Stop();
        _blocker?.Hide();
        _effectControl?.Clear();
        _statusHud?.ResetBattle();
        _leftCharacter?.Play(CharacterAnimationState.Idle);
        _rightCharacter?.Play(CharacterAnimationState.Idle);
        _currentPresentation = null;
        _leftUnit = null;
        _rightUnit = null;
        _strikeIndex = 0;
        _phase = PlaybackPhase.Hidden;
        BattleAnimationBus.EndPlayback();
    }

    /// <summary>停止协调器拥有的全部计时器。</summary>
    private void StopAllTimers()
    {
        _phaseTimer?.Stop();
        _watchdogTimer?.Stop();
        _movementGateTimer?.Stop();
    }

    /// <summary>所有 Timer 和事件入口都通过异常隔离执行。</summary>
    private void RunSafely(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            GD.PushError($"Battle animation failed and was safely aborted: {exception}");
            EmergencyFinishExchange("战斗演出发生运行时异常");
        }
    }

    /// <summary>返回当前正在播放的攻击记录。</summary>
    private CombatStrikeResult? CurrentStrike()
    {
        if (_currentPresentation is null ||
            _strikeIndex < 0 ||
            _strikeIndex >= _currentPresentation.Exchange.Strikes.Count)
        {
            return null;
        }

        return _currentPresentation.Exchange.Strikes[_strikeIndex];
    }

    /// <summary>魔法蓄力略长于物理攻击。</summary>
    private double CurrentWindupDuration()
    {
        CombatStrikeResult? strike = CurrentStrike();
        if (strike is null)
        {
            return 0.30;
        }

        return strike.Attacker.EquippedWeapon.DamageType == DamageType.Magical ? 0.50 : 0.34;
    }

    /// <summary>判断某人物是否固定站在左侧。</summary>
    private bool IsLeftUnit(UnitModel unit)
    {
        return ReferenceEquals(unit, _leftUnit);
    }

    /// <summary>根据人物返回其固定战斗位控件。</summary>
    private AnimatedBattleCharacterControl? ControlFor(UnitModel unit)
    {
        return IsLeftUnit(unit) ? _leftCharacter : _rightCharacter;
    }

    /// <summary>
    /// 创建全屏战斗演出 UI。
    /// 人物控件使用 420×408，使内部正式纹理区域恰好为 384×384：96px 素材为 4×，128px 素材为 3×，避免非整数拉伸造成模糊。
    /// </summary>
    private void CreateBattleOverlay()
    {
        CanvasLayer layer = new()
        {
            Layer = 80
        };
        AddChild(layer);

        _blocker = new Control
        {
            Position = Vector2.Zero,
            Size = new Vector2(1280, 720),
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = false
        };
        layer.AddChild(_blocker);

        ColorRect backdrop = new()
        {
            Position = Vector2.Zero,
            Size = new Vector2(1280, 720),
            Color = new Color(0.018f, 0.022f, 0.032f, 0.94f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _blocker.AddChild(backdrop);

        PanelContainer battlePanel = new()
        {
            Position = new Vector2(80, 70),
            Size = new Vector2(1120, 580),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _blocker.AddChild(battlePanel);

        StyleBoxFlat panelStyle = new()
        {
            BgColor = new Color(0.055f, 0.065f, 0.08f, 1.0f),
            BorderColor = new Color(0.48f, 0.37f, 0.22f, 1.0f),
            BorderWidthLeft = 3,
            BorderWidthTop = 3,
            BorderWidthRight = 3,
            BorderWidthBottom = 3,
            CornerRadiusTopLeft = 0,
            CornerRadiusTopRight = 0,
            CornerRadiusBottomLeft = 0,
            CornerRadiusBottomRight = 0
        };
        battlePanel.AddThemeStyleboxOverride("panel", panelStyle);

        Control stage = new()
        {
            CustomMinimumSize = new Vector2(1100, 560),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        battlePanel.AddChild(stage);

        // 96×96 正式素材会落到 384×384 的整数 4 倍显示区，128×128 则正好是整数 3 倍。
        _leftCharacter = new AnimatedBattleCharacterControl
        {
            Position = new Vector2(25, 48),
            Size = new Vector2(420, 408),
            MirrorHorizontally = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        stage.AddChild(_leftCharacter);

        _rightCharacter = new AnimatedBattleCharacterControl
        {
            Position = new Vector2(655, 48),
            Size = new Vector2(420, 408),
            MirrorHorizontally = true,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        stage.AddChild(_rightCharacter);

        _leftLabel = CreateTopLabel(new Vector2(45, 10), new Vector2(360, 34), HorizontalAlignment.Left);
        stage.AddChild(_leftLabel);

        _rightLabel = CreateTopLabel(new Vector2(695, 10), new Vector2(360, 34), HorizontalAlignment.Right);
        stage.AddChild(_rightLabel);

        Label versus = CreateTopLabel(new Vector2(500, 10), new Vector2(100, 34), HorizontalAlignment.Center);
        versus.Text = "VS";
        stage.AddChild(versus);

        _resultLabel = new Label
        {
            Position = new Vector2(360, 395),
            Size = new Vector2(380, 36),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _resultLabel.AddThemeColorOverride("font_color", new Color(0.96f, 0.90f, 0.73f));
        _resultLabel.AddThemeFontSizeOverride("font_size", 20);
        stage.AddChild(_resultLabel);

        _effectControl = new RetroBattleEffectControl
        {
            Position = Vector2.Zero,
            Size = new Vector2(1100, 430),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        stage.AddChild(_effectControl);

        _statusHud = new RetroBattleStatusHudControl
        {
            Position = new Vector2(35, 432),
            Size = new Vector2(1030, 116),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        stage.AddChild(_statusHud);

        Button skipButton = new()
        {
            Text = "跳过",
            Position = new Vector2(995, 8),
            Size = new Vector2(80, 34),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        skipButton.Pressed += SkipAllBattleAnimations;
        stage.AddChild(skipButton);
    }

    /// <summary>创建顶部人物名称标签。</summary>
    private static Label CreateTopLabel(Vector2 position, Vector2 size, HorizontalAlignment alignment)
    {
        Label label = new()
        {
            Position = position,
            Size = size,
            HorizontalAlignment = alignment,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeColorOverride("font_color", new Color(0.92f, 0.88f, 0.78f));
        label.AddThemeFontSizeOverride("font_size", 19);
        return label;
    }

    /// <summary>
    /// 保存一场战斗在表现层开始播放前必须冻结的数值。
    /// UnitModel 本体会在动画期间继续保留真实最终结算，因此 HP/EXP 的起点不能延迟读取。
    /// </summary>
    private sealed class QueuedBattlePresentation
    {
        /// <summary>创建表现快照。</summary>
        public QueuedBattlePresentation(
            CombatExchangeResult exchange,
            int leftMaxHpBefore,
            int rightMaxHpBefore,
            UnitModel? experienceUnit,
            int experienceLevelBefore,
            int experienceBefore)
        {
            Exchange = exchange;
            LeftMaxHpBefore = Math.Max(1, leftMaxHpBefore);
            RightMaxHpBefore = Math.Max(1, rightMaxHpBefore);
            ExperienceUnit = experienceUnit;
            ExperienceLevelBefore = experienceLevelBefore;
            ExperienceBefore = Math.Clamp(experienceBefore, 0, 99);
        }

        /// <summary>已经结算完成的真实战斗交换。</summary>
        public CombatExchangeResult Exchange { get; }

        /// <summary>左侧单位开战时最大 HP。</summary>
        public int LeftMaxHpBefore { get; }

        /// <summary>右侧单位开战时最大 HP。</summary>
        public int RightMaxHpBefore { get; }

        /// <summary>本场实际参与攻击、因此可能获得经验的玩家单位。</summary>
        public UnitModel? ExperienceUnit { get; }

        /// <summary>发放经验前等级。</summary>
        public int ExperienceLevelBefore { get; }

        /// <summary>发放经验前 EXP。</summary>
        public int ExperienceBefore { get; }
    }

    /// <summary>战斗演出内部阶段。</summary>
    private enum PlaybackPhase
    {
        Hidden,
        Intro,
        Windup,
        Impact,
        Recovery,
        Outro
    }
}
