using FlameEmblem.Game;
using Godot;
using System.Reflection;

namespace FlameEmblem.Visual;

/// <summary>
/// 最终战斗画面真正显示的人物层。
/// 已完成角色优先绘制用户确认的正式设计稿；尚未补齐正式全身图的角色临时绘制安全角色图集，
/// 读取隐藏状态节点的单位与动作时间，不修改任何战斗规则。
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

    /// <summary>低于等于这个尺寸的纹理视为安全角色图集回退帧，必须使用最近邻显示。</summary>
    private const int LowResolutionFallbackThreshold = 128;

    /// <summary>物理攻击时间与现有战斗时间线保持一致。</summary>
    private const float AttackDuration = 0.34f;

    /// <summary>施法时间与现有战斗时间线保持一致。</summary>
    private const float CastDuration = 0.50f;

    /// <summary>绑定隐藏的战斗状态节点，只读取人物与时间线。</summary>
    public void Bind(AnimatedBattleCharacterControl source)
    {
        _source = source;
        BindingFlags members = BindingFlags.Instance | BindingFlags.NonPublic;
        Type sourceType = typeof(AnimatedBattleCharacterControl);
        _unitField = sourceType.GetField("_unit", members);
        _stateField = sourceType.GetField("_state", members);
        _elapsedField = sourceType.GetField("_stateElapsed", members);

        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = TextureFilterEnum.Linear;
        SetProcess(true);
        QueueRedraw();
    }

    /// <summary>隐藏状态节点推进时间线时，本层只请求重绘。</summary>
    public override void _Process(double delta)
    {
        QueueRedraw();
    }

    /// <summary>绘制当前人物；正式稿缺失时由资源目录返回安全角色图集，避免出现纯黑影。</summary>
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

        bool lowResolutionFallback = IsLowResolutionFallback(texture);
        TextureFilter = lowResolutionFallback
            ? TextureFilterEnum.Nearest
            : TextureFilterEnum.Linear;

        CharacterAnimationState state = ReadState();
        float elapsed = ReadElapsed();
        Vector2 motion = ResolveMotion(state, elapsed);
        float opacity = ResolveOpacity(state, elapsed);

        DrawGround(unit, motion, opacity);
        DrawFigure(texture, motion, opacity, lowResolutionFallback);
    }

    /// <summary>绘制低矮阴影和阵营色落脚线，使人物在纯黑背景上有明确接地感。</summary>
    private void DrawGround(UnitModel unit, Vector2 motion, float opacity)
    {
        float centerX = Size.X * 0.5f + motion.X;
        float groundY = ReferenceBattleLayout.CharacterGroundY;
        Rect2 shadow = new(
            new Vector2(Mathf.Round(centerX - 118), groundY - 8 + motion.Y),
            new Vector2(236, 7));

        DrawRect(shadow, new Color(0.03f, 0.025f, 0.025f, 0.52f * opacity), true);
        DrawRect(
            new Rect2(shadow.Position + new Vector2(46, 7), new Vector2(144, 3)),
            Fade(TeamVisualPalette.Primary(unit.Team).Darkened(0.12f), opacity),
            true);
    }

    /// <summary>
    /// 等比缩放人物，使脚底锁到统一基准线。
    /// 正式 WebP 已按最终敌左我右方向制作；只有旧安全图集回退帧需要按右侧站位做水平镜像。
    /// </summary>
    private void DrawFigure(Texture2D texture, Vector2 motion, float opacity, bool lowResolutionFallback)
    {
        float sourceWidth = Math.Max(1, texture.GetWidth());
        float sourceHeight = Math.Max(1, texture.GetHeight());
        float scale = MathF.Min(
            ReferenceBattleLayout.CharacterMaximumWidth / sourceWidth,
            ReferenceBattleLayout.CharacterMaximumHeight / sourceHeight);
        Vector2 targetSize = new(sourceWidth * scale, sourceHeight * scale);
        float x = (Size.X - targetSize.X) * 0.5f + motion.X;
        float y = ReferenceBattleLayout.CharacterGroundY - targetSize.Y + motion.Y;

        Rect2 target = new(
            new Vector2(Mathf.Round(x), Mathf.Round(y)),
            new Vector2(Mathf.Round(targetSize.X), Mathf.Round(targetSize.Y)));

        bool mirrorFallbackForRightSide = lowResolutionFallback && _source?.MirrorHorizontally == true;
        if (mirrorFallbackForRightSide)
        {
            // 回退帧统一按旧图集朝向制作；围绕人物区域中心镜像后即可保持敌左我右面对面。
            DrawSetTransform(new Vector2(Size.X, 0), 0.0f, new Vector2(-1, 1));
            DrawTextureRect(texture, target, false, new Color(1, 1, 1, opacity));
            DrawSetTransform(Vector2.Zero, 0.0f, Vector2.One);
            return;
        }

        DrawTextureRect(texture, target, false, new Color(1, 1, 1, opacity));
    }

    /// <summary>判断当前纹理是否来自 96×96 左右的临时安全角色图集。</summary>
    private static bool IsLowResolutionFallback(Texture2D texture)
    {
        return texture.GetWidth() <= LowResolutionFallbackThreshold &&
               texture.GetHeight() <= LowResolutionFallbackThreshold;
    }

    /// <summary>正式人物与回退人物都只做克制的整体位移，不旋转、不扭曲纹理。</summary>
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
                -direction * Mathf.Sin(Mathf.Clamp(elapsed / 0.28f, 0.0f, 1.0f) * Mathf.Pi) * 30.0f,
                0),
            CharacterAnimationState.Hit => new Vector2(
                -direction * Mathf.Sin(Mathf.Clamp(elapsed / 0.24f, 0.0f, 1.0f) * Mathf.Pi) * 12.0f,
                0),
            CharacterAnimationState.Defeat => new Vector2(
                -direction * Mathf.Clamp(elapsed / 0.90f, 0.0f, 1.0f) * 10.0f,
                Mathf.Clamp(elapsed / 0.90f, 0.0f, 1.0f) * 28.0f),
            _ => Vector2.Zero
        };

        return new Vector2(Mathf.Round(motion.X), Mathf.Round(motion.Y));
    }

    /// <summary>攻击阶段只做短后撤、快速突进、收势三段，保持人物本身的美术不被变形。</summary>
    private static Vector2 ResolveAttackMotion(float elapsed, float direction)
    {
        float t = Mathf.Clamp(elapsed / AttackDuration, 0.0f, 1.0f);
        if (t < 0.28f)
        {
            return new Vector2(-direction * (t / 0.28f) * 7.0f, 0);
        }

        if (t < 0.60f)
        {
            float strike = (t - 0.28f) / 0.32f;
            return new Vector2(direction * strike * 26.0f, 0);
        }

        float recover = (t - 0.60f) / 0.40f;
        return new Vector2(direction * (1.0f - recover) * 26.0f, 0);
    }

    /// <summary>施法只做轻微上浮。</summary>
    private static Vector2 ResolveCastMotion(float elapsed)
    {
        float t = Mathf.Clamp(elapsed / CastDuration, 0.0f, 1.0f);
        return new Vector2(0, -Mathf.Sin(t * Mathf.Pi) * 8.0f);
    }

    /// <summary>倒下时逐步淡出。</summary>
    private static float ResolveOpacity(CharacterAnimationState state, float elapsed)
    {
        if (state != CharacterAnimationState.Defeat)
        {
            return 1.0f;
        }

        float t = Mathf.Clamp(elapsed / 1.05f, 0.0f, 1.0f);
        return Mathf.Lerp(1.0f, 0.12f, t);
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
