using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlameEmblem.Game;

/// <summary>
/// 描述转职后各项属性的最低基准。
/// 角色原属性高于基准时完全保留，只有低于新职业基准的属性才会被补足。
/// </summary>
public sealed class PromotionStatFloor
{
    /// <summary>最大生命最低值。</summary>
    public int MaxHp { get; init; }

    /// <summary>力量最低值。</summary>
    public int Strength { get; init; }

    /// <summary>魔力最低值。</summary>
    public int Magic { get; init; }

    /// <summary>技巧最低值。</summary>
    public int Skill { get; init; }

    /// <summary>速度最低值。</summary>
    public int Speed { get; init; }

    /// <summary>幸运最低值。</summary>
    public int Luck { get; init; }

    /// <summary>防御最低值。</summary>
    public int Defense { get; init; }

    /// <summary>魔防最低值。</summary>
    public int Resistance { get; init; }
}

/// <summary>
/// 一条可执行的职业晋升规则。
/// 当前第一版是一对一晋升；未来需要分支转职时可以让同一个 FromClassId 对应多条规则。
/// </summary>
public sealed class PromotionRule
{
    /// <summary>创建一条已经解析完成的转职规则。</summary>
    public PromotionRule(
        string fromClassId,
        UnitClassDefinition targetClass,
        int minimumLevel,
        bool resetLevel,
        PromotionStatFloor statFloor)
    {
        FromClassId = fromClassId;
        TargetClass = targetClass;
        MinimumLevel = Math.Max(1, minimumLevel);
        ResetLevel = resetLevel;
        StatFloor = statFloor;
    }

    /// <summary>转职前职业 ID。</summary>
    public string FromClassId { get; }

    /// <summary>转职后的完整职业定义。</summary>
    public UnitClassDefinition TargetClass { get; }

    /// <summary>允许转职的最低当前等级。</summary>
    public int MinimumLevel { get; }

    /// <summary>转职后是否把等级和当前 EXP 重置为 Lv.1 / 0 EXP。</summary>
    public bool ResetLevel { get; }

    /// <summary>新职业的属性最低基准。</summary>
    public PromotionStatFloor StatFloor { get; }

    /// <summary>判断指定玩家单位当前是否满足这条转职规则。</summary>
    public bool CanPromote(UnitModel unit)
    {
        return unit.Team == UnitTeam.Player &&
               unit.IsAlive &&
               unit.ClassDefinition.Id.Equals(FromClassId, StringComparison.OrdinalIgnoreCase) &&
               unit.Level >= MinimumLevel;
    }
}

/// <summary>
/// 从 data/promotions.json 读取玩家转职路线。
/// 这里只负责固定数据规则，不处理 UI、场景切换或祠堂事件。
/// </summary>
public static class PromotionCatalog
{
    /// <summary>按转职前职业 ID 缓存当前第一条晋升规则。</summary>
    private static Dictionary<string, PromotionRule>? _rules;

    /// <summary>JSON 解析使用大小写不敏感选项。</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>返回指定单位当前职业对应的转职规则；没有路线时返回 null。</summary>
    public static PromotionRule? TryGetRule(UnitModel unit)
    {
        EnsureLoaded();
        return _rules is not null &&
               _rules.TryGetValue(unit.ClassDefinition.Id, out PromotionRule? rule)
            ? rule
            : null;
    }

    /// <summary>生成适合右侧 HUD 使用的转职条件说明。</summary>
    public static string RequirementText(UnitModel unit)
    {
        PromotionRule? rule = TryGetRule(unit);
        if (rule is null)
        {
            return $"{unit.ClassDefinition.DisplayName} 当前没有后续转职路线。";
        }

        return unit.Level >= rule.MinimumLevel
            ? $"可转职为 {rule.TargetClass.DisplayName}。"
            : $"{rule.TargetClass.DisplayName} 需要 Lv.{rule.MinimumLevel}，当前 Lv.{unit.Level}。";
    }

