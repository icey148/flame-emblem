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
/// 统一解析人物正式美术资源。
/// 解析顺序兼容旧独立 PNG，也支持新的 512×576 统一角色图集；
/// 统一图集可以是直接的 sheet.png / sheet.svg，也可以是仓库中的 sheet.b64 PNG 文本资源。
/// </summary>
public static class CharacterAssetResolver
{
    /// <summary>缓存已经加载的直接纹理与 AtlasTexture 图集切片。</summary>
    private static readonly Dictionary<string, Texture2D> TextureCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>缓存某个动画状态的实际帧数，避免每帧重复检查资源。</summary>
    private static readonly Dictionary<string, int> FrameCountCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>统一正式图集固定宽度。</summary>
    private const int SheetWidth = 512;

    /// <summary>统一正式图集固定高度。</summary>
    private const int SheetHeight = 576;

    /// <summary>统一头像固定使用左上角 64×64。</summary>
    private static readonly Rect2 PortraitRegion = new(0, 0, 64, 64);

    /// <summary>地图帧从图集 y=64 开始。</summary>
    private const int MapOriginY = 64;

    /// <summary>地图帧使用 32×32 固定格。</summary>
    private const int MapCell = 32;

    /// <summary>战斗帧从图集 x=112 开始。</summary>
    private const int BattleOriginX = 112;

    /// <summary>战斗帧使用 96×96 固定格。</summary>
    private const int BattleCell = 96;

    /// <summary>
    /// 尝试加载人物的单张美术。
    /// 每个候选键先检查旧独立 PNG，再从正式统一图集切出默认帧。
    /// </summary>
    public static Texture2D? TryLoad(UnitModel unit, CharacterArtSlot slot)
    {
        foreach (string key in CandidateKeys(unit))
        {
            Texture2D? legacyTexture = TryLoadDirectPath(BuildLegacyPath(key, slot));
            if (legacyTexture is not null)
            {
                return legacyTexture;
            }

            Rect2 region = slot switch
            {
                CharacterArtSlot.Portrait => PortraitRegion,
                CharacterArtSlot.Map => BuildMapRegion(CharacterAnimationState.Idle, CharacterFacing.Down, 0),
                CharacterArtSlot.Battle => BuildBattleRegion(CharacterAnimationState.Idle, 0),
                _ => PortraitRegion
            };

            Texture2D? sheetTexture = TryLoadSheetRegion(key, $"slot:{slot}", region);
            if (sheetTexture is not null)
            {
                return sheetTexture;
            }
        }

        return null;
    }

    /// <summary>
    /// 返回地图待机/行走动画帧数。
    /// 旧独立帧存在时按连续文件计数；统一图集固定为待机 2 帧、行走 3 帧。
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

            int count = CountContinuousFrames(index =>
                BuildLegacyMapFramePath(key, state, facing, index));

            if (count <= 0 && HasValidSheet(key))
            {
                count = MapFrameCount(state);
            }

