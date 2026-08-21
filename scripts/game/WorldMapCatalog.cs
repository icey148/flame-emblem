using Godot;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlameEmblem.Game;

/// <summary>世界地图节点类型。</summary>
public enum WorldMapNodeType
{
    Battle,
    Shrine,
    FutureBattle
}

/// <summary>一个可以显示在世界地图上的节点。</summary>
public sealed class WorldMapNodeDefinition
{
    /// <summary>创建世界地图节点。</summary>
    public WorldMapNodeDefinition(
        string id,
        string displayName,
        WorldMapNodeType type,
        string chapterId,
        string chapterPath,
        Vector2 position,
        IReadOnlyList<string> requiredCompletedChapterIds)
    {
        Id = id;
        DisplayName = displayName;
        Type = type;
        ChapterId = chapterId;
        ChapterPath = chapterPath;
        Position = position;
        RequiredCompletedChapterIds = requiredCompletedChapterIds;
    }

    /// <summary>稳定节点 ID。</summary>
    public string Id { get; }

    /// <summary>界面显示名称。</summary>
    public string DisplayName { get; }

    /// <summary>节点功能类型。</summary>
    public WorldMapNodeType Type { get; }

    /// <summary>战斗节点对应章节 ID。</summary>
    public string ChapterId { get; }

    /// <summary>战斗节点对应章节数据路径。</summary>
    public string ChapterPath { get; }

    /// <summary>节点在 1280×720 世界地图上的像素坐标。</summary>
    public Vector2 Position { get; }

    /// <summary>解锁节点前必须已经完成的章节。</summary>
    public IReadOnlyList<string> RequiredCompletedChapterIds { get; }

    /// <summary>按当前战役状态判断节点是否已经解锁。</summary>
    public bool IsUnlocked => CampaignState.AreRequirementsMet(RequiredCompletedChapterIds);
}

/// <summary>已经解析好的世界地图数据。</summary>
public sealed class WorldMapDefinition
{
    /// <summary>创建世界地图定义。</summary>
    public WorldMapDefinition(string title, IReadOnlyList<WorldMapNodeDefinition> nodes)
    {
        Title = title;
        Nodes = nodes;
    }

    /// <summary>地图区域标题。</summary>
    public string Title { get; }

    /// <summary>全部地图节点。</summary>
    public IReadOnlyList<WorldMapNodeDefinition> Nodes { get; }
}

/// <summary>
/// 从 data/world_map.json 读取世界地图结构。
/// 解锁条件和节点位置由数据控制，后续新增路线时不需要修改世界地图脚本。
/// </summary>
public static class WorldMapCatalog
{
    /// <summary>世界地图数据路径。</summary>
    private const string WorldMapPath = "res://data/world_map.json";

    /// <summary>JSON 读取选项。</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>缓存已经读取的世界地图。</summary>
    private static WorldMapDefinition? _cached;

    /// <summary>读取当前世界地图。</summary>
    public static WorldMapDefinition Load()
    {
        if (_cached is not null)
        {
            return _cached;
        }

        if (!Godot.FileAccess.FileExists(WorldMapPath))
        {
            throw new FileNotFoundException($"找不到世界地图数据：{WorldMapPath}");
        }

        string json = Godot.FileAccess.GetFileAsString(WorldMapPath);
        WorldMapFileDto? file = JsonSerializer.Deserialize<WorldMapFileDto>(json, JsonOptions);
        if (file is null)
        {
            throw new InvalidOperationException("世界地图数据无法解析。");
        }

        List<WorldMapNodeDefinition> nodes = new();
        foreach (WorldMapNodeDto node in file.Nodes)
        {
            nodes.Add(new WorldMapNodeDefinition(
                node.Id,
                node.Name,
                ParseNodeType(node.Type),
                node.ChapterId,
                node.ChapterPath,
                new Vector2(node.X, node.Y),
                node.RequiresCompleted));
        }

        _cached = new WorldMapDefinition(
            string.IsNullOrWhiteSpace(file.Title) ? "世界地图" : file.Title,
            nodes);
        return _cached;
    }

    /// <summary>把 JSON 字符串转换为地图节点类型。</summary>
    private static WorldMapNodeType ParseNodeType(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "shrine" => WorldMapNodeType.Shrine,
            "future_battle" => WorldMapNodeType.FutureBattle,
            _ => WorldMapNodeType.Battle
        };
    }

    /// <summary>world_map.json 根对象。</summary>
    private sealed class WorldMapFileDto
    {
        /// <summary>区域标题。</summary>
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        /// <summary>节点列表。</summary>
        [JsonPropertyName("nodes")]
        public List<WorldMapNodeDto> Nodes { get; set; } = new();
    }

    /// <summary>单个世界地图节点 JSON 数据。</summary>
    private sealed class WorldMapNodeDto
    {
        /// <summary>节点 ID。</summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>显示名称。</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>battle / shrine / future_battle。</summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = "battle";

        /// <summary>章节 ID。</summary>
        [JsonPropertyName("chapter_id")]
        public string ChapterId { get; set; } = string.Empty;

        /// <summary>章节数据路径。</summary>
        [JsonPropertyName("chapter_path")]
        public string ChapterPath { get; set; } = string.Empty;

        /// <summary>地图 X 坐标。</summary>
        [JsonPropertyName("x")]
        public float X { get; set; }

        /// <summary>地图 Y 坐标。</summary>
        [JsonPropertyName("y")]
        public float Y { get; set; }

        /// <summary>前置完成章节列表。</summary>
        [JsonPropertyName("requires_completed")]
        public List<string> RequiresCompleted { get; set; } = new();
    }
}
