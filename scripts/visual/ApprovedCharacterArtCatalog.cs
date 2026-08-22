using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 读取已经由用户确认的正式人物设计稿资源。
/// 正式资源使用仓库内经过校验的 WebP 文件；运行时只做本地资源加载与缓存，
/// 不联网、不生成图片，也不参与命中、伤害、经验或回合规则。
/// </summary>
public static class ApprovedCharacterArtCatalog
{
    /// <summary>正式战斗人物缓存。</summary>
    private static readonly Dictionary<string, Texture2D> BattleCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>正式对话头像缓存。</summary>
    private static readonly Dictionary<string, Texture2D> PortraitCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>本次运行已经确认缺少战斗素材的键。</summary>
    private static readonly HashSet<string> MissingBattle = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>本次运行已经确认缺少头像素材的键。</summary>
    private static readonly HashSet<string> MissingPortrait = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>返回正式战斗人物；没有正式设计稿时返回空，不再回退到旧方块人物。</summary>
    public static Texture2D? TryLoadBattle(UnitModel unit)
    {
        return TryLoad(unit, "approved_battle.webp", BattleCache, MissingBattle);
    }

    /// <summary>返回正式对话头像；没有正式设计稿时返回空，不再绘制旧程序头像。</summary>
    public static Texture2D? TryLoadPortrait(UnitModel unit)
    {
        return TryLoad(unit, "approved_portrait.webp", PortraitCache, MissingPortrait);
    }

    /// <summary>
    /// 按稳定人物/职业键读取一份正式 WebP。
    /// 使用 ResourceLoader 让资源在编辑器和导出包中都走 Godot 的标准资源管线，避免再次出现文本截断或手动二进制解码问题。
    /// </summary>
    private static Texture2D? TryLoad(
        UnitModel unit,
        string fileName,
        Dictionary<string, Texture2D> cache,
        HashSet<string> missing)
    {
        string key = ResolveKey(unit);
        string cacheKey = $"{key}:{fileName}";
        if (cache.TryGetValue(cacheKey, out Texture2D? cached))
        {
            return cached;
        }

        if (missing.Contains(cacheKey))
        {
            return null;
        }

        string path = $"res://assets/characters/{key}/{fileName}";
        if (!ResourceLoader.Exists(path))
        {
            missing.Add(cacheKey);
            return null;
        }

        try
        {
            Texture2D? texture = GD.Load<Texture2D>(path);
            if (texture is null || texture.GetWidth() <= 0 || texture.GetHeight() <= 0)
            {
                missing.Add(cacheKey);
                GD.PushWarning($"正式人物资源 {path} 无法作为有效纹理读取。");
                return null;
            }

            cache[cacheKey] = texture;
            return texture;
        }
        catch (Exception exception)
        {
            missing.Add(cacheKey);
            GD.PushWarning($"正式人物资源 {path} 读取失败：{exception.Message}");
            return null;
        }
    }

    /// <summary>
    /// 四名主角按稳定人物 ID；普通敌军按职业 ID。
    /// 因此 enemy_01、road_enemy_04、boss 等实例名仍会命中掠夺者/守卫/队长的正式设计。
    /// </summary>
    private static string ResolveKey(UnitModel unit)
    {
        string id = unit.Id.Trim().ToLowerInvariant();
        if (id is "adrian" or "celine" or "rowan" or "mira")
        {
            return id;
        }

        string classId = unit.ClassDefinition.Id.Trim().ToLowerInvariant();
        return classId switch
        {
            "guard" => "guard",
            "captain" => "captain",
            _ => "raider"
        };
    }
}
