using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 独立战斗演出中的程序化像素风效果类型。
/// 这些效果只负责视觉反馈，不参与命中、伤害、经验或回合规则。
/// </summary>
public enum RetroBattleEffectKind
{
    None,
    Hit,
    Dodge,
    Critical,
    Magic,
    Arrow,
    Slash,
    Thrust
}

/// <summary>
/// 绘制命中、闪避、必杀、魔法以及不同武器的飞行/动作轨迹。
/// 战场背景已经由 RetroBattleStageBackdropControl 独立负责，本控件不再重复绘制暗色背景。
/// </summary>
public partial class RetroBattleEffectControl : Control
{
    /// <summary>当前效果类型。</summary>
    private RetroBattleEffectKind _kind;

    /// <summary>当前效果已经播放的时间。</summary>
    private float _elapsed;

    /// <summary>特效是否从左侧朝右侧释放。</summary>
    private bool _leftToRight = true;

    /// <summary>初始化纯特效层；不再改变子节点顺序或绘制背景。</summary>
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        Visible = true;
        SetProcess(false);
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
    /// 按攻击者职业武器选择蓄势阶段效果。
    /// 魔法优先使用能量飞行；物理武器分别使用箭矢、枪刺或剑斩轨迹。
    /// </summary>
    public void PlayWeaponWindup(
        CharacterWeaponSilhouette weapon,
        bool magical,
        bool leftToRight)
    {
        RetroBattleEffectKind kind = magical
            ? RetroBattleEffectKind.Magic
            : weapon switch
            {
                CharacterWeaponSilhouette.Bow => RetroBattleEffectKind.Arrow,
                CharacterWeaponSilhouette.Spear => RetroBattleEffectKind.Thrust,
                _ => RetroBattleEffectKind.Slash
            };

        Play(kind, leftToRight);
    }

    /// <summary>清除当前攻击特效；独立亮色战场背景不会受影响。</summary>
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

