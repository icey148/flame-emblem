using Godot;
using System.Text;

namespace FlameEmblem.Visual;

/// <summary>
/// 加载拆分保存的 Base64 正式角色 PNG 图集。
/// 每套图集拆成三个小文本片段，避免仓库写入通道截断单个大文件；
/// 运行时只做固定文本拼接与 PNG 解码，不联网、不生成角色内容，也不参与任何战斗规则。
/// </summary>
public static class EmbeddedCharacterArtCatalog
{
    /// <summary>每套正式图集固定拆成三个 Base64 片段。</summary>
    private const int SheetPartCount = 3;

    /// <summary>缓存已经解码成功的角色图集，避免重复拼接 Base64 和 PNG。</summary>
    private static readonly Dictionary<string, Texture2D> SheetCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 记录已经确认缺少分段素材或加载失败的素材键。
    /// 失败后直接回退程序人物，避免头像/地图/战斗绘制循环重复检查文件。
    /// </summary>
    private static readonly HashSet<string> FailedSheetKeys = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>尝试读取 sheet.0.b64～sheet.2.b64，拼接后解码为完整正式角色纹理。</summary>
    public static Texture2D? TryLoadSheet(string key)
    {
        string normalizedKey = key.Trim().ToLowerInvariant().Replace(' ', '_');
        if (SheetCache.TryGetValue(normalizedKey, out Texture2D? cached))
        {
            return cached;
        }

        if (FailedSheetKeys.Contains(normalizedKey))
        {
            return null;
        }

        try
        {
            StringBuilder encodedBuilder = new();
            for (int partIndex = 0; partIndex < SheetPartCount; partIndex++)
            {
                string partPath = $"res://assets/characters/{normalizedKey}/sheet.{partIndex}.b64";

                // 显式使用 Godot.FileAccess，避免与 System.IO.FileAccess 的枚举名称发生歧义。
                if (!Godot.FileAccess.FileExists(partPath))
                {
                    FailedSheetKeys.Add(normalizedKey);
                    return null;
                }

                string part = Godot.FileAccess.GetFileAsString(partPath).Trim();
                if (string.IsNullOrEmpty(part))
                {
                    FailedSheetKeys.Add(normalizedKey);
                    return null;
                }

                encodedBuilder.Append(part);
            }

            byte[] pngBytes = Convert.FromBase64String(encodedBuilder.ToString());
            Image image = new();
            Error error = image.LoadPngFromBuffer(pngBytes);
            if (error != Error.Ok)
            {
                FailedSheetKeys.Add(normalizedKey);
                GD.PushWarning($"角色分段图集 {normalizedKey} PNG 解码失败：{error}。本次运行将回退到程序人物。");
                return null;
            }

            // 统一图集必须保持固定尺寸，否则切片坐标会读取到错误区域。
            if (image.GetWidth() != 512 || image.GetHeight() != 576)
            {
                FailedSheetKeys.Add(normalizedKey);
                GD.PushWarning($"角色分段图集 {normalizedKey} 尺寸不是 512×576，本次运行将回退到程序人物。");
                return null;
            }

            ImageTexture texture = ImageTexture.CreateFromImage(image);
            SheetCache[normalizedKey] = texture;
            return texture;
        }
        catch (Exception exception)
        {
            FailedSheetKeys.Add(normalizedKey);
            GD.PushWarning($"角色分段图集 {normalizedKey} 读取失败：{exception.Message}。本次运行将回退到程序人物。");
            return null;
        }
    }
}
