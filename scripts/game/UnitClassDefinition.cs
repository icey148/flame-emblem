namespace FlameEmblem.Game;

/// <summary>
/// 描述一个职业在战斗层面的基础规则。
/// 职业数据来自 data/classes.json，因此以后新增职业时不需要修改主场景代码。
/// </summary>
public sealed class UnitClassDefinition
{
    /// <summary>
    /// 创建一个职业定义。
    /// </summary>
    public UnitClassDefinition(
        string id,
        string displayName,
        int move,
        int minAttackRange,
        int maxAttackRange)
    {
        Id = id;
        DisplayName = displayName;
        Move = move;
        MinAttackRange = minAttackRange;
        MaxAttackRange = maxAttackRange;
    }

    /// <summary>职业稳定 ID，用于 JSON 和存档关联。</summary>
    public string Id { get; }

    /// <summary>职业在 HUD 中显示的名称。</summary>
    public string DisplayName { get; }

    /// <summary>该职业每回合可消耗的基础移动力。</summary>
    public int Move { get; }

    /// <summary>职业允许的最小攻击距离。</summary>
    public int MinAttackRange { get; }

    /// <summary>职业允许的最大攻击距离。</summary>
    public int MaxAttackRange { get; }
}

/// <summary>
/// 保存一个角色升级时各属性的成长率。
/// 成长率使用 0~100 的百分比整数，便于直接写入 JSON。
/// </summary>
public sealed class StatGrowthDefinition
{
    /// <summary>
    /// 创建角色成长率定义。
    /// </summary>
    public StatGrowthDefinition(int hp, int strength, int defense, int speed)
    {
        Hp = hp;
        Strength = strength;
        Defense = defense;
        Speed = speed;
    }

    /// <summary>生命成长率。</summary>
    public int Hp { get; }

    /// <summary>力量成长率。</summary>
    public int Strength { get; }

    /// <summary>防御成长率。</summary>
    public int Defense { get; }

    /// <summary>速度成长率。</summary>
    public int Speed { get; }
}
