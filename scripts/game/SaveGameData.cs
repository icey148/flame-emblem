using Godot;
using System.Text.Json;

namespace FlameEmblem.Game;

/// <summary>
/// 一份本地存档的根对象。
/// 当前第一版只保存单章节战斗继续所需的数据；后续世界地图、转职和背包可以通过提高 SchemaVersion 继续扩展。
/// </summary>
public sealed class SaveGameData
{
    /// <summary>当前存档格式版本，用于以后安全迁移旧存档。</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>写入存档时使用的格式版本。</summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>当前章节稳定 ID。</summary>
    public string ChapterId { get; set; } = "chapter_01";

    /// <summary>当前玩家回合编号。</summary>
    public int Round { get; set; } = 1;

    /// <summary>最近一次战斗记录；读取后继续显示在右侧 HUD。</summary>
    public string LastBattleLog { get; set; } = "尚未发生战斗。";

    /// <summary>章节中的全部单位状态，包括已经倒下的单位。</summary>
    public List<UnitSaveData> Units { get; set; } = new();
}

/// <summary>
/// 保存一个运行时单位可以变化的状态。
/// 固定名称、成长率和职业基础数据仍然来自原始 JSON，避免把整份角色定义重复写进存档。
/// </summary>
public sealed class UnitSaveData
{
    /// <summary>运行时单位稳定 ID。</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>职业 ID；当前主要用于读取时验证存档与章节数据是否兼容。</summary>
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
/// 负责把 SaveGameData 写入 Godot 的 user:// 目录以及从该目录读取。
/// 只使用本机 JSON 文件，不访问网络，也不参与战斗规则。
/// </summary>
public static class SaveGameService
{
    /// <summary>第一版单槽位存档路径。</summary>
    public const string SavePath = "user://save_slot_01.json";

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
    /// 只负责 JSON 与格式版本检查，具体单位状态恢复由战斗场景协调器执行。
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

            if (data.SchemaVersion != SaveGameData.CurrentSchemaVersion)
            {
                message = $"存档版本 {data.SchemaVersion} 与当前版本 {SaveGameData.CurrentSchemaVersion} 不兼容。";
                data = null;
                return false;
            }

            message = "存档读取成功。";
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
}
