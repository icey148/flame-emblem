using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 把真实 CombatExchangeResult 按攻击顺序播放成独立复古战斗演出。
/// 主动攻击、反击、追击、命中、闪避、必杀、魔法和击倒均直接读取已经结算好的结果，不重新计算概率。
/// 演出层带有超时、异常隔离和手动跳过保护，任何表现层错误都不能永久锁住战棋地图输入。
/// </summary>
public partial class RetroBattleAnimationCoordinator : Node
{
    /// <summary>等待播放的战斗交换队列；敌军连续战斗时会依次播放。</summary>
    private readonly Queue<CombatExchangeResult> _queue = new();

    /// <summary>全屏输入拦截层；演出期间阻止玩家继续点击战场。</summary>
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

    /// <summary>当前正在播放的完整交换。</summary>
    private CombatExchangeResult? _currentExchange;

    /// <summary>当前交换左侧固定人物；第一击主动方始终放在左侧。</summary>
    private UnitModel? _leftUnit;

    /// <summary>当前交换右侧固定人物；第一击防守方始终放在右侧。</summary>
    private UnitModel? _rightUnit;

    /// <summary>当前正在播放第几次实际攻击。</summary>
    private int _strikeIndex;

    /// <summary>当前演出阶段。</summary>
    private PlaybackPhase _phase = PlaybackPhase.Hidden;

    /// <summary>当前阶段已经持续的秒数。</summary>
    private float _phaseElapsed;

    /// <summary>当前整场战斗已经播放的秒数，用于最终安全超时。</summary>
    private float _exchangeElapsed;

    /// <summary>单个演出阶段允许停留的最大时间；正常阶段全部远短于该值。</summary>
    private const float MaximumPhaseSeconds = 2.0f;

    /// <summary>每一击额外允许的总演出时间，用于计算整场战斗的动态上限。</summary>
    private const float MaximumSecondsPerStrike = 2.2f;

    /// <summary>没有攻击数量可参考时仍至少允许的整场演出时间。</summary>
    private const float MinimumExchangeTimeoutSeconds = 6.0f;

    /// <summary>
    /// 进入场景树时订阅战斗结算事件。
    /// </summary>
    public override void _EnterTree()
    {
        BattleAnimationBus.ExchangeResolved += OnExchangeResolved;
    }

    /// <summary>
    /// 离开场景树时解除静态事件订阅并确保输入拦截层被隐藏。
    /// </summary>
    public override void _ExitTree()
    {
        BattleAnimationBus.ExchangeResolved -= OnExchangeResolved;
        _blocker?.Hide();
    }

