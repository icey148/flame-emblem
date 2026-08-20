namespace FlameEmblem.Game;

/// <summary>
/// 战斗地图中可用的基础地形类型。
/// 目前只放入第一关需要的类型，后续可以继续增加山地、城墙、神殿等地形。
/// </summary>
public enum TerrainType
{
    Plain,
    Forest,
    Bridge,
    River,
    Fort
}

/// <summary>
/// 描述一个地形对移动与防御造成的规则影响。
/// </summary>
public sealed class TerrainDefinition
{
    /// <summary>
    /// 创建地形规则定义。
    /// </summary>
    public TerrainDefinition(
        TerrainType type,
        string displayName,
        int moveCost,
        int defenseBonus,
        int avoidBonus,
        bool passable)
    {
        Type = type;
        DisplayName = displayName;
        MoveCost = moveCost;
        DefenseBonus = defenseBonus;
        AvoidBonus = avoidBonus;
        Passable = passable;
    }

    /// <summary>地形枚举值。</summary>
    public TerrainType Type { get; }

    /// <summary>HUD 中显示的中文名称。</summary>
    public string DisplayName { get; }

    /// <summary>进入该格需要消耗的移动力。</summary>
    public int MoveCost { get; }

    /// <summary>站在该地形上受到物理攻击时获得的防御加成。</summary>
    public int DefenseBonus { get; }

    /// <summary>预留的回避加成；当前确定性命中版本只展示，不参与命中计算。</summary>
    public int AvoidBonus { get; }

    /// <summary>普通地面单位是否能够进入该地形。</summary>
    public bool Passable { get; }
}

/// <summary>
/// 集中提供地形规则，避免移动、绘制和战斗各自维护一套数值。
/// </summary>
public static class TerrainRules
{
    /// <summary>平地规则。</summary>
    private static readonly TerrainDefinition Plain = new(TerrainType.Plain, "平地", 1, 0, 0, true);

    /// <summary>森林移动较慢，但提供少量防御与回避。</summary>
    private static readonly TerrainDefinition Forest = new(TerrainType.Forest, "森林", 2, 1, 20, true);

    /// <summary>桥面按照普通道路处理。</summary>
    private static readonly TerrainDefinition Bridge = new(TerrainType.Bridge, "石桥", 1, 0, 0, true);

    /// <summary>第一版普通地面单位无法进入深河。</summary>
    private static readonly TerrainDefinition River = new(TerrainType.River, "河流", 99, 0, 0, false);

    /// <summary>据点提供较明显的防御收益。</summary>
    private static readonly TerrainDefinition Fort = new(TerrainType.Fort, "据点", 1, 2, 10, true);

    /// <summary>
    /// 根据枚举值返回对应地形规则。
    /// </summary>
    public static TerrainDefinition Get(TerrainType type)
    {
        return type switch
        {
            TerrainType.Forest => Forest,
            TerrainType.Bridge => Bridge,
            TerrainType.River => River,
            TerrainType.Fort => Fort,
            _ => Plain
        };
    }

    /// <summary>
    /// 把 JSON 中的字符串转换为内部地形枚举。
    /// 未识别值自动退回平地，避免单个拼写错误导致整个关卡无法加载。
    /// </summary>
    public static TerrainType Parse(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "forest" => TerrainType.Forest,
            "bridge" => TerrainType.Bridge,
            "river" => TerrainType.River,
            "fort" => TerrainType.Fort,
            _ => TerrainType.Plain
        };
    }
}
