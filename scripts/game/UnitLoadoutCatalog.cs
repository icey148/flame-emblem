using Godot;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlameEmblem.Game;

/// <summary>
/// 读取玩家单位可切换的武器/法术列表。
/// 当前阶段先使用简单 loadouts.json；后续正式背包系统可以在不改战斗公式的前提下替换这个目录层。
/// </summary>
public static class UnitLoadoutCatalog
{
    /// <summary>JSON 反序列化选项。</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>按武器 ID 缓存完整定义。</summary>
    private static Dictionary<string, WeaponDefinition>? _weapons;

    /// <summary>按人物 ID 缓存允许使用的武器 ID。</summary>
    private static Dictionary<string, List<string>>? _loadouts;

    /// <summary>
    /// 返回指定单位允许切换的全部装备。
    /// 没有配置 loadout 时至少返回当前装备，保证敌军和临时单位也能正常工作。
    /// </summary>
    public static IReadOnlyList<WeaponDefinition> GetAvailableWeapons(UnitModel unit)
    {
        EnsureLoaded();

        if (_weapons is null || _loadouts is null ||
            !_loadouts.TryGetValue(unit.Id, out List<string>? weaponIds))
        {
            return new[] { unit.EquippedWeapon };
        }

        List<WeaponDefinition> result = new();
        foreach (string weaponId in weaponIds)
        {
            if (_weapons.TryGetValue(weaponId, out WeaponDefinition? weapon))
            {
                result.Add(weapon);
            }
        }

        // 数据表中如果误写了不存在的武器，仍然保留当前装备作为安全兜底。
        if (result.Count == 0)
        {
            result.Add(unit.EquippedWeapon);
        }

        return result;
    }

    /// <summary>
    /// 按稳定武器 ID 返回完整定义。
    /// 存档只写武器 ID，读取时必须重新引用当前 data/weapons.json 的规则数据，不能信任存档自带的战斗参数。
    /// </summary>
    public static WeaponDefinition? TryGetWeapon(string weaponId)
    {
        EnsureLoaded();
        if (_weapons is null || string.IsNullOrWhiteSpace(weaponId))
        {
            return null;
        }

        return _weapons.TryGetValue(weaponId, out WeaponDefinition? weapon)
            ? weapon
            : null;
    }

    /// <summary>
    /// 把单位切换到 loadout 中的下一件装备，并返回切换后的装备。
    /// </summary>
    public static WeaponDefinition CycleNext(UnitModel unit)
    {
        IReadOnlyList<WeaponDefinition> available = GetAvailableWeapons(unit);
        if (available.Count <= 1)
        {
            return unit.EquippedWeapon;
        }

        int currentIndex = -1;
        for (int index = 0; index < available.Count; index++)
        {
            if (available[index].Id.Equals(unit.EquippedWeapon.Id, StringComparison.OrdinalIgnoreCase))
            {
                currentIndex = index;
                break;
            }
        }

        int nextIndex = currentIndex < 0 ? 0 : (currentIndex + 1) % available.Count;
        unit.Equip(available[nextIndex]);
        return unit.EquippedWeapon;
    }

    /// <summary>
    /// 首次使用时加载 weapons.json 和 loadouts.json。
    /// </summary>
    private static void EnsureLoaded()
    {
        if (_weapons is not null && _loadouts is not null)
        {
            return;
        }

        WeaponFile weaponFile = DeserializeFile<WeaponFile>("res://data/weapons.json");
        LoadoutFile loadoutFile = DeserializeFile<LoadoutFile>("res://data/loadouts.json");

        _weapons = weaponFile.Weapons.ToDictionary(
            dto => dto.Id,
            dto => new WeaponDefinition(
                dto.Id,
                dto.Name,
                ParseDamageType(dto.DamageType),
                dto.Might,
                dto.Hit,
                dto.Critical,
                dto.MinRange,
                dto.MaxRange,
                dto.HpCost),
            StringComparer.OrdinalIgnoreCase);

        _loadouts = loadoutFile.Loadouts.ToDictionary(
            dto => dto.UnitId,
            dto => dto.WeaponIds,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 把 JSON 中的伤害类型转换成战斗枚举。
    /// </summary>
    private static DamageType ParseDamageType(string value)
    {
        return value.Equals("magical", StringComparison.OrdinalIgnoreCase)
            ? DamageType.Magical
            : DamageType.Physical;
    }

    /// <summary>
    /// 读取并反序列化 res:// 下的 JSON 文件。
    /// </summary>
    private static T DeserializeFile<T>(string path) where T : class
    {
        if (!Godot.FileAccess.FileExists(path))
        {
            throw new FileNotFoundException($"找不到装备数据文件：{path}");
        }

        string json = Godot.FileAccess.GetFileAsString(path);
        T? result = JsonSerializer.Deserialize<T>(json, JsonOptions);
        return result ?? throw new InvalidOperationException($"无法解析装备数据文件：{path}");
    }

    /// <summary>weapons.json 根对象。</summary>
    private sealed class WeaponFile
    {
        /// <summary>全部武器定义。</summary>
        [JsonPropertyName("weapons")]
        public List<WeaponDto> Weapons { get; set; } = new();
    }

    /// <summary>单件武器 JSON 数据。</summary>
    private sealed class WeaponDto
    {
        /// <summary>武器 ID。</summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>显示名称。</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>physical 或 magical。</summary>
        [JsonPropertyName("damage_type")]
        public string DamageType { get; set; } = "physical";

        /// <summary>威力。</summary>
        [JsonPropertyName("might")]
        public int Might { get; set; }

        /// <summary>基础命中。</summary>
        [JsonPropertyName("hit")]
        public int Hit { get; set; }

        /// <summary>基础必杀。</summary>
        [JsonPropertyName("critical")]
        public int Critical { get; set; }

        /// <summary>最小射程。</summary>
        [JsonPropertyName("min_range")]
        public int MinRange { get; set; } = 1;

        /// <summary>最大射程。</summary>
        [JsonPropertyName("max_range")]
        public int MaxRange { get; set; } = 1;

        /// <summary>使用时 HP 消耗。</summary>
        [JsonPropertyName("hp_cost")]
        public int HpCost { get; set; }
    }

    /// <summary>loadouts.json 根对象。</summary>
    private sealed class LoadoutFile
    {
        /// <summary>全部人物装备表。</summary>
        [JsonPropertyName("loadouts")]
        public List<LoadoutDto> Loadouts { get; set; } = new();
    }

    /// <summary>单个人物可使用装备列表。</summary>
    private sealed class LoadoutDto
    {
        /// <summary>人物运行时/模板 ID。</summary>
        [JsonPropertyName("unit_id")]
        public string UnitId { get; set; } = string.Empty;

        /// <summary>按切换顺序排列的武器 ID。</summary>
        [JsonPropertyName("weapon_ids")]
        public List<string> WeaponIds { get; set; } = new();
    }
}
