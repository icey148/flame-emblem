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

    /// <summary>
    /// 职业层面的默认最小攻击距离。
    /// 当前实际攻击距离由装备决定，该字段作为未来职业限制/无装备攻击的预留规则保留。
    /// </summary>
    public int MinAttackRange { get; }

    /// <summary>
    /// 职业层面的默认最大攻击距离。
    /// 当前实际攻击距离由装备决定，该字段作为未来职业限制/无装备攻击的预留规则保留。
    /// </summary>
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
    public StatGrowthDefinition(
        int hp,
        int strength,
        int magic,
        int skill,
        int speed,
        int luck,
        int defense,
        int resistance)
    {
        Hp = hp;
        Strength = strength;
        Magic = magic;
        Skill = skill;
        Speed = speed;
        Luck = luck;
        Defense = defense;
        Resistance = resistance;
    }

    /// <summary>生命成长率。</summary>
    public int Hp { get; }

    /// <summary>力量成长率。</summary>
    public int Strength { get; }

    /// <summary>魔力成长率。</summary>
    public int Magic { get; }

    /// <summary>技巧成长率，主要影响命中与必杀。</summary>
    public int Skill { get; }

    /// <summary>速度成长率，主要影响回避与追击。</summary>
    public int Speed { get; }

    /// <summary>幸运成长率，影响命中、回避以及抗必杀能力。</summary>
    public int Luck { get; }

    /// <summary>防御成长率。</summary>
    public int Defense { get; }

    /// <summary>魔防成长率。</summary>
    public int Resistance { get; }
}
