using Godot;
using System.Reflection;

namespace FlameEmblem.Visual;

/// <summary>
/// 替换 RetroBattleEffectControl 的旧大块程序特效。
/// 原控件继续接收 Play/Clear 并维持准确计时；本层只读取类型、时间和方向，绘制更紧凑的像素命中与武器轨迹。
/// </summary>
public partial class CinematicBattleEffectControl : Control
{
    /// <summary>仍由战斗时间线驱动的原特效控件。</summary>
    private RetroBattleEffectControl? _source;

    /// <summary>原控件当前效果类型字段。</summary>
    private FieldInfo? _kindField;

    /// <summary>原控件效果计时字段。</summary>
    private FieldInfo? _elapsedField;

    /// <summary>原控件攻击方向字段。</summary>
    private FieldInfo? _leftToRightField;

    /// <summary>绑定原特效控件并隐藏其旧绘制内容。</summary>
    public void Bind(RetroBattleEffectControl source)
    {
        _source = source;
        BindingFlags members = BindingFlags.Instance | BindingFlags.NonPublic;
        Type type = typeof(RetroBattleEffectControl);
        _kindField = type.GetField("_kind", members);
        _elapsedField = type.GetField("_elapsed", members);
        _leftToRightField = type.GetField("_leftToRight", members);

        Position = Vector2.Zero;
        Size = source.Size;
        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = CanvasItem.TextureFilterEnum.Nearest;

        // SelfModulate 只隐藏原控件自身绘制，不会隐藏作为子节点的本层。
        source.SelfModulate = new Color(1, 1, 1, 0);
        SetProcess(true);
        QueueRedraw();
    }

    /// <summary>原控件可能随舞台调整尺寸，因此逐帧同步并重绘。</summary>
    public override void _Process(double delta)
    {
        if (_source is not null)
        {
            if (Size != _source.Size)
            {
                Size = _source.Size;
            }

            // 其它表现协调器不应重新把旧特效显示出来；这里保持原绘制透明。
            if (_source.SelfModulate.A > 0.001f)
            {
                _source.SelfModulate = new Color(1, 1, 1, 0);
            }
        }

        QueueRedraw();
    }

    /// <summary>根据原时间线当前效果类型绘制新的紧凑像素反馈。</summary>
    public override void _Draw()
    {
        RetroBattleEffectKind kind = ReadKind();
        float elapsed = ReadElapsed();
        bool leftToRight = ReadLeftToRight();

        switch (kind)
        {
            case RetroBattleEffectKind.Hit:
                DrawCompactHit(elapsed, leftToRight);
                break;
            case RetroBattleEffectKind.Dodge:
                DrawDodgeStreaks(elapsed, leftToRight);
                break;
            case RetroBattleEffectKind.Critical:
                DrawCriticalBurst(elapsed, leftToRight);
                break;
            case RetroBattleEffectKind.Arrow:
                DrawArrow(elapsed, leftToRight);
                break;
            case RetroBattleEffectKind.Slash:
                DrawSwordTrail(elapsed, leftToRight);
                break;
            case RetroBattleEffectKind.Thrust:
                DrawSpearPressure(elapsed, leftToRight);
                break;
            case RetroBattleEffectKind.Magic:
                DrawMagicGlyph(elapsed, leftToRight);
                break;
        }
    }

