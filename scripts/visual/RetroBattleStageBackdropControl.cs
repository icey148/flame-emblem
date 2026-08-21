using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 横向战斗界面的原创复古像素舞台背景。
/// 使用亮一些的蓝灰天空、远山、旧城墙和草地石台，让人物轮廓更清楚，同时保持硬边像素风格。
/// </summary>
public partial class RetroBattleStageBackdropControl : Control
{
    /// <summary>背景只负责绘制，不参与任何鼠标交互。</summary>
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
    }

    /// <summary>按由远到近的顺序绘制天空、远山、城墙、草地和战斗平台。</summary>
    public override void _Draw()
    {
        DrawSkyBands();
        DrawClouds();
        DrawMountains();
        DrawRuinedWall();
        DrawField();
        DrawStoneStage();
        DrawCharacterGrounding();
    }

    /// <summary>天空使用几条离散色带，不使用平滑渐变，避免破坏像素质感。</summary>
    private void DrawSkyBands()
    {
        DrawRect(new Rect2(0, 0, Size.X, 74), new Color(0.34f, 0.46f, 0.58f), true);
        DrawRect(new Rect2(0, 74, Size.X, 70), new Color(0.40f, 0.52f, 0.61f), true);
        DrawRect(new Rect2(0, 144, Size.X, 72), new Color(0.47f, 0.57f, 0.61f), true);
        DrawRect(new Rect2(0, 216, Size.X, 54), new Color(0.54f, 0.60f, 0.58f), true);
    }

    /// <summary>绘制低饱和硬边云块，让上半场不再是一整片空色。</summary>
    private void DrawClouds()
    {
        Color cloud = new(0.72f, 0.76f, 0.74f, 0.82f);
        Color cloudShadow = new(0.58f, 0.64f, 0.65f, 0.76f);

        DrawRect(new Rect2(92, 58, 118, 18), cloudShadow, true);
        DrawRect(new Rect2(110, 48, 86, 18), cloud, true);
        DrawRect(new Rect2(132, 40, 44, 12), cloud, true);

        DrawRect(new Rect2(760, 82, 164, 18), cloudShadow, true);
        DrawRect(new Rect2(786, 70, 122, 18), cloud, true);
        DrawRect(new Rect2(826, 58, 56, 14), cloud, true);
    }

    /// <summary>用阶梯多边形绘制两层远山，保持老式战棋横向舞台的层次感。</summary>
    private void DrawMountains()
    {
        PackedVector2Array farMountain = new(new[]
        {
            new Vector2(0, 208), new Vector2(0, 178), new Vector2(84, 126),
            new Vector2(164, 176), new Vector2(260, 110), new Vector2(348, 180),
            new Vector2(444, 132), new Vector2(548, 190), new Vector2(650, 118),
            new Vector2(748, 176), new Vector2(846, 128), new Vector2(946, 184),
            new Vector2(1084, 132), new Vector2(1084, 208)
        });
        DrawColoredPolygon(farMountain, new Color(0.29f, 0.38f, 0.40f));

        PackedVector2Array nearMountain = new(new[]
        {
            new Vector2(0, 244), new Vector2(0, 206), new Vector2(116, 166),
            new Vector2(236, 220), new Vector2(364, 164), new Vector2(496, 226),
            new Vector2(632, 170), new Vector2(768, 218), new Vector2(900, 164),
            new Vector2(1010, 214), new Vector2(1084, 188), new Vector2(1084, 244)
        });
        DrawColoredPolygon(nearMountain, new Color(0.24f, 0.33f, 0.32f));
    }

    /// <summary>战场中景使用残旧石墙和断旗，增加战争环境而不抢人物主体。</summary>
    private void DrawRuinedWall()
    {
        Color wallDark = new(0.31f, 0.31f, 0.29f);
        Color wall = new(0.43f, 0.42f, 0.37f);
        Color wallLight = new(0.53f, 0.51f, 0.43f);

        DrawRect(new Rect2(0, 222, Size.X, 46), wallDark, true);
        DrawRect(new Rect2(0, 218, 166, 10), wall, true);
        DrawRect(new Rect2(198, 218, 212, 10), wall, true);
        DrawRect(new Rect2(456, 218, 146, 10), wall, true);
        DrawRect(new Rect2(642, 218, 244, 10), wall, true);
        DrawRect(new Rect2(930, 218, 154, 10), wall, true);

        // 稀疏高光砖缝只用矩形表现，避免大量噪点让画面变脏。
        for (int x = 18; x < 1060; x += 82)
        {
            DrawRect(new Rect2(x, 232 + ((x / 82) % 2) * 14, 38, 4), wallLight, true);
        }

        // 左右两面褪色旗帜给阵营空间一点颜色，但不直接复制任何现有作品图案。
        DrawRect(new Rect2(82, 174, 4, 50), new Color(0.24f, 0.20f, 0.16f), true);
        DrawRect(new Rect2(86, 178, 28, 22), new Color(0.28f, 0.42f, 0.54f), true);
        DrawRect(new Rect2(994, 174, 4, 50), new Color(0.24f, 0.20f, 0.16f), true);
        DrawRect(new Rect2(966, 178, 28, 22), new Color(0.48f, 0.28f, 0.28f), true);
    }

    /// <summary>近景草地比旧背景明显更亮，为人物暗色轮廓提供清晰对比。</summary>
    private void DrawField()
    {
        DrawRect(new Rect2(0, 268, Size.X, 118), new Color(0.29f, 0.41f, 0.29f), true);
        DrawRect(new Rect2(0, 268, Size.X, 12), new Color(0.39f, 0.50f, 0.34f), true);
        DrawRect(new Rect2(0, 344, Size.X, 42), new Color(0.24f, 0.33f, 0.24f), true);

        Color grassLight = new(0.47f, 0.56f, 0.35f);
        for (int x = 26; x < 1060; x += 54)
        {
            int y = 294 + ((x / 54) % 3) * 14;
            DrawRect(new Rect2(x, y, 3, 10), grassLight, true);
            DrawRect(new Rect2(x + 4, y + 4, 3, 8), grassLight.Darkened(0.12f), true);
        }
    }

    /// <summary>人物脚下使用浅灰褐石台，明确横向战斗中的共同基准线。</summary>
    private void DrawStoneStage()
    {
        Color stoneDark = new(0.31f, 0.30f, 0.27f);
        Color stone = new(0.47f, 0.45f, 0.39f);
        Color stoneLight = new(0.59f, 0.55f, 0.46f);

        DrawRect(new Rect2(0, 386, Size.X, 44), stoneDark, true);
        DrawRect(new Rect2(0, 386, Size.X, 8), stoneLight, true);
        DrawRect(new Rect2(0, 394, Size.X, 36), stone, true);

        for (int x = 22; x < 1060; x += 96)
        {
            DrawRect(new Rect2(x, 404, 52, 3), stoneDark, true);
            DrawRect(new Rect2(x + 36, 418, 44, 3), stoneDark, true);
        }
    }

    /// <summary>左右人物脚下增加硬边接地阴影，减少人物像漂浮在背景上的感觉。</summary>
    private void DrawCharacterGrounding()
    {
        Color shadow = new(0.10f, 0.12f, 0.10f, 0.42f);
        DrawRect(new Rect2(154, 378, 226, 10), shadow, true);
        DrawRect(new Rect2(704, 378, 226, 10), shadow, true);
        DrawRect(new Rect2(186, 374, 162, 4), shadow, true);
        DrawRect(new Rect2(736, 374, 162, 4), shadow, true);
    }
}
