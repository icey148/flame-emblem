using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 把真实 CombatExchangeResult 按攻击顺序播放成独立复古战斗演出。
/// 时间线由 Godot Timer 驱动，而不是依赖 _Process；这样人物绘制或帧刷新出现异常时，
/// 主战斗演出仍然能够继续推进并最终释放输入遮罩。
/// </summary>
public partial class RetroBattleAnimationCoordinator : Node
{
    /// <summary>等待播放的战斗交换队列；敌军连续结算时按顺序保留。</summary>
    private readonly Queue<CombatExchangeResult> _queue = new();

    /// <summary>地图人物表现协调器；开始敌军战斗前先等待地图移动动画真正结束。</summary>
    private CharacterVisualCoordinator? _visualCoordinator;

    /// <summary>全屏输入拦截层；只有真正进入横向战斗演出后才显示。</summary>
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

    /// <summary>驱动 Intro/Windup/Impact/Recovery/Outro 的一次性阶段计时器。</summary>
    private Timer? _phaseTimer;

    /// <summary>独立于阶段计时器的整场战斗安全超时。</summary>
    private Timer? _watchdogTimer;

    /// <summary>
    /// 等待地图人物移动结束的短周期计时器。
    /// 第一次也会至少等待一次短计时，让 CharacterVisualCoordinator 有机会检测同帧发生的敌军格子变化。
    /// </summary>
    private Timer? _movementGateTimer;

    /// <summary>当前正在播放的完整交换。</summary>
    private CombatExchangeResult? _currentExchange;

    /// <summary>当前交换左侧固定人物；第一击主动方固定站左侧。</summary>
    private UnitModel? _leftUnit;

    /// <summary>当前交换右侧固定人物；第一击防守方固定站右侧。</summary>
    private UnitModel? _rightUnit;

    /// <summary>当前正在播放第几次实际攻击。</summary>
    private int _strikeIndex;

    /// <summary>当前演出阶段。</summary>
    private PlaybackPhase _phase = PlaybackPhase.Hidden;

    /// <summary>每一击允许的最大整场预算，用于生成安全超时。</summary>
    private const double MaximumSecondsPerStrike = 2.2;

    /// <summary>无论攻击次数多少，一场演出至少拥有的安全超时时间。</summary>
    private const double MinimumExchangeTimeoutSeconds = 6.0;

    /// <summary>地图移动检测轮询间隔；很短，只用于确保移动先于战斗窗口展示。</summary>
    private const double MovementGateIntervalSeconds = 0.05;

    /// <summary>
    /// 进入场景树时订阅真实战斗结算事件。
    /// </summary>
    public override void _EnterTree()
    {
        BattleAnimationBus.ExchangeResolved += OnExchangeResolved;
    }

    /// <summary>
    /// 离开场景树时解除静态事件订阅并释放任何可能存在的输入遮罩。
    /// </summary>
    public override void _ExitTree()
    {
        BattleAnimationBus.ExchangeResolved -= OnExchangeResolved;
        StopAllTimers();
        _blocker?.Hide();
    }

