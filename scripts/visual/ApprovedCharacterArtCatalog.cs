using FlameEmblem.Game;
using Godot;
using System.Text;

namespace FlameEmblem.Visual;

/// <summary>
/// 读取已经由用户确认的正式人物设计稿资源。
/// 正式资源拆成多个小型 Base64 WebP 文本片段，运行时只做确定性的本地拼接、解码与缓存；
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
        return TryLoad(unit, "approved_battle", BattleCache, MissingBattle);
    }

    /// <summary>返回正式对话头像；没有正式设计稿时返回空，不再绘制旧程序头像。</summary>
    public static Texture2D? TryLoadPortrait(UnitModel unit)
    {
        return TryLoad(unit, "approved_portrait", PortraitCache, MissingPortrait);
    }

    /// <summary>按稳定人物/职业键读取一个经过确认的正式资源。</summary>
    private static Texture2D? TryLoad(
        UnitModel unit,
        string assetName,
        Dictionary<string, Texture2D> cache,
        HashSet<string> missing)
    {
        string key = ResolveKey(unit);
        string cacheKey = $"{key}:{assetName}";
        if (cache.TryGetValue(cacheKey, out Texture2D? cached))
        {
            return cached;
        }

        if (missing.Contains(cacheKey))
        {
            return null;
        }

        try
        {
            string? encoded = ReadSplitBase64(key, assetName);
            if (string.IsNullOrWhiteSpace(encoded))
            {
                missing.Add(cacheKey);
                return null;
            }

            byte[] bytes = Convert.FromBase64String(encoded);
            Image image = new();
            Error error = image.LoadWebpFromBuffer(bytes);
            if (error != Error.Ok || image.GetWidth() <= 0 || image.GetHeight() <= 0)
            {
                missing.Add(cacheKey);
                GD.PushWarning($"正式人物资源 {key}/{assetName} 解码失败：{error}");
                return null;
            }

            ImageTexture texture = ImageTexture.CreateFromImage(image);
            cache[cacheKey] = texture;
            return texture;
        }
        catch (Exception exception)
        {
            missing.Add(cacheKey);
            GD.PushWarning($"正式人物资源 {key}/{assetName} 读取失败：{exception.Message}");
            return null;
        }
    }

    /// <summary>
    /// 按 0、1、2……顺序读取小片段；只要第一片不存在就视为该角色尚未完成正式美术。
    /// 小片段可以避免此前大型二进制/Base64 文件通过提交通道时被截断。
    /// </summary>
    private static string? ReadSplitBase64(string key, string assetName)
    {
        StringBuilder builder = new();
        for (int index = 0; index < 16; index++)
        {
            string path = $"res://assets/characters/{key}/{assetName}.{index}.b64";
            if (!Godot.FileAccess.FileExists(path))
            {
                return index == 0 ? null : builder.ToString();
            }

            string part = Godot.FileAccess.GetFileAsString(path).Trim();
            if (string.IsNullOrEmpty(part))
            {
                return null;
            }

            builder.Append(part);
        }

        return builder.Length == 0 ? null : builder.ToString();
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
