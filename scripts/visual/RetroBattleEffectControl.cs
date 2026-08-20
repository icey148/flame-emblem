using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 独立战斗演出中的程序化像素风效果类型。
/// 当前使用原创几何效果保证攻击反馈完整，之后可以逐项替换为正式特效序列帧。
/// </summary>
public enum RetroBattleEffectKind
{
    None,
    Hit,
    Dodge,
    Critical,
    Magic
}

/// <summary>
/// 绘制复古像素战斗舞台，以及命中、闪避、必杀与魔法的轻量战斗特效。
/// 该控件完全属于表现层，不会修改单位生命值或战斗结果。
/// </summary>
public partial class RetroBattleEffectControl : Control
{
    /// <summary>当前效果类型。</summary>
    private RetroBattleEffectKind _kind;

    /// <summary>当前效果已经播放的时间。</summary>
    private float _elapsed;

    /// <summary>特效是否从左侧朝右侧释放。</summary>
    private bool _leftToRight = true;

    /// <summary>
    /// 初始化战斗舞台。
    /// 把本控件移到 stage 的第一个子节点，使背景稳定绘制在人物和 HUD 后方，同时仍位于战斗面板内部。
    /// </summary>
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        Visible = true;

        Node? parent = GetParent();
        if (parent is not null && GetIndex() != 0)
        {
            parent.MoveChild(this, 0);
        }