    /// <summary>首次使用时读取并验证所有转职路线。</summary>
    private static void EnsureLoaded()
    {
        if (_rules is not null)
        {
            return;
        }

        const string path = "res://data/promotions.json";
        if (!Godot.FileAccess.FileExists(path))
        {
            throw new FileNotFoundException($"找不到转职数据文件：{path}");
        }

        string json = Godot.FileAccess.GetFileAsString(path);
        PromotionFile? file = JsonSerializer.Deserialize<PromotionFile>(json, JsonOptions);
        if (file is null)
        {
            throw new InvalidOperationException($"无法解析转职数据文件：{path}");
        }

        Dictionary<string, PromotionRule> rules = new(StringComparer.OrdinalIgnoreCase);
        foreach (PromotionDto item in file.Promotions)
        {
            UnitClassDefinition targetClass = UnitClassCatalog.TryGet(item.ToClassId)
                ?? throw new InvalidOperationException($"转职路线引用了不存在的目标职业：{item.ToClassId}");

            // 起始职业也必须存在，尽早暴露数据拼写问题，而不是等玩家点击转职时才失败。
            if (UnitClassCatalog.TryGet(item.FromClassId) is null)
            {
                throw new InvalidOperationException($"转职路线引用了不存在的起始职业：{item.FromClassId}");
            }

            PromotionStatFloor floors = new()
            {
                MaxHp = Math.Max(1, item.StatFloors.MaxHp),
                Strength = Math.Max(0, item.StatFloors.Strength),
                Magic = Math.Max(0, item.StatFloors.Magic),
                Skill = Math.Max(0, item.StatFloors.Skill),
                Speed = Math.Max(0, item.StatFloors.Speed),
                Luck = Math.Max(0, item.StatFloors.Luck),
                Defense = Math.Max(0, item.StatFloors.Defense),
                Resistance = Math.Max(0, item.StatFloors.Resistance)
            };

            rules[item.FromClassId] = new PromotionRule(
                item.FromClassId,
                targetClass,
                item.MinimumLevel,
                item.ResetLevel,
                floors);
        }

        _rules = rules;
    }

    /// <summary>promotions.json 根对象。</summary>
    private sealed class PromotionFile
    {
        /// <summary>全部一对一转职路线。</summary>
        [JsonPropertyName("promotions")]
        public List<PromotionDto> Promotions { get; set; } = new();
    }

    /// <summary>单条转职路线 JSON 数据。</summary>
    private sealed class PromotionDto
    {
        /// <summary>起始职业 ID。</summary>
        [JsonPropertyName("from_class_id")]
        public string FromClassId { get; set; } = string.Empty;

        /// <summary>目标职业 ID。</summary>
        [JsonPropertyName("to_class_id")]
        public string ToClassId { get; set; } = string.Empty;

        /// <summary>最低转职等级。</summary>
        [JsonPropertyName("minimum_level")]
        public int MinimumLevel { get; set; } = 1;

        /// <summary>是否在转职后重置等级。</summary>
        [JsonPropertyName("reset_level")]
        public bool ResetLevel { get; set; } = true;

        /// <summary>目标职业属性最低基准。</summary>
        [JsonPropertyName("stat_floors")]
        public PromotionStatFloorDto StatFloors { get; set; } = new();
    }

    /// <summary>属性最低基准 JSON 数据。</summary>
    private sealed class PromotionStatFloorDto
    {
        /// <summary>最大生命最低值。</summary>
        [JsonPropertyName("max_hp")]
        public int MaxHp { get; set; } = 1;

        /// <summary>力量最低值。</summary>
        [JsonPropertyName("strength")]
        public int Strength { get; set; }

        /// <summary>魔力最低值。</summary>
        [JsonPropertyName("magic")]
        public int Magic { get; set; }

        /// <summary>技巧最低值。</summary>
        [JsonPropertyName("skill")]
        public int Skill { get; set; }

        /// <summary>速度最低值。</summary>
        [JsonPropertyName("speed")]
        public int Speed { get; set; }

        /// <summary>幸运最低值。</summary>
        [JsonPropertyName("luck")]
        public int Luck { get; set; }

        /// <summary>防御最低值。</summary>
        [JsonPropertyName("defense")]
        public int Defense { get; set; }

        /// <summary>魔防最低值。</summary>
        [JsonPropertyName("resistance")]
        public int Resistance { get; set; }
    }
}