    /// <summary>普通命中只在防守方胸口附近形成一次紧凑十字爆点，不再覆盖大半人物。</summary>
    private void DrawCompactHit(float elapsed, bool leftToRight)
    {
        float t = Mathf.Clamp(elapsed / 0.28f, 0.0f, 1.0f);
        float fade = 1.0f - t;
        float spread = 12.0f + EaseOut(t) * 30.0f;
        Vector2 center = DefenderCenter(leftToRight);
        Color warm = new(1.0f, 0.86f, 0.57f, fade);
        Color hot = new(1.0f, 0.97f, 0.82f, fade);

        DrawRect(new Rect2(center.X - 7, center.Y - 7, 14, 14), hot, true);
        DrawRect(new Rect2(center.X - spread - 10, center.Y - 2, 10, 4), warm, true);
        DrawRect(new Rect2(center.X + spread, center.Y - 2, 10, 4), warm, true);
        DrawRect(new Rect2(center.X - 2, center.Y - spread - 10, 4, 10), warm, true);
        DrawRect(new Rect2(center.X - 2, center.Y + spread, 4, 10), warm, true);

        // 四个短斜角只作为余光，不使用贯穿屏幕的大交叉线。
        DrawRect(new Rect2(center + new Vector2(-spread * 0.70f - 5, -spread * 0.70f - 5), new Vector2(8, 4)), warm, true);
        DrawRect(new Rect2(center + new Vector2(spread * 0.70f, -spread * 0.70f - 5), new Vector2(8, 4)), warm, true);
        DrawRect(new Rect2(center + new Vector2(-spread * 0.70f - 5, spread * 0.70f), new Vector2(8, 4)), warm, true);
        DrawRect(new Rect2(center + new Vector2(spread * 0.70f, spread * 0.70f), new Vector2(8, 4)), warm, true);
    }

    /// <summary>闪避只留下三条细速度线，方向与人物后撤一致。</summary>
    private void DrawDodgeStreaks(float elapsed, bool leftToRight)
    {
        float t = Mathf.Clamp(elapsed / 0.30f, 0.0f, 1.0f);
        float fade = 1.0f - t;
        float retreat = leftToRight ? 1.0f : -1.0f;
        Vector2 center = DefenderCenter(leftToRight);
        Color streak = new(0.76f, 0.88f, 1.0f, fade * 0.85f);

        for (int index = -1; index <= 1; index++)
        {
            float y = center.Y + index * 18.0f;
            DrawLine(
                new Vector2(center.X - retreat * 52.0f, y),
                new Vector2(center.X - retreat * 8.0f, y),
                streak,
                index == 0 ? 4.0f : 2.0f,
                false);
        }
    }

    /// <summary>必杀使用非常短的暖色闪屏和分段爆发，强调力度但不长时间遮住人物。</summary>
    private void DrawCriticalBurst(float elapsed, bool leftToRight)
    {
        float t = Mathf.Clamp(elapsed / 0.38f, 0.0f, 1.0f);
        float fade = 1.0f - t;
        Vector2 center = DefenderCenter(leftToRight);

        // 闪屏只持续前约三分之一，避免旧版整段都泛白。
        float flash = Mathf.Clamp(1.0f - t * 3.2f, 0.0f, 1.0f);
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(1.0f, 0.94f, 0.72f, flash * 0.18f), true);

        Color gold = new(1.0f, 0.73f, 0.24f, fade);
        Color white = new(1.0f, 0.96f, 0.80f, fade);
        float radius = 28.0f + EaseOut(t) * 62.0f;

