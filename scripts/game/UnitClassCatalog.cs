using Godot;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlameEmblem.Game;

/// <summary>
/// 统一读取 data/classes.json 中的职业定义。
/// 章节加载、转职和存档恢复都可以通过稳定职业 ID 查找同一份规则数据。
/// </summary>
public static class UnitClassCatalog
{
    /// <summary>职业数据首次读取后按 ID 缓存，避免重复解析 JSON。</summary>
    private static Dictionary<string, UnitClassDefinition>? _classes;

    /// <summary>职业 JSON 使用宽松大小写，保持与现有数据加载器一致。</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>按职业 ID 查找定义；不存在时返回 null。</summary>
    public static UnitClassDefinition? TryGet(string classId)
    {
        EnsureLoaded();
        return _classes is not null && _classes.TryGetValue(classId, out UnitClassDefinition? definition)
            ? definition
            : null;
    }

    /// <summary>读取全部职业定义，供后续转职树或职业图鉴使用。</summary>
    public static IReadOnlyCollection<UnitClassDefinition> GetAll()
    {
        EnsureLoaded();
        return _classes?.Values.ToArray() ?? Array.Empty<UnitClassDefinition>();
    }

    /// <summary>首次使用时把 classes.json 转换成运行时职业定义。</summary>
    private static void EnsureLoaded()
    {
        if (_classes is not null)
        {
            return;
        }

        const string path = "res://data/classes.json";
        if (!Godot.FileAccess.FileExists(path))
        {
            throw new FileNotFoundException($"找不到职业数据文件：{path}");
        }

        string json = Godot.FileAccess.GetFileAsString(path);
        ClassFile? file = JsonSerializer.Deserialize<ClassFile>(json, JsonOptions);
        if (file is null)
        {
            throw new InvalidOperationException($"无法解析职业数据文件：{path}");
        }

        _classes = file.Classes.ToDictionary(
            item => item.Id,
            item => new UnitClassDefinition(
                item.Id,
                item.Name,
                Mathf.Max(1, item.Move),
                Mathf.Max(1, item.MinAttackRange),
                Mathf.Max(item.MinAttackRange, item.MaxAttackRange)),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>classes.json 根对象。</summary>
    private sealed class ClassFile
    {
        /// <summary>全部职业条目。</summary>
        [JsonPropertyName("classes")]
        public List<ClassDto> Classes { get; set; } = new();
    }

    /// <summary>单个职业的 JSON 字段。</summary>
    private sealed class ClassDto
    {
        /// <summary>稳定职业 ID。</summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>界面显示名称。</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>基础移动力。</summary>
        [JsonPropertyName("move")]
        public int Move { get; set; } = 1;

        /// <summary>职业层预留最小攻击距离。</summary>
        [JsonPropertyName("min_attack_range")]
        public int MinAttackRange { get; set; } = 1;

        /// <summary>职业层预留最大攻击距离。</summary>
        [JsonPropertyName("max_attack_range")]
        public int MaxAttackRange { get; set; } = 1;
    }
}
