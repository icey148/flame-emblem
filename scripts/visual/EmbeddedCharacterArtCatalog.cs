using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 加载以 Base64 文本保存的正式角色 PNG 图集。
/// 这是为当前仓库文本写入通道准备的资源封装：运行时只在首次使用时解码一次，
/// 不联网、不生成图像，也不参与任何战斗规则；解码后的结果仍然是普通 ImageTexture。
/// </summary>
public static class EmbeddedCharacterArtCatalog
{
    /// <summary>缓存已经解码成功的角色图集，避免重复解析 Base64 和 PNG。</summary>
    private static readonly Dictionary<string, Texture2D> SheetCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>尝试读取 res://assets/characters/&lt;key&gt;/sheet.b64 并解码为纹理。</summary>
    public static Texture2D? TryLoadSheet(string key)
    {
        string normalizedKey = key.Trim().ToLowerInvariant().Replace(' ', '_');
        if (SheetCache.TryGetValue(normalizedKey, out Texture2D? cached))
        {
            return cached;
        }

        string path = $"res://assets/characters/{normalizedKey}/sheet.b64";
        if (!FileAccess.FileExists(path))
        {
            return null;
        }

        try
        {
            // Base64 文件只包含 PNG 字节的文本编码；去掉首尾空白后交给 .NET 固定解码。
            string encoded = FileAccess.GetFileAsString(path).Trim();
            if (string.IsNullOrWhiteSpace(encoded))
            {
                return null;
            }

            byte[] pngBytes = Convert.FromBase64String(encoded);
            Image image = new();
            Error error = image.LoadPngFromBuffer(pngBytes);
            if (error != Error.Ok)
            {
                GD.PushWarning($"角色图集 {path} PNG 解码失败：{error}。");
                return null;
            }

            ImageTexture texture = ImageTexture.CreateFromImage(image);
            SheetCache[normalizedKey] = texture;
            return texture;
        }
        catch (Exception exception)
        {
            GD.PushWarning($"角色图集 {path} 读取失败：{exception.Message}");
            return null;
        }
    }
}
