namespace FlameEmblem.Game;

/// <summary>
/// 把战斗结算结果转换为可直接显示在 HUD/战斗演出层中的中文文本。
/// 该类只负责表现，不修改任何战斗状态，因此以后可以安全替换成真正的动画场景。
/// </summary>
public static class BattlePresentationFormatter
{
    /// <summary>
    /// 按实际发生顺序格式化一次完整战斗交换。
    /// </summary>
    public static string FormatExchange(CombatExchangeResult exchange)
    {
        if (exchange.Strikes.Count == 0)
        {
            return "本次战斗没有产生有效攻击。";
        }

        List<string> lines = new();
        foreach (CombatStrikeResult strike in exchange.Strikes)
        {
            if (strike.HpCostPaid > 0)
            {
                lines.Add($"{strike.Attacker.DisplayName} 消耗 {strike.HpCostPaid} HP 使用 {strike.Attacker.EquippedWeapon.DisplayName}。" );
            }

            if (!strike.Hit)
            {
                lines.Add($"{strike.Attacker.DisplayName} → {strike.Defender.DisplayName}：未命中（命中 {strike.HitRate}%）。");
                continue;
            }

            string criticalText = strike.Critical ? " 必杀！" : string.Empty;
            lines.Add(
                $"{strike.Attacker.DisplayName} → {strike.Defender.DisplayName}：命中，造成 {strike.Damage} 伤害。{criticalText}");
        }

        return string.Join("\n", lines);
    }
}
