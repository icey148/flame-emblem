using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 为战斗界面提供统一的硬边系统字体。
/// 不打包任何外部字体文件，而是从当前系统选择可显示中文的字体，并关闭抗锯齿与次像素定位，
/// 让 1280×720 基准画面中的文字更接近参考稿的像素硬边效果。
/// </summary>
public static class BattlePixelFontCatalog
{
    /// <summary>缓存一份共享字体资源，避免每个 Label 都重复创建系统字体。</summary>
    private static SystemFont? _font;

    /// <summary>返回战斗 UI 使用的硬边字体。</summary>
    public static Font Font
    {
        get
        {
            _font ??= CreateFont();
            return _font;
        }
    }

    /// <summary>创建关闭抗锯齿、关闭次像素定位的系统字体。</summary>
    private static SystemFont CreateFont()
    {
        return new SystemFont
        {
            // 优先使用常见中文无衬线字体；系统不存在时继续由 Godot 做系统回退。
            FontNames = new[]
            {
                "Microsoft YaHei UI",
                "Noto Sans CJK SC",
                "PingFang SC",
                "SimHei",
                "sans-serif"
            },
            FontWeight = 700,
            Antialiasing = TextServer.FontAntialiasing.None,
            SubpixelPositioning = TextServer.SubpixelPositioning.Disabled,
            Hinting = TextServer.Hinting.Normal,
            GenerateMipmaps = false,
            AllowSystemFallback = true,
            Oversampling = 1.0f
        };
    }
}