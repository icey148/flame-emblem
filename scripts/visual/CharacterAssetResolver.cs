using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>人物美术在游戏中的三个固定用途。</summary>
public enum CharacterArtSlot
{
    Map,
    Portrait,
    Battle
}

/// <summary>
/// 统一解析人物正式美术。
/// 兼容旧独立 PNG，同时优先支持每个角色一张 512×576 的 sheet.svg：
/// 左上 64×64 为头像，左侧为 32×32 地图帧，右侧为 96×96 战斗帧。
/// </summary>
public static class CharacterAssetResolver
{
    /// <summary>缓存已加载纹理和图集切片。</summary>
    private static readonly Dictionary<string, Texture2D> TextureCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>缓存动画帧数，避免每帧重复检查资源。</summary>
    private static readonly Dictionary<string, int> FrameCountCache = new(StringComparer.OrdinalIgnoreCase);

    private const int SheetWidth = 512;
    private const int SheetHeight = 576;
    private const int MapOriginY = 64;
    private const int MapCell = 32;
    private const int BattleOriginX = 112;
    private const int BattleCell = 96;
    private static readonly Rect2 PortraitRegion = new(0, 0, 64, 64);

    /// <summary>按人物 ID、职业 ID、阵营默认值的顺序寻找单张美术。</summary>
    public static Texture2D? TryLoad(UnitModel unit, CharacterArtSlot slot)
    {
        foreach (string key in CandidateKeys(unit))
        {
            // 旧项目若已经放入独立 PNG，仍然拥有最高优先级。
            Texture2D? legacy = TryLoadPath(BuildLegacyPath(key, slot));
            if (legacy is not null)
            {
                return legacy;
            }

            Rect2 region = slot switch
            {
                CharacterArtSlot.Portrait => PortraitRegion,
                CharacterArtSlot.Map => BuildMapRegion(CharacterAnimationState.Idle, CharacterFacing.Down, 0),
                CharacterArtSlot.Battle => BuildBattleRegion(CharacterAnimationState.Idle, 0),
                _ => PortraitRegion
            };
            Texture2D? sheet = TryLoadSheetRegion(key, $"slot:{slot}", region);
            if (sheet is not null)
            {
                return sheet;
            }
        }

        return null;
    }

    /// <summary>返回地图待机/行走动画实际可用的帧数。</summary>
    public static int GetMapFrameCount(UnitModel unit, CharacterAnimationState state, CharacterFacing facing)
    {
        foreach (string key in CandidateKeys(unit))
        {
            string cacheKey = $"map:{key}:{state}:{facing}";
            if (FrameCountCache.TryGetValue(cacheKey, out int cached))
            {
                if (cached > 0)
                {
                    return cached;
                }
                continue;
            }

            int count = CountContinuousFrames(index => BuildLegacyMapFramePath(key, state, facing, index));
            if (count <= 0 && HasValidSheet(key))
            {
                count = state switch
                {
                    CharacterAnimationState.Idle => 2,
                    CharacterAnimationState.Walk => 3,
                    _ => 0
                };
            }

            FrameCountCache[cacheKey] = count;
            if (count > 0)
            {
                return count;
            }
        }

        return 0;
    }

    /// <summary>加载一张地图方向帧；旧独立 PNG 不存在时从正式图集切片。</summary>
    public static Texture2D? TryLoadMapFrame(
        UnitModel unit,
        CharacterAnimationState state,
        CharacterFacing facing,
        int frameIndex)
    {
        foreach (string key in CandidateKeys(unit))
        {
            int safeIndex = Math.Max(0, frameIndex);
            Texture2D? legacy = TryLoadPath(BuildLegacyMapFramePath(key, state, facing, safeIndex));
            if (legacy is not null)
            {
                return legacy;
            }

            int count = state switch
            {
                CharacterAnimationState.Idle => 2,
                CharacterAnimationState.Walk => 3,
                _ => 0
            };
            if (count <= 0 || !HasValidSheet(key))
            {
                continue;
            }

            safeIndex = Math.Clamp(safeIndex, 0, count - 1);
            Texture2D? sheet = TryLoadSheetRegion(
                key,
                $"map:{state}:{facing}:{safeIndex}",
                BuildMapRegion(state, facing, safeIndex));
            if (sheet is not null)
            {
                return sheet;
            }
        }

        return null;
    }

    /// <summary>返回正式横向战斗动作的帧数。</summary>
    public static int GetBattleFrameCount(UnitModel unit, CharacterAnimationState state)
    {
        foreach (string key in CandidateKeys(unit))
        {
            string cacheKey = $"battle:{key}:{state}";
            if (FrameCountCache.TryGetValue(cacheKey, out int cached))
            {
                if (cached > 0)
                {
                    return cached;
                }
                continue;
            }

            int count = CountContinuousFrames(index => BuildLegacyBattleFramePath(key, state, index));
            if (count <= 0 && HasValidSheet(key))
            {
                count = BattleFrameCount(state);
            }

            FrameCountCache[cacheKey] = count;
            if (count > 0)
            {
                return count;
            }
        }

        return 0;
    }

