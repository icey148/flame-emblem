using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 独立战斗演出中的程序化像素风效果类型。
/// 当前先用原创几何效果保证攻击反馈完整，之后可以逐项替换为正式特效序列帧。
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
/// 绘制命中、闪避、必杀与魔法的轻量战斗特效。
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
    /// 开始播放一个新效果。
    /// </summary>
    public void Play(RetroBattleEffectKind kind, bool leftToRight)
    {
        _kind = kind;
        _leftToRight = leftToRight;
        _elapsed = 0.0f;
        Visible = kind != RetroBattleEffectKind.None;
        QueueRedraw();
    }

    /// <summary>
    /// 清除当前效果。
    /// </summary>
    public void Clear()
    {
        _kind = RetroBattleEffectKind.None;
        _elapsed = 0.0f;
        Visible = false;
        QueueRedraw();
    }

    /// <summary>
    /// 推进效果时间并重绘。
    /// </summary>
    public override void _Process(double delta)
    {
        if (!Visible)
        {
            return;
        }

        _elapsed += (float)delta;
        QueueRedraw();
    }

    /// <summary>
    /// 根据效果类型绘制简洁的复古战斗反馈。
    /// </summary>
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
        }
    }

    /// <summary>
    /// 命中时绘制短促的交叉斩击线。
    /// </summary>
    private void DrawHitEffect()
    {
        float fade = Mathf.Clamp(1.0f - _elapsed / 0.32f, 0.0f, 1.0f);
        Vector2 center = new(Size.X * 0.5f, Size.Y * 0.48f);
        Color bright = new(1.0f, 0.92f, 0.72f, fade);
        DrawLine(center + new Vector2(-42, -36), center + new Vector2(42, 36), bright, 8.0f);
        DrawLine(center + new Vector2(-34, 42), center + new Vector2(34, -42), bright, 5.0f);
    }

    /// <summary>
    /// 闪避时绘制向后拖出的速度线。
    /// </summary>
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

    /// <summary>
    /// 必杀时绘制一次全屏闪光和中央爆发线。
    /// </summary>
    private void DrawCriticalEffect()
    {
        float flash = Mathf.Clamp(1.0f - _elapsed / 0.42f, 0.0f, 1.0f);
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(1.0f, 0.93f, 0.72f, flash * 0.42f), true);

        Vector2 center = new(Size.X * 0.5f, Size.Y * 0.46f);
        Color ray = new(1.0f, 0.78f, 0.28f, flash);
        for (int index = 0; index < 8; index++)
        {
            float angle = Mathf.Tau * index / 8.0f;
            Vector2 direction = Vector2.Right.Rotated(angle);
            DrawLine(center + direction * 20.0f, center + direction * 105.0f, ray, 6.0f);
        }
    }

    /// <summary>
    /// 魔法时绘制从施法方朝目标方飞行的能量核心与环形波纹。
    /// </summary>
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
