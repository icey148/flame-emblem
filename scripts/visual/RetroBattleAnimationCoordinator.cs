using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 把真实 CombatExchangeResult 按攻击顺序播放成独立复古战斗演出。
/// 时间线完全由 Godot Timer 驱动，不依赖 _Process；玩家主动攻击优先立刻播放，
/// 敌军攻击则先等待地图移动动画结束，保证视觉顺序不会被同步结算打乱。
/// </summary>
public partial class RetroBattleAnimationCoordinator : Node
{
    /// <summary>等待播放的战斗交换队列。</summary>
    private readonly Queue<CombatExchangeResult> _queue = new();

    /// <summary>地图人物表现协调器，用于等待敌军地图移动完成。</summary>
    private CharacterVisualCoordinator? _visualCoordinator;

    /// <summary>全屏输入拦截层。</summary>
    private Control? _blocker;

    /// <summary>左侧战斗人物。</summary>
    private AnimatedBattleCharacterControl? _leftCharacter;

    /// <summary>右侧战斗人物。</summary>
    private AnimatedBattleCharacterControl? _rightCharacter;

    /// <summary>左侧人物名称与装备。</summary>
    private Label? _leftLabel;

    /// <summary>右侧人物名称与装备。</summary>
    private Label? _rightLabel;

    /// <summary>中央逐击结果文本。</summary>
    private Label? _resultLabel;

    /// <summary>命中、闪避、必杀和魔法的程序特效层。</summary>
    private RetroBattleEffectControl? _effectControl;

    /// <summary>驱动 Intro/Windup/Impact/Recovery/Outro 的阶段计时器。</summary>
    private Timer? _phaseTimer;

    /// <summary>整场战斗安全超时。</summary>
    private Timer? _watchdogTimer;

    /// <summary>等待地图移动结束的轮询计时器。</summary>
    private Timer? _movementGateTimer;

    /// <summary>当前正在播放的完整交换。</summary>
    private CombatExchangeResult? _currentExchange;

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
    /// 收到真实战斗结果后入队。
    /// 玩家主动攻击必须优先立刻打开演出，避免后续自动敌军回合抢先移动；
    /// 敌军攻击则先经过地图移动 gate。
    /// </summary>
    private void OnExchangeResolved(CombatExchangeResult exchange)
    {
        if (exchange.Strikes.Count == 0)
        {
            return;
        }

        _queue.Enqueue(exchange);
        RequestStartForQueueHead();
    }

    /// <summary>根据队首攻击方决定立即播放还是先等地图移动。</summary>
    private void RequestStartForQueueHead()
    {
        if (_currentExchange is not null || _queue.Count == 0)
        {
            return;
        }

        CombatExchangeResult next = _queue.Peek();
        bool playerInitiated = next.Strikes.Count > 0 &&
                               next.Strikes[0].Attacker.Team == UnitTeam.Player;
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
        if (_currentExchange is not null || _queue.Count == 0 || _movementGateTimer is null)
        {
            return;
        }

        if (_movementGateTimer.IsStopped())
        {
            _movementGateTimer.Start(MovementGateIntervalSeconds);
        }
    }

    /// <summary>地图仍在移动时继续等；全部到达后才打开敌军横向战斗界面。</summary>
    private void CheckMovementAndStartExchange()
    {
        if (_currentExchange is not null || _queue.Count == 0)
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
        if (_currentExchange is not null || _queue.Count == 0)
        {
            return;
        }

        CombatExchangeResult exchange = _queue.Dequeue();
        if (exchange.Strikes.Count == 0)
        {
            RequestStartForQueueHead();
            return;
        }

        _movementGateTimer?.Stop();
        _currentExchange = exchange;
        _strikeIndex = 0;
        _leftUnit = exchange.Strikes[0].Attacker;
        _rightUnit = exchange.Strikes[0].Defender;

        // 先标记全局表现状态，再显示遮罩；地图移动层可以据此暂停后台动画。
        BattleAnimationBus.BeginPlayback();
        _blocker?.Show();
        _leftCharacter?.SetUnit(_leftUnit);
        _rightCharacter?.SetUnit(_rightUnit);
        _leftCharacter?.Play(CharacterAnimationState.Idle);
        _rightCharacter?.Play(CharacterAnimationState.Idle);
        _effectControl?.Clear();

        if (_leftLabel is not null)
        {
            _leftLabel.Text = $"{_leftUnit.DisplayName}\n{_leftUnit.EquippedWeapon.DisplayName}";
        }

        if (_rightLabel is not null)
        {
            _rightLabel.Text = $"{_rightUnit.DisplayName}\n{_rightUnit.EquippedWeapon.DisplayName}";
        }

        if (_resultLabel is not null)
        {
            _resultLabel.Text = "战斗开始";
        }

        double timeout = Math.Max(
            MinimumExchangeTimeoutSeconds,
            exchange.Strikes.Count * MaximumSecondsPerStrike + 2.0);
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
        if (_currentExchange is null)
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
            string costText = strike.HpCostPaid > 0 ? $" / 消耗 {strike.HpCostPaid} HP" : string.Empty;
            _resultLabel.Text = $"{strike.Attacker.DisplayName} 使用 {strike.Attacker.EquippedWeapon.DisplayName}{costText}";
        }