    /// <summary>加载战斗动作帧；正式图集提供待机、攻击、施法、受击、闪避与倒下。</summary>
    public static Texture2D? TryLoadBattleFrame(UnitModel unit, CharacterAnimationState state, int frameIndex)
    {
        foreach (string key in CandidateKeys(unit))
        {
            int safeIndex = Math.Max(0, frameIndex);
            Texture2D? legacy = TryLoadPath(BuildLegacyBattleFramePath(key, state, safeIndex));
            if (legacy is not null)
            {
                return legacy;
            }

            int count = BattleFrameCount(state);
            if (count <= 0 || !HasValidSheet(key))
            {
                continue;
            }

            safeIndex = Math.Clamp(safeIndex, 0, count - 1);
            Texture2D? sheet = TryLoadSheetRegion(
                key,
                $"battle:{state}:{safeIndex}",
                BuildBattleRegion(state, safeIndex));
            if (sheet is not null)
            {
                return sheet;
            }
        }

        return null;
    }

    /// <summary>统一图集每个战斗状态的固定帧数。</summary>
    private static int BattleFrameCount(CharacterAnimationState state)
    {
        return state switch
        {
            CharacterAnimationState.Idle => 2,
            CharacterAnimationState.Attack => 4,
            CharacterAnimationState.Cast => 4,
            CharacterAnimationState.Hit => 2,
            CharacterAnimationState.Dodge => 2,
            CharacterAnimationState.Defeat => 3,
            _ => 0
        };
    }

    /// <summary>计算地图图集中的整数切片区域。</summary>
    private static Rect2 BuildMapRegion(CharacterAnimationState state, CharacterFacing facing, int frameIndex)
    {
        int stateRow = state == CharacterAnimationState.Walk ? 4 : 0;
        int row = stateRow + (int)facing;
        return new Rect2(frameIndex * MapCell, MapOriginY + row * MapCell, MapCell, MapCell);
    }

    /// <summary>计算战斗图集中的整数切片区域。</summary>
    private static Rect2 BuildBattleRegion(CharacterAnimationState state, int frameIndex)
    {
        int row = state switch
        {
            CharacterAnimationState.Idle => 0,
            CharacterAnimationState.Attack => 1,
            CharacterAnimationState.Cast => 2,
            CharacterAnimationState.Hit => 3,
            CharacterAnimationState.Dodge => 4,
            CharacterAnimationState.Defeat => 5,
            _ => 0
        };
        return new Rect2(BattleOriginX + frameIndex * BattleCell, row * BattleCell, BattleCell, BattleCell);
    }

    /// <summary>从 sheet.svg 创建 AtlasTexture；所有区域都使用整数坐标。</summary>
    private static Texture2D? TryLoadSheetRegion(string key, string regionKey, Rect2 region)
    {
        string cacheKey = $"sheet-region:{key}:{regionKey}";
        if (TextureCache.TryGetValue(cacheKey, out Texture2D? cached))
        {
            return cached;
        }

        Texture2D? sheet = TryLoadPath(BuildSheetPath(key));
        if (sheet is null)
        {
            return null;
        }

        AtlasTexture atlas = new()
        {
            Atlas = sheet,
            Region = region
        };
        TextureCache[cacheKey] = atlas;
        return atlas;
    }

    /// <summary>尺寸严格匹配生产规格时才允许切片，防止错误图集越界。</summary>
    private static bool HasValidSheet(string key)
    {
        Texture2D? sheet = TryLoadPath(BuildSheetPath(key));
        return sheet is not null && sheet.GetWidth() == SheetWidth && sheet.GetHeight() == SheetHeight;
    }

    /// <summary>扫描旧连续帧，最多读取 24 张，避免错误目录造成无界检查。</summary>
    private static int CountContinuousFrames(Func<int, string> pathFactory)
    {
        int count = 0;
        for (int index = 0; index < 24; index++)
        {
            if (!ResourceLoader.Exists(pathFactory(index)))
            {
                break;
            }
            count++;
        }
        return count;
    }

    /// <summary>加载并缓存一个 Godot 纹理资源；不存在时静默返回 null。</summary>
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

    /// <summary>人物专属素材优先，其次职业素材，最后才使用阵营通用素材。</summary>
    private static IEnumerable<string> CandidateKeys(UnitModel unit)
    {
        yield return NormalizeKey(unit.Id);
        yield return NormalizeKey(unit.ClassDefinition.Id);
        yield return unit.Team == UnitTeam.Enemy ? "enemy_default" : "player_default";
    }

    /// <summary>旧单图路径继续保留，方便未来直接覆盖某一个槽位。</summary>
    private static string BuildLegacyPath(string key, CharacterArtSlot slot)
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

    /// <summary>新正式角色图集使用可文本提交的 SVG 容器。</summary>
    private static string BuildSheetPath(string key)
    {
        return $"res://assets/characters/{key}/sheet.svg";
    }

    /// <summary>旧地图多帧路径。</summary>
    private static string BuildLegacyMapFramePath(
        string key,
        CharacterAnimationState state,
        CharacterFacing facing,
        int frameIndex)
    {
        return $"res://assets/characters/{key}/map/{state.ToString().ToLowerInvariant()}_{facing.ToString().ToLowerInvariant()}_{frameIndex}.png";
    }

    /// <summary>旧战斗多帧路径。</summary>
    private static string BuildLegacyBattleFramePath(string key, CharacterAnimationState state, int frameIndex)
    {
        return $"res://assets/characters/{key}/battle/{state.ToString().ToLowerInvariant()}_{frameIndex}.png";
    }

    /// <summary>把运行时 ID 统一成稳定目录键。</summary>
    private static string NormalizeKey(string value)
    {
        return value.Trim().ToLowerInvariant().Replace(' ', '_');
    }
}
