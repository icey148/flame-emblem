using Godot;

namespace FlameEmblem.Game;

/// <summary>
/// 集中管理第一版战斗公式，避免伤害、距离等规则散落在界面代码中。
/// 后续加入命中、暴击、速度追击、魔法和地形修正时都从这里扩展。
/// </summary>
public static class CombatRules
{
    /// <summary>
    /// 计算两个格子之间的曼哈顿距离。
    /// 战棋只允许上下左右移动，因此该距离适合用于基础移动和攻击判定。
    /// </summary>
    public static int GridDistance(Vector2I from, Vector2I to)
    {
        return Mathf.Abs(from.X - to.X) + Mathf.Abs(from.Y - to.Y);
    }

    /// <summary>
    /// 判断攻击者当前是否能够攻击目标。
    /// </summary>
    public static bool IsInAttackRange(UnitModel attacker, UnitModel target)
    {
        // 第一版只使用“最大距离 <= 攻击距离”的规则；以后可以支持 2~3 格弓箭或最小射程。
        return GridDistance(attacker.GridPosition, target.GridPosition) <= attacker.AttackRange;
    }

    /// <summary>
    /// 根据力量、武器威力和目标防御计算一次物理攻击伤害。
    /// </summary>
    public static int CalculateDamage(UnitModel attacker, UnitModel defender)
    {
        // 经典 SRPG 的基础思路：攻击力 = 力量 + 武器威力，最终至少造成 1 点伤害。
        int rawDamage = attacker.Strength + attacker.WeaponMight - defender.Defense;
        return Mathf.Max(1, rawDamage);
    }

    /// <summary>
    /// 执行一次确定性攻击并返回造成的实际伤害。
    /// 第一版暂不加入随机命中率，方便先验证回合和地图逻辑是否正确。
    /// </summary>
    public static int ResolveAttack(UnitModel attacker, UnitModel defender)
    {
        int damage = CalculateDamage(attacker, defender);
        defender.TakeDamage(damage);
        return damage;
    }
}
