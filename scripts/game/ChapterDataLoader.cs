using Godot;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlameEmblem.Game;

/// <summary>
/// 表示已经完成数据合并、可以直接交给战斗场景使用的章节数据。
/// </summary>
public sealed class LoadedChapter
{
    /// <summary>
    /// 创建一份运行时章节数据。
    /// </summary>
    public LoadedChapter(
        string id,
        string title,
        int width,
        int height,
        string victoryCondition,
        string victoryTargetId,
        IReadOnlyDictionary<Vector2I, TerrainType> terrain,
        IReadOnlyList<UnitModel> units)
    {
        Id = id;
        Title = title;
        Width = width;
        Height = height;
        VictoryCondition = victoryCondition;
        VictoryTargetId = victoryTargetId;
        Terrain = terrain;
        Units = units;
    }

    /// <summary>章节稳定 ID。</summary>
    public string Id { get; }

    /// <summary>章节显示名称。</summary>
    public string Title { get; }

    /// <summary>地图宽度，单位为格。</summary>
    public int Width { get; }

    /// <summary>地图高度，单位为格。</summary>
    public int Height { get; }

    /// <summary>胜利条件类型；当前支持 defeat_target 与 rout。</summary>
    public string VictoryCondition { get; }

    /// <summary>defeat_target 胜利条件对应的目标单位实例 ID。</summary>
    public string VictoryTargetId { get; }

    /// <summary>非默认平地格的地形覆盖表。</summary>
    public IReadOnlyDictionary<Vector2I, TerrainType> Terrain { get; }

    /// <summary>本章节生成的全部战斗单位。</summary>
    public IReadOnlyList<UnitModel> Units { get; }
}

/// <summary>
/// 从 res://data 下的 JSON 文件加载职业、武器、角色模板和章节部署数据。
/// 这样关卡内容与 C# 战斗规则分离，后续扩角色和装备时不需要修改主场景。
/// </summary>
public static class ChapterDataLoader
{
    /// <summary>JSON 反序列化选项；允许文件里使用更自然的大小写。</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// 加载指定章节，并把职业、武器、角色模板、地图部署合并成运行时模型。
    /// </summary>
    public static LoadedChapter Load(string chapterPath)
    {
        ClassFile classFile = DeserializeFile<ClassFile>("res://data/classes.json");
        WeaponFile weaponFile = DeserializeFile<WeaponFile>("res://data/weapons.json");
        UnitFile unitFile = DeserializeFile<UnitFile>("res://data/units.json");
        ChapterFile chapterFile = DeserializeFile<ChapterFile>(chapterPath);

        Dictionary<string, UnitClassDefinition> classes = classFile.Classes.ToDictionary(
            item => item.Id,
            item => new UnitClassDefinition(
                item.Id,
                item.Name,
                Mathf.Max(1, item.Move),
                Mathf.Max(1, item.MinAttackRange),
                Mathf.Max(item.MinAttackRange, item.MaxAttackRange)),
            StringComparer.OrdinalIgnoreCase);

        Dictionary<string, WeaponDefinition> weapons = weaponFile.Weapons.ToDictionary(
            item => item.Id,
            item => new WeaponDefinition(
                item.Id,
                item.Name,
                ParseDamageType(item.DamageType),
                item.Might,
                item.Hit,
                item.Critical,
                item.MinRange,
                item.MaxRange,
                item.HpCost),
            StringComparer.OrdinalIgnoreCase);

        Dictionary<string, UnitTemplateDto> templates = unitFile.Units.ToDictionary(
            item => item.Id,
            StringComparer.OrdinalIgnoreCase);

        Dictionary<Vector2I, TerrainType> terrain = BuildTerrainMap(chapterFile);
        List<UnitModel> units = new();

        foreach (UnitSpawnDto spawn in chapterFile.Units)
        {
            if (!templates.TryGetValue(spawn.UnitId, out UnitTemplateDto? template))
            {
                throw new InvalidOperationException($"章节引用了不存在的角色模板：{spawn.UnitId}");
            }

            if (!classes.TryGetValue(template.ClassId, out UnitClassDefinition? classDefinition))
            {
                throw new InvalidOperationException($"角色 {template.Id} 引用了不存在的职业：{template.ClassId}");
            }

            if (!weapons.TryGetValue(template.WeaponId, out WeaponDefinition? weaponDefinition))
            {
                throw new InvalidOperationException($"角色 {template.Id} 引用了不存在的武器/法术：{template.WeaponId}");
            }

            UnitTeam team = spawn.Team.Equals("enemy", StringComparison.OrdinalIgnoreCase)
                ? UnitTeam.Enemy
                : UnitTeam.Player;

            StatGrowthDefinition growth = new(
                template.Growth.Hp,
                template.Growth.Strength,
                template.Growth.Magic,
                template.Growth.Skill,
                template.Growth.Speed,
                template.Growth.Luck,
                template.Growth.Defense,
                template.Growth.Resistance);

            // instance_id 用于同一角色模板在同一章节生成多个独立敌军实例。
            string runtimeId = string.IsNullOrWhiteSpace(spawn.InstanceId)
                ? template.Id
                : spawn.InstanceId;

            units.Add(new UnitModel(
                runtimeId,
                template.Name,
                team,
                new Vector2I(spawn.X, spawn.Y),
                classDefinition,
                weaponDefinition,
                template.Level,
                template.MaxHp,
                template.Strength,
                template.Magic,
                template.Skill,
                template.Speed,
                template.Luck,
                template.Defense,
                template.Resistance,
                growth));
        }

        return new LoadedChapter(
            chapterFile.Id,
            chapterFile.Title,
            Mathf.Max(1, chapterFile.Width),
            Mathf.Max(1, chapterFile.Height),
            string.IsNullOrWhiteSpace(chapterFile.VictoryCondition) ? "rout" : chapterFile.VictoryCondition,
            chapterFile.VictoryTargetId,
            terrain,
            units);
    }

