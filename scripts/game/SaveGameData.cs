using Godot;
using System.Text.Json;

namespace FlameEmblem.Game;

/// <summary>
/// 一份本地存档的根对象。
/// 单场战斗快照与跨章节战役状态同时保存，使读档既能精确恢复当前回合，也能恢复世界地图解锁与长期队伍成长。
/// </summary>
public sealed class SaveGameData
{
    /// <summary>当前存档格式版本；v2 开始包含完整战役快照。</summary>
    public const int CurrentSchemaVersion = 2;

    /// <summary>最早仍允许兼容读取的存档格式版本。</summary>
    public const int MinimumSupportedSchemaVersion = 1;

    /// <summary>写入存档时使用的格式版本。</summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>当前单场战斗章节稳定 ID。</summary>
    public string ChapterId { get; set; } = CampaignState.DefaultChapterId;

    /// <summary>当前单场战斗章节 JSON 路径；用于跨章节读档时自动进入正确地图。</summary>
    public string ChapterPath { get; set; } = CampaignState.DefaultChapterPath;

    /// <summary>当前玩家回合编号。</summary>
    public int Round { get; set; } = 1;

    /// <summary>最近一次战斗记录；读取后继续显示在右侧 HUD。</summary>
    public string LastBattleLog { get; set; } = "尚未发生战斗。";

    /// <summary>当前章节中的全部单位状态，包括敌军和已经倒下的单位。</summary>
    public List<UnitSaveData> Units { get; set; } = new();

    /// <summary>世界地图解锁、当前章节与跨章节玩家队伍状态。</summary>
    public CampaignProgressSaveData Campaign { get; set; } = new();
}

/// <summary>
/// 保存一个运行时单位可以变化的单场战斗状态。
/// 固定名称、成长率和规则定义仍然来自当前版本 JSON，避免存档注入旧规则数值。
/// </summary>
public sealed class UnitSaveData
{
    /// <summary>运行时单位稳定 ID。</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>当前职业 ID。</summary>
    public string ClassId { get; set; } = string.Empty;

    /// <summary>当前装备的武器或法术 ID。</summary>
    public string EquippedWeaponId { get; set; } = string.Empty;

    /// <summary>当前地图 X 坐标。</summary>
    public int X { get; set; }

    /// <summary>当前地图 Y 坐标。</summary>
    public int Y { get; set; }

    /// <summary>当前等级。</summary>
    public int Level { get; set; } = 1;

    /// <summary>当前等级内 EXP。</summary>
    public int Experience { get; set; }

    /// <summary>最大生命。</summary>
    public int MaxHp { get; set; } = 1;

    /// <summary>当前生命；0 表示该单位已经倒下。</summary>
    public int CurrentHp { get; set; } = 1;

    /// <summary>力量。</summary>
    public int Strength { get; set; }

    /// <summary>魔力。</summary>
    public int Magic { get; set; }

    /// <summary>技巧。</summary>
    public int Skill { get; set; }

    /// <summary>速度。</summary>
    public int Speed { get; set; }

    /// <summary>幸运。</summary>
    public int Luck { get; set; }

    /// <summary>防御。</summary>
    public int Defense { get; set; }

    /// <summary>魔防。</summary>
    public int Resistance { get; set; }

    /// <summary>本玩家回合是否已经行动；读取后可以继续未完成的回合。</summary>
    public bool HasActed { get; set; }
}

/// <summary>
/// 保存跨章节战役状态。
/// 这里不保存敌军与战斗坐标，只保存世界地图流程和玩家角色长期成长。
/// </summary>
public sealed class CampaignProgressSaveData
{
    /// <summary>当前准备进入或正在进行的章节 ID。</summary>
    public string CurrentChapterId { get; set; } = CampaignState.DefaultChapterId;

    /// <summary>当前章节数据路径。</summary>
    public string CurrentChapterPath { get; set; } = CampaignState.DefaultChapterPath;

    /// <summary>已经完成的章节 ID，用于恢复世界地图解锁。</summary>
    public List<string> CompletedChapterIds { get; set; } = new();

    /// <summary>玩家角色跨章节长期状态。</summary>
    public List<CampaignUnitSaveData> PlayerRoster { get; set; } = new();
}

/// <summary>一个玩家角色在章节之间需要长期保存的成长状态。</summary>
public sealed class CampaignUnitSaveData
{
    /// <summary>角色稳定 ID。</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>当前职业 ID。</summary>
    public string ClassId { get; set; } = string.Empty;

    /// <summary>当前装备 ID。</summary>
    public string WeaponId { get; set; } = string.Empty;

    /// <summary>当前等级。</summary>
    public int Level { get; set; } = 1;

    /// <summary>当前等级内 EXP。</summary>
    public int Experience { get; set; }

    /// <summary>最大 HP。</summary>
    public int MaxHp { get; set; } = 1;

    /// <summary>当前 HP；0 表示角色当前处于阵亡状态。</summary>
    public int CurrentHp { get; set; } = 1;

    /// <summary>力量。</summary>
    public int Strength { get; set; }

    /// <summary>魔力。</summary>
    public int Magic { get; set; }

    /// <summary>技巧。</summary>
    public int Skill { get; set; }

    /// <summary>速度。</summary>
    public int Speed { get; set; }

    /// <summary>幸运。</summary>
    public int Luck { get; set; }

