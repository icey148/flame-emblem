using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 定义人物美术在游戏中的使用槽位。
/// 地图小人、人物头像和战斗演出分别使用独立素材，方便未来逐步替换而不是一次性完成全部美术。
/// </summary>
public enum CharacterArtSlot
{
    Map,
    Portrait,
    Battle
}

/// <summary>
/// 统一解析人物正式美术资源路径。
/// 当前仓库还没有正式人物 PNG，因此加载失败时调用方会自动退回程序绘制占位模型。
/// </summary>
public static class CharacterAssetResolver
{
    /// <summary>缓存已经成功加载的纹理，避免每帧重复读取 Godot ResourceLoader。</summary>
    private static readonly Dictionary<string, Texture2D> TextureCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 尝试为指定人物和用途加载纹理。
    /// 查找顺序为：人物实例 ID → 职业 ID → enemy_default/player_default；这样普通敌军不需要每个实例都准备一套素材。
    /// </summary>
    public static Texture2D? TryLoad(UnitModel unit, CharacterArtSlot slot)
    {
        foreach (string key in CandidateKeys(unit))
        {
            string path = BuildPath(key, slot);
            if (TextureCache.TryGetValue(path, out Texture2D? cached))
            {
                return cached;
            }

            // ResourceLoader.Exists 先检查资源是否存在，避免正式素材尚未加入时产生无意义的 Godot 错误日志。
            if (!ResourceLoader.Exists(path))
            {
                continue;
            }

            Texture2D? texture = GD.Load<Texture2D>(path);
            if (texture is null)
            {
                continue;
            }

            TextureCache[path] = texture;
            return texture;
        }

        return null;
    }

    /// <summary>
    /// 生成可能对应当前人物的素材目录键。
    /// </summary>
    private static IEnumerable<string> CandidateKeys(UnitModel unit)
    {
        yield return NormalizeKey(unit.Id);
        yield return NormalizeKey(unit.ClassDefinition.Id);
        yield return unit.Team == UnitTeam.Enemy ? "enemy_default" : "player_default";
    }

    /// <summary>
    /// 按统一目录约定构造资源路径。
    /// 正式素材只需要放进 assets/characters/&lt;key&gt;/ 对应文件名即可被自动发现。
    /// </summary>
    private static string BuildPath(string key, CharacterArtSlot slot)
    {
        string fileName = slot switch
        {
            CharacterArtSlot.Map => "map.png",
            CharacterArtSlot.Portrait => "portrait.png",
            CharacterArtSlot.Battle => "battle.png",
            _ => "portrait.png"
        };

        return $"res://assets/characters/{key}/{fileName}";
    }

    /// <summary>
    /// 把运行时 ID 转成稳定安全的目录名。
    /// </summary>
    private static string NormalizeKey(string value)
    {
        return value.Trim().ToLowerInvariant().Replace(' ', '_');
    }
}
