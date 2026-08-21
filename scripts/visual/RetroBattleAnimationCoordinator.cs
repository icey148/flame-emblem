using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 把真实 CombatExchangeResult 按攻击顺序播放成独立复古战斗演出。
/// 战斗画面采用已经确认的唯一规格：纯黑背景、敌军固定左侧、我方固定右侧、
/// 双状态框与中央攻击提示框；本类只负责表现时间线，不改变任何战斗结算规则。
/// </summary>
public partial class RetroBattleAnimationCoordinator : Node
{
    /// <summary>等待播放的战斗交换及其表现快照。</summary>
    private readonly Queue<QueuedBattlePresentation> _queue = new();

    /// <summary>地图人物表现协调器，用于等待敌军地图移动完成。</summary>
    private CharacterVisualCoordinator? _visualCoordinator;

    /// <summary>全屏输入拦截层。</summary>
    private Control? _blocker;

    /// <summary>左侧战斗人物；标准布局中优先固定为敌军。</summary>
    private AnimatedBattleCharacterControl? _leftCharacter;

    /// <summary>右侧战斗人物；标准布局中优先固定为我方。</summary>
    private AnimatedBattleCharacterControl? _rightCharacter;

    /// <summary>
    /// 兼容旧表现协调器保留的左侧姓名标签。
    /// 正式规格已经把姓名放进底部状态框，因此该标签默认隐藏。
    /// </summary>
    private Label? _leftLabel;

    /// <summary>兼容旧表现协调器保留的右侧姓名标签。</summary>
    private Label? _rightLabel;

    /// <summary>中央攻击/命中/经验提示文字。</summary>
    private Label? _resultLabel;

    /// <summary>双方 HP、HIT、ATC、DEF 的底部状态框。</summary>
    private RetroBattleStatusHudControl? _statusHud;

    /// <summary>命中、闪避、必杀和武器轨迹程序特效层。</summary>
    private RetroBattleEffectControl? _effectControl;

    /// <summary>驱动 Intro/Windup/Impact/Recovery/Outro 的阶段计时器。</summary>
    private Timer? _phaseTimer;

    /// <summary>整场战斗安全超时。</summary>
    private Timer? _watchdogTimer;

    /// <summary>等待地图移动结束的轮询计时器。</summary>
    private Timer? _movementGateTimer;

    /// <summary>当前正在播放的表现快照。</summary>
    private QueuedBattlePresentation? _currentPresentation;

    /// <summary>当前交换画面左侧固定人物。</summary>
    private UnitModel? _leftUnit;

    /// <summary>当前交换画面右侧固定人物。</summary>
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