    /// <summary>
    /// 创建独立战斗演出 UI，并强制本协调器在场景暂停时也继续推进。
    /// 这样以后即使加入暂停菜单或剧情暂停，也不会出现战斗遮罩永久停住的情况。
    /// </summary>
    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        SetProcess(true);
        CreateBattleOverlay();
    }

    /// <summary>
    /// 按阶段推进当前战斗动画。
    /// 所有演出状态切换都放在异常保护中，表现层错误只会终止动画，不会影响已经完成的战斗结算。
    /// </summary>
    public override void _Process(double delta)
    {
        try
        {
            if (_currentExchange is null)
            {
                TryStartNextExchange();
                return;
            }

            float frameDelta = Math.Max(0.0f, (float)delta);
            _phaseElapsed += frameDelta;
            _exchangeElapsed += frameDelta;

            // 正常一击不到 1.2 秒；动态总上限只是最后保险，不参与正常节奏。
            float exchangeTimeout = Math.Max(
                MinimumExchangeTimeoutSeconds,
                _currentExchange.Strikes.Count * MaximumSecondsPerStrike + 2.0f);
            if (_exchangeElapsed >= exchangeTimeout)
            {
                EmergencyFinishExchange($"战斗演出超过 {exchangeTimeout:0.0} 秒安全上限");
                return;
            }

            // 任一阶段单独停留超过两秒也视为状态机异常，避免总超时前长时间黑屏。
            if (_phase != PlaybackPhase.Hidden && _phaseElapsed >= MaximumPhaseSeconds)
            {
                EmergencyFinishExchange($"战斗演出阶段 {_phase} 未能按时推进");
                return;
            }

            switch (_phase)
            {
                case PlaybackPhase.Intro when _phaseElapsed >= 0.28f:
                    StartCurrentStrike();
                    break;
                case PlaybackPhase.Windup when _phaseElapsed >= CurrentWindupDuration():
                    StartImpact();
                    break;
                case PlaybackPhase.Impact when _phaseElapsed >= 0.34f:
                    StartRecovery();
                    break;
                case PlaybackPhase.Recovery when _phaseElapsed >= 0.20f:
                    AdvanceStrikeOrFinish();
                    break;
                case PlaybackPhase.Outro when _phaseElapsed >= 0.42f:
                    FinishExchange();
                    break;
                case PlaybackPhase.Hidden:
                    // 有交换却进入 Hidden 说明内部状态不一致，直接释放遮罩比永久卡住更安全。
                    EmergencyFinishExchange("存在战斗交换但演出阶段意外进入 Hidden");
                    break;
            }
        }
        catch (Exception exception)
        {
            // 战斗数值已经在 CombatResolver 中结算完成，因此这里出现表现异常时只需要释放 UI。
            GD.PushError($"Battle animation failed and was safely aborted: {exception}");
            EmergencyFinishExchange("战斗演出发生运行时异常");
        }
    }

    /// <summary>
    /// 收到真实战斗结果后加入播放队列。
    /// 战斗逻辑可能在同一帧连续结算多场敌军攻击，因此不能直接覆盖当前演出。
    /// </summary>
    private void OnExchangeResolved(CombatExchangeResult exchange)
    {
        if (exchange.Strikes.Count == 0)
        {
            return;
        }

        _queue.Enqueue(exchange);
        TryStartNextExchange();
    }

    /// <summary>
    /// 创建全屏输入拦截、暗色背景、左右人物位、中央特效层和紧急跳过按钮。
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

    /// <summary>
    /// 没有正在播放的交换时，从队列取下一场战斗。
    /// </summary>
    private void TryStartNextExchange()
    {
        if (_currentExchange is not null || _queue.Count == 0)
        {
            return;
        }

        CombatExchangeResult exchange = _queue.Dequeue();
        if (exchange.Strikes.Count == 0)
        {
            return;
        }

        _currentExchange = exchange;
        _strikeIndex = 0;
        _leftUnit = exchange.Strikes[0].Attacker;
        _rightUnit = exchange.Strikes[0].Defender;
        _phase = PlaybackPhase.Intro;
        _phaseElapsed = 0.0f;
        _exchangeElapsed = 0.0f;

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
            FinishExchange();
            return;
        }

        _phase = PlaybackPhase.Windup;
        _phaseElapsed = 0.0f;
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
    }

    /// <summary>
    /// 进入这一击的结果阶段：未命中播放闪避，命中播放受击或倒下，必杀额外播放闪光。
    /// </summary>
    private void StartImpact()
    {
        CombatStrikeResult? strike = CurrentStrike();
        if (strike is null)
        {
            FinishExchange();
            return;
        }

        _phase = PlaybackPhase.Impact;
        _phaseElapsed = 0.0f;
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

            return;
        }

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

    /// <summary>
    /// 结果展示完成后恢复攻击方待机；未被击倒的防守方也恢复待机。
    /// </summary>
    private void StartRecovery()
    {
        CombatStrikeResult? strike = CurrentStrike();
        if (strike is null)
        {
            FinishExchange();
            return;
        }

        _phase = PlaybackPhase.Recovery;
        _phaseElapsed = 0.0f;
        ControlFor(strike.Attacker)?.Play(CharacterAnimationState.Idle);

        if (!strike.DefenderDefeated)
        {
            ControlFor(strike.Defender)?.Play(CharacterAnimationState.Idle);
        }

        _effectControl?.Clear();
    }

    /// <summary>
    /// 当前一击结束后进入下一次反击/追击；全部攻击播放完成则进入收尾。
    /// </summary>
    private void AdvanceStrikeOrFinish()
    {
        if (_currentExchange is null)
        {
            FinishExchange();
            return;
        }

        _strikeIndex++;
        if (_strikeIndex < _currentExchange.Strikes.Count)
        {
            StartCurrentStrike();
            return;
        }

        _phase = PlaybackPhase.Outro;
        _phaseElapsed = 0.0f;
        _effectControl?.Clear();
        if (_resultLabel is not null)
        {
            _resultLabel.Text = "战斗结束";
        }
    }

    /// <summary>
    /// 隐藏当前战斗演出并尝试继续播放队列中的下一场交换。
    /// </summary>
    private void FinishExchange()
    {
        ResetCurrentExchangeState();
        TryStartNextExchange();
    }

    /// <summary>
    /// 手动跳过当前以及排队中的全部演出。
    /// 战斗数值在进入演出前已经结算完成，所以跳过只影响视觉，不会改变游戏结果。
    /// </summary>
    private void SkipAllBattleAnimations()
    {
        _queue.Clear();
        ResetCurrentExchangeState();
    }

    /// <summary>
    /// 异常或超时时安全结束演出，并清空后续队列，防止相同表现错误连续锁住玩家。
    /// </summary>
    private void EmergencyFinishExchange(string reason)
    {
        GD.PushWarning($"Battle animation aborted safely: {reason}");
        _queue.Clear();
        ResetCurrentExchangeState();
    }

    /// <summary>
    /// 统一释放当前演出状态和全屏输入遮罩。
    /// 所有正常结束、手动跳过和异常退出都必须走这里，避免某条分支忘记 Hide blocker。
    /// </summary>
    private void ResetCurrentExchangeState()
    {
        _blocker?.Hide();
        _effectControl?.Clear();
        _leftCharacter?.Play(CharacterAnimationState.Idle);
        _rightCharacter?.Play(CharacterAnimationState.Idle);
        _currentExchange = null;
        _leftUnit = null;
        _rightUnit = null;
        _strikeIndex = 0;
        _phase = PlaybackPhase.Hidden;
        _phaseElapsed = 0.0f;
        _exchangeElapsed = 0.0f;
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

    /// <summary>
    /// 魔法施法需要略长蓄力时间，物理攻击保持短促。
    /// </summary>
    private float CurrentWindupDuration()
    {
        CombatStrikeResult? strike = CurrentStrike();
        if (strike is null)
        {
            return 0.30f;
        }

        return strike.Attacker.EquippedWeapon.DamageType == DamageType.Magical ? 0.48f : 0.32f;
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