            FrameCountCache[cacheKey] = count;
            if (count > 0)
            {
                return count;
            }
        }

        return 0;
    }

    /// <summary>加载地图方向帧；旧独立 PNG 缺失时从统一正式图集读取。</summary>
    public static Texture2D? TryLoadMapFrame(
        UnitModel unit,
        CharacterAnimationState state,
        CharacterFacing facing,
        int frameIndex)
    {
        foreach (string key in CandidateKeys(unit))
        {
            int safeIndex = Math.Max(0, frameIndex);
            Texture2D? legacyTexture = TryLoadDirectPath(
                BuildLegacyMapFramePath(key, state, facing, safeIndex));
            if (legacyTexture is not null)
            {
                return legacyTexture;
            }

            int frameCount = MapFrameCount(state);
            if (frameCount <= 0 || !HasValidSheet(key))
            {
                continue;
            }

            safeIndex = Math.Clamp(safeIndex, 0, frameCount - 1);
            Texture2D? sheetTexture = TryLoadSheetRegion(
                key,
                $"map:{state}:{facing}:{safeIndex}",
                BuildMapRegion(state, facing, safeIndex));
            if (sheetTexture is not null)
            {
                return sheetTexture;
            }
        }

        return null;
    }

    /// <summary>
    /// 返回横向战斗动画帧数。
    /// 正式统一图集提供待机、攻击、施法、受击、闪避和倒下动作。
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

    /// <summary>加载横向战斗动作帧；旧独立 PNG 缺失时从统一正式图集读取。</summary>
    public static Texture2D? TryLoadBattleFrame(UnitModel unit, CharacterAnimationState state, int frameIndex)
    {
        foreach (string key in CandidateKeys(unit))
        {
            int safeIndex = Math.Max(0, frameIndex);
            Texture2D? legacyTexture = TryLoadDirectPath(
                BuildLegacyBattleFramePath(key, state, safeIndex));
            if (legacyTexture is not null)
            {
                return legacyTexture;
            }

            int frameCount = BattleFrameCount(state);
            if (frameCount <= 0 || !HasValidSheet(key))
            {
                continue;
            }

            safeIndex = Math.Clamp(safeIndex, 0, frameCount - 1);
            Texture2D? sheetTexture = TryLoadSheetRegion(
                key,
                $"battle:{state}:{safeIndex}",
                BuildBattleRegion(state, safeIndex));
            if (sheetTexture is not null)
            {
                return sheetTexture;
            }
        }

        return null;
    }

    /// <summary>统一地图图集只定义待机和行走两类动作。</summary>
    private static int MapFrameCount(CharacterAnimationState state)
    {
        return state switch
        {
            CharacterAnimationState.Idle => 2,
            CharacterAnimationState.Walk => 3,
            _ => 0
        };
    }

    /// <summary>统一战斗图集各状态的固定帧数。</summary>
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

    /// <summary>按状态、方向和帧号计算 32×32 地图图集区域。</summary>
    private static Rect2 BuildMapRegion(
        CharacterAnimationState state,
        CharacterFacing facing,
        int frameIndex)
    {
        int stateRow = state == CharacterAnimationState.Walk ? 4 : 0;
        int row = stateRow + (int)facing;
        return new Rect2(
            frameIndex * MapCell,
            MapOriginY + row * MapCell,
            MapCell,
            MapCell);
    }

    /// <summary>按状态和帧号计算 96×96 战斗图集区域。</summary>
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

        return new Rect2(
            BattleOriginX + frameIndex * BattleCell,
            row * BattleCell,
            BattleCell,
            BattleCell);
    }

    /// <summary>
    /// 从完整角色图集创建整数区域 AtlasTexture。
    /// 图集切片本身不做缩放，因此不会破坏原始像素栅格。
    /// </summary>
    private static Texture2D? TryLoadSheetRegion(string key, string regionKey, Rect2 region)
    {
        string cacheKey = $"sheet-region:{key}:{regionKey}";
        if (TextureCache.TryGetValue(cacheKey, out Texture2D? cachedTexture))
        {
            return cachedTexture;
        }

        Texture2D? fullSheet = TryLoadFullSheet(key);
        if (fullSheet is null)
        {
            return null;
        }

        AtlasTexture atlasTexture = new()
        {
            Atlas = fullSheet,
            Region = region
        };
        TextureCache[cacheKey] = atlasTexture;
        return atlasTexture;
    }

    /// <summary>只有尺寸严格为 512×576 的正式图集才允许按固定区域切片。</summary>
    private static bool HasValidSheet(string key)
    {
        Texture2D? sheet = TryLoadFullSheet(key);
        return sheet is not null &&
               sheet.GetWidth() == SheetWidth &&
               sheet.GetHeight() == SheetHeight;
    }

    /// <summary>
    /// 加载一个完整角色图集。
    /// 未来直接加入 sheet.png / sheet.svg 时会自动优先使用；当前仓库则回退到固定 Base64 PNG 文本资源。
    /// </summary>
    private static Texture2D? TryLoadFullSheet(string key)
    {
        string cacheKey = $"full-sheet:{key}";
        if (TextureCache.TryGetValue(cacheKey, out Texture2D? cachedTexture))
        {
            return cachedTexture;
        }

        Texture2D? texture = TryLoadDirectPath($"res://assets/characters/{key}/sheet.png")
                            ?? TryLoadDirectPath($"res://assets/characters/{key}/sheet.svg")
                            ?? EmbeddedCharacterArtCatalog.TryLoadSheet(key);
        if (texture is not null)
        {
            TextureCache[cacheKey] = texture;
        }

        return texture;
    }

    /// <summary>扫描从 0 开始连续编号的旧多帧 PNG，最多检查 24 帧。</summary>
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

    /// <summary>从一个普通 Godot 资源路径加载纹理并缓存。</summary>
    private static Texture2D? TryLoadDirectPath(string path)
    {
        if (TextureCache.TryGetValue(path, out Texture2D? cachedTexture))
        {
            return cachedTexture;
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

    /// <summary>人物实例素材优先，其次职业素材，最后才使用阵营通用素材。</summary>
    private static IEnumerable<string> CandidateKeys(UnitModel unit)
    {
        yield return NormalizeKey(unit.Id);
        yield return NormalizeKey(unit.ClassDefinition.Id);
        yield return unit.Team == UnitTeam.Enemy ? "enemy_default" : "player_default";
    }

    /// <summary>构造旧单图人物资源路径。</summary>
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

    /// <summary>构造旧地图方向多帧路径。</summary>
    private static string BuildLegacyMapFramePath(
        string key,
        CharacterAnimationState state,
        CharacterFacing facing,
        int frameIndex)
    {
        string stateName = state.ToString().ToLowerInvariant();
        string facingName = facing.ToString().ToLowerInvariant();
        return $"res://assets/characters/{key}/map/{stateName}_{facingName}_{frameIndex}.png";
    }

    /// <summary>构造旧横向战斗多帧路径。</summary>
    private static string BuildLegacyBattleFramePath(
        string key,
        CharacterAnimationState state,
        int frameIndex)
    {
        string stateName = state.ToString().ToLowerInvariant();
        return $"res://assets/characters/{key}/battle/{stateName}_{frameIndex}.png";
    }

    /// <summary>把运行时 ID 转成稳定安全的素材目录键。</summary>
    private static string NormalizeKey(string value)
    {
        return value.Trim().ToLowerInvariant().Replace(' ', '_');
    }
}
