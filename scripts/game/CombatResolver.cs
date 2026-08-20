namespace FlameEmblem.Game;

/// <summary>
/// 保存战斗交换中的一次实际攻击结果。
/// 一次完整战斗可能包含主动攻击、反击以及其中一方的速度追击。
/// </summary>
public sealed class CombatStrikeResult
{
    /// <summary>
    /// 创建一次攻击结果。
    /// </summary>
    public CombatStrikeResult(
        UnitModel attacker,
        UnitModel defender,
        int hitRate,
        int criticalRate,
        bool hit,
        bool critical,
        int damage,
        int hpCostPaid,
        bool defenderDefeated)
    {
        Attacker = attacker;
        Defender = defender;
        HitRate = hitRate;
        CriticalRate = criticalRate;
        Hit = hit;
        Critical = critical;
        Damage = damage;
        HpCostPaid = hpCostPaid;
        DefenderDefeated = defenderDefeated;
    }

    /// <summary>发动本次攻击的单位。</summary>
    public UnitModel Attacker { get; }

    /// <summary>承受本次攻击的单位。</summary>
    public UnitModel Defender { get; }

    /// <summary>本次攻击结算时使用的命中率。</summary>
    public int HitRate { get; }

    /// <summary>本次攻击结算时使用的必杀率。</summary>
    public int CriticalRate { get; }

    /// <summary>本次攻击是否命中。</summary>
    public bool Hit { get; }

    /// <summary>本次攻击是否触发必杀。</summary>
    public bool Critical { get; }

    /// <summary>本次攻击实际造成的伤害；未命中时为 0。</summary>
    public int Damage { get; }

    /// <summary>发动攻击时支付的 HP 消耗；普通武器为 0。</summary>
    public int HpCostPaid { get; }

    /// <summary>
    /// 这一击结算结束后防守方是否已经倒下。
    /// 该快照专门用于动画层判断应该播放受击还是倒下，不能用战斗全部结束后的最终生命状态替代。
    /// </summary>
    public bool DefenderDefeated { get; }
}

/// <summary>
/// 保存一次完整战斗交换的全部攻击记录。
/// UI 可以根据这些记录生成战斗日志或逐条播放动画。
/// </summary>
public sealed class CombatExchangeResult
{
    /// <summary>
    /// 创建战斗交换结果。
    /// </summary>
    public CombatExchangeResult(IReadOnlyList<CombatStrikeResult> strikes)
    {
        Strikes = strikes;
    }

    /// <summary>按照实际发生顺序排列的攻击记录。</summary>
    public IReadOnlyList<CombatStrikeResult> Strikes { get; }
}

/// <summary>
/// 负责真正执行一次战斗交换。
/// CombatRules 只计算概率和数值，本类负责掷随机数、扣除施法 HP、处理反击和速度追击。
/// </summary>
public static class CombatResolver
{
    /// <summary>
    /// 执行主动攻击 → 反击 → 速度追击的完整流程。
    /// 任意一方被击倒后会立即停止后续攻击。
    /// </summary>
    public static CombatExchangeResult ResolveExchange(
        UnitModel attacker,
        UnitModel defender,
        TerrainDefinition attackerTerrain,
        TerrainDefinition defenderTerrain,
        Random random)
    {
        List<CombatStrikeResult> strikes = new();

        // 主动方先攻击一次，这是一次交换成立的前提。
        CombatStrikeResult? openingStrike = ResolveStrike(
            attacker,
            defender,
            defenderTerrain,
            random);
        if (openingStrike is not null)
        {
            strikes.Add(openingStrike);
        }

        // 防守方只有仍存活、射程匹配且能够支付装备成本时才能反击。
        if (attacker.IsAlive && defender.IsAlive && CombatRules.CanAttack(defender, attacker))
        {
            CombatStrikeResult? counterStrike = ResolveStrike(
                defender,
                attacker,
                attackerTerrain,
                random);
            if (counterStrike is not null)
            {
                strikes.Add(counterStrike);
            }
        }

        // 追击放在首次反击之后；速度领先至少 4 点的一方最多追加一次攻击。
        if (attacker.IsAlive && defender.IsAlive)
        {
            if (CombatRules.CanFollowUp(attacker, defender) && CombatRules.CanAttack(attacker, defender))
            {
                CombatStrikeResult? followUp = ResolveStrike(
                    attacker,
                    defender,
                    defenderTerrain,
                    random);
                if (followUp is not null)
                {
                    strikes.Add(followUp);
                }
            }
            else if (CombatRules.CanFollowUp(defender, attacker) && CombatRules.CanAttack(defender, attacker))
            {
                CombatStrikeResult? followUp = ResolveStrike(
                    defender,
                    attacker,
                    attackerTerrain,
                    random);
                if (followUp is not null)
                {
                    strikes.Add(followUp);
                }
            }
        }

        CombatExchangeResult exchange = new(strikes);

        // 战斗数值全部确定以后再通知表现层；动画只消费结果，不参与规则运算。
        BattleAnimationBus.Publish(exchange);
        return exchange;
    }

    /// <summary>
    /// 执行单次攻击：先支付装备 HP 成本，再掷命中与必杀，最后应用实际伤害。
    /// HP 成本无论攻击是否命中都会支付，这让魔法消耗规则清晰可预测。
    /// </summary>
    private static CombatStrikeResult? ResolveStrike(
        UnitModel attacker,
        UnitModel defender,
        TerrainDefinition defenderTerrain,
        Random random)
    {
        if (!CombatRules.CanAttack(attacker, defender))
        {
            return null;
        }

        int hpCost = attacker.EquippedWeapon.HpCost;
        if (!attacker.TryPayEquippedWeaponHpCost())
        {
            return null;
        }

        int hitRate = CombatRules.CalculateHitRate(attacker, defender, defenderTerrain.AvoidBonus);
        int criticalRate = CombatRules.CalculateCriticalRate(attacker, defender);
        bool hit = random.Next(100) < hitRate;
        bool critical = false;
        int damage = 0;

        if (hit)
        {
            critical = random.Next(100) < criticalRate;
            int normalDamage = CombatRules.CalculateDamage(attacker, defender, defenderTerrain.DefenseBonus);

            // 必杀伤害使用普通伤害的 3 倍；0 伤害即使必杀仍然保持 0。
            damage = critical ? normalDamage * 3 : normalDamage;
            defender.TakeDamage(damage);
        }

        return new CombatStrikeResult(
            attacker,
            defender,
            hitRate,
            criticalRate,
            hit,
            critical,
            damage,
            hpCost,
            !defender.IsAlive);
    }
}
