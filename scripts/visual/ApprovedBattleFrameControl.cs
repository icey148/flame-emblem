using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 绘制最终定稿战斗界面的整屏金色装饰框、人物落脚线和底部中央菱形装饰。
/// 本控件只负责静态视觉，不读取单位、不参与战斗规则，也不会拦截鼠标输入。
/// </summary>
public partial class ApprovedBattleFrameControl : Control
{
    /// <summary>主金色，负责外框与主要分隔线。</summary>
    private static readonly Color Gold = new("b8833f");

    /// <summary>较暗金色，负责第二层细线，避免边框显得过亮。</summary>
    private static readonly Color GoldDark = new("6f4924");

    /// <summary>中心宝石使用的深蓝色，与我方阵营色呼应。</summary>
    private static readonly Color CenterGem = new("1f5b92");

    /// <summary>节点就绪后锁定尺寸、鼠标行为并请求首次绘制。</summary>
    public override void _Ready()
    {
        Position = Vector2.Zero;
        Size = ReferenceBattleLayout.ViewportSize;
        MouseFilter = MouseFilterEnum.Ignore;
        QueueRedraw();
    }

    /// <summary>绘制整屏双层金边与克制的角落装饰。</summary>
    public override void _Draw()
    {
        float inset = ReferenceBattleLayout.FrameInset;
        Rect2 outer = new(
            new Vector2(inset, inset),
            ReferenceBattleLayout.ViewportSize - new Vector2(inset * 2.0f, inset * 2.0f));
        Rect2 inner = outer.Grow(-4.0f);

        // 双层细金边直接对应最终设计图的黑底金线框架。
        DrawRect(outer, Gold, false, 2.0f);
        DrawRect(inner, GoldDark, false, 1.0f);

        DrawCornerOrnament(outer.Position, new Vector2(1, 1));
        DrawCornerOrnament(new Vector2(outer.End.X, outer.Position.Y), new Vector2(-1, 1));
        DrawCornerOrnament(new Vector2(outer.Position.X, outer.End.Y), new Vector2(1, -1));
        DrawCornerOrnament(outer.End, new Vector2(-1, -1));

        // 人物脚下只保留极细的暗金地面线，不再添加旧版蓝灰舞台或地形背景。
        float groundY = ReferenceBattleLayout.GroundLineY;
        DrawLine(new Vector2(34, groundY), new Vector2(1246, groundY), GoldDark, 2.0f, false);
        DrawLine(new Vector2(96, groundY + 3), new Vector2(1184, groundY + 3), new Color(0.08f, 0.06f, 0.04f), 2.0f, false);

        DrawCenterGem();
    }

    /// <summary>用短线组合出不依赖贴图的角落装饰，保证任何导出环境都稳定显示。</summary>
    private void DrawCornerOrnament(Vector2 anchor, Vector2 direction)
    {
        Vector2 horizontal = new(direction.X * 34.0f, 0);
        Vector2 vertical = new(0, direction.Y * 34.0f);
        Vector2 innerHorizontal = new(direction.X * 18.0f, 0);
        Vector2 innerVertical = new(0, direction.Y * 18.0f);

        DrawLine(anchor, anchor + horizontal, Gold, 2.0f, false);
        DrawLine(anchor, anchor + vertical, Gold, 2.0f, false);
        DrawLine(anchor + new Vector2(0, direction.Y * 6.0f), anchor + new Vector2(innerHorizontal.X, direction.Y * 6.0f), GoldDark, 1.0f, false);
        DrawLine(anchor + new Vector2(direction.X * 6.0f, 0), anchor + new Vector2(direction.X * 6.0f, innerVertical.Y), GoldDark, 1.0f, false);
    }

    /// <summary>在画面底部中央绘制定稿图里的小型蓝色菱形节点。</summary>
    private void DrawCenterGem()
    {
        Vector2 center = new(ReferenceBattleLayout.ViewportSize.X * 0.5f, ReferenceBattleLayout.ViewportSize.Y - 12.0f);
        Vector2 top = center + new Vector2(0, -10);
        Vector2 right = center + new Vector2(10, 0);
        Vector2 bottom = center + new Vector2(0, 10);
        Vector2 left = center + new Vector2(-10, 0);

        // 先用较粗金线勾外轮廓，再画内层蓝色宝石，避免依赖多边形填充 API。
        DrawLine(top, right, Gold, 3.0f, false);
        DrawLine(right, bottom, Gold, 3.0f, false);
        DrawLine(bottom, left, Gold, 3.0f, false);
        DrawLine(left, top, Gold, 3.0f, false);

        DrawLine(center + new Vector2(0, -6), center + new Vector2(6, 0), CenterGem, 4.0f, false);
        DrawLine(center + new Vector2(6, 0), center + new Vector2(0, 6), CenterGem, 4.0f, false);
        DrawLine(center + new Vector2(0, 6), center + new Vector2(-6, 0), CenterGem, 4.0f, false);
        DrawLine(center + new Vector2(-6, 0), center + new Vector2(0, -6), CenterGem, 4.0f, false);
    }
}
