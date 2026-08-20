using Godot;

namespace FlameEmblem.Game;

/// <summary>
/// 保存一次战斗预测需要展示的核心数值。
/// 预测展示单次伤害、命中、必杀与最大攻击次数；真正是否命中由实际结算阶段随机判定。
/// </summary>
public sealed class CombatForecast
{
    /// <summary>
    /// 创建战斗预测结果。
    /// </summary>
    public CombatForecast(
        int attackerDamage,
        int attackerHitRate,
        int attackerCriticalRate,
        int attackerStrikeCount,
        bool defenderCanCounter,
        int defenderCounterDamage,
        int defenderHitRate,
        int defenderCriticalRate,
        int defenderStrikeCount)
    {
        AttackerDamage = attackerDamage;
        AttackerHitRate = attackerHitRate;
        AttackerCriticalRate = attackerCriticalRate;
        AttackerStrikeCount = attackerStrikeCount;
        DefenderCanCounter = defenderCanCounter;
        DefenderCounterDamage = defenderCounterDamage;
        DefenderHitRate = defenderHitRate;
        DefenderCriticalRate = defenderCriticalRate;
        DefenderStrikeCount = defenderStrikeCount;
    }

    /// <summary>攻击方单次普通命中的基础伤害。</summary>
    public int AttackerDamage { get; }

    /// <summary>攻击方命中率，范围 0~100。</summary>
    public int AttackerHitRate { get; }

    /// <summary>攻击方必杀率，范围 0~100。</summary>
    public int AttackerCriticalRate { get; }

    /// <summary>攻击方在当前状态下最多可进行的攻击次数。</summary>
    public int AttackerStrikeCount { get; }

    /// <summary>防守方当前是否具有反击条件。</summary>
    public bool DefenderCanCounter { get; }

    /// <summary>防守方反击时单次普通命中的基础伤害。</summary>
    public int DefenderCounterDamage { get; }

    /// <summary>防守方反击命中率。</summary>
    public int DefenderHitRate { get; }

    /// <summary>防守方反击必杀率。</summary>
    public int DefenderCriticalRate { get; }

    /// <summary>防守方在速度足够高时最多可反击的次数。</summary>
    public int DefenderStrikeCount { get; }
}

/// <summary>
/// 集中管理战斗公式，避免伤害、命中、必杀、速度追击与经验规则散落在 UI 代码中。
/// </summary>
public static class CombatRules
{
    /// <summary>速度至少领先该数值时获得一次追击。</summary>
    public const int FollowUpSpeedThreshold = 4;

    /// <summary>
    /// 计算两个格子之间的曼哈顿距离。
    /// 战棋只允许上下左右移动，因此该距离适合基础移动和攻击判定。
    /// </summary>
    public static int GridDistance(Vector2I from, Vector2I to)
    {
        return Mathf.Abs(from.X - to.X) + Mathf.Abs(from.Y - to.Y);
    }

    /// <summary>
    /// 判断攻击者当前位置是否位于当前装备的攻击距离内。
    /// </summary>
    public static bool IsInAttackRange(UnitModel attacker, UnitModel target)
    {
        int distance = GridDistance(attacker.GridPosition, target.GridPosition);
        return distance >= attacker.MinAttackRange && distance <= attacker.MaxAttackRange;
    }

    /// <summary>
    /// 判断单位当前是否能够实际发动一次攻击。
    /// 除射程外还会检查双方存活状态以及法术 HP 是否足够。
    /// </summary>
    public static bool CanAttack(UnitModel attacker, UnitModel target)
    {
        return attacker.IsAlive &&
               target.IsAlive &&
               attacker.CanUseEquippedWeapon &&
               IsInAttackRange(attacker, target);
    }

    /// <summary>
    /// 根据攻击类型计算一次普通命中的基础伤害。
    /// 物理攻击使用力量对防御，并受到地形防御加成；魔法使用魔力对魔防，当前不吃物理地形防御。
    /// </summary>
    public static int CalculateDamage(UnitModel attacker, UnitModel defender, int defenderTerrainDefenseBonus)
    {
        int offense;
        int defense;

        if (attacker.EquippedWeapon.DamageType == DamageType.Magical)
        {
            offense = attacker.Magic;
            defense = defender.Resistance;
        }
        else
        {
            offense = attacker.Strength;
            defense = defender.Defense + Mathf.Max(0, defenderTerrainDefenseBonus);
        }

        int rawDamage = offense + attacker.EquippedWeapon.Might - defense;

        // 正式公式允许 0 伤害；高防单位可以真正挡住低攻击武器，而不是强制掉 1 HP。
        return Mathf.Max(0, rawDamage);
    }

