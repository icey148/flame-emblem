using Godot;

namespace FlameEmblem.Game;

/// <summary>
/// 表示单位所属阵营。
/// 第一版只实现玩家与敌军，后续可以继续扩展 NPC / 中立阵营。
/// </summary>
public enum UnitTeam
{
    Player,
    Enemy
}

/// <summary>
/// 保存一个战棋单位在运行时所需的基础数据。
/// 该模型不依赖具体 UI，方便后续从 JSON、Resource 或存档中创建单位。
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
        int maxHp,
        int strength,
        int defense,
        int move,
        int attackRange,
        int weaponMight)
    {
        Id = id;
        DisplayName = displayName;
        Team = team;
        GridPosition = gridPosition;
        MaxHp = maxHp;
        CurrentHp = maxHp;
        Strength = strength;
        Defense = defense;
        Move = move;
        AttackRange = attackRange;
        WeaponMight = weaponMight;
    }

    /// <summary>稳定的单位 ID，未来用于存档和数据表关联。</summary>
    public string Id { get; }

    /// <summary>界面上展示的单位名称。</summary>
    public string DisplayName { get; }

    /// <summary>单位当前所属阵营。</summary>
    public UnitTeam Team { get; }

    /// <summary>单位当前所在的逻辑格子坐标。</summary>
    public Vector2I GridPosition { get; set; }

    /// <summary>最大生命值。</summary>
    public int MaxHp { get; }

    /// <summary>当前生命值。</summary>
    public int CurrentHp { get; private set; }

    /// <summary>力量属性，参与物理伤害计算。</summary>
    public int Strength { get; }

    /// <summary>防御属性，用于减少物理伤害。</summary>
    public int Defense { get; }

    /// <summary>每回合最多可移动的格数。</summary>
    public int Move { get; }

    /// <summary>基础攻击距离；第一版近战单位默认为 1。</summary>
    public int AttackRange { get; }

    /// <summary>当前武器的基础威力。</summary>
    public int WeaponMight { get; }

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
    /// 在新回合开始时清理单位的回合状态。
    /// </summary>
    public void ResetForNewTurn()
    {
        HasActed = false;
    }
}