    /// <summary>创建 Timer 时间线和正式规格战斗 UI。</summary>
    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _visualCoordinator = GetNodeOrNull<CharacterVisualCoordinator>("../CharacterVisualCoordinator");
        CreateTimers();
        CreateBattleOverlay();
    }

    /// <summary>收到真实战斗结果后保存表现所需快照并入队。</summary>
    private void OnExchangeResolved(CombatExchangeResult exchange)
    {
        if (exchange.Strikes.Count == 0)
        {
            return;
        }

        UnitModel? experienceUnit = exchange.Strikes
            .Select(strike => strike.Attacker)
            .FirstOrDefault(unit => unit.Team == UnitTeam.Player);

        QueuedBattlePresentation presentation = new(
            exchange,
            exchange.InitiatingAttacker.MaxHp,
            exchange.InitiatingDefender.MaxHp,
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

    /// <summary>敌军战斗至少延后一个短计时周期，让地图人物移动先完成。</summary>
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

    /// <summary>地图仍在移动时继续等待；全部到达后才打开战斗界面。</summary>
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

    /// <summary>从队列取出下一场战斗并按“敌左我右”固定双方。</summary>
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
        AssignScreenSides(exchange);

        if (_leftUnit is null || _rightUnit is null)
        {
            EmergencyFinishExchange("无法确定战斗双方画面位置");
            return;
        }

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

        bool attackerOnLeft = ReferenceEquals(_leftUnit, exchange.InitiatingAttacker);
        int leftHpBefore = attackerOnLeft
            ? exchange.InitiatingAttackerHpBefore
            : exchange.InitiatingDefenderHpBefore;
        int rightHpBefore = attackerOnLeft
            ? exchange.InitiatingDefenderHpBefore
            : exchange.InitiatingAttackerHpBefore;
        int leftMaxHpBefore = attackerOnLeft
            ? presentation.AttackerMaxHpBefore
            : presentation.DefenderMaxHpBefore;
        int rightMaxHpBefore = attackerOnLeft
            ? presentation.DefenderMaxHpBefore
            : presentation.AttackerMaxHpBefore;

        _statusHud?.BeginBattle(
            _leftUnit,
            leftHpBefore,
            leftMaxHpBefore,
            _rightUnit,
            rightHpBefore,
            rightMaxHpBefore,
            presentation.ExperienceUnit,
            presentation.ExperienceLevelBefore,
            presentation.ExperienceBefore);

        double timeout = Math.Max(
            MinimumExchangeTimeoutSeconds,
            exchange.Strikes.Count * MaximumSecondsPerStrike + 3.0);
        _watchdogTimer?.Start(timeout);
        SchedulePhase(PlaybackPhase.Intro, 0.24);
    }

    /// <summary>
    /// 正式规格固定敌军在左、我方在右。
    /// 如果未来出现非敌我战斗，则回退到攻击方左、防守方右，避免表现层阻断规则测试。
    /// </summary>
    private void AssignScreenSides(CombatExchangeResult exchange)
    {
        UnitModel attacker = exchange.InitiatingAttacker;
        UnitModel defender = exchange.InitiatingDefender;

        if (attacker.Team == UnitTeam.Enemy && defender.Team == UnitTeam.Player)
        {
            _leftUnit = attacker;
            _rightUnit = defender;
            return;
        }

        if (attacker.Team == UnitTeam.Player && defender.Team == UnitTeam.Enemy)
        {
            _leftUnit = defender;
            _rightUnit = attacker;
            return;
        }

        _leftUnit = attacker;
        _rightUnit = defender;
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

    /// <summary>开始当前一击，并把中央提示改成“某某的攻击”。</summary>
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
            _resultLabel.Text = $"{strike.Attacker.DisplayName}的攻击{costText}";
        }

        SchedulePhase(PlaybackPhase.Windup, CurrentWindupDuration());
    }

    /// <summary>显示命中、闪避、必杀或倒下结果，并同步推进 HP。</summary>
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
        _statusHud?.ApplyStrike(strike);

        if (!strike.Hit)
        {
            defenderControl?.Play(CharacterAnimationState.Dodge);
            _effectControl?.Play(RetroBattleEffectKind.Dodge, IsLeftUnit(strike.Attacker));
            if (_resultLabel is not null)
            {
                _resultLabel.Text = "未命中";
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
                _resultLabel.Text = $"{criticalText}造成 {strike.Damage} 伤害{defeatText}";
            }
        }

        SchedulePhase(PlaybackPhase.Impact, 0.40);
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

        double outroDuration = _currentPresentation.ExperienceUnit is null ? 0.52 : 1.00;
        SchedulePhase(PlaybackPhase.Outro, outroDuration);
    }

    /// <summary>正常结束当前演出并准备队列下一场。</summary>
    private void FinishExchange()
    {
        ResetCurrentExchangeState();
        RequestStartForQueueHead();
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
    /// 创建正式规格的全屏战斗界面。
    /// 所有尺寸都直接读取 ReferenceBattleLayout，避免第一帧先出现旧尺寸再被表现协调器纠正。
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
            Size = ReferenceBattleLayout.ViewportSize,
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = false
        };
        layer.AddChild(_blocker);

        ColorRect backdrop = new()
        {
            Position = Vector2.Zero,
            Size = ReferenceBattleLayout.ViewportSize,
            Color = Colors.Black,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _blocker.AddChild(backdrop);

        Control stage = new()
        {
            Name = "ReferenceBattleStage",
            Position = Vector2.Zero,
            Size = ReferenceBattleLayout.ViewportSize,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest
        };
        _blocker.AddChild(stage);

        // 敌军固定左侧、我方固定右侧；正式位置仍由最终人物表现层做 3× 像素补偿。
        _leftCharacter = new AnimatedBattleCharacterControl
        {
            Position = ReferenceBattleLayout.LeftCharacterPosition,
            Size = ReferenceBattleLayout.CharacterControlSize,
            MirrorHorizontally = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest
        };
        stage.AddChild(_leftCharacter);

        _rightCharacter = new AnimatedBattleCharacterControl
        {
            Position = ReferenceBattleLayout.RightCharacterPosition,
            Size = ReferenceBattleLayout.CharacterControlSize,
            MirrorHorizontally = true,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest
        };
        stage.AddChild(_rightCharacter);

        // 旧阵营同步层仍通过反射读取两个姓名字段，因此保留不可见兼容标签。
        _leftLabel = new Label
        {
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        stage.AddChild(_leftLabel);

        _rightLabel = new Label
        {
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        stage.AddChild(_rightLabel);

        _effectControl = new RetroBattleEffectControl
        {
            Position = Vector2.Zero,
            Size = new Vector2(
                ReferenceBattleLayout.ViewportSize.X,
                ReferenceBattleLayout.EffectRegionHeight),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = true,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest
        };
        stage.AddChild(_effectControl);

        _statusHud = new RetroBattleStatusHudControl
        {
            Position = ReferenceBattleLayout.StatusHudPosition,
            Size = ReferenceBattleLayout.StatusHudSize,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest
        };
        stage.AddChild(_statusHud);

        // 中央信息框覆盖两边身份区下沿，和确认参考图使用同一构图关系。
        PanelContainer resultPanel = new()
        {
            Position = ReferenceBattleLayout.ResultPanelPosition,
            Size = ReferenceBattleLayout.ResultPanelSize,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        resultPanel.AddThemeStyleboxOverride("panel", BuildReferencePanelStyle());
        stage.AddChild(resultPanel);

        _resultLabel = new Label
        {
            Text = "战斗开始",
            CustomMinimumSize = ReferenceBattleLayout.ResultLabelMinimumSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _resultLabel.AddThemeColorOverride("font_color", new Color("f3f3ef"));
        _resultLabel.AddThemeColorOverride("font_shadow_color", Colors.Black);
        _resultLabel.AddThemeConstantOverride("shadow_offset_x", 2);
        _resultLabel.AddThemeConstantOverride("shadow_offset_y", 2);
        _resultLabel.AddThemeFontSizeOverride("font_size", 28);
        _resultLabel.AddThemeFontOverride("font", BattlePixelFontCatalog.Font);
        resultPanel.AddChild(_resultLabel);
    }

    /// <summary>创建中央信息框使用的纯黑白硬边样式。</summary>
    private static StyleBoxFlat BuildReferencePanelStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = Colors.Black,
            BorderColor = new Color("f3f3ef"),
            BorderWidthLeft = 4,
            BorderWidthTop = 4,
            BorderWidthRight = 4,
            BorderWidthBottom = 4,
            CornerRadiusTopLeft = 0,
            CornerRadiusTopRight = 0,
            CornerRadiusBottomLeft = 0,
            CornerRadiusBottomRight = 0
        };
    }

    /// <summary>保存一场战斗在表现层开始播放前必须冻结的数值。</summary>
    private sealed class QueuedBattlePresentation
    {
        /// <summary>创建表现快照。</summary>
        public QueuedBattlePresentation(
            CombatExchangeResult exchange,
            int attackerMaxHpBefore,
            int defenderMaxHpBefore,
            UnitModel? experienceUnit,
            int experienceLevelBefore,
            int experienceBefore)
        {
            Exchange = exchange;
            AttackerMaxHpBefore = Math.Max(1, attackerMaxHpBefore);
            DefenderMaxHpBefore = Math.Max(1, defenderMaxHpBefore);
            ExperienceUnit = experienceUnit;
            ExperienceLevelBefore = experienceLevelBefore;
            ExperienceBefore = Math.Clamp(experienceBefore, 0, 99);
        }

        /// <summary>已经结算完成的真实战斗交换。</summary>
        public CombatExchangeResult Exchange { get; }

        /// <summary>原始攻击方开战时最大 HP。</summary>
        public int AttackerMaxHpBefore { get; }

        /// <summary>原始防守方开战时最大 HP。</summary>
        public int DefenderMaxHpBefore { get; }

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
