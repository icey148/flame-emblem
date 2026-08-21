using FlameEmblem.Game;
using Godot;
using System.Reflection;

namespace FlameEmblem.Visual;

/// <summary>
/// 在不修改核心战斗 Timer 的前提下，为物理攻击启动剑斩、枪刺和箭矢飞行表现。
/// 新战斗布局固定敌军在左、我方在右，因此效果方向直接读取实际屏幕左侧单位，
/// 不再假设“主动攻击方永远站在左边”。
/// </summary>
public partial class BattleWeaponEffectCoordinator : Node
{
    /// <summary>现有横向战斗时间线协调器。</summary>
    private RetroBattleAnimationCoordinator? _battleCoordinator;

    /// <summary>读取当前表现快照的私有字段。</summary>
    private FieldInfo? _currentPresentationField;

    /// <summary>读取当前攻击索引的私有字段。</summary>
    private FieldInfo? _strikeIndexField;

    /// <summary>读取当前播放阶段的私有字段。</summary>
    private FieldInfo? _phaseField;

    /// <summary>读取现有特效控件的私有字段。</summary>
    private FieldInfo? _effectControlField;

    /// <summary>读取当前画面左侧固定单位。</summary>
    private FieldInfo? _leftUnitField;

    /// <summary>上一场已经启动物理轨迹的战斗交换。</summary>
    private CombatExchangeResult? _lastExchange;

    /// <summary>上一场已经启动物理轨迹的攻击索引。</summary>
    private int _lastStrikeIndex = -1;

    /// <summary>启动时缓存战斗表现字段；失败时安全停用，不影响实际战斗。</summary>
    public override void _Ready()
    {
        ProcessPriority = 140;
        _battleCoordinator = GetNodeOrNull<RetroBattleAnimationCoordinator>("../RetroBattleAnimationCoordinator");
        if (_battleCoordinator is null)
        {
            GD.PushWarning("BattleWeaponEffectCoordinator 找不到战斗演出协调器，武器轨迹不会启动。");
            SetProcess(false);
            return;
        }

        BindingFlags members = BindingFlags.Instance | BindingFlags.NonPublic;
        Type coordinatorType = typeof(RetroBattleAnimationCoordinator);
        _currentPresentationField = coordinatorType.GetField("_currentPresentation", members);
        _strikeIndexField = coordinatorType.GetField("_strikeIndex", members);
        _phaseField = coordinatorType.GetField("_phase", members);
        _effectControlField = coordinatorType.GetField("_effectControl", members);
        _leftUnitField = coordinatorType.GetField("_leftUnit", members);

        if (_currentPresentationField is null ||
            _strikeIndexField is null ||
            _phaseField is null ||
            _effectControlField is null ||
            _leftUnitField is null)
        {
            GD.PushWarning("BattleWeaponEffectCoordinator 无法读取战斗表现字段，武器轨迹不会启动。");
            SetProcess(false);
        }
    }

    /// <summary>
    /// 每帧只在 Windup 阶段检查一次当前攻击。
    /// 同一个 exchange/strikeIndex 只触发一次，避免每帧把飞行动画重新从起点播放。
    /// </summary>
    public override void _Process(double delta)
    {
        if (_battleCoordinator is null ||
            (_phaseField?.GetValue(_battleCoordinator)?.ToString() ?? string.Empty) != "Windup")
        {
            return;
        }

        CombatExchangeResult? exchange = ReadCurrentExchange();
        int strikeIndex = _strikeIndexField?.GetValue(_battleCoordinator) is int value ? value : -1;
        if (exchange is null || strikeIndex < 0 || strikeIndex >= exchange.Strikes.Count)
        {
            return;
        }

        if (ReferenceEquals(exchange, _lastExchange) && strikeIndex == _lastStrikeIndex)
        {
            return;
        }

        _lastExchange = exchange;
        _lastStrikeIndex = strikeIndex;

        CombatStrikeResult strike = exchange.Strikes[strikeIndex];
        bool magical = strike.Attacker.EquippedWeapon.DamageType == DamageType.Magical;

        // 魔法已经由核心时间线启动；这里仅补物理武器轨迹，避免重复重启魔法表现。
        if (magical)
        {
            return;
        }

        if (_effectControlField?.GetValue(_battleCoordinator) is not RetroBattleEffectControl effectControl)
        {
            return;
        }

        CharacterAppearanceDefinition appearance = CharacterAppearanceCatalog.Get(strike.Attacker);
        UnitModel? leftUnit = _leftUnitField?.GetValue(_battleCoordinator) as UnitModel;

        // 效果方向只由当前实际屏幕位置决定：左侧攻击向右，右侧攻击向左。
        bool leftToRight = leftUnit is not null && ReferenceEquals(strike.Attacker, leftUnit);
        effectControl.PlayWeaponWindup(appearance.WeaponSilhouette, false, leftToRight);
    }

    /// <summary>从私有表现快照读取公开的 CombatExchangeResult。</summary>
    private CombatExchangeResult? ReadCurrentExchange()
    {
        if (_battleCoordinator is null || _currentPresentationField is null)
        {
            return null;
        }

        object? presentation = _currentPresentationField.GetValue(_battleCoordinator);
        if (presentation is null)
        {
            return null;
        }

        PropertyInfo? exchangeProperty = presentation.GetType().GetProperty(
            "Exchange",
            BindingFlags.Instance | BindingFlags.Public);
        return exchangeProperty?.GetValue(presentation) as CombatExchangeResult;
    }
}