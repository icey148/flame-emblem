using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 加载战斗标准稿专用的固定像素贴图。
/// 每张人物贴图都是仓库内真实的 80×75 透明 PNG；运行时只加载固定资源，
/// 不联网、不生成新图，也不参与任何命中、伤害或回合规则。
/// </summary>
public static class ReferenceBattleSpriteCatalog
{
    /// <summary>标准稿人物逻辑宽度。</summary>
    public const int SpriteWidth = 80;

    /// <summary>标准稿人物逻辑高度。</summary>
    public const int SpriteHeight = 75;

    /// <summary>已经成功加载的人物贴图缓存。</summary>
    private static readonly Dictionary<string, Texture2D> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>已经确认缺失或损坏的键，避免绘制循环重复访问同一路径。</summary>
    private static readonly HashSet<string> FailedKeys = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>根据人物 ID / 职业 ID 返回标准稿战斗贴图。</summary>
    public static Texture2D? TryLoad(UnitModel unit)
    {
        string key = ResolveKey(unit);
        if (Cache.TryGetValue(key, out Texture2D? cached))
        {
            return cached;
        }

        if (FailedKeys.Contains(key))
        {
            return null;
        }

        string path = $"res://assets/characters/{key}/battle_ref.png";
        if (!ResourceLoader.Exists(path))
        {
            FailedKeys.Add(key);
            return null;
        }

        try
        {
            // 这批 battle_ref.png 通过 Git blob 二进制提交，不经过旧的 Base64 文本载体，
            // 因此直接交给 Godot 资源系统导入并保持最近邻显示即可。
            Texture2D? texture = GD.Load<Texture2D>(path);
            if (texture is null || texture.GetWidth() != SpriteWidth || texture.GetHeight() != SpriteHeight)
            {
                FailedKeys.Add(key);
                GD.PushWarning($"标准战斗贴图 {path} 不是有效的 {SpriteWidth}×{SpriteHeight} PNG，暂时使用程序兜底人物。");
                return null;
            }

            Cache[key] = texture;
            return texture;
        }
        catch (Exception exception)
        {
            FailedKeys.Add(key);
            GD.PushWarning($"标准战斗贴图 {path} 读取失败：{exception.Message}");
            return null;
        }
    }

    /// <summary>
    /// 四名主角按稳定人物 ID 命中个人贴图；敌军按职业 ID 命中通用职业贴图。
    /// 这样 enemy_01、boss 等运行时实例名不会导致正式人物丢失。
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
