using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 读取已经由用户确认的正式人物设计稿资源。
/// 正式资源优先使用仓库内经过校验的 WebP 文件；运行时只做本地资源加载与缓存，
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

    /// <summary>
    /// 返回正式战斗人物。
    /// 已完成正式设计稿的角色优先读取 approved_battle.webp；尚未完成正式美术的角色临时读取 battle_ref.png，
    /// 只为避免战斗画面出现纯黑人物或空白人物，等正式资源补齐后会自动被 approved_battle.webp 覆盖。
    /// </summary>
    public static Texture2D? TryLoadBattle(UnitModel unit)
    {
        string key = ResolveKey(unit);
        string cacheKey = $"{key}:battle";
        if (BattleCache.TryGetValue(cacheKey, out Texture2D? cached))
        {
            return cached;
        }

        if (MissingBattle.Contains(cacheKey))
        {
            return null;
        }

        Texture2D? approved = TryLoadTexture(key, "approved_battle.webp");
        if (approved is not null)
        {
            BattleCache[cacheKey] = approved;
            return approved;
        }

        // 正式全身像还没完成时，临时使用已有彩色 battle_ref.png；
        // 这里明确禁止再回退到程序方块人或全黑剪影。
        Texture2D? temporaryVisibleFallback = TryLoadTexture(key, "battle_ref.png");
        if (temporaryVisibleFallback is not null)
        {
            BattleCache[cacheKey] = temporaryVisibleFallback;
            return temporaryVisibleFallback;
        }

        MissingBattle.Add(cacheKey);
        GD.PushWarning($"角色 {key} 缺少 approved_battle.webp 与 battle_ref.png，战斗人物将暂时不显示。");
        return null;
    }

    /// <summary>
    /// 返回正式对话头像。
    /// 对话继续坚持只使用已经确认的 approved_portrait.webp；未完成人物保持干净空框，避免旧程序头像重新出现。
    /// </summary>
    public static Texture2D? TryLoadPortrait(UnitModel unit)
    {
        return TryLoadApprovedPortrait(unit);
    }

    /// <summary>读取一名人物的正式头像并缓存。</summary>
    private static Texture2D? TryLoadApprovedPortrait(UnitModel unit)
    {
        string key = ResolveKey(unit);
        string cacheKey = $"{key}:portrait";
        if (PortraitCache.TryGetValue(cacheKey, out Texture2D? cached))
        {
            return cached;
        }

        if (MissingPortrait.Contains(cacheKey))
        {
            return null;
        }

        Texture2D? texture = TryLoadTexture(key, "approved_portrait.webp");
        if (texture is null)
        {
            MissingPortrait.Add(cacheKey);
            return null;
        }

        PortraitCache[cacheKey] = texture;
        return texture;
    }

    /// <summary>
    /// 从某个角色目录读取一个标准 Godot 纹理资源。
    /// 使用 ResourceLoader 让编辑器与导出包都走同一条资源管线，避免再次出现手动二进制解码或文本截断问题。
    /// </summary>
    private static Texture2D? TryLoadTexture(string key, string fileName)
    {
        string path = $"res://assets/characters/{key}/{fileName}";
        if (!ResourceLoader.Exists(path))
        {
            return null;
        }

        try
        {
            Texture2D? texture = GD.Load<Texture2D>(path);
            if (texture is null || texture.GetWidth() <= 0 || texture.GetHeight() <= 0)
            {
                GD.PushWarning($"人物资源 {path} 无法作为有效纹理读取。");
                return null;
            }

            return texture;
        }
        catch (Exception exception)
        {
            GD.PushWarning($"人物资源 {path} 读取失败：{exception.Message}");
            return null;
        }
    }

    /// <summary>
    /// 四名主角按稳定人物 ID；普通敌军按职业 ID。
    /// 因此 enemy_01、road_enemy_04、boss 等实例名仍会命中掠夺者/守卫/队长的对应人物资源。
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
