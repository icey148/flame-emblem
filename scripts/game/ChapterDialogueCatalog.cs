using Godot;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlameEmblem.Game;

/// <summary>一条已经解析完成的章节对话。</summary>
public sealed class ChapterDialogueLine
{
    /// <summary>创建对话行。</summary>
    public ChapterDialogueLine(string speakerId, string speakerName, string side, string text)
    {
        SpeakerId = speakerId;
        SpeakerName = speakerName;
        Side = side;
        Text = text;
    }

    /// <summary>对应战场运行时单位 ID；找不到时仍可使用 SpeakerName 显示文字。</summary>
    public string SpeakerId { get; }

    /// <summary>界面显示的说话者名称。</summary>
    public string SpeakerName { get; }

    /// <summary>头像显示位置，当前支持 left / right。</summary>
    public string Side { get; }

    /// <summary>原创剧情文本。</summary>
    public string Text { get; }
}

/// <summary>某章节某个事件的一整段对话。</summary>
public sealed class ChapterDialogueSequence
{
    /// <summary>创建章节对话序列。</summary>
    public ChapterDialogueSequence(string chapterId, string eventId, IReadOnlyList<ChapterDialogueLine> lines)
    {
        ChapterId = chapterId;
        EventId = eventId;
        Lines = lines;
    }

    /// <summary>章节稳定 ID。</summary>
    public string ChapterId { get; }

    /// <summary>事件 ID，例如 start / victory。</summary>
    public string EventId { get; }

    /// <summary>按播放顺序排列的全部台词。</summary>
    public IReadOnlyList<ChapterDialogueLine> Lines { get; }
}

/// <summary>
/// 从 data/dialogues.json 读取章节剧情。
/// 对话数据与战斗代码分离，后续增加战中事件、支援对话或第三章时只需要扩数据和触发条件。
/// </summary>
public static class ChapterDialogueCatalog
{
    /// <summary>对话数据路径。</summary>
    private const string DialoguePath = "res://data/dialogues.json";

    /// <summary>JSON 读取选项。</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>按“章节 ID + 事件 ID”缓存已经解析的对话。</summary>
    private static Dictionary<string, ChapterDialogueSequence>? _sequences;

    /// <summary>查找指定章节事件对话；没有配置时返回 null。</summary>
    public static ChapterDialogueSequence? TryGet(string chapterId, string eventId)
    {
        EnsureLoaded();
        string key = BuildKey(chapterId, eventId);
        return _sequences is not null && _sequences.TryGetValue(key, out ChapterDialogueSequence? sequence)
            ? sequence
            : null;
    }

    /// <summary>首次使用时读取并验证全部章节对话。</summary>
    private static void EnsureLoaded()
    {
        if (_sequences is not null)
        {
            return;
        }

        if (!Godot.FileAccess.FileExists(DialoguePath))
        {
            GD.PushWarning($"找不到章节对话数据：{DialoguePath}");
            _sequences = new Dictionary<string, ChapterDialogueSequence>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        string json = Godot.FileAccess.GetFileAsString(DialoguePath);
        DialogueFileDto? file = JsonSerializer.Deserialize<DialogueFileDto>(json, JsonOptions);
        if (file is null)
        {
            throw new InvalidOperationException($"无法解析章节对话数据：{DialoguePath}");
        }

        _sequences = new Dictionary<string, ChapterDialogueSequence>(StringComparer.OrdinalIgnoreCase);
        foreach (DialogueSequenceDto sequenceDto in file.Sequences)
        {
            if (string.IsNullOrWhiteSpace(sequenceDto.ChapterId) ||
                string.IsNullOrWhiteSpace(sequenceDto.Event))
            {
                continue;
            }

            List<ChapterDialogueLine> lines = sequenceDto.Lines
                .Where(line => !string.IsNullOrWhiteSpace(line.Text))
                .Select(line => new ChapterDialogueLine(
                    line.SpeakerId,
                    string.IsNullOrWhiteSpace(line.SpeakerName) ? line.SpeakerId : line.SpeakerName,
                    line.Side.Equals("right", StringComparison.OrdinalIgnoreCase) ? "right" : "left",
                    line.Text))
                .ToList();

            if (lines.Count == 0)
            {
                continue;
            }

            ChapterDialogueSequence sequence = new(
                sequenceDto.ChapterId,
                sequenceDto.Event,
                lines);
            _sequences[BuildKey(sequence.ChapterId, sequence.EventId)] = sequence;
        }
    }

    /// <summary>生成大小写不敏感缓存键。</summary>
    private static string BuildKey(string chapterId, string eventId)
    {
        return $"{chapterId.Trim()}::{eventId.Trim()}";
    }

    /// <summary>对话 JSON 根对象。</summary>
    private sealed class DialogueFileDto
    {
        /// <summary>全部章节事件序列。</summary>
        [JsonPropertyName("sequences")]
        public List<DialogueSequenceDto> Sequences { get; set; } = new();
    }

    /// <summary>单个章节事件 JSON 对象。</summary>
    private sealed class DialogueSequenceDto
    {
        /// <summary>章节 ID。</summary>
        [JsonPropertyName("chapter_id")]
        public string ChapterId { get; set; } = string.Empty;

        /// <summary>事件 ID。</summary>
        [JsonPropertyName("event")]
        public string Event { get; set; } = string.Empty;

        /// <summary>台词列表。</summary>
        [JsonPropertyName("lines")]
        public List<DialogueLineDto> Lines { get; set; } = new();
    }

    /// <summary>单句台词 JSON 对象。</summary>
    private sealed class DialogueLineDto
    {
        /// <summary>战场单位 ID。</summary>
        [JsonPropertyName("speaker_id")]
        public string SpeakerId { get; set; } = string.Empty;

        /// <summary>显示名称。</summary>
        [JsonPropertyName("speaker_name")]
        public string SpeakerName { get; set; } = string.Empty;

        /// <summary>头像位置。</summary>
        [JsonPropertyName("side")]
        public string Side { get; set; } = "left";

        /// <summary>对话正文。</summary>
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }
}