    /// <summary>只绘制当前攻击反馈，不再覆盖后方的亮色像素舞台。</summary>
    public override void _Draw()
    {
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
            case RetroBattleEffectKind.Arrow:
                DrawArrowEffect();
                break;
            case RetroBattleEffectKind.Slash:
                DrawSlashEffect();
                break;
            case RetroBattleEffectKind.Thrust:
                DrawThrustEffect();
                break;
        }
    }

    /// <summary>命中时在防守方位置绘制短促的硬边交叉冲击。</summary>
    private void DrawHitEffect()
    {
        float fade = Mathf.Clamp(1.0f - _elapsed / 0.32f, 0.0f, 1.0f);
        Vector2 center = DefenderCenter();
        Color bright = new(1.0f, 0.92f, 0.72f, fade);
        Color shadow = new(0.66f, 0.36f, 0.18f, fade * 0.82f);

        // 两层线条使用默认非抗锯齿绘制，保留块状冲击感。
        DrawLine(center + new Vector2(-34, -30), center + new Vector2(34, 30), shadow, 10.0f);
        DrawLine(center + new Vector2(-34, -30), center + new Vector2(34, 30), bright, 5.0f);
        DrawLine(center + new Vector2(-27, 34), center + new Vector2(27, -34), bright, 4.0f);

        // 四个方形火花代替平滑圆形粒子。
        DrawRect(new Rect2(center + new Vector2(-50, -7), new Vector2(12, 6)), bright, true);
        DrawRect(new Rect2(center + new Vector2(38, 5), new Vector2(14, 6)), bright, true);
        DrawRect(new Rect2(center + new Vector2(-5, -48), new Vector2(6, 12)), bright, true);
        DrawRect(new Rect2(center + new Vector2(7, 37), new Vector2(6, 14)), bright, true);
    }

    /// <summary>闪避时在防守方位置绘制向后拖出的速度线。</summary>
    private void DrawDodgeEffect()
    {
        float fade = Mathf.Clamp(1.0f - _elapsed / 0.35f, 0.0f, 1.0f);
        float retreatDirection = _leftToRight ? 1.0f : -1.0f;
        Color streak = new(0.72f, 0.88f, 1.0f, fade);
        Vector2 center = DefenderCenter();

        for (int index = -2; index <= 2; index++)
        {
            float y = center.Y + index * 18.0f;
            DrawLine(
                new Vector2(center.X - retreatDirection * 74.0f, y),
                new Vector2(center.X + retreatDirection * 12.0f, y),
                streak,
                4.0f);
        }
    }

    /// <summary>必杀时绘制一次浅色闪光和防守方位置的像素爆发线。</summary>
    private void DrawCriticalEffect()
    {
        float flash = Mathf.Clamp(1.0f - _elapsed / 0.42f, 0.0f, 1.0f);
        DrawRect(
            new Rect2(Vector2.Zero, Size),
            new Color(1.0f, 0.93f, 0.72f, flash * 0.22f),
            true);

        Vector2 center = DefenderCenter();
        Color ray = new(1.0f, 0.78f, 0.28f, flash);

        // 八个方向使用水平、垂直和阶梯斜线，不使用平滑圆形爆发。
        DrawLine(center + new Vector2(20, 0), center + new Vector2(112, 0), ray, 6.0f);
        DrawLine(center + new Vector2(-20, 0), center + new Vector2(-112, 0), ray, 6.0f);
        DrawLine(center + new Vector2(0, 20), center + new Vector2(0, 100), ray, 6.0f);
        DrawLine(center + new Vector2(0, -20), center + new Vector2(0, -100), ray, 6.0f);
        DrawLine(center + new Vector2(15, 15), center + new Vector2(76, 76), ray, 5.0f);
        DrawLine(center + new Vector2(-15, 15), center + new Vector2(-76, 76), ray, 5.0f);
        DrawLine(center + new Vector2(15, -15), center + new Vector2(76, -76), ray, 5.0f);
        DrawLine(center + new Vector2(-15, -15), center + new Vector2(-76, -76), ray, 5.0f);
    }

    /// <summary>
    /// 弓箭在蓄势时间内真正从攻击方飞向防守方。
    /// 箭杆、箭头和尾羽全部使用整数矩形，移动坐标也锁到整屏幕像素。
    /// </summary>
    private void DrawArrowEffect()
    {
        float t = Mathf.Clamp(_elapsed / 0.34f, 0.0f, 1.0f);
        float direction = _leftToRight ? 1.0f : -1.0f;
        Vector2 from = AttackerLaunchPoint();
        Vector2 to = DefenderCenter() + new Vector2(-direction * 26.0f, -6.0f);

        // 轻微抛物线只改变纵坐标，但最后仍取整，避免亚像素模糊。
        float arc = -Mathf.Sin(t * Mathf.Pi) * 18.0f;
        Vector2 center = SnapScreenPixel(from.Lerp(to, t) + new Vector2(0, arc));
        Color shaft = new(0.48f, 0.31f, 0.18f, 1.0f);
        Color metal = new(0.88f, 0.87f, 0.78f, 1.0f);
        Color feather = new(0.78f, 0.65f, 0.42f, 1.0f);

        float shaftX = direction > 0 ? center.X - 22 : center.X - 22;
        DrawRect(new Rect2(shaftX, center.Y - 2, 44, 4), shaft, true);

        float tipX = direction > 0 ? center.X + 20 : center.X - 28;
        DrawRect(new Rect2(tipX, center.Y - 5, 8, 10), metal, true);
        DrawRect(new Rect2(direction > 0 ? center.X - 27 : center.X + 19, center.Y - 7, 8, 4), feather, true);
        DrawRect(new Rect2(direction > 0 ? center.X - 27 : center.X + 19, center.Y + 3, 8, 4), feather, true);

        // 快速移动时在箭尾留两段短轨迹，加强“已经离弓”的可读性。
        Color trail = new(0.90f, 0.84f, 0.64f, 0.48f);
        DrawRect(new Rect2(center.X - direction * 46 - 8, center.Y - 2, 14, 3), trail, true);
        DrawRect(new Rect2(center.X - direction * 68 - 6, center.Y - 1, 10, 2), trail, true);
    }

    /// <summary>剑士攻击时绘制从身体前方扫出的三段硬边斩击轨迹。</summary>
    private void DrawSlashEffect()
    {
        float t = Mathf.Clamp(_elapsed / 0.34f, 0.0f, 1.0f);
        float direction = _leftToRight ? 1.0f : -1.0f;
        Vector2 start = AttackerLaunchPoint() + new Vector2(direction * 12, -42);
        Vector2 center = SnapScreenPixel(start + new Vector2(direction * t * 118.0f, t * 74.0f));
        float fade = Mathf.Clamp(1.0f - MathF.Abs(t - 0.55f) * 1.35f, 0.22f, 1.0f);
        Color outer = new(0.45f, 0.34f, 0.20f, fade * 0.72f);
        Color blade = new(0.96f, 0.91f, 0.74f, fade);

        // 三条平行硬边轨迹形成“刃面”，比单根细线更接近像素剑光。
        DrawLine(center + new Vector2(-direction * 54, -42), center + new Vector2(direction * 42, 44), outer, 11.0f);
        DrawLine(center + new Vector2(-direction * 52, -40), center + new Vector2(direction * 40, 42), blade, 5.0f);
        DrawLine(center + new Vector2(-direction * 34, -50), center + new Vector2(direction * 50, 28), blade, 3.0f);
    }

    /// <summary>枪兵攻击时显示从枪尖向前延伸的直线压迫感和短促空气轨迹。</summary>
    private void DrawThrustEffect()
    {
        float t = Mathf.Clamp(_elapsed / 0.34f, 0.0f, 1.0f);
        float direction = _leftToRight ? 1.0f : -1.0f;
        Vector2 start = AttackerLaunchPoint() + new Vector2(direction * 12, 10);
        float length = 34.0f + t * 150.0f;
        Color dark = new(0.37f, 0.29f, 0.20f, 0.64f);
        Color light = new(0.92f, 0.88f, 0.72f, 0.88f);

        Vector2 end = SnapScreenPixel(start + new Vector2(direction * length, 0));
        DrawLine(start, end, dark, 9.0f);
        DrawLine(start, end, light, 3.0f);

        // 枪尖前方的三条短线在最大突刺阶段展开，强调直线速度而不是剑的弧形轨迹。
        if (t > 0.42f)
        {
            DrawLine(end + new Vector2(-direction * 28, -13), end + new Vector2(direction * 18, -13), light, 3.0f);
            DrawLine(end + new Vector2(-direction * 34, 0), end + new Vector2(direction * 24, 0), light, 4.0f);
            DrawLine(end + new Vector2(-direction * 28, 13), end + new Vector2(direction * 18, 13), light, 3.0f);
        }
    }

    /// <summary>
    /// 魔法从施法方聚成方形能量核后飞向防守方，沿途留下离散像素尾迹。
    /// 不使用圆形/圆弧，避免在复古像素人物旁边出现过于平滑的现代特效。
    /// </summary>
    private void DrawMagicEffect()
    {
        float t = Mathf.Clamp(_elapsed / 0.50f, 0.0f, 1.0f);
        Vector2 from = AttackerLaunchPoint() + new Vector2(0, -42);
        Vector2 to = DefenderCenter() + new Vector2(0, -26);
        Vector2 center = SnapScreenPixel(from.Lerp(to, t));
        float pulse = 12.0f + Mathf.Abs(Mathf.Sin(_elapsed * 18.0f)) * 7.0f;
        int half = Math.Max(6, (int)MathF.Round(pulse));

        Color glow = new(0.57f, 0.78f, 1.0f, 0.55f);
        Color core = new(0.88f, 0.66f, 1.0f, 0.96f);
        Color bright = new(0.96f, 0.88f, 1.0f, 0.94f);

        DrawRect(new Rect2(center.X - half - 7, center.Y - 5, (half + 7) * 2, 10), glow, true);
        DrawRect(new Rect2(center.X - 5, center.Y - half - 7, 10, (half + 7) * 2), glow, true);
        DrawRect(new Rect2(center.X - half, center.Y - half, half * 2, half * 2), core, true);
        DrawRect(new Rect2(center.X - 5, center.Y - 5, 10, 10), bright, true);

        float direction = _leftToRight ? 1.0f : -1.0f;
        for (int index = 1; index <= 3; index++)
        {
            Vector2 trail = SnapScreenPixel(center - new Vector2(direction * index * 24.0f, index * 3.0f));
            int size = Math.Max(4, 11 - index * 2);
            DrawRect(new Rect2(trail.X - size / 2.0f, trail.Y - size / 2.0f, size, size), glow, true);
        }
    }

    /// <summary>返回攻击方武器/施法效果在舞台中的近似发射点。</summary>
    private Vector2 AttackerLaunchPoint()
    {
        return _leftToRight
            ? new Vector2(Size.X * 0.30f, Size.Y * 0.50f)
            : new Vector2(Size.X * 0.70f, Size.Y * 0.50f);
    }

    /// <summary>返回防守方身体中心，用于命中、闪避和飞行终点。</summary>
    private Vector2 DefenderCenter()
    {
        return _leftToRight
            ? new Vector2(Size.X * 0.73f, Size.Y * 0.50f)
            : new Vector2(Size.X * 0.27f, Size.Y * 0.50f);
    }

    /// <summary>把动画坐标锁到整数屏幕像素，避免飞行效果产生半像素模糊。</summary>
    private static Vector2 SnapScreenPixel(Vector2 value)
    {
        return new Vector2(Mathf.Round(value.X), Mathf.Round(value.Y));
    }
}
