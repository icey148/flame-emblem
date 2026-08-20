namespace FlameEmblem.Game;

/// <summary>
/// 表示一次攻击使用的伤害属性。
/// 物理攻击读取力量/防御，魔法攻击读取魔力/魔防。
/// </summary>
public enum DamageType
{
    Physical,
    Magical
}

/// <summary>
/// 描述一个武器或法术在战斗层面的固定参数。
/// 数据来自 data/weapons.json，角色只保存当前装备的定义引用。
/// </summary>
public sealed class WeaponDefinition
{
    /// <summary>
    /// 创建一个武器或法术定义。
    /// </summary>
    public WeaponDefinition(
        string id,
        string displayName,
        DamageType damageType,
        int might,
        int hit,
        int critical,
        int minRange,
        int maxRange,
        int hpCost)
    {
        Id = id;
        DisplayName = displayName;
        DamageType = damageType;
        Might = Math.Max(0, might);
        Hit = Math.Clamp(hit, 0, 100);
        Critical = Math.Clamp(critical, 0, 100);
        MinRange = Math.Max(1, minRange);
        MaxRange = Math.Max(MinRange, maxRange);
        HpCost = Math.Max(0, hpCost);
    }

    /// <summary>武器稳定 ID，用于 JSON、存档和后续装备系统关联。</summary>
    public string Id { get; }

    /// <summary>HUD 中显示的武器或法术名称。</summary>
    public string DisplayName { get; }

    /// <summary>决定使用力量/防御还是魔力/魔防进行伤害计算。</summary>
    public DamageType DamageType { get; }

    /// <summary>武器或法术基础威力。</summary>
    public int Might { get; }

    /// <summary>基础命中率。</summary>
    public int Hit { get; }

    /// <summary>基础必杀率。</summary>
    public int Critical { get; }

    /// <summary>最小攻击距离。</summary>
    public int MinRange { get; }

    /// <summary>最大攻击距离。</summary>
    public int MaxRange { get; }

    /// <summary>
    /// 每次实际施放需要支付的生命值。
    /// 普通武器为 0；魔法可配置大于 0 的数值，实现类似“以 HP 施法”的规则。
    /// </summary>
    public int HpCost { get; }

    /// <summary>是否属于会消耗 HP 的攻击手段。</summary>
    public bool CostsHp => HpCost > 0;

    /// <summary>
    /// 判断当前生命值是否足够使用该武器/法术。
    /// 施法不能主动把自己扣到 0 HP，因此必须至少保留 1 点生命。
    /// </summary>
    public bool CanPayHpCost(int currentHp)
    {
        return HpCost == 0 || currentHp > HpCost;
    }
}
