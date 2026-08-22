using FlameEmblem.Game;
using Godot;
using System.Text;

namespace FlameEmblem.Visual;

/// <summary>
/// 加载战斗标准稿专用的固定像素贴图。
/// 每张人物贴图都是 80×75 的透明 PNG，并拆成三个小 Base64 文本片段保存；
/// 运行时只做确定性的文本拼接和 PNG 解码，不联网、不生成新图，也不参与任何战斗规则。
/// </summary>
public static class ReferenceBattleSpriteCatalog
{
    /// <summary>标准稿人物逻辑宽度。</summary>
    public const int SpriteWidth = 80;

    /// <summary>标准稿人物逻辑高度。</summary>
    public const int SpriteHeight = 75;

    /// <summary>每张贴图固定拆成三个文本片段，避免大文本在仓库写入时被截断。</summary>
    private const int PartCount = 3;

    /// <summary>已经成功解码的人物贴图缓存。</summary>
    private static readonly Dictionary<string, Texture2D> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>已经确认缺失或损坏的键，避免绘制循环重复读取。</summary>
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

        try
        {
            StringBuilder encoded = new();
            for (int partIndex = 0; partIndex < PartCount; partIndex++)
            {
                string path = $"res://assets/characters/{key}/battle_ref.{partIndex}.b64";
                if (!Godot.FileAccess.FileExists(path))
                {
                    FailedKeys.Add(key);
                    return null;
                }

                string part = Godot.FileAccess.GetFileAsString(path).Trim();
                if (string.IsNullOrEmpty(part))
                {
                    FailedKeys.Add(key);
                    return null;
                }

                encoded.Append(part);
            }

            byte[] pngBytes = Convert.FromBase64String(encoded.ToString());
            Image image = new();
            Error error = image.LoadPngFromBuffer(pngBytes);
            if (error != Error.Ok || image.GetWidth() != SpriteWidth || image.GetHeight() != SpriteHeight)
            {
                FailedKeys.Add(key);
                GD.PushWarning($"标准战斗贴图 {key} 无法读取为 {SpriteWidth}×{SpriteHeight} PNG，暂时使用程序兜底人物。");
                return null;
            }

            ImageTexture texture = ImageTexture.CreateFromImage(image);
            Cache[key] = texture;
            return texture;
        }
        catch (Exception exception)
        {
            FailedKeys.Add(key);
            GD.PushWarning($"标准战斗贴图 {key} 读取失败：{exception.Message}");
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