        SchedulePhase(PlaybackPhase.Windup, CurrentWindupDuration());
    }

    /// <summary>显示命中、闪避、必杀或倒下结果。</summary>
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

        SchedulePhase(PlaybackPhase.Impact, 0.34);
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
        SchedulePhase(PlaybackPhase.Recovery, 0.20);
    }

    /// <summary>进入下一次反击/追击，或进入整场收尾。</summary>
    private void AdvanceStrikeOrFinish()
    {
        if (_currentExchange is null)
        {
            EmergencyFinishExchange("恢复阶段失去当前战斗交换");
            return;
        }

        _strikeIndex++;
        if (_strikeIndex < _currentExchange.Strikes.Count)
        {
            StartCurrentStrike();
            return;
        }

        _effectControl?.Clear();
        if (_resultLabel is not null)
        {
            _resultLabel.Text = "战斗结束";
        }

        SchedulePhase(PlaybackPhase.Outro, 0.42);
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
        _leftCharacter?.Play(CharacterAnimationState.Idle);
        _rightCharacter?.Play(CharacterAnimationState.Idle);
        _currentExchange = null;
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
        if (_currentExchange is null ||
            _strikeIndex < 0 ||
            _strikeIndex >= _currentExchange.Strikes.Count)
        {
            return null;
        }

        return _currentExchange.Strikes[_strikeIndex];
    }

    /// <summary>魔法蓄力略长于物理攻击。</summary>
    private double CurrentWindupDuration()
    {
        CombatStrikeResult? strike = CurrentStrike();
        if (strike is null)
        {
            return 0.30;
        }

        return strike.Attacker.EquippedWeapon.DamageType == DamageType.Magical ? 0.48 : 0.32;
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

    /// <summary>创建全屏战斗演出 UI。</summary>
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
            Color = new Color(0.02f, 0.025f, 0.04f, 0.88f),
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

        Control stage = new()
        {
            CustomMinimumSize = new Vector2(1100, 560),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        battlePanel.AddChild(stage);

        _leftCharacter = new AnimatedBattleCharacterControl
        {
            Position = new Vector2(55, 95),
            Size = new Vector2(390, 370),
            MirrorHorizontally = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        stage.AddChild(_leftCharacter);

        _rightCharacter = new AnimatedBattleCharacterControl
        {
            Position = new Vector2(655, 95),
            Size = new Vector2(390, 370),
            MirrorHorizontally = true,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        stage.AddChild(_rightCharacter);

        _leftLabel = new Label
        {
            Position = new Vector2(55, 28),
            Size = new Vector2(390, 58),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        stage.AddChild(_leftLabel);

        _rightLabel = new Label
        {
            Position = new Vector2(655, 28),
            Size = new Vector2(390, 58),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        stage.AddChild(_rightLabel);

        Label versus = new()
        {
            Text = "VS",
            Position = new Vector2(500, 40),
            Size = new Vector2(100, 45),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        stage.AddChild(versus);

        _resultLabel = new Label
        {
            Position = new Vector2(360, 475),
            Size = new Vector2(380, 65),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        stage.AddChild(_resultLabel);

        _effectControl = new RetroBattleEffectControl
        {
            Position = Vector2.Zero,
            Size = new Vector2(1100, 560),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        stage.AddChild(_effectControl);

        Button skipButton = new()
        {
            Text = "跳过全部演出",
            Position = new Vector2(945, 510),
            Size = new Vector2(140, 38),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        skipButton.Pressed += SkipAllBattleAnimations;
        stage.AddChild(skipButton);
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
