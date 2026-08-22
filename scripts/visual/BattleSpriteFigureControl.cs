using FlameEmblem.Game;
using Godot;
using System.Reflection;

namespace FlameEmblem.Visual;

/// <summary>
/// 标准战斗界面真正显示的人物层。
/// 本层只绘制仓库内固定的 96×96 透明像素人物，并读取隐藏状态节点的单位与动作时间；
/// 旧程序方块人和旧 battle_ref.png 都不会参与最终绘制。
/// </summary>
public partial class BattleSpriteFigureControl : Control
{
    /// <summary>只负责保存人物与动作时间线的隐藏状态节点。</summary>
    private AnimatedBattleCharacterControl? _source;

    /// <summary>读取隐藏状态节点中的当前人物。</summary>
    private FieldInfo? _unitField;

    /// <summary>读取隐藏状态节点中的当前动作。</summary>
    private FieldInfo? _stateField;

    /// <summary>读取隐藏状态节点中的动作播放时间。</summary>
    private FieldInfo? _elapsedField;

    /// <summary>
    /// 96×96 源图固定整数放大 4 倍，再由外层人物节点使用 0.75 倍缩放；
    /// 最终每个源像素严格显示为 3×3 屏幕像素。
    /// </summary>
    private static readonly Vector2 DrawSize = new(384, 384);

    /// <summary>
    /// 420 宽人物区域左右各留 18 个本地像素；纵向从 0 开始，
    /// 这样 96px 图底部经过 4× 和 0.75× 后与 y=305 状态框上沿对齐。
    /// </summary>
    private static readonly Vector2 DrawOrigin = new(18, 0);

    /// <summary>物理攻击时间与现有战斗时间线保持一致。</summary>
    private const float AttackDuration = 0.34f;

    /// <summary>施法时间与现有战斗时间线保持一致。</summary>
    private const float CastDuration = 0.50f;

    /// <summary>绑定隐藏的战斗状态节点，只读取它的单位与时间线。</summary>
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

    /// <summary>隐藏状态节点推进时间线时，本层只请求重新绘制固定贴图。</summary>
    public override void _Process(double delta)
    {
        QueueRedraw();
    }

    /// <summary>绘制当前人物；资源缺失时保持空白，不再出现废弃的程序方块人。</summary>
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
            return;
        }

        CharacterAnimationState state = ReadState();
        float elapsed = ReadElapsed();
        Vector2 motion = ResolveMotion(state, elapsed);
        float opacity = ResolveOpacity(state, elapsed);

        DrawGround(unit, motion, opacity);
        DrawSprite(texture, motion, opacity);
    }

    /// <summary>绘制低矮阴影和阵营细线，使人物在纯黑背景上有明确落脚点。</summary>
    private void DrawGround(UnitModel unit, Vector2 motion, float opacity)
    {
        Vector2 groundOrigin = DrawOrigin + motion + new Vector2(64, 372);
        DrawRect(
            new Rect2(groundOrigin, new Vector2(256, 8)),
            new Color(0.04f, 0.04f, 0.06f, 0.44f * opacity),
            true);
        DrawRect(
            new Rect2(groundOrigin + new Vector2(56, 8), new Vector2(144, 4)),
            Fade(TeamVisualPalette.Primary(unit.Team), opacity),
            true);
    }

    /// <summary>
    /// 绘制固定 96×96 人物。
    /// 所有源图统一朝右：左侧敌军保持原方向，右侧我方水平镜像后朝左。
    /// </summary>
    private void DrawSprite(Texture2D texture, Vector2 motion, float opacity)
    {
        Rect2 target = new(DrawOrigin + motion, DrawSize);
        Color modulate = new(1, 1, 1, opacity);
        bool mirrorForRightSide = _source?.MirrorHorizontally == true;

        if (mirrorForRightSide)
        {
            // 围绕 420px 人物区域右边界镜像；18 + 384 + 18 正好保持左右留白对称。
            DrawSetTransform(new Vector2(Size.X, 0), 0.0f, new Vector2(-1, 1));
            DrawTextureRect(texture, target, false, modulate);
            DrawSetTransform(Vector2.Zero, 0.0f, Vector2.One);
            return;
        }

        DrawTextureRect(texture, target, false, modulate);
    }

    /// <summary>
    /// 固定贴图不旋转、不做非整数缩放，只用像素安全的整体位移表现蓄势、攻击、闪避和受击。
    /// </summary>
    private Vector2 ResolveMotion(CharacterAnimationState state, float elapsed)
    {
        if (_source is null)
        {
            return Vector2.Zero;
        }

        float direction = _source.MirrorHorizontally ? -1.0f : 1.0f;
        Vector2 motion = state switch
        {
            CharacterAnimationState.Attack => ResolveAttackMotion(elapsed, direction),
            CharacterAnimationState.Cast => ResolveCastMotion(elapsed),
            CharacterAnimationState.Dodge => new Vector2(
                -direction * Mathf.Sin(Mathf.Clamp(elapsed / 0.28f, 0.0f, 1.0f) * Mathf.Pi) * 28.0f,
                0),
            CharacterAnimationState.Hit => new Vector2(
                -direction * Mathf.Sin(Mathf.Clamp(elapsed / 0.24f, 0.0f, 1.0f) * Mathf.Pi) * 12.0f,
                0),
            CharacterAnimationState.Defeat => new Vector2(
                -direction * Mathf.Clamp(elapsed / 0.90f, 0.0f, 1.0f) * 8.0f,
                Mathf.Clamp(elapsed / 0.90f, 0.0f, 1.0f) * 28.0f),
            _ => Vector2.Zero
        };

        return SnapToLogicalPixel(motion);
    }

    /// <summary>攻击使用短后撤、快速前冲、收势三段，保持人物像素轮廓完整。</summary>
    private static Vector2 ResolveAttackMotion(float elapsed, float direction)
    {
        float t = Mathf.Clamp(elapsed / AttackDuration, 0.0f, 1.0f);
        if (t < 0.28f)
        {
            return new Vector2(-direction * (t / 0.28f) * 8.0f, 0);
        }

        if (t < 0.60f)
        {
            float strike = (t - 0.28f) / 0.32f;
            return new Vector2(direction * strike * 32.0f, 0);
        }

        float recover = (t - 0.60f) / 0.40f;
        return new Vector2(direction * (1.0f - recover) * 32.0f, 0);
    }

    /// <summary>施法只做轻微整数像素上浮，不改变贴图比例。</summary>
    private static Vector2 ResolveCastMotion(float elapsed)
    {
        float t = Mathf.Clamp(elapsed / CastDuration, 0.0f, 1.0f);
        return new Vector2(0, -Mathf.Sin(t * Mathf.Pi) * 8.0f);
    }

    /// <summary>倒下时淡出；其他动作保持完全不透明。</summary>
    private static float ResolveOpacity(CharacterAnimationState state, float elapsed)
    {
        if (state != CharacterAnimationState.Defeat)
        {
            return 1.0f;
        }

        float t = Mathf.Clamp(elapsed / 1.05f, 0.0f, 1.0f);
        return Mathf.Lerp(1.0f, 0.12f, t);
    }

    /// <summary>锁到 4 个本地像素，外层 0.75 倍后得到严格的 3 屏幕像素位移。</summary>
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

    /// <summary>统一套用动作透明度。</summary>
    private static Color Fade(Color color, float opacity)
    {
        return new Color(color.R, color.G, color.B, color.A * opacity);
    }
}
