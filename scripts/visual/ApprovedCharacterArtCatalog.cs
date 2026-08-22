using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 读取用户已经确认的正式人物设计稿资源。
/// 正式战斗人物和正式头像优先使用 approved_battle.webp / approved_portrait.webp；
/// 尚未补齐正式稿的角色临时回退到现有安全角色图集，避免战斗或对话中出现纯黑影。
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

    /// <summary>
    /// 返回战斗人物。
    /// 正式全身图优先；未完成正式稿时回退到当前角色已有战斗图集，保证人物始终可见。
    /// </summary>
    public static Texture2D? TryLoadBattle(UnitModel unit)
    {
        Texture2D? approved = TryLoadApprovedTexture(
            unit,
            "approved_battle.webp",
            "battle",
            BattleCache,
            MissingBattle);
        if (approved is not null)
        {
            return approved;
        }

        // 临时兼容层只在 approved_battle.webp 缺失时生效；正式图一旦补齐会自动覆盖这一回退。
        return CharacterAssetResolver.TryLoad(unit, CharacterArtSlot.Battle);
    }

    /// <summary>
    /// 返回人物头像。
    /// 正式头像优先；未完成正式稿时回退到现有角色图集头像，避免对话框出现黑块或空脸。
    /// </summary>
    public static Texture2D? TryLoadPortrait(UnitModel unit)
    {
        Texture2D? approved = TryLoadApprovedTexture(
            unit,
            "approved_portrait.webp",
            "portrait",
            PortraitCache,
            MissingPortrait);
        if (approved is not null)
        {
            return approved;
        }

        // 战斗 HUD 与章节对话共用这一条回退，确保同一角色在两个界面中都不会变成黑影。
        return CharacterAssetResolver.TryLoad(unit, CharacterArtSlot.Portrait);
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
                GD.PushWarning($"正式人物资源 {path} 解码失败，将临时使用安全角色图集：{error}");
                return null;
            }

            ImageTexture texture = ImageTexture.CreateFromImage(image);
            cache[cacheKey] = texture;
            return texture;
        }
        catch (Exception exception)
        {
            missing.Add(cacheKey);
            GD.PushWarning($"正式人物资源 {path} 读取失败，将临时使用安全角色图集：{exception.Message}");
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