        QueueRedraw();
    }

    /// <summary>开始播放一个新效果。</summary>
    public void Play(RetroBattleEffectKind kind, bool leftToRight)
    {
        _kind = kind;
        _leftToRight = leftToRight;
        _elapsed = 0.0f;
        Visible = true;
        SetProcess(kind != RetroBattleEffectKind.None);
        QueueRedraw();
    }

    /// <summary>
    /// 清除当前攻击特效，但保留像素战场背景。
    /// 旧实现会把整个控件隐藏，导致战斗间隙重新露出纯黑面板。
    /// </summary>
    public void Clear()
    {
        _kind = RetroBattleEffectKind.None;
        _elapsed = 0.0f;
        Visible = true;
        SetProcess(false);
        QueueRedraw();
    }

    /// <summary>推进效果时间并重绘。</summary>
    public override void _Process(double delta)
    {
        if (_kind == RetroBattleEffectKind.None)
        {
            SetProcess(false);
            return;
        }

        _elapsed += Math.Max(0.0f, (float)delta);
        QueueRedraw();
    }

    /// <summary>
    /// 先绘制原创复古战场，再根据效果类型叠加攻击反馈。
    /// 背景使用水平色带、阶梯远山和块状地面，避免渐变与抗锯齿造成现代平滑感。
    /// </summary>
    public override void _Draw()
    {
        DrawBattleStage();

        switch (_kind)
        {
            case RetroBattleEffectKind.Hit:
                DrawHitEffect();
                break;
            case RetroBattleEffectKind.Dodge:
                DrawDodgeEffect();
                break;
            case RetroBattleEffectKind.Critical:
                DrawCriticalEffect();
                break;
            case RetroBattleEffectKind.Magic:
                DrawMagicEffect();
                break;
        }
    }

    /// <summary>
    /// 绘制原创的古典战棋横向战斗舞台。
    /// 视觉语言使用低饱和天空、远山、旧城墙、草地与石质前景，保持战争中的古典乡野氛围，但不复制任何原作背景。
    /// </summary>
    private void DrawBattleStage()
    {
        float width = Math.Max(1.0f, Size.X);
        float height = Math.Max(1.0f, Size.Y);
        int horizon = (int)MathF.Round(height * 0.58f);

        // 天空使用离散水平色带，而不是连续渐变，放大后仍保持像素游戏的硬边层次。
        DrawRect(new Rect2(0, 0, width, horizon * 0.34f), new Color(0.19f, 0.25f, 0.32f), true);
        DrawRect(new Rect2(0, horizon * 0.34f, width, horizon * 0.34f), new Color(0.27f, 0.33f, 0.37f), true);
        DrawRect(new Rect2(0, horizon * 0.68f, width, horizon * 0.32f), new Color(0.38f, 0.39f, 0.36f), true);

        // 稀疏的块状云只使用矩形，避免圆形云朵带来平滑卡通感。
        DrawPixelCloud(new Vector2(width * 0.12f, height * 0.14f), 1.0f);
        DrawPixelCloud(new Vector2(width * 0.67f, height * 0.10f), 0.82f);

        // 远山使用阶梯矩形建立层次，轮廓比人物更暗、更低对比，保证角色仍是视觉焦点。
        DrawSteppedHill(width * 0.02f, horizon, width * 0.32f, height * 0.22f, new Color(0.18f, 0.24f, 0.23f));
        DrawSteppedHill(width * 0.30f, horizon, width * 0.28f, height * 0.18f, new Color(0.22f, 0.27f, 0.24f));
        DrawSteppedHill(width * 0.58f, horizon, width * 0.40f, height * 0.24f, new Color(0.17f, 0.22f, 0.21f));

        // 右侧远景加入低矮旧城墙，让战斗场景更像一处真实战场，而不是空摄影棚。
        Color wallDark = new(0.24f, 0.24f, 0.22f);
        Color wallLight = new(0.31f, 0.30f, 0.27f);
        float wallY = horizon - 54;
        DrawRect(new Rect2(width * 0.72f, wallY, width * 0.23f, 54), wallDark, true);
        for (int index = 0; index < 6; index++)
        {
            DrawRect(new Rect2(width * 0.72f + index * 42, wallY - 12, 25, 14), wallLight, true);
        }
        DrawRect(new Rect2(width * 0.80f, wallY + 15, 34, 39), new Color(0.12f, 0.13f, 0.13f), true);

        // 中景草地使用两个色带和稀疏草簇，不把地面做成纯色矩形。
        DrawRect(new Rect2(0, horizon, width, height - horizon), new Color(0.21f, 0.30f, 0.20f), true);
        DrawRect(new Rect2(0, horizon + 48, width, height - horizon - 48), new Color(0.18f, 0.24f, 0.18f), true);
        for (int index = 0; index < 18; index++)
        {
            float x = 18 + index * 63;
            float y = horizon + 18 + (index % 3) * 17;
            DrawRect(new Rect2(x, y, 4, 12), new Color(0.31f, 0.40f, 0.24f), true);
            DrawRect(new Rect2(x + 5, y + 5, 4, 8), new Color(0.27f, 0.36f, 0.22f), true);
        }

        // 前景石质战斗平台提供稳定脚底基准，并用裂纹打破规则方块感。
        float platformY = height - 62;
        DrawRect(new Rect2(0, platformY, width, 62), new Color(0.27f, 0.25f, 0.22f), true);
        DrawRect(new Rect2(0, platformY, width, 5), new Color(0.43f, 0.38f, 0.29f), true);
        for (int index = 0; index < 14; index++)
        {
            float x = index * 82;
            DrawLine(
                new Vector2(x, platformY + 6),
                new Vector2(x + 24, platformY + 28),
                new Color(0.14f, 0.13f, 0.12f),
                3.0f);
            DrawLine(
                new Vector2(x + 24, platformY + 28),
                new Vector2(x + 16, platformY + 45),
                new Color(0.14f, 0.13f, 0.12f),
                2.0f);
        }
    }

    /// <summary>绘制一组块状像素云。</summary>
    private void DrawPixelCloud(Vector2 origin, float scale)
    {
        Color cloud = new(0.58f, 0.59f, 0.56f, 0.72f);
        DrawRect(new Rect2(origin, new Vector2(78 * scale, 14 * scale)), cloud, true);
        DrawRect(new Rect2(origin + new Vector2(18 * scale, -10 * scale), new Vector2(38 * scale, 12 * scale)), cloud, true);
        DrawRect(new Rect2(origin + new Vector2(55 * scale, 5 * scale), new Vector2(42 * scale, 9 * scale)), cloud, true);
    }

    /// <summary>使用多级矩形而不是抗锯齿多边形绘制远山。</summary>
    private void DrawSteppedHill(float x, float horizon, float width, float height, Color color)
    {
        const int steps = 6;
        float stepWidth = width / steps;
        for (int index = 0; index < steps; index++)
        {
            float normalized = index / (float)(steps - 1);
            float peak = 1.0f - MathF.Abs(normalized * 2.0f - 1.0f);
            float blockHeight = Math.Max(18.0f, height * (0.38f + peak * 0.62f));
            DrawRect(
                new Rect2(x + index * stepWidth, horizon - blockHeight, stepWidth + 2, blockHeight),
                color,
                true);
        }
    }

    /// <summary>命中时绘制短促的交叉斩击线。</summary>
    private void DrawHitEffect()
    {
        float fade = Mathf.Clamp(1.0f - _elapsed / 0.32f, 0.0f, 1.0f);
        Vector2 center = new(Size.X * 0.5f, Size.Y * 0.48f);
        Color bright = new(1.0f, 0.92f, 0.72f, fade);
        DrawLine(center + new Vector2(-42, -36), center + new Vector2(42, 36), bright, 8.0f);
        DrawLine(center + new Vector2(-34, 42), center + new Vector2(34, -42), bright, 5.0f);
    }

    /// <summary>闪避时绘制向后拖出的速度线。</summary>
    private void DrawDodgeEffect()
    {
        float fade = Mathf.Clamp(1.0f - _elapsed / 0.35f, 0.0f, 1.0f);
        float direction = _leftToRight ? 1.0f : -1.0f;
        Color streak = new(0.72f, 0.88f, 1.0f, fade);
        Vector2 center = new(Size.X * 0.5f, Size.Y * 0.5f);

        for (int index = -2; index <= 2; index++)
        {
            float y = center.Y + index * 18.0f;
            DrawLine(
                new Vector2(center.X - direction * 65.0f, y),
                new Vector2(center.X + direction * 15.0f, y),
                streak,
                4.0f);
        }
    }

    /// <summary>必杀时绘制一次全屏闪光和中央爆发线。</summary>
    private void DrawCriticalEffect()
    {
        float flash = Mathf.Clamp(1.0f - _elapsed / 0.42f, 0.0f, 1.0f);
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(1.0f, 0.93f, 0.72f, flash * 0.34f), true);

        Vector2 center = new(Size.X * 0.5f, Size.Y * 0.46f);
        Color ray = new(1.0f, 0.78f, 0.28f, flash);
        for (int index = 0; index < 8; index++)
        {
            float angle = Mathf.Tau * index / 8.0f;
            Vector2 direction = Vector2.Right.Rotated(angle);
            DrawLine(center + direction * 20.0f, center + direction * 105.0f, ray, 6.0f);
        }
    }

    /// <summary>魔法时绘制从施法方朝目标方飞行的能量核心与环形波纹。</summary>
    private void DrawMagicEffect()
    {
        float t = Mathf.Clamp(_elapsed / 0.48f, 0.0f, 1.0f);
        float fromX = _leftToRight ? Size.X * 0.35f : Size.X * 0.65f;
        float toX = _leftToRight ? Size.X * 0.65f : Size.X * 0.35f;
        Vector2 center = new(Mathf.Lerp(fromX, toX, t), Size.Y * 0.45f);
        float pulse = 12.0f + Mathf.Abs(Mathf.Sin(_elapsed * 18.0f)) * 8.0f;
        Color core = new(0.88f, 0.62f, 1.0f, 0.95f);
        Color ring = new(0.56f, 0.82f, 1.0f, 0.75f);

        DrawCircle(center, pulse, core);
        DrawArc(center, pulse + 12.0f, 0, Mathf.Tau, 24, ring, 5.0f);
    }
}
