using FlameEmblem.Game;
using Godot;
using System.Reflection;

namespace FlameEmblem.Visual;

/// <summary>
/// 标准战斗界面真正显示的人物层。
/// 本层只绘制仓库内固定的透明像素 PNG，并读取旧人物节点已经存在的单位与动作计时；
/// 不重新计算命中、伤害、反击、经验或回合规则，也不在运行时生成人物美术。
/// </summary>
public partial class BattleSpriteFigureControl : Control
{
    /// <summary>只负责保存人物和动作时间线的隐藏状态节点。</summary>
    private AnimatedBattleCharacterControl? _source;

    /// <summary>读取隐藏状态节点中的当前人物。</summary>
    private FieldInfo? _unitField;

    /// <summary>读取隐藏状态节点中的当前动作。</summary>
    private FieldInfo? _stateField;

    /// <summary>读取隐藏状态节点中的动作播放时间。</summary>
    private FieldInfo? _elapsedField;

    /// <summary>80×75 原图先整数放大 4 倍，再由外层 0.75 缩放，最终每个源像素严格显示为 3×3。</summary>
    private static readonly Vector2 DrawSize = new(320, 300);

    /// <summary>
    /// 贴图在 420×390 人物控件中的整数放大位置。
    /// y=76 配合人物节点 y=23 与 0.75 外层倍率，使贴图脚底准确落在屏幕 y=305 的状态框上沿。
    /// </summary>
    private static readonly Vector2 DrawOrigin = new(50, 76);

    /// <summary>物理攻击时间与现有战斗时间线保持一致。</summary>
    private const float AttackDuration = 0.34f;

    /// <summary>施法时间与现有战斗时间线保持一致。</summary>
    private const float CastDuration = 0.50f;

    /// <summary>绑定隐藏的状态节点，只读取它的单位与时间线。</summary>
    public void Bind(AnimatedBattleCharacterControl source)
    {
        _source = source;
        BindingFlags members = BindingFlags.Instance | BindingFlags.NonPublic;
        Type sourceType = typeof(AnimatedBattleCharacterControl);
        _unitField = sourceType.GetField("_unit", members);
        _stateField = sourceType.GetField("_state", members);
        _elapsedField = sourceType.GetField("_stateElapsed", members);

        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = TextureFilterEnum.Nearest;
        SetProcess(true);
        QueueRedraw();
    }

    /// <summary>原战斗节点推进时间线时，本层只请求重新绘制贴图。</summary>
    public override void _Process(double delta)
    {
        QueueRedraw();
    }

    /// <summary>绘制当前人物的真实 PNG；正式贴图缺失时保持空白，不再退回方块人。</summary>
    public override void _Draw()
    {
        UnitModel? unit = ReadUnit();
        if (_source is null || unit is null)
        {
            return;
        }

        Texture2D? texture = ReferenceBattleSpriteCatalog.TryLoad(unit);
        if (texture is null)
        {
            // 正式战斗画面宁可暂时缺人物，也不再显示已经废弃的矩形拼接角色。
            return;
        }

        CharacterAnimationState state = ReadState();
        float elapsed = ReadElapsed();
        Vector2 motion = ResolveMotion(state, elapsed);
        float opacity = ResolveOpacity(state, elapsed);

        DrawGround(unit, motion, opacity);
        DrawSprite(unit, texture, motion, opacity);
    }

    /// <summary>绘制低矮阴影与阵营细线，让透明贴图在纯黑背景上仍然有明确落脚点。</summary>
    private void DrawGround(UnitModel unit, Vector2 motion, float opacity)
    {
        Vector2 groundOrigin = DrawOrigin + motion + new Vector2(32, 292);
        DrawRect(
            new Rect2(groundOrigin, new Vector2(256, 8)),
            new Color(0.04f, 0.04f, 0.06f, 0.48f * opacity),
            true);
        DrawRect(
            new Rect2(groundOrigin + new Vector2(56, 8), new Vector2(144, 4)),
            Fade(TeamVisualPalette.Primary(unit.Team), opacity),
            true);
    }

    /// <summary>
    /// 绘制 80×75 正式贴图。
    /// 四名主角、守卫和队长的源图朝左，因此位于左侧时需要镜像朝右；
    /// 掠夺者源图直接来自确认标准稿，本身已经朝右，放在左侧时保持原方向即可。
    /// </summary>
    private void DrawSprite(UnitModel unit, Texture2D texture, Vector2 motion, float opacity)
    {
        Rect2 target = new(DrawOrigin + motion, DrawSize);
        Color modulate = new(1, 1, 1, opacity);
        bool isLeftSide = _source?.MirrorHorizontally == false;
        bool raiderAlreadyFacesRight = unit.ClassDefinition.Id.Equals("raider", StringComparison.OrdinalIgnoreCase);
        bool mirrorForLeftSide = isLeftSide && !raiderAlreadyFacesRight;

        if (mirrorForLeftSide)
        {
            // 420 宽控件左右各留 50 像素，围绕控件右边界镜像后目标区域仍保持完全对称。
            DrawSetTransform(new Vector2(Size.X, 0), 0.0f, new Vector2(-1, 1));
            DrawTextureRect(texture, target, false, modulate);
            DrawSetTransform(Vector2.Zero, 0.0f, Vector2.One);
            return;
        }

        DrawTextureRect(texture, target, false, modulate);
    }