    /// <summary>防御。</summary>
    public int Defense { get; set; }

    /// <summary>魔防。</summary>
    public int Resistance { get; set; }
}

/// <summary>
/// 负责把 SaveGameData 写入 Godot 的 user:// 目录以及从该目录读取。
/// 只使用本机 JSON 文件，不访问网络，也不参与战斗规则。
/// </summary>
public static class SaveGameService
{
    /// <summary>当前基础版单槽位存档路径。</summary>
    public const string SavePath = "user://save_slot_01.json";

    /// <summary>跨场景读档时暂存一次已经解析完成的单场战斗快照。</summary>
    private static SaveGameData? _pendingSceneRestore;

    /// <summary>存档 JSON 使用缩进，方便开发阶段人工检查损坏数据。</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>当前单槽位是否已经存在。</summary>
    public static bool HasSave => Godot.FileAccess.FileExists(SavePath);

    /// <summary>
    /// 把当前快照写入本地文件。
    /// 失败时返回 false 和可显示给玩家的错误文本，不让文件异常中断游戏。
    /// </summary>
    public static bool TrySave(SaveGameData data, out string message)
    {
        try
        {
            data.SchemaVersion = SaveGameData.CurrentSchemaVersion;
            string json = JsonSerializer.Serialize(data, JsonOptions);
            using Godot.FileAccess file = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Write)
                ?? throw new IOException($"无法打开存档路径：{SavePath}");
            file.StoreString(json);
            message = "进度已保存到本地槽位 1。";
            return true;
        }
        catch (Exception exception)
        {
            GD.PushError($"保存进度失败：{exception}");
            message = $"保存失败：{exception.Message}";
            return false;
        }
    }

    /// <summary>
    /// 从本地单槽位读取存档。
    /// v1 旧存档仍允许读取；缺少的战役字段会在内存中补成安全默认值，并在下一次保存时升级到 v2。
    /// </summary>
    public static bool TryLoad(out SaveGameData? data, out string message)
    {
        data = null;
        if (!HasSave)
        {
            message = "还没有本地存档。";
            return false;
        }

        try
        {
            string json = Godot.FileAccess.GetFileAsString(SavePath);
            data = JsonSerializer.Deserialize<SaveGameData>(json, JsonOptions);
            if (data is null)
            {
                message = "存档内容为空或无法解析。";
                return false;
            }

            if (data.SchemaVersion < SaveGameData.MinimumSupportedSchemaVersion ||
                data.SchemaVersion > SaveGameData.CurrentSchemaVersion)
            {
                message = $"存档版本 {data.SchemaVersion} 与当前版本 {SaveGameData.CurrentSchemaVersion} 不兼容。";
                data = null;
                return false;
            }

            NormalizeLoadedData(data);
            message = data.SchemaVersion == SaveGameData.CurrentSchemaVersion
                ? "存档读取成功。"
                : "旧版存档读取成功；下次保存会自动升级格式。";
            return true;
        }
        catch (Exception exception)
        {
            GD.PushError($"读取进度失败：{exception}");
            message = $"读取失败：{exception.Message}";
            data = null;
            return false;
        }
    }

    /// <summary>跨章节读档前暂存单场战斗快照，场景切换完成后由新战斗场景取走。</summary>
    public static void QueuePendingSceneRestore(SaveGameData data)
    {
        _pendingSceneRestore = data;
    }

    /// <summary>取走一次跨场景待恢复快照；读取后立即清空，避免重复套用。</summary>
    public static SaveGameData? TakePendingSceneRestore()
    {
        SaveGameData? pending = _pendingSceneRestore;
        _pendingSceneRestore = null;
        return pending;
    }

    /// <summary>只查看是否存在跨场景待恢复快照，不提前消费它。</summary>
    public static bool HasPendingSceneRestore => _pendingSceneRestore is not null;

    /// <summary>为 v1 或字段缺失的存档补齐安全默认值。</summary>
    private static void NormalizeLoadedData(SaveGameData data)
    {
        data.ChapterId = string.IsNullOrWhiteSpace(data.ChapterId)
            ? CampaignState.DefaultChapterId
            : data.ChapterId;
        data.ChapterPath = string.IsNullOrWhiteSpace(data.ChapterPath)
            ? ResolveFallbackChapterPath(data.ChapterId)
            : data.ChapterPath;
        data.Round = Math.Max(1, data.Round);
        data.Units ??= new List<UnitSaveData>();
        data.Campaign ??= new CampaignProgressSaveData();

        if (string.IsNullOrWhiteSpace(data.Campaign.CurrentChapterId))
        {
            data.Campaign.CurrentChapterId = data.ChapterId;
        }

        if (string.IsNullOrWhiteSpace(data.Campaign.CurrentChapterPath))
        {
            data.Campaign.CurrentChapterPath = data.ChapterPath;
        }

        data.Campaign.CompletedChapterIds ??= new List<string>();
        data.Campaign.PlayerRoster ??= new List<CampaignUnitSaveData>();
    }

    /// <summary>旧存档没有章节路径时，按当前数据目录命名规则生成安全回退路径。</summary>
    private static string ResolveFallbackChapterPath(string chapterId)
    {
        return string.IsNullOrWhiteSpace(chapterId)
            ? CampaignState.DefaultChapterPath
            : $"res://data/{chapterId}.json";
    }
}
