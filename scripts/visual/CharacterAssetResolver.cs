using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 定义人物美术在游戏中的使用槽位。
/// 地图小人、人物头像和战斗演出分别使用独立素材，方便逐步替换而不耦合战斗规则。
/// </summary>
public enum CharacterArtSlot
{
    Map,
    Portrait,
    Battle
}

/// <summary>
/// 统一解析人物正式美术资源路径。
/// 地图人物支持方向序列帧；战斗演出支持攻击、施法、受击、闪避和倒下等独立序列帧。
/// </summary>
public static class CharacterAssetResolver
{
    /// <summary>缓存已经成功加载的纹理，避免每帧重复读取 Godot ResourceLoader。</summary>
    private static readonly Dictionary<string, Texture2D> TextureCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>缓存某个动画状态实际拥有的连续帧数。</summary>
    private static readonly Dictionary<string, int> FrameCountCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 尝试为指定人物和用途加载单张纹理。
    /// 查找顺序为：人物实例 ID → 职业 ID → enemy_default/player_default。
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
            string cacheKey = $"map:{key}:{state}:{facing}";
            if (FrameCountCache.TryGetValue(cacheKey, out int cachedCount))
            {
                if (cachedCount > 0)
                {
                    return cachedCount;
                }

                continue;
            }

            int count = CountContinuousFrames(index => BuildMapFramePath(key, state, facing, index));
            FrameCountCache[cacheKey] = count;
            if (count > 0)
            {
                return count;
            }
        }

        return 0;
    }

    /// <summary>
    /// 加载地图人物的一张方向序列帧。
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
    /// 获取独立战斗演出某个状态实际存在的连续帧数。
    /// 战斗画面左右站位固定，因此这里不再要求四方向素材。
    /// </summary>
    public static int GetBattleFrameCount(UnitModel unit, CharacterAnimationState state)
    {
        foreach (string key in CandidateKeys(unit))
        {
            string cacheKey = $"battle:{key}:{state}";
            if (FrameCountCache.TryGetValue(cacheKey, out int cachedCount))
            {
                if (cachedCount > 0)
                {
                    return cachedCount;
                }

                continue;
            }

            int count = CountContinuousFrames(index => BuildBattleFramePath(key, state, index));
            FrameCountCache[cacheKey] = count;
            if (count > 0)
            {
                return count;
            }
        }

        return 0;
    }

    /// <summary>
    /// 加载独立战斗演出的一张序列帧。
    /// 缺少对应状态时调用方可以回退到 battle.png、portrait.png 或程序绘制人物。
    /// </summary>
    public static Texture2D? TryLoadBattleFrame(UnitModel unit, CharacterAnimationState state, int frameIndex)
    {
        foreach (string key in CandidateKeys(unit))
        {
            string path = BuildBattleFramePath(key, state, Math.Max(0, frameIndex));
            Texture2D? texture = TryLoadPath(path);
            if (texture is not null)
            {
                return texture;
            }
        }

        return null;
    }

    /// <summary>
    /// 扫描一组从 0 开始连续编号的帧，最多允许 24 帧。
    /// 设定上限可避免错误目录造成无界资源检查。
    /// </summary>
    private static int CountContinuousFrames(Func<int, string> pathFactory)
    {
        int count = 0;
        for (int frameIndex = 0; frameIndex < 24; frameIndex++)
        {
            if (!ResourceLoader.Exists(pathFactory(frameIndex)))
            {
                break;
            }

            count++;
        }

        return count;
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
    /// 构造地图方向序列帧路径。
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
    /// 构造独立战斗演出序列帧路径。
    /// 示例：res://assets/characters/adrian/battle/attack_2.png。
    /// </summary>
    private static string BuildBattleFramePath(string key, CharacterAnimationState state, int frameIndex)
    {
        string stateName = state.ToString().ToLowerInvariant();
        return $"res://assets/characters/{key}/battle/{stateName}_{frameIndex}.png";
    }

    /// <summary>
    /// 把运行时 ID 转成稳定安全的目录名。
    /// </summary>
    private static string NormalizeKey(string value)
    {
        return value.Trim().ToLowerInvariant().Replace(' ', '_');
    }
}
