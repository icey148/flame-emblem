namespace FlameEmblem.Game;

/// <summary>
/// 在战斗结算层与表现层之间传递一次完整战斗交换。
/// 该总线只传递已经确定的结果，不参与命中、伤害或回合规则，因此动画不会反过来影响战斗数值。
/// </summary>
public static class BattleAnimationBus
{
    /// <summary>
    /// 当一次完整战斗交换结算完成后触发。
    /// 表现层可以订阅该事件，把真实的攻击记录转换成逐击动画。
    /// </summary>
    public static event Action<CombatExchangeResult>? ExchangeResolved;

    /// <summary>
    /// 发布一次已经完成结算的战斗交换。
    /// 没有任何有效攻击记录时不触发事件，避免空演出占用玩家时间。
    /// </summary>
    public static void Publish(CombatExchangeResult exchange)
    {
        if (exchange.Strikes.Count == 0)
        {
            return;
        }

        ExchangeResolved?.Invoke(exchange);
    }
}