    /// <summary>
    /// 创建演出 UI 与独立 Timer 时间线。
    /// Timer 和协调器都使用 Always 处理模式，未来即使加入暂停菜单也不会把战斗演出冻结。
    /// </summary>
    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _visualCoordinator = GetNodeOrNull<CharacterVisualCoordinator>("../CharacterVisualCoordinator");
        CreateTimers();
        CreateBattleOverlay();
    }

    /// <summary>
    /// 收到真实战斗结果后加入队列。
    /// 不在事件回调里立即弹战斗窗口；先通过 movement gate 等至少一个引擎时刻，
    /// 让敌军地图位移能够被人物表现层检测并播放。
    /// </summary>
    private void OnExchangeResolved(CombatExchangeResult exchange)
    {
        if (exchange.Strikes.Count == 0)
        {
            return;
        }

        _queue.Enqueue(exchange);
        RequestStartAfterMovement();
    }

    /// <summary>
    /// 创建三个独立一次性 Timer：阶段、整场安全超时、地图移动等待。
    /// </summary>
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
    /// 请求开始下一场交换，但至少延后一个短计时周期。
    /// 这一步非常重要：敌军逻辑会在同一帧修改 GridPosition 并结算攻击，
    /// 延后后地图人物层才能先发现位置变化并创建逐格移动动画。
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

    /// <summary>
    /// 检查地图人物是否还在移动。
    /// 有移动时继续等待；全部到达后才显示横向战斗演出。
    /// </summary>
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

    /// <summary>
    /// 从队列取出下一场战斗并正式打开横向演出窗口。
    /// </summary>
    private void StartNextExchangeNow()
    {
        if (_currentExchange is not null || _queue.Count == 0)
        {
            return;
        }

        CombatExchangeResult exchange = _queue.Dequeue();
        if (exchange.Strikes.Count == 0)
        {
            RequestStartAfterMovement();
            return;
        }

        _currentExchange = exchange;
        _strikeIndex = 0;
        _leftUnit = exchange.Strikes[0].Attacker;
        _rightUnit = exchange.Strikes[0].Defender;

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

    /// <summary>
    /// 把状态切到指定阶段，并让阶段 Timer 在给定时间后推进。
    /// </summary>
    private void SchedulePhase(PlaybackPhase phase, double seconds)
    {
        _phase = phase;
        _phaseTimer?.Start(Math.Max(0.01, seconds));
    }

    /// <summary>
    /// Timer 到点后只根据当前阶段推进一次。
    /// 没有逐帧累加状态，因此不会再出现 _Process 停止后整场演出永久卡住的问题。
    /// </summary>
    private void AdvancePlaybackPhase()
    {
        if (_currentExchange is null)
        {
            ResetCurrentExchangeState();
            RequestStartAfterMovement();
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

    /// <summary>
    /// 开始当前一击的蓄力/挥击阶段。
    /// 物理攻击播放 Attack，魔法攻击播放 Cast。
    /// </summary>
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

    /// <summary>
    /// 进入这一击的结果阶段：未命中播放闪避，命中播放受击或倒下，必杀额外播放闪光。
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

    /// <summary>
    /// 结果展示完成后恢复攻击方待机；未被击倒的防守方也恢复待机。
    /// </summary>
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

    /// <summary>
    /// 当前一击结束后进入下一次反击/追击；全部攻击结束后进入收尾。
    /// </summary>
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

    /// <summary>
    /// 正常结束当前横向演出，回到地图并准备下一场排队战斗。
    /// </summary>
    private void FinishExchange()
    {
        ResetCurrentExchangeState();
        RequestStartAfterMovement();
    }

    /// <summary>
    /// 手动跳过当前以及排队中的全部演出。
    /// 战斗数值早已结算完成，因此跳过只影响视觉。
    /// </summary>
    private void SkipAllBattleAnimations()
    {
        _queue.Clear();
        ResetCurrentExchangeState();
    }

    /// <summary>
    /// 异常或安全超时时强制结束当前演出，并清空后续队列。
    /// </summary>
    private void EmergencyFinishExchange(string reason)
    {
        GD.PushWarning($"Battle animation aborted safely: {reason}");
        _queue.Clear();
        ResetCurrentExchangeState();
    }

    /// <summary>
    /// 统一释放当前演出状态与全屏遮罩。
    /// </summary>
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
    }

    /// <summary>停止协调器拥有的全部计时器。</summary>
    private void StopAllTimers()
    {
        _phaseTimer?.Stop();
        _watchdogTimer?.Stop();
        _movementGateTimer?.Stop();
    }

    /// <summary>
    /// 所有 Timer 和事件入口都通过这一层执行。
    /// 表现层异常只会终止演出，不能破坏已经完成的战斗数值结算。
    /// </summary>
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

    /// <summary>魔法施法需要略长蓄力时间，物理攻击保持短促。</summary>
    private double CurrentWindupDuration()
    {
        CombatStrikeResult? strike = CurrentStrike();
        if (strike is null)
        {
            return 0.30;
        }

        return strike.Attacker.EquippedWeapon.DamageType == DamageType.Magical ? 0.48 : 0.32;
    }

    /// <summary>判断某人物是否固定站在当前战斗画面的左侧。</summary>
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
    /// 创建全屏输入拦截、暗色背景、左右人物位、中央特效层和跳过按钮。
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
