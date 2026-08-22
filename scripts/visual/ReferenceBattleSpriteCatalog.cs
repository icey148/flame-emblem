using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 加载战斗标准界面专用的固定 96×96 像素人物。
/// 每名角色使用仓库内的小型 Base64 PNG 文本资源，运行时只做确定性的 PNG 解码与缓存；
/// 不联网、不生成角色，也不参与命中、伤害、反击、经验或回合规则。
/// </summary>
public static class ReferenceBattleSpriteCatalog
{
    /// <summary>固定战斗人物源图宽度。</summary>
    public const int SpriteWidth = 96;

    /// <summary>固定战斗人物源图高度。</summary>
    public const int SpriteHeight = 96;

    /// <summary>已经成功解码的人物纹理缓存。</summary>
    private static readonly Dictionary<string, Texture2D> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>本次运行已经确认失败的素材键，避免绘制循环重复刷同一条错误。</summary>
    private static readonly HashSet<string> FailedKeys = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>根据稳定人物 ID 或敌军职业 ID 返回固定战斗人物纹理。</summary>
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

        string path = $"res://assets/characters/{key}/battle_v3.b64";
        if (!Godot.FileAccess.FileExists(path))
        {
            FailedKeys.Add(key);
            GD.PushWarning($"缺少固定战斗人物资源：{path}");
            return null;
        }

        try
        {
            // battle_v3.b64 体积很小，避免此前大型整张图集经过文本通道时发生截断。
            string encoded = Godot.FileAccess.GetFileAsString(path).Trim();
            if (string.IsNullOrEmpty(encoded))
            {
                FailedKeys.Add(key);
                return null;
            }

            byte[] pngBytes = Convert.FromBase64String(encoded);
            Image image = new();
            Error error = image.LoadPngFromBuffer(pngBytes);
            if (error != Error.Ok || image.GetWidth() != SpriteWidth || image.GetHeight() != SpriteHeight)
            {
                FailedKeys.Add(key);
                GD.PushWarning($"固定战斗人物 {key} 解码失败或尺寸不是 {SpriteWidth}×{SpriteHeight}：{error}");
                return null;
            }

            ImageTexture texture = ImageTexture.CreateFromImage(image);
            Cache[key] = texture;
            return texture;
        }
        catch (Exception exception)
        {
            FailedKeys.Add(key);
            GD.PushWarning($"固定战斗人物 {key} 读取失败：{exception.Message}");
            return null;
        }
    }

    /// <summary>
    /// 四名主角使用个人 ID；敌军实例使用职业 ID。
    /// 因此 enemy_01、road_enemy_04、boss 等实例名称不会让正式人物掉回占位图。
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
