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
/// 地图人物既支持旧版单张 map.png，也支持按状态/方向拆分的多帧像素动画。
/// </summary>
public static class CharacterAssetResolver
{
    /// <summary>缓存已经成功加载的纹理，避免每帧重复读取 Godot ResourceLoader。</summary>
    private static readonly Dictionary<string, Texture2D> TextureCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>缓存某个状态/方向实际拥有的动画帧数。</summary>
    private static readonly Dictionary<string, int> FrameCountCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 尝试为指定人物和用途加载单张纹理。
    /// 查找顺序为：人物实例 ID → 职业 ID → enemy_default/player_default；这样普通敌军不需要每个实例都准备一套素材。
    /// </summary>
    public static Texture2D? TryLoad(UnitModel unit, CharacterArtSlot slot)
    {
        foreach (string key in CandidateKeys(unit))
        {
            string path = BuildPath(key, slot);
            Texture2D? texture = TryLoadPath(path);
            if (texture is not null)
            {
                return texture;
            }
        }

        return null;
    }

    /// <summary>
    /// 获取地图人物某个动画状态和方向实际存在的帧数。
    /// 文件从 0 开始连续编号；遇到第一张缺失帧后停止扫描。
    /// </summary>
    public static int GetMapFrameCount(UnitModel unit, CharacterAnimationState state, CharacterFacing facing)
    {
        foreach (string key in CandidateKeys(unit))
        {
            string cacheKey = $"{key}:{state}:{facing}";
            if (FrameCountCache.TryGetValue(cacheKey, out int cachedCount))
            {
                if (cachedCount > 0)
                {
                    return cachedCount;
                }

                continue;
            }

            int count = 0;
            for (int frameIndex = 0; frameIndex < 16; frameIndex++)
            {
                string path = BuildMapFramePath(key, state, facing, frameIndex);
                if (!ResourceLoader.Exists(path))
                {
                    break;
                }

                count++;
            }

            FrameCountCache[cacheKey] = count;
            if (count > 0)
            {
                return count;
            }
        }

        return 0;
    }

    /// <summary>
    /// 加载地图人物的一张序列帧。
    /// 调用方应先用 GetMapFrameCount 确认该动画存在；缺失时返回 null 并继续使用单图或程序占位回退。
    /// </summary>
    public static Texture2D? TryLoadMapFrame(
        UnitModel unit,
        CharacterAnimationState state,
        CharacterFacing facing,
        int frameIndex)
    {
        foreach (string key in CandidateKeys(unit))
        {
            string path = BuildMapFramePath(key, state, facing, Math.Max(0, frameIndex));
            Texture2D? texture = TryLoadPath(path);
            if (texture is not null)
            {
                return texture;
            }
        }

        return null;
    }

    /// <summary>
    /// 从指定 res:// 路径加载纹理并写入缓存。
    /// 正式素材尚未加入时不输出错误日志，让程序自然回退到占位表现。
    /// </summary>
    private static Texture2D? TryLoadPath(string path)
    {
        if (TextureCache.TryGetValue(path, out Texture2D? cached))
        {
            return cached;
        }

        if (!ResourceLoader.Exists(path))
        {
            return null;
        }

        Texture2D? texture = GD.Load<Texture2D>(path);
        if (texture is null)
        {
            return null;
        }

        TextureCache[path] = texture;
        return texture;
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
    /// 按统一目录约定构造单张人物资源路径。
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
    /// 构造地图序列帧路径。
    /// 示例：res://assets/characters/adrian/map/walk_left_1.png。
    /// </summary>
    private static string BuildMapFramePath(
        string key,
        CharacterAnimationState state,
        CharacterFacing facing,
        int frameIndex)
    {
        string stateName = state.ToString().ToLowerInvariant();
        string facingName = facing.ToString().ToLowerInvariant();
        return $"res://assets/characters/{key}/map/{stateName}_{facingName}_{frameIndex}.png";
    }

    /// <summary>
    /// 把运行时 ID 转成稳定安全的目录名。
    /// </summary>
    private static string NormalizeKey(string value)
    {
        return value.Trim().ToLowerInvariant().Replace(' ', '_');
    }
}
