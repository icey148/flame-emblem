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
/// 除了兼容旧版独立 PNG，本解析器也支持一个角色只放一张 sheet.png：
/// 同一张图集同时提供 64×64 头像、32×32 四方向地图帧和 96×96 战斗动作帧，
/// 从根源上保证地图、对话与战斗中的人物设计保持一致。
/// </summary>
public static class CharacterAssetResolver
{
    /// <summary>缓存已经成功加载的纹理或 AtlasTexture，避免每帧重复读取资源。</summary>
    private static readonly Dictionary<string, Texture2D> TextureCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>缓存某个动画状态实际拥有的连续帧数。</summary>
    private static readonly Dictionary<string, int> FrameCountCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>统一角色图集宽度；当前生产规格为 512×576。</summary>
    private const int SheetWidth = 512;

    /// <summary>统一角色图集高度；当前生产规格为 512×576。</summary>
    private const int SheetHeight = 576;

    /// <summary>头像固定占据图集左上角 64×64。</summary>
    private static readonly Rect2 PortraitSheetRegion = new(0, 0, 64, 64);

    /// <summary>地图帧区域从 y=64 开始，每格 32×32。</summary>
    private const int MapSheetOriginY = 64;

    /// <summary>地图帧固定边长。</summary>
    private const int MapSheetCellSize = 32;

    /// <summary>战斗帧区域从 x=112 开始，每格 96×96。</summary>
    private const int BattleSheetOriginX = 112;

    /// <summary>战斗帧固定边长。</summary>
    private const int BattleSheetCellSize = 96;

    /// <summary>
    /// 尝试为指定人物和用途加载单张纹理。
    /// 查找顺序为：人物实例 ID → 职业 ID → enemy_default/player_default。
    /// 每个键优先兼容旧独立 PNG，没有时再从统一 sheet.png 切片。
    /// </summary>
    public static Texture2D? TryLoad(UnitModel unit, CharacterArtSlot slot)
    {
        foreach (string key in CandidateKeys(unit))
        {
            Texture2D? legacyTexture = TryLoadPath(BuildPath(key, slot));
            if (legacyTexture is not null)
            {
                return legacyTexture;
            }

            Texture2D? sheetTexture = TryLoadSingleSheetSlot(key, slot);
            if (sheetTexture is not null)
            {
                return sheetTexture;
            }
        }

        return null;
    }

