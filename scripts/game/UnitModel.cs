using Godot;

namespace FlameEmblem.Game;

/// <summary>
/// 表示单位所属阵营。
/// 当前实现玩家与敌军，后续可以继续扩展 NPC / 中立阵营。
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
/// 保存一次转职产生的职业与属性变化。
/// UI 只消费结果文本，不参与真正的属性修改。
/// </summary>
public sealed class PromotionResult
{
    /// <summary>创建一次转职结果。</summary>
    public PromotionResult(
        string previousClassName,
        string newClassName,
        IReadOnlyList<string> statChanges)
    {
        PreviousClassName = previousClassName;
        NewClassName = newClassName;
        StatChanges = statChanges;
    }

    /// <summary>转职前职业名称。</summary>
    public string PreviousClassName { get; }

    /// <summary>转职后职业名称。</summary>
    public string NewClassName { get; }

    /// <summary>因为新职业属性基准而获得的提升。</summary>
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
        WeaponDefinition equippedWeapon,
        int level,
        int maxHp,
        int strength,
        int magic,
        int skill,
        int speed,
        int luck,
        int defense,
        int resistance,
        StatGrowthDefinition growth)
    {
        Id = id;
        DisplayName = displayName;
        Team = team;
        GridPosition = gridPosition;
        ClassDefinition = classDefinition;
        EquippedWeapon = equippedWeapon;
        Level = Mathf.Max(1, level);
        MaxHp = Mathf.Max(1, maxHp);
        CurrentHp = MaxHp;
        Strength = Mathf.Max(0, strength);
        Magic = Mathf.Max(0, magic);
        Skill = Mathf.Max(0, skill);
        Speed = Mathf.Max(0, speed);
        Luck = Mathf.Max(0, luck);
        Defense = Mathf.Max(0, defense);
        Resistance = Mathf.Max(0, resistance);
        Growth = growth;
    }

    /// <summary>稳定的单位 ID，用于存档和数据表关联。</summary>
    public string Id { get; }

    /// <summary>界面上展示的单位名称。</summary>
    public string DisplayName { get; }

    /// <summary>单位当前所属阵营。</summary>
    public UnitTeam Team { get; }

    /// <summary>单位当前所在的逻辑格子坐标。</summary>
    public Vector2I GridPosition { get; set; }

    /// <summary>
    /// 当前职业定义。
    /// 只能由构造、受控转职或已经验证过的存档恢复入口修改。
    /// </summary>
    public UnitClassDefinition ClassDefinition { get; private set; }

    /// <summary>当前装备的武器或法术。</summary>
    public WeaponDefinition EquippedWeapon { get; private set; }

    /// <summary>当前角色等级。</summary>
    public int Level { get; private set; }

    /// <summary>当前等级内累计经验，达到 100 后升级。</summary>
    public int Experience { get; private set; }

    /// <summary>最大生命值。</summary>
    public int MaxHp { get; private set; }

    /// <summary>当前生命值。</summary>
    public int CurrentHp { get; private set; }

    /// <summary>力量属性，参与物理攻击伤害。</summary>
    public int Strength { get; private set; }

    /// <summary>魔力属性，参与魔法攻击伤害。</summary>
    public int Magic { get; private set; }

    /// <summary>技巧属性，参与命中与必杀率计算。</summary>
    public int Skill { get; private set; }

    /// <summary>速度属性，参与回避和追击判定。</summary>
    public int Speed { get; private set; }

    /// <summary>幸运属性，参与命中、回避与抗必杀。</summary>
    public int Luck { get; private set; }

    /// <summary>防御属性，用于减少物理伤害。</summary>
    public int Defense { get; private set; }

    /// <summary>魔防属性，用于减少魔法伤害。</summary>
    public int Resistance { get; private set; }

    /// <summary>每回合最多可消耗的移动力，由职业提供。</summary>
    public int Move => ClassDefinition.Move;

    /// <summary>当前装备允许的最小攻击距离。</summary>
    public int MinAttackRange => EquippedWeapon.MinRange;

    /// <summary>当前装备允许的最大攻击距离。</summary>
    public int MaxAttackRange => EquippedWeapon.MaxRange;

    /// <summary>升级时使用的角色成长率。</summary>
    public StatGrowthDefinition Growth { get; }

    /// <summary>本回合是否已经完成行动。</summary>
    public bool HasActed { get; set; }

    /// <summary>生命值大于 0 时单位仍然存活。</summary>
    public bool IsAlive => CurrentHp > 0;

    /// <summary>
    /// 判断当前生命值是否足够使用装备。
    /// 主要用于阻止 HP 不足的施法者继续施放消耗生命的法术。
    /// </summary>
    public bool CanUseEquippedWeapon => IsAlive && EquippedWeapon.CanPayHpCost(CurrentHp);

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
    /// 为当前装备支付一次 HP 使用成本。
    /// 普通武器费用为 0；法术必须保证支付后至少剩余 1 HP。
    /// </summary>
    public bool TryPayEquippedWeaponHpCost()
    {
        if (!EquippedWeapon.CanPayHpCost(CurrentHp))
        {
            return false;
        }

        if (EquippedWeapon.HpCost > 0)
        {
            CurrentHp -= EquippedWeapon.HpCost;
        }

        return true;
    }

    /// <summary>
    /// 替换当前装备。
    /// 当前阶段每个单位只装备一件武器/法术，后续加入背包时仍可复用此入口。
    /// </summary>
    public void Equip(WeaponDefinition weapon)
    {
        EquippedWeapon = weapon;
    }

    /// <summary>
    /// 按已经验证过的转职规则切换职业，并把低于新职业基准的属性补足。
    /// 高于基准的成长完全保留；最大 HP 提升时当前 HP 同步增加相同数值，避免转职反而降低当前生命比例。
    /// </summary>
    public PromotionResult PromoteTo(
        UnitClassDefinition targetClass,
        PromotionStatFloor statFloor,
        bool resetLevel)
    {
        if (Team != UnitTeam.Player)
        {
            throw new InvalidOperationException("当前基础转职系统只允许玩家单位转职。");
        }

        string previousClassName = ClassDefinition.DisplayName;
        List<string> changes = new();

        if (MaxHp < statFloor.MaxHp)
        {
            int increase = statFloor.MaxHp - MaxHp;
            MaxHp = statFloor.MaxHp;
            CurrentHp = Mathf.Min(MaxHp, CurrentHp + increase);
            changes.Add($"HP +{increase}");
        }

        Strength = ApplyPromotionFloor(Strength, statFloor.Strength, "力量", changes);
        Magic = ApplyPromotionFloor(Magic, statFloor.Magic, "魔力", changes);
        Skill = ApplyPromotionFloor(Skill, statFloor.Skill, "技巧", changes);
        Speed = ApplyPromotionFloor(Speed, statFloor.Speed, "速度", changes);
        Luck = ApplyPromotionFloor(Luck, statFloor.Luck, "幸运", changes);
        Defense = ApplyPromotionFloor(Defense, statFloor.Defense, "防御", changes);
        Resistance = ApplyPromotionFloor(Resistance, statFloor.Resistance, "魔防", changes);

        ClassDefinition = targetClass;
        if (resetLevel)
        {
            // 采用经典分阶段职业成长方式：晋升后从新职业 Lv.1 重新成长。
            Level = 1;
            Experience = 0;
        }

        return new PromotionResult(previousClassName, targetClass.DisplayName, changes);
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
    /// 从已经验证过的本地存档恢复可变化的运行时状态。
    /// 职业和装备必须由当前版本的数据目录先解析成功，存档本身不能注入新的规则定义。
    /// </summary>
    public void RestoreRuntimeState(
        Vector2I gridPosition,
        UnitClassDefinition classDefinition,
        WeaponDefinition equippedWeapon,
        int level,
        int experience,
        int maxHp,
        int currentHp,
        int strength,
        int magic,
        int skill,
        int speed,
        int luck,
        int defense,
        int resistance,
        bool hasActed)
    {
        GridPosition = gridPosition;
        ClassDefinition = classDefinition;
        EquippedWeapon = equippedWeapon;
        Level = Mathf.Max(1, level);
        Experience = Mathf.Clamp(experience, 0, 99);
        MaxHp = Mathf.Max(1, maxHp);
        CurrentHp = Mathf.Clamp(currentHp, 0, MaxHp);
        Strength = Mathf.Max(0, strength);
        Magic = Mathf.Max(0, magic);
        Skill = Mathf.Max(0, skill);
        Speed = Mathf.Max(0, speed);
        Luck = Mathf.Max(0, luck);
        Defense = Mathf.Max(0, defense);
        Resistance = Mathf.Max(0, resistance);
        HasActed = hasActed;
    }

    /// <summary>
    /// 在新回合开始时清理单位的回合状态。
    /// </summary>
    public void ResetForNewTurn()
    {
        HasActed = false;
    }

    /// <summary>把单项属性补到转职基准，并记录实际增加值。</summary>
    private static int ApplyPromotionFloor(
        int currentValue,
        int floorValue,
        string displayName,
        ICollection<string> changes)
    {
        int safeFloor = Mathf.Max(0, floorValue);
        if (currentValue >= safeFloor)
        {
            return currentValue;
        }

        int increase = safeFloor - currentValue;
        changes.Add($"{displayName} +{increase}");
        return safeFloor;
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

        if (PassesGrowthRoll(Growth.Magic, "magic"))
        {
            Magic++;
            changes.Add("魔力 +1");
        }

        if (PassesGrowthRoll(Growth.Skill, "skill"))
        {
            Skill++;
            changes.Add("技巧 +1");
        }

        if (PassesGrowthRoll(Growth.Speed, "speed"))
        {
            Speed++;
            changes.Add("速度 +1");
        }

        if (PassesGrowthRoll(Growth.Luck, "luck"))
        {
            Luck++;
            changes.Add("幸运 +1");
        }

        if (PassesGrowthRoll(Growth.Defense, "defense"))
        {
            Defense++;
            changes.Add("防御 +1");
        }

        if (PassesGrowthRoll(Growth.Resistance, "resistance"))
        {
            Resistance++;
            changes.Add("魔防 +1");
        }

        return new LevelUpResult(Level, changes);
    }

    /// <summary>
    /// 对指定属性执行可复现的成长率判定。
    /// 这里只使用普通确定性哈希与百分比比较，不依赖联网服务或机器学习。
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

        // FNV-1a 提供简单稳定的跨运行哈希，足够用于当前升级成长率判定。
        foreach (char character in seedText)
        {
            hash ^= character;
            hash *= 16777619;
        }

        int roll = (int)(hash % 100);
        return roll < clampedRate;
    }
}
