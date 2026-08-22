using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 读取用户已经确认的正式人物设计稿资源。
/// 正式战斗人物和正式头像只认 approved_battle.webp / approved_portrait.webp；
/// 缺少正式素材时保持空白，不再回退到旧 battle_ref、battle_v3 或程序绘制人物。
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

    /// <summary>返回正式战斗人物；缺少正式美术时返回空。</summary>
    public static Texture2D? TryLoadBattle(UnitModel unit)
    {
        return TryLoadApprovedTexture(unit, "approved_battle.webp", "battle", BattleCache, MissingBattle);
    }

    /// <summary>返回正式对话头像；缺少正式美术时返回空。</summary>
    public static Texture2D? TryLoadPortrait(UnitModel unit)
    {
        return TryLoadApprovedTexture(unit, "approved_portrait.webp", "portrait", PortraitCache, MissingPortrait);
    }

    /// <summary>
    /// 直接读取 WebP 文件字节并交给 Godot Image 解码。
    /// 不经过 ResourceLoader/import 缓存，避免替换正式美术后仍命中旧导入资源。
    /// </summary>
    private static Texture2D? TryLoadApprovedTexture(
        UnitModel unit,
        string fileName,
        string slotName,
        Dictionary<string, Texture2D> cache,
        HashSet<string> missing)
    {
        string key = ResolveKey(unit);
        string cacheKey = $"{key}:{slotName}";
        if (cache.TryGetValue(cacheKey, out Texture2D? cached))
        {
            return cached;
        }

        if (missing.Contains(cacheKey))
        {
            return null;
        }

        string path = $"res://assets/characters/{key}/{fileName}";
        if (!Godot.FileAccess.FileExists(path))
        {
            missing.Add(cacheKey);
            return null;
        }

        try
        {
            byte[] bytes = Godot.FileAccess.GetFileAsBytes(path);
            if (bytes.Length == 0)
            {
                missing.Add(cacheKey);
                return null;
            }

            Image image = new();
            Error error = image.LoadWebpFromBuffer(bytes);
            if (error != Error.Ok || image.GetWidth() <= 0 || image.GetHeight() <= 0)
            {
                missing.Add(cacheKey);
                GD.PushWarning($"正式人物资源 {path} 解码失败：{error}");
                return null;
            }

            ImageTexture texture = ImageTexture.CreateFromImage(image);
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
    /// enemy_01、road_enemy_04、boss 等实例名仍会命中掠夺者/守卫/队长的正式设计。
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
