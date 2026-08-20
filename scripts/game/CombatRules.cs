using Godot;

namespace FlameEmblem.Game;

/// <summary>
/// 保存一次战斗预测需要展示的核心数值。
/// 预测只计算当前已实现的确定性物理规则，因此界面展示结果与实际结算保持一致。
/// </summary>
public sealed class CombatForecast
{
    /// <summary>
    /// 创建战斗预测结果。
    /// </summary>
    public CombatForecast(int attackerDamage, bool defenderCanCounter, int defenderCounterDamage)
    {
        AttackerDamage = attackerDamage;
        DefenderCanCounter = defenderCanCounter;
        DefenderCounterDamage = defenderCounterDamage;
    }

    /// <summary>攻击方本次会造成的伤害。</summary>
    public int AttackerDamage { get; }

    /// <summary>防守方在受到攻击后是否具备反击距离。</summary>
    public bool DefenderCanCounter { get; }

    /// <summary>防守方若能反击，会造成的伤害。</summary>
    public int DefenderCounterDamage { get; }
}

/// <summary>
/// 集中管理战斗公式，避免伤害、距离、地形修正等规则散落在 UI 代码中。
/// </summary>
public static class CombatRules
{
    /// <summary>
    /// 计算两个格子之间的曼哈顿距离。
    /// 战棋只允许上下左右移动，因此该距离适合基础移动和攻击判定。
    /// </summary>
    public static int GridDistance(Vector2I from, Vector2I to)
    {
        return Mathf.Abs(from.X - to.X) + Mathf.Abs(from.Y - to.Y);
    }

    /// <summary>
    /// 判断攻击者当前是否能够攻击目标。
    /// 同时检查职业的最小与最大攻击距离，支持后续 2 格弓、1~2 格魔法等配置。
    /// </summary>
    public static bool IsInAttackRange(UnitModel attacker, UnitModel target)
    {
        int distance = GridDistance(attacker.GridPosition, target.GridPosition);
        return distance >= attacker.MinAttackRange && distance <= attacker.MaxAttackRange;
    }

    /// <summary>
    /// 根据力量、武器威力、目标防御和目标地形防御加成计算一次物理攻击伤害。
    /// </summary>
    public static int CalculateDamage(UnitModel attacker, UnitModel defender, int defenderTerrainDefenseBonus)
    {
        int totalDefense = defender.Defense + Mathf.Max(0, defenderTerrainDefenseBonus);
        int rawDamage = attacker.Strength + attacker.WeaponMight - totalDefense;

        // 当前原型保持经典“至少造成 1 点伤害”的简化规则，避免完全无伤导致测试节奏停滞。
        return Mathf.Max(1, rawDamage);
    }

    /// <summary>
    /// 根据双方当前位置和地形生成战斗预测。
    /// </summary>
    public static CombatForecast CreateForecast(
        UnitModel attacker,
        UnitModel defender,
        int attackerTerrainDefenseBonus,
        int defenderTerrainDefenseBonus)
    {
        int attackerDamage = CalculateDamage(attacker, defender, defenderTerrainDefenseBonus);
        bool defenderCanCounter = defender.IsAlive && IsInAttackRange(defender, attacker);
        int counterDamage = defenderCanCounter
            ? CalculateDamage(defender, attacker, attackerTerrainDefenseBonus)
            : 0;

        return new CombatForecast(attackerDamage, defenderCanCounter, counterDamage);
    }

    /// <summary>
    /// 执行一次确定性攻击并返回造成的实际伤害。
    /// 命中、暴击与追击将在后续阶段加入，这一版先保证预测与结算完全一致。
    /// </summary>
    public static int ResolveAttack(UnitModel attacker, UnitModel defender, int defenderTerrainDefenseBonus)
    {
        int damage = CalculateDamage(attacker, defender, defenderTerrainDefenseBonus);
        defender.TakeDamage(damage);
        return damage;
    }
}