        DrawRect(new Rect2(center.X - 10, center.Y - 10, 20, 20), white, true);
        DrawLine(center + new Vector2(16, 0), center + new Vector2(radius, 0), gold, 5.0f, false);
        DrawLine(center + new Vector2(-16, 0), center + new Vector2(-radius, 0), gold, 5.0f, false);
        DrawLine(center + new Vector2(0, 16), center + new Vector2(0, radius), gold, 5.0f, false);
        DrawLine(center + new Vector2(0, -16), center + new Vector2(0, -radius), gold, 5.0f, false);
        DrawLine(center + new Vector2(12, 12), center + new Vector2(radius * 0.67f, radius * 0.67f), white, 3.0f, false);
        DrawLine(center + new Vector2(-12, -12), center + new Vector2(-radius * 0.67f, -radius * 0.67f), white, 3.0f, false);
    }

    /// <summary>箭矢采用短箭身和轻微抛物线，飞行速度前慢后快，命中前不会拖出巨大光带。</summary>
    private void DrawArrow(float elapsed, bool leftToRight)
    {
        float raw = Mathf.Clamp(elapsed / 0.34f, 0.0f, 1.0f);
        float t = EaseIn(raw);
        float direction = leftToRight ? 1.0f : -1.0f;
        Vector2 from = AttackerLaunchPoint(leftToRight) + new Vector2(direction * 18, -4);
        Vector2 to = DefenderCenter(leftToRight) + new Vector2(-direction * 28, -4);
        float arc = -Mathf.Sin(raw * Mathf.Pi) * 12.0f;
        Vector2 center = Snap(from.Lerp(to, t) + new Vector2(0, arc));

        Color wood = new("5a402d");
        Color steel = new("d4d6cf");
        Color feather = new("d1b575");

        DrawRect(new Rect2(center.X - 18, center.Y - 1, 36, 3), wood, true);
        float tipX = direction > 0 ? center.X + 17 : center.X - 23;
        DrawRect(new Rect2(tipX, center.Y - 3, 6, 7), steel, true);
        float tailX = direction > 0 ? center.X - 22 : center.X + 16;
        DrawRect(new Rect2(tailX, center.Y - 5, 6, 3), feather, true);
        DrawRect(new Rect2(tailX, center.Y + 2, 6, 3), feather, true);

        if (raw > 0.45f)
        {
            Color trail = new(0.95f, 0.88f, 0.70f, (raw - 0.45f) * 0.45f);
            DrawRect(new Rect2(center.X - direction * 42 - 5, center.Y, 10, 2), trail, true);
        }
    }

    /// <summary>剑光只跟随出手中段出现，由三段错位刃光组成，不再是一根巨大斜线贯穿人物。</summary>
    private void DrawSwordTrail(float elapsed, bool leftToRight)
    {
        float t = Mathf.Clamp(elapsed / 0.34f, 0.0f, 1.0f);
        if (t < 0.23f || t > 0.88f)
        {
            return;
        }

        float local = Mathf.Clamp((t - 0.23f) / 0.65f, 0.0f, 1.0f);
        float direction = leftToRight ? 1.0f : -1.0f;
        Vector2 basePoint = AttackerLaunchPoint(leftToRight) + new Vector2(direction * (26 + local * 72), -34 + local * 62);
        float fade = Mathf.Sin(local * Mathf.Pi);
        Color edge = new(1.0f, 0.96f, 0.80f, fade);
        Color warm = new(0.83f, 0.65f, 0.38f, fade * 0.65f);

        DrawLine(basePoint + new Vector2(-direction * 34, -26), basePoint + new Vector2(direction * 20, 24), warm, 7.0f, false);
        DrawLine(basePoint + new Vector2(-direction * 30, -24), basePoint + new Vector2(direction * 22, 22), edge, 3.0f, false);
        DrawLine(basePoint + new Vector2(-direction * 12, -31), basePoint + new Vector2(direction * 29, 8), edge, 2.0f, false);
    }

    /// <summary>枪刺效果只表现枪尖前方空气压缩，不重复画一根和人物武器重叠的粗枪杆。</summary>
    private void DrawSpearPressure(float elapsed, bool leftToRight)
    {
        float t = Mathf.Clamp(elapsed / 0.34f, 0.0f, 1.0f);
        if (t < 0.28f)
        {
            return;
        }

        float local = Mathf.Clamp((t - 0.28f) / 0.72f, 0.0f, 1.0f);
        float direction = leftToRight ? 1.0f : -1.0f;
        Vector2 start = AttackerLaunchPoint(leftToRight) + new Vector2(direction * 92.0f, 8.0f);
        float length = 18.0f + EaseOut(local) * 68.0f;
        Color air = new(0.94f, 0.91f, 0.78f, Mathf.Sin(local * Mathf.Pi) * 0.82f);
        Vector2 end = start + new Vector2(direction * length, 0);

        DrawLine(start, end, air, 3.0f, false);
        DrawLine(start + new Vector2(0, -9), end - new Vector2(direction * 12, 9), air, 2.0f, false);
        DrawLine(start + new Vector2(0, 9), end - new Vector2(direction * 12, -9), air, 2.0f, false);
    }

    /// <summary>魔法以较小的符文核心和离散尾迹飞行，保留神秘感但不把人物盖成大色块。</summary>
    private void DrawMagicGlyph(float elapsed, bool leftToRight)
    {
        float raw = Mathf.Clamp(elapsed / 0.50f, 0.0f, 1.0f);
        float t = Smooth(raw);
        float direction = leftToRight ? 1.0f : -1.0f;
        Vector2 from = AttackerLaunchPoint(leftToRight) + new Vector2(direction * 12, -46);
        Vector2 to = DefenderCenter(leftToRight) + new Vector2(-direction * 16, -22);
        Vector2 center = Snap(from.Lerp(to, t));
        float pulse = 1.0f + Mathf.Sin(elapsed * 20.0f) * 0.12f;

        Color violet = new(0.72f, 0.58f, 0.92f, 0.88f);
        Color blue = new(0.52f, 0.76f, 1.0f, 0.72f);
        Color core = new(0.96f, 0.91f, 1.0f, 0.96f);
        float arm = 12.0f * pulse;

        DrawRect(new Rect2(center.X - 5, center.Y - 5, 10, 10), core, true);
        DrawRect(new Rect2(center.X - arm, center.Y - 2, arm * 2, 4), violet, true);
        DrawRect(new Rect2(center.X - 2, center.Y - arm, 4, arm * 2), blue, true);

        for (int index = 1; index <= 3; index++)
        {
            Vector2 trail = Snap(center - new Vector2(direction * (18 + index * 14), index * 2));
            int size = 7 - index;
            DrawRect(new Rect2(trail.X - size / 2.0f, trail.Y - size / 2.0f, size, size), new Color(violet.R, violet.G, violet.B, 0.52f / index), true);
        }
    }

    /// <summary>返回攻击方武器附近的舞台发射点。</summary>
    private Vector2 AttackerLaunchPoint(bool leftToRight)
    {
        return leftToRight
            ? new Vector2(Size.X * 0.30f, Size.Y * 0.50f)
            : new Vector2(Size.X * 0.70f, Size.Y * 0.50f);
    }

    /// <summary>返回防守方胸口附近的舞台中心。</summary>
    private Vector2 DefenderCenter(bool leftToRight)
    {
        return leftToRight
            ? new Vector2(Size.X * 0.73f, Size.Y * 0.50f)
            : new Vector2(Size.X * 0.27f, Size.Y * 0.50f);
    }

    /// <summary>读取原控件当前效果类型。</summary>
    private RetroBattleEffectKind ReadKind()
    {
        return _source is not null && _kindField?.GetValue(_source) is RetroBattleEffectKind kind
            ? kind
            : RetroBattleEffectKind.None;
    }

    /// <summary>读取原控件效果已经播放的秒数。</summary>
    private float ReadElapsed()
    {
        return _source is not null && _elapsedField?.GetValue(_source) is float elapsed
            ? elapsed
            : 0.0f;
    }

    /// <summary>读取原控件当前效果方向。</summary>
    private bool ReadLeftToRight()
    {
        return _source is not null && _leftToRightField?.GetValue(_source) is bool value
            ? value
            : true;
    }

    /// <summary>标准 ease-in，用于箭矢出弦后的加速。</summary>
    private static float EaseIn(float t)
    {
        return t * t;
    }

    /// <summary>标准 ease-out，用于命中爆点与枪刺快速展开。</summary>
    private static float EaseOut(float t)
    {
        float inverse = 1.0f - t;
        return 1.0f - inverse * inverse;
    }

    /// <summary>平滑插值，用于魔法飞行而不产生机械匀速。</summary>
    private static float Smooth(float t)
    {
        return t * t * (3.0f - 2.0f * t);
    }

    /// <summary>把舞台坐标锁到整数屏幕像素。</summary>
    private static Vector2 Snap(Vector2 value)
    {
        return new Vector2(Mathf.Round(value.X), Mathf.Round(value.Y));
    }
}
