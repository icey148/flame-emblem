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
    /// 当前是否正在显示横向战斗演出。
    /// 地图人物表现层会利用这个只读状态暂停后台移动动画，避免敌军在战斗遮罩后面把移动动画偷偷播完。
    /// </summary>
    public static bool IsPlaybackActive { get; private set; }

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

    /// <summary>
    /// 由战斗表现协调器在真正打开横向战斗窗口时调用。
    /// 这是纯表现状态，不代表游戏规则正在重新结算。
    /// </summary>
    public static void BeginPlayback()
    {
        IsPlaybackActive = true;
    }

    /// <summary>
    /// 由战斗表现协调器在正常结束、跳过或异常退出时调用。
    /// 必须确保所有退出路径都会执行，防止地图人物动画永久停住。
    /// </summary>
    public static void EndPlayback()
    {
        IsPlaybackActive = false;
    }
}