    /// <summary>
    /// 计算命中率。
    /// 攻击侧使用武器命中、技巧与幸运；防守侧使用速度、幸运以及地形回避。
    /// </summary>
    public static int CalculateHitRate(UnitModel attacker, UnitModel defender, int defenderTerrainAvoidBonus)
    {
        int attackHit = attacker.EquippedWeapon.Hit + attacker.Skill * 2 + attacker.Luck / 2;
        int defenderAvoid = defender.Speed * 2 + defender.Luck + Mathf.Max(0, defenderTerrainAvoidBonus);
        return Mathf.Clamp(attackHit - defenderAvoid, 0, 100);
    }

    /// <summary>
    /// 计算必杀率。
    /// 当前公式由武器基础必杀与技巧提高，并由目标幸运降低。
    /// </summary>
    public static int CalculateCriticalRate(UnitModel attacker, UnitModel defender)
    {
        int critical = attacker.EquippedWeapon.Critical + attacker.Skill / 2 - defender.Luck;
        return Mathf.Clamp(critical, 0, 100);
    }

    /// <summary>
    /// 判断 faster 是否因为速度优势获得追击资格。
    /// </summary>
    public static bool CanFollowUp(UnitModel faster, UnitModel slower)
    {
        return faster.Speed - slower.Speed >= FollowUpSpeedThreshold;
    }

    /// <summary>
    /// 根据当前 HP 和装备 HP 消耗，估算单位最多能够完成几次攻击。
    /// 该方法只用于预测；真实战斗中还会受到被反击击倒等状态变化影响。
    /// </summary>
    public static int CalculateMaximumStrikeCount(UnitModel unit, bool hasFollowUp)
    {
        if (!unit.CanUseEquippedWeapon)
        {
            return 0;
        }

        int strikeCount = 1;
        if (!hasFollowUp)
        {
            return strikeCount;
        }

        int hpAfterFirstUse = unit.CurrentHp - unit.EquippedWeapon.HpCost;
        bool canPayForSecondUse = unit.EquippedWeapon.HpCost == 0 ||
                                  hpAfterFirstUse > unit.EquippedWeapon.HpCost;
        return canPayForSecondUse ? 2 : 1;
    }

    /// <summary>
    /// 根据双方当前位置、地形和装备生成战斗预测。
    /// </summary>
    public static CombatForecast CreateForecast(
        UnitModel attacker,
        UnitModel defender,
        int attackerTerrainDefenseBonus,
        int attackerTerrainAvoidBonus,
        int defenderTerrainDefenseBonus,
        int defenderTerrainAvoidBonus)
    {
        int attackerDamage = CalculateDamage(attacker, defender, defenderTerrainDefenseBonus);
        int attackerHit = CalculateHitRate(attacker, defender, defenderTerrainAvoidBonus);
        int attackerCritical = CalculateCriticalRate(attacker, defender);
        int attackerStrikes = CalculateMaximumStrikeCount(attacker, CanFollowUp(attacker, defender));

        bool defenderCanCounter = CanAttack(defender, attacker);
        int defenderDamage = defenderCanCounter
            ? CalculateDamage(defender, attacker, attackerTerrainDefenseBonus)
            : 0;
        int defenderHit = defenderCanCounter
            ? CalculateHitRate(defender, attacker, attackerTerrainAvoidBonus)
            : 0;
        int defenderCritical = defenderCanCounter
            ? CalculateCriticalRate(defender, attacker)
            : 0;
        int defenderStrikes = defenderCanCounter
            ? CalculateMaximumStrikeCount(defender, CanFollowUp(defender, attacker))
            : 0;

        return new CombatForecast(
            attackerDamage,
            attackerHit,
            attackerCritical,
            attackerStrikes,
            defenderCanCounter,
            defenderDamage,
            defenderHit,
            defenderCritical,
            defenderStrikes);
    }

    /// <summary>
    /// 计算玩家攻击后获得的经验值。
    /// 等级更高的目标奖励更多，击败目标会额外获得经验；结果限制在合理区间。
    /// </summary>
    public static int CalculateExperienceGain(UnitModel attacker, UnitModel defender, bool defeatedDefender)
    {
        int levelDifference = defender.Level - attacker.Level;
        int baseExperience = 12 + levelDifference * 2;
        int defeatBonus = defeatedDefender ? 22 : 0;
        return Mathf.Clamp(baseExperience + defeatBonus, 5, 60);
    }
}