    /// <summary>
    /// 把 JSON 中的伤害类型字符串转换成运行时枚举。
    /// 未识别值默认按物理处理，避免单个拼写错误导致整个章节无法进入。
    /// </summary>
    private static DamageType ParseDamageType(string value)
    {
        return value.Equals("magical", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("magic", StringComparison.OrdinalIgnoreCase)
            ? DamageType.Magical
            : DamageType.Physical;
    }

    /// <summary>
    /// 读取并反序列化一个 Godot res:// JSON 文件。
    /// 读取失败时直接抛出带路径的异常，方便开发阶段快速发现数据问题。
    /// </summary>
    private static T DeserializeFile<T>(string path) where T : class
    {
        if (!Godot.FileAccess.FileExists(path))
        {
            throw new FileNotFoundException($"找不到游戏数据文件：{path}");
        }

        string json = Godot.FileAccess.GetFileAsString(path);
        T? result = JsonSerializer.Deserialize<T>(json, JsonOptions);
        return result ?? throw new InvalidOperationException($"无法解析游戏数据文件：{path}");
    }

    /// <summary>
    /// 把章节 JSON 中的地形分组转换成按坐标查询的运行时字典。
    /// 没有写入字典的格子默认视为平地。
    /// </summary>
    private static Dictionary<Vector2I, TerrainType> BuildTerrainMap(ChapterFile chapterFile)
    {
        Dictionary<Vector2I, TerrainType> terrain = new();

        foreach (TerrainPatchDto patch in chapterFile.Terrain)
        {
            TerrainType type = TerrainRules.Parse(patch.Type);
            foreach (List<int> cell in patch.Cells)
            {
                // 坐标必须至少包含 x 与 y 两个值；无效条目忽略，不让单个坏格子拖垮整张地图。
                if (cell.Count < 2)
                {
                    continue;
                }

                Vector2I position = new(cell[0], cell[1]);
                if (position.X < 0 || position.X >= chapterFile.Width ||
                    position.Y < 0 || position.Y >= chapterFile.Height)
                {
                    continue;
                }

                terrain[position] = type;
            }
        }

        return terrain;
    }

    /// <summary>classes.json 的根对象。</summary>
    private sealed class ClassFile
    {
        /// <summary>全部职业条目。</summary>
        [JsonPropertyName("classes")]
        public List<ClassDto> Classes { get; set; } = new();
    }

    /// <summary>单个职业的 JSON 数据对象。</summary>
    private sealed class ClassDto
    {
        /// <summary>职业 ID。</summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>职业显示名称。</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>职业移动力。</summary>
        [JsonPropertyName("move")]
        public int Move { get; set; }

        /// <summary>职业默认最小攻击距离。</summary>
        [JsonPropertyName("min_attack_range")]
        public int MinAttackRange { get; set; } = 1;

        /// <summary>职业默认最大攻击距离。</summary>
        [JsonPropertyName("max_attack_range")]
        public int MaxAttackRange { get; set; } = 1;
    }

    /// <summary>weapons.json 的根对象。</summary>
    private sealed class WeaponFile
    {
        /// <summary>全部武器与法术条目。</summary>
        [JsonPropertyName("weapons")]
        public List<WeaponDto> Weapons { get; set; } = new();
    }

    /// <summary>单个武器或法术的数据对象。</summary>
    private sealed class WeaponDto
    {
        /// <summary>武器 ID。</summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>武器显示名称。</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>physical 或 magical。</summary>
        [JsonPropertyName("damage_type")]
        public string DamageType { get; set; } = "physical";

        /// <summary>基础威力。</summary>
        [JsonPropertyName("might")]
        public int Might { get; set; }

        /// <summary>基础命中率。</summary>
        [JsonPropertyName("hit")]
        public int Hit { get; set; }

        /// <summary>基础必杀率。</summary>
        [JsonPropertyName("critical")]
        public int Critical { get; set; }

        /// <summary>最小攻击距离。</summary>
        [JsonPropertyName("min_range")]
        public int MinRange { get; set; } = 1;

        /// <summary>最大攻击距离。</summary>
        [JsonPropertyName("max_range")]
        public int MaxRange { get; set; } = 1;

        /// <summary>每次使用需要支付的 HP。</summary>
        [JsonPropertyName("hp_cost")]
        public int HpCost { get; set; }
    }

    /// <summary>units.json 的根对象。</summary>
    private sealed class UnitFile
    {
        /// <summary>全部角色模板。</summary>
        [JsonPropertyName("units")]
        public List<UnitTemplateDto> Units { get; set; } = new();
    }

    /// <summary>角色模板 JSON 数据对象。</summary>
    private sealed class UnitTemplateDto
    {
        /// <summary>模板 ID。</summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>角色显示名称。</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>职业 ID。</summary>
        [JsonPropertyName("class_id")]
        public string ClassId { get; set; } = string.Empty;

        /// <summary>当前装备的武器/法术 ID。</summary>
        [JsonPropertyName("weapon_id")]
        public string WeaponId { get; set; } = string.Empty;

        /// <summary>初始等级。</summary>
        [JsonPropertyName("level")]
        public int Level { get; set; } = 1;

        /// <summary>最大生命。</summary>
        [JsonPropertyName("max_hp")]
        public int MaxHp { get; set; }

        /// <summary>力量。</summary>
        [JsonPropertyName("strength")]
        public int Strength { get; set; }

        /// <summary>魔力。</summary>
        [JsonPropertyName("magic")]
        public int Magic { get; set; }

        /// <summary>技巧。</summary>
        [JsonPropertyName("skill")]
        public int Skill { get; set; }

        /// <summary>速度。</summary>
        [JsonPropertyName("speed")]
        public int Speed { get; set; }

        /// <summary>幸运。</summary>
        [JsonPropertyName("luck")]
        public int Luck { get; set; }

        /// <summary>防御。</summary>
        [JsonPropertyName("defense")]
        public int Defense { get; set; }

        /// <summary>魔防。</summary>
        [JsonPropertyName("resistance")]
        public int Resistance { get; set; }

        /// <summary>升级成长率。</summary>
        [JsonPropertyName("growth")]
        public GrowthDto Growth { get; set; } = new();
    }

    /// <summary>角色成长率 JSON 数据对象。</summary>
    private sealed class GrowthDto
    {
        /// <summary>生命成长率。</summary>
        [JsonPropertyName("hp")]
        public int Hp { get; set; }

        /// <summary>力量成长率。</summary>
        [JsonPropertyName("strength")]
        public int Strength { get; set; }

        /// <summary>魔力成长率。</summary>
        [JsonPropertyName("magic")]
        public int Magic { get; set; }

        /// <summary>技巧成长率。</summary>
        [JsonPropertyName("skill")]
        public int Skill { get; set; }

        /// <summary>速度成长率。</summary>
        [JsonPropertyName("speed")]
        public int Speed { get; set; }

        /// <summary>幸运成长率。</summary>
        [JsonPropertyName("luck")]
        public int Luck { get; set; }

        /// <summary>防御成长率。</summary>
        [JsonPropertyName("defense")]
        public int Defense { get; set; }

        /// <summary>魔防成长率。</summary>
        [JsonPropertyName("resistance")]
        public int Resistance { get; set; }
    }

    /// <summary>章节 JSON 根对象。</summary>
    private sealed class ChapterFile
    {
        /// <summary>章节 ID。</summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>章节显示标题。</summary>
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        /// <summary>地图宽度。</summary>
        [JsonPropertyName("width")]
        public int Width { get; set; }

        /// <summary>地图高度。</summary>
        [JsonPropertyName("height")]
        public int Height { get; set; }

        /// <summary>胜利条件类型。</summary>
        [JsonPropertyName("victory_condition")]
        public string VictoryCondition { get; set; } = "rout";

        /// <summary>需要击败的目标单位实例 ID。</summary>
        [JsonPropertyName("victory_target_id")]
        public string VictoryTargetId { get; set; } = string.Empty;

        /// <summary>非平地地形分组。</summary>
        [JsonPropertyName("terrain")]
        public List<TerrainPatchDto> Terrain { get; set; } = new();

        /// <summary>单位部署列表。</summary>
        [JsonPropertyName("units")]
        public List<UnitSpawnDto> Units { get; set; } = new();
    }

    /// <summary>同类地形的一组坐标。</summary>
    private sealed class TerrainPatchDto
    {
        /// <summary>地形字符串类型。</summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = "plain";

        /// <summary>二维坐标数组，每项格式为 [x, y]。</summary>
        [JsonPropertyName("cells")]
        public List<List<int>> Cells { get; set; } = new();
    }

    /// <summary>章节中的一个单位部署点。</summary>
    private sealed class UnitSpawnDto
    {
        /// <summary>引用的角色模板 ID。</summary>
        [JsonPropertyName("unit_id")]
        public string UnitId { get; set; } = string.Empty;

        /// <summary>运行时实例 ID；重复敌军模板时必须不同。</summary>
        [JsonPropertyName("instance_id")]
        public string InstanceId { get; set; } = string.Empty;

        /// <summary>player 或 enemy。</summary>
        [JsonPropertyName("team")]
        public string Team { get; set; } = "player";

        /// <summary>部署 X 坐标。</summary>
        [JsonPropertyName("x")]
        public int X { get; set; }

        /// <summary>部署 Y 坐标。</summary>
        [JsonPropertyName("y")]
        public int Y { get; set; }
    }
}