    /// <summary>
    /// 正式贴图已经包含完整身体、护甲、披风和武器，因此动作只做像素安全的整体位移。
    /// 位移始终锁到 4 个本地像素，经过 0.75 倍后仍然是整数 3 屏幕像素，不会发糊。
    /// </summary>
    private Vector2 ResolveMotion(CharacterAnimationState state, float elapsed)
    {
        if (_source is null)
        {
            return Vector2.Zero;
        }

        // 左侧人物向右攻击，右侧人物向左攻击。
        float direction = _source.MirrorHorizontally ? -1.0f : 1.0f;
        Vector2 motion = state switch
        {
            CharacterAnimationState.Attack => ResolveAttackMotion(elapsed, direction),
            CharacterAnimationState.Cast => ResolveCastMotion(elapsed),
            CharacterAnimationState.Dodge => new Vector2(
                -direction * Mathf.Sin(Mathf.Clamp(elapsed / 0.28f, 0.0f, 1.0f) * Mathf.Pi) * 24.0f,
                0),
            CharacterAnimationState.Hit => new Vector2(
                -direction * Mathf.Sin(Mathf.Clamp(elapsed / 0.24f, 0.0f, 1.0f) * Mathf.Pi) * 8.0f,
                0),
            CharacterAnimationState.Defeat => new Vector2(
                -direction * Mathf.Clamp(elapsed / 0.90f, 0.0f, 1.0f) * 8.0f,
                Mathf.Clamp(elapsed / 0.90f, 0.0f, 1.0f) * 24.0f),
            _ => Vector2.Zero
        };

        return SnapToLogicalPixel(motion);
    }

    /// <summary>攻击使用轻微后撤、快速前冲、回到原位三段，不旋转像素贴图。</summary>
    private static Vector2 ResolveAttackMotion(float elapsed, float direction)
    {
        float t = Mathf.Clamp(elapsed / AttackDuration, 0.0f, 1.0f);
        if (t < 0.30f)
        {
            return new Vector2(-direction * (t / 0.30f) * 8.0f, 0);
        }

        if (t < 0.62f)
        {
            float strike = (t - 0.30f) / 0.32f;
            return new Vector2(direction * strike * 28.0f, 0);
        }

        float recover = (t - 0.62f) / 0.38f;
        return new Vector2(direction * (1.0f - recover) * 28.0f, 0);
    }

    /// <summary>施法只做轻微上浮，避免对固定像素贴图进行非整数旋转或缩放。</summary>
    private static Vector2 ResolveCastMotion(float elapsed)
    {
        float t = Mathf.Clamp(elapsed / CastDuration, 0.0f, 1.0f);
        float lift = Mathf.Sin(t * Mathf.Pi) * 8.0f;
        return new Vector2(0, -lift);
    }

    /// <summary>倒下时逐步淡出；其余动作保持完全不透明。</summary>
    private static float ResolveOpacity(CharacterAnimationState state, float elapsed)
    {
        if (state != CharacterAnimationState.Defeat)
        {
            return 1.0f;
        }

        float t = Mathf.Clamp(elapsed / 1.05f, 0.0f, 1.0f);
        return Mathf.Lerp(1.0f, 0.12f, t);
    }

    /// <summary>锁到 4 像素逻辑栅格，外层 0.75 倍后得到整数 3 像素位移。</summary>
    private static Vector2 SnapToLogicalPixel(Vector2 value)
    {
        return new Vector2(
            Mathf.Round(value.X / 4.0f) * 4.0f,
            Mathf.Round(value.Y / 4.0f) * 4.0f);
    }

    /// <summary>读取当前人物。</summary>
    private UnitModel? ReadUnit()
    {
        return _source is null ? null : _unitField?.GetValue(_source) as UnitModel;
    }

    /// <summary>读取当前动作状态。</summary>
    private CharacterAnimationState ReadState()
    {
        return _source is not null && _stateField?.GetValue(_source) is CharacterAnimationState state
            ? state
            : CharacterAnimationState.Idle;
    }

    /// <summary>读取当前动作已经播放的秒数。</summary>
    private float ReadElapsed()
    {
        return _source is not null && _elapsedField?.GetValue(_source) is float elapsed
            ? elapsed
            : 0.0f;
    }

    /// <summary>统一套用人物动作透明度。</summary>
    private static Color Fade(Color color, float opacity)
    {
        return new Color(color.R, color.G, color.B, color.A * opacity);
    }
}
