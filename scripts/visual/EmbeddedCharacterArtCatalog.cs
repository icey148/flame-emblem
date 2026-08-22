using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 加载以 Base64 文本保存的正式角色 PNG 图集。
/// 运行时只在首次使用时解码一次，不联网、不生成图像，也不参与任何战斗规则；
/// 解码后的结果仍然是普通 ImageTexture。
/// </summary>
public static class EmbeddedCharacterArtCatalog
{
    /// <summary>缓存已经解码成功的角色图集，避免重复解析 Base64 和 PNG。</summary>
    private static readonly Dictionary<string, Texture2D> SheetCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 记录已经确认加载失败的素材键。
    /// 这样即使某个资源文件损坏，也只尝试一次并自动回退，不会在每个绘制帧重复刷错误日志。
    /// </summary>
    private static readonly HashSet<string> FailedSheetKeys = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>尝试读取 res://assets/characters/&lt;key&gt;/sheet.b64 并解码为纹理。</summary>
    public static Texture2D? TryLoadSheet(string key)
    {
        string normalizedKey = key.Trim().ToLowerInvariant().Replace(' ', '_');
        if (SheetCache.TryGetValue(normalizedKey, out Texture2D? cached))
        {
            return cached;
        }

        // 已经确认失败的资源不再重复解码，防止头像/地图/战斗绘制循环持续刷同一条错误。
        if (FailedSheetKeys.Contains(normalizedKey))
        {
            return null;
        }

        string path = $"res://assets/characters/{normalizedKey}/sheet.b64";

        // 项目启用了 .NET 隐式 using，System.IO.FileAccess 会与 Godot.FileAccess 同名。
        // 这里显式使用 Godot.FileAccess，确保调用的是 Godot 的 res:// 文件系统接口。
        if (!Godot.FileAccess.FileExists(path))
        {
            FailedSheetKeys.Add(normalizedKey);
            return null;
        }

        try
        {
            // Base64 文件只包含 PNG 字节的文本编码；去掉首尾空白后交给 .NET 固定解码。
            string encoded = Godot.FileAccess.GetFileAsString(path).Trim();
            if (string.IsNullOrWhiteSpace(encoded))
            {
                FailedSheetKeys.Add(normalizedKey);
                return null;
            }

            byte[] pngBytes = Convert.FromBase64String(encoded);
            Image image = new();
            Error error = image.LoadPngFromBuffer(pngBytes);
            if (error != Error.Ok)
            {
                FailedSheetKeys.Add(normalizedKey);
                GD.PushWarning($"角色图集 {path} PNG 解码失败：{error}。本次运行将回退到程序人物。\n");
                return null;
            }

            ImageTexture texture = ImageTexture.CreateFromImage(image);
            SheetCache[normalizedKey] = texture;
            return texture;
        }
        catch (Exception exception)
        {
            FailedSheetKeys.Add(normalizedKey);
            GD.PushWarning($"角色图集 {path} 读取失败：{exception.Message}。本次运行将回退到程序人物。\n");
            return null;
        }
    }
}
