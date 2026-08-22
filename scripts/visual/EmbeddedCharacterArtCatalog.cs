using Godot;
using System.Text;

namespace FlameEmblem.Visual;

/// <summary>
/// 加载拆分保存的 Base64 正式角色 PNG 图集。
/// 标准图集拆成三个主片段；如果某个主片段仍然过大，可以继续拆成四个子片段。
/// 运行时只做固定文本拼接与 PNG 解码，不联网、不生成角色内容，也不参与任何战斗规则。
/// </summary>
public static class EmbeddedCharacterArtCatalog
{
    /// <summary>每套正式图集固定包含三个主 Base64 片段。</summary>
    private const int SheetPartCount = 3;

    /// <summary>过大的主片段可以进一步拆成四个小片段，降低文本写入被截断的风险。</summary>
    private const int SheetSubPartCount = 4;

    /// <summary>缓存已经解码成功的角色图集，避免重复拼接 Base64 和 PNG。</summary>
    private static readonly Dictionary<string, Texture2D> SheetCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 记录已经确认缺少分段素材或加载失败的素材键。
    /// 失败后直接回退程序人物，避免头像、地图和战斗绘制循环重复检查文件。
    /// </summary>
    private static readonly HashSet<string> FailedSheetKeys = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>读取正式角色的全部 Base64 分段，拼接并解码成固定 512×576 图集。</summary>
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
                if (!TryAppendPart(normalizedKey, partIndex, encodedBuilder))
                {
                    FailedSheetKeys.Add(normalizedKey);
                    return null;
                }
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

            // 统一图集必须保持固定尺寸，否则后续头像、地图和战斗切片坐标都会错误。
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

    /// <summary>
    /// 追加一个主片段。
    /// 如果存在 sheet.N.0.b64，则优先读取四个更小的子片段；否则读取标准 sheet.N.b64。
    /// 这样可以只对确实需要的片段继续细分，而不破坏其余已经验证正确的角色资源。
    /// </summary>
    private static bool TryAppendPart(string normalizedKey, int partIndex, StringBuilder encodedBuilder)
    {
        string firstSubPartPath = $"res://assets/characters/{normalizedKey}/sheet.{partIndex}.0.b64";
        if (Godot.FileAccess.FileExists(firstSubPartPath))
        {
            for (int subPartIndex = 0; subPartIndex < SheetSubPartCount; subPartIndex++)
            {
                string subPartPath = $"res://assets/characters/{normalizedKey}/sheet.{partIndex}.{subPartIndex}.b64";
                if (!TryAppendTextFile(subPartPath, encodedBuilder))
                {
                    return false;
                }
            }

            return true;
        }

        string partPath = $"res://assets/characters/{normalizedKey}/sheet.{partIndex}.b64";
        return TryAppendTextFile(partPath, encodedBuilder);
    }

    /// <summary>读取一个非空 Base64 文本片段并追加到完整图集字符串。</summary>
    private static bool TryAppendTextFile(string path, StringBuilder encodedBuilder)
    {
        // 显式使用 Godot.FileAccess，避免与 System.IO.FileAccess 的枚举名称发生歧义。
        if (!Godot.FileAccess.FileExists(path))
        {
            return false;
        }

        string part = Godot.FileAccess.GetFileAsString(path).Trim();
        if (string.IsNullOrEmpty(part))
        {
            return false;
        }

        encodedBuilder.Append(part);
        return true;
    }
}