    /// <summary>
    /// 获取地图人物某个动画状态和方向实际存在的帧数。
    /// 旧独立文件仍按连续编号扫描；统一图集则使用固定的待机 2 帧、行走 3 帧规格。
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
            if (count <= 0 && HasValidSheet(key))
            {
                count = MapSheetFrameCount(state);
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
    /// 加载地图人物的一张方向序列帧。
    /// 先兼容旧独立文件，再从统一角色图集读取对应状态、方向和帧号。
    /// </summary>
    public static Texture2D? TryLoadMapFrame(
        UnitModel unit,
        CharacterAnimationState state,
        CharacterFacing facing,
        int frameIndex)
    {
        foreach (string key in CandidateKeys(unit))
        {
            int safeIndex = Math.Max(0, frameIndex);
            Texture2D? legacyTexture = TryLoadPath(BuildMapFramePath(key, state, facing, safeIndex));
            if (legacyTexture is not null)
            {
                return legacyTexture;
            }

            Texture2D? sheetTexture = TryLoadMapSheetFrame(key, state, facing, safeIndex);
            if (sheetTexture is not null)
            {
                return sheetTexture;
            }
        }

        return null;
    }

    /// <summary>
    /// 获取独立战斗演出某个状态实际存在的连续帧数。
    /// 统一图集固定提供待机、攻击、施法、受击、闪避和倒下动作。
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
            if (count <= 0 && HasValidSheet(key))
            {
                count = BattleSheetFrameCount(state);
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
    /// 加载独立战斗演出的一张序列帧。
    /// 旧独立文件优先；不存在时直接从统一角色图集切出对应 96×96 帧。
    /// </summary>
    public static Texture2D? TryLoadBattleFrame(UnitModel unit, CharacterAnimationState state, int frameIndex)
    {
        foreach (string key in CandidateKeys(unit))
        {
            int safeIndex = Math.Max(0, frameIndex);
            Texture2D? legacyTexture = TryLoadPath(BuildBattleFramePath(key, state, safeIndex));
            if (legacyTexture is not null)
            {
                return legacyTexture;
            }

            Texture2D? sheetTexture = TryLoadBattleSheetFrame(key, state, safeIndex);
            if (sheetTexture is not null)
            {
                return sheetTexture;
            }
        }

        return null;
    }

    /// <summary>从统一图集读取 Portrait、Map 或 Battle 的默认静态图。</summary>
    private static Texture2D? TryLoadSingleSheetSlot(string key, CharacterArtSlot slot)
    {
        Rect2 region = slot switch
        {
            CharacterArtSlot.Portrait => PortraitSheetRegion,
            CharacterArtSlot.Map => BuildMapSheetRegion(CharacterAnimationState.Idle, CharacterFacing.Down, 0),
            CharacterArtSlot.Battle => BuildBattleSheetRegion(CharacterAnimationState.Idle, 0),
            _ => PortraitSheetRegion
        };

        return TryLoadSheetRegion(key, $"slot:{slot}", region);
    }

    /// <summary>从图集切出一张地图方向帧；只有 Idle 与 Walk 使用地图区域。</summary>
    private static Texture2D? TryLoadMapSheetFrame(
        string key,
        CharacterAnimationState state,
        CharacterFacing facing,
        int frameIndex)
    {
        int frameCount = MapSheetFrameCount(state);
        if (frameCount <= 0 || !HasValidSheet(key))
        {
            return null;
        }

        int safeIndex = Math.Clamp(frameIndex, 0, frameCount - 1);
        Rect2 region = BuildMapSheetRegion(state, facing, safeIndex);
        return TryLoadSheetRegion(key, $"map:{state}:{facing}:{safeIndex}", region);
    }

    /// <summary>从图集切出一张战斗动作帧。</summary>
    private static Texture2D? TryLoadBattleSheetFrame(
        string key,
        CharacterAnimationState state,
        int frameIndex)
    {
        int frameCount = BattleSheetFrameCount(state);
        if (frameCount <= 0 || !HasValidSheet(key))
        {
            return null;
        }

        int safeIndex = Math.Clamp(frameIndex, 0, frameCount - 1);
        Rect2 region = BuildBattleSheetRegion(state, safeIndex);
        return TryLoadSheetRegion(key, $"battle:{state}:{safeIndex}", region);
    }

    /// <summary>
    /// 创建一个只显示图集指定区域的 AtlasTexture，并把切片结果写入缓存。
    /// 所有区域都是整数坐标，不会发生纹理插值或边缘串色。
    /// </summary>
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

        AtlasTexture atlasTexture = new()
        {
            Atlas = sheet,
            Region = region
        };
        TextureCache[cacheKey] = atlasTexture;
        return atlasTexture;
    }

    /// <summary>统一图集存在且尺寸符合 512×576 时才启用切片，防止错误素材被越界读取。</summary>
    private static bool HasValidSheet(string key)
    {
        Texture2D? sheet = TryLoadPath(BuildSheetPath(key));
        return sheet is not null && sheet.GetWidth() == SheetWidth && sheet.GetHeight() == SheetHeight;
    }

    /// <summary>统一地图图集只有待机和行走两种状态。</summary>
    private static int MapSheetFrameCount(CharacterAnimationState state)
    {
        return state switch
        {
            CharacterAnimationState.Idle => 2,
            CharacterAnimationState.Walk => 3,
            _ => 0
        };
    }

    /// <summary>统一战斗图集每个状态的固定帧数。</summary>
    private static int BattleSheetFrameCount(CharacterAnimationState state)
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

    /// <summary>根据地图状态、方向和帧号计算 32×32 图集区域。</summary>
    private static Rect2 BuildMapSheetRegion(
        CharacterAnimationState state,
        CharacterFacing facing,
        int frameIndex)
    {
        int stateRow = state == CharacterAnimationState.Walk ? 4 : 0;
        int row = stateRow + (int)facing;
        return new Rect2(
            frameIndex * MapSheetCellSize,
            MapSheetOriginY + row * MapSheetCellSize,
            MapSheetCellSize,
            MapSheetCellSize);
    }

    /// <summary>根据战斗状态和帧号计算 96×96 图集区域。</summary>
    private static Rect2 BuildBattleSheetRegion(CharacterAnimationState state, int frameIndex)
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
            BattleSheetOriginX + frameIndex * BattleSheetCellSize,
            row * BattleSheetCellSize,
            BattleSheetCellSize,
            BattleSheetCellSize);
    }

    /// <summary>
    /// 扫描一组从 0 开始连续编号的旧独立帧，最多允许 24 帧。
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

    /// <summary>生成可能对应当前人物的素材目录键。</summary>
    private static IEnumerable<string> CandidateKeys(UnitModel unit)
    {
        yield return NormalizeKey(unit.Id);
        yield return NormalizeKey(unit.ClassDefinition.Id);
        yield return unit.Team == UnitTeam.Enemy ? "enemy_default" : "player_default";
    }

    /// <summary>按统一目录约定构造旧单张人物资源路径。</summary>
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

    /// <summary>构造新统一角色图集路径。</summary>
    private static string BuildSheetPath(string key)
    {
        return $"res://assets/characters/{key}/sheet.png";
    }

    /// <summary>构造旧地图方向序列帧路径。</summary>
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

    /// <summary>构造旧独立战斗演出序列帧路径。</summary>
    private static string BuildBattleFramePath(string key, CharacterAnimationState state, int frameIndex)
    {
        string stateName = state.ToString().ToLowerInvariant();
        return $"res://assets/characters/{key}/battle/{stateName}_{frameIndex}.png";
    }

    /// <summary>把运行时 ID 转成稳定安全的目录名。</summary>
    private static string NormalizeKey(string value)
    {
        return value.Trim().ToLowerInvariant().Replace(' ', '_');
    }
}
