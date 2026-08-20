using Godot;

namespace FlameEmblem.Game;

/// <summary>
/// 表示单位所属阵营。
/// 第一阶段实现玩家与敌军，后续可以继续扩展 NPC / 中立阵营。
/// </summary>
public enum UnitTeam
{
    Player,
    Enemy
}

/// <summary>
/// 保存一次升级产生的属性变化，供 HUD 或后续升级动画展示。
/// </summary>
public sealed class LevelUpResult
{
    /// <summary>
    /// 创建升级结果。
    /// </summary>
    public LevelUpResult(int newLevel, IReadOnlyList<string> statChanges)
    {
        NewLevel = newLevel;
        StatChanges = statChanges;
    }

    /// <summary>升级后的等级。</summary>
    public int NewLevel { get; }

    /// <summary>本次升级提升的属性文本。</summary>
    public IReadOnlyList<string> StatChanges { get; }
}

/// <summary>
/// 保存一个战棋单位在运行时所需的数据。
/// 模型本身不依赖具体 UI，因此可以安全地由 JSON、Resource 或存档创建。
/// </summary>
public sealed class UnitModel
{
    /// <summary>
    /// 创建一个可参与战斗的单位。
    /// </summary>
    public UnitModel(
        string id,
        string displayName,
        UnitTeam team,
        Vector2I gridPosition,
        UnitClassDefinition classDefinition,
        int level,
        int maxHp,
        int strength,
        int defense,
        int speed,
        int weaponMight,
        StatGrowthDefinition growth)
    {
        Id = id;
        DisplayName = displayName;
        Team = team;
        GridPosition = gridPosition;
        ClassDefinition = classDefinition;
        Level = Mathf.Max(1, level);
        MaxHp = Mathf.Max(1, maxHp);
        CurrentHp = MaxHp;
        Strength = Mathf.Max(0, strength);
        Defense = Mathf.Max(0, defense);
        Speed = Mathf.Max(0, speed);
        WeaponMight = Mathf.Max(0, weaponMight);
        Growth = growth;
    }

    /// <summary>稳定的单位 ID，未来用于存档和数据表关联。</summary>
    public string Id { get; }

    /// <summary>界面上展示的单位名称。</summary>
    public string DisplayName { get; }

    /// <summary>单位当前所属阵营。</summary>
    public UnitTeam Team { get; }

    /// <summary>单位当前所在的逻辑格子坐标。</summary>
    public Vector2I GridPosition { get; set; }

    /// <summary>当前职业定义。</summary>
    public UnitClassDefinition ClassDefinition { get; }

    /// <summary>当前角色等级。</summary>
    public int Level { get; private set; }

    /// <summary>当前等级内累计经验，达到 100 后升级。</summary>
    public int Experience { get; private set; }

    /// <summary>最大生命值。</summary>
    public int MaxHp { get; private set; }

    /// <summary>当前生命值。</summary>
    public int CurrentHp { get; private set; }

    /// <summary>力量属性，参与物理伤害计算。</summary>
    public int Strength { get; private set; }

    /// <summary>防御属性，用于减少物理伤害。</summary>
    public int Defense { get; private set; }

    /// <summary>速度属性，当前主要用于 HUD；后续会用于追击判定。</summary>
    public int Speed { get; private set; }

    /// <summary>每回合最多可消耗的移动力，由职业提供。</summary>
    public int Move => ClassDefinition.Move;

    /// <summary>当前职业允许的最小攻击距离。</summary>
    public int MinAttackRange => ClassDefinition.MinAttackRange;

    /// <summary>当前职业允许的最大攻击距离。</summary>
    public int MaxAttackRange => ClassDefinition.MaxAttackRange;

    /// <summary>当前武器的基础威力。</summary>
    public int WeaponMight { get; }

    /// <summary>升级时使用的角色成长率。</summary>
    public StatGrowthDefinition Growth { get; }

    /// <summary>本回合是否已经完成行动。</summary>
    public bool HasActed { get; set; }

    /// <summary>生命值大于 0 时单位仍然存活。</summary>
    public bool IsAlive => CurrentHp > 0;

    /// <summary>
    /// 对单位造成伤害，并把最终生命值限制在 0 以上。
    /// </summary>
    public void TakeDamage(int damage)
    {
        // 即使外部传入负数，也不能通过“伤害”意外给单位回血。
        int safeDamage = Mathf.Max(0, damage);
        CurrentHp = Mathf.Max(0, CurrentHp - safeDamage);
    }

    /// <summary>
    /// 获得经验值，并执行所有达到 100 经验触发的升级。
    /// 返回每次升级结果，方便界面展示成长属性。
    /// </summary>
    public IReadOnlyList<LevelUpResult> GainExperience(int amount)
    {
        List<LevelUpResult> results = new();

        // 经验值只允许增加，避免调用方误传负数导致等级倒退。
        Experience += Mathf.Max(0, amount);
        while (Experience >= 100)
        {
            Experience -= 100;
            Level++;
            results.Add(ApplyLevelGrowth());
        }

        return results;
    }

    /// <summary>
    /// 在新回合开始时清理单位的回合状态。
    /// </summary>
    public void ResetForNewTurn()
    {
        HasActed = false;
    }

    /// <summary>
    /// 根据角色成长率处理一次升级。
    /// 原型使用“角色 ID + 新等级 + 属性名”生成稳定结果，因此同一角色同一级不会因为重新运行而反复刷成长。
    /// </summary>
    private LevelUpResult ApplyLevelGrowth()
    {
        List<string> changes = new();

        if (PassesGrowthRoll(Growth.Hp, "hp"))
        {
            MaxHp++;
            CurrentHp++;
            changes.Add("HP +1");
        }

        if (PassesGrowthRoll(Growth.Strength, "strength"))
        {
            Strength++;
            changes.Add("力量 +1");
        }

        if (PassesGrowthRoll(Growth.Defense, "defense"))
        {
            Defense++;
            changes.Add("防御 +1");
        }

        if (PassesGrowthRoll(Growth.Speed, "speed"))
        {
            Speed++;
            changes.Add("速度 +1");
        }

        return new LevelUpResult(Level, changes);
    }

    /// <summary>
    /// 对指定属性执行可复现的成长率判定。
    /// 这里不调用联网服务，也不使用机器学习；只是普通确定性哈希和百分比比较。
    /// </summary>
    private bool PassesGrowthRoll(int growthRate, string statKey)
    {
        int clampedRate = Mathf.Clamp(growthRate, 0, 100);
        if (clampedRate <= 0)
        {
            return false;
        }

        if (clampedRate >= 100)
        {
            return true;
        }

        string seedText = $"{Id}:{Level}:{statKey}";
        uint hash = 2166136261;

        // FNV-1a 提供简单稳定的跨运行哈希，足够用于原型成长率判定。
        foreach (char character in seedText)
        {
            hash ^= character;
            hash *= 16777619;
        }

        int roll = (int)(hash % 100);
        return roll < clampedRate;
    }
}
