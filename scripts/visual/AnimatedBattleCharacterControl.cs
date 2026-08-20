using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 独立战斗画面中的多帧人物控件。
/// 优先播放 battle/&lt;state&gt;_N.png 序列帧；缺少序列帧时回退到 battle.png、portrait.png 或程序绘制人物。
/// </summary>
public partial class AnimatedBattleCharacterControl : Control
{
    /// <summary>当前展示的人物。</summary>
    private UnitModel? _unit;

    /// <summary>当前正在播放的战斗动画状态。</summary>
    private CharacterAnimationState _state = CharacterAnimationState.Idle;

    /// <summary>当前状态已经播放的秒数。</summary>
    private float _stateElapsed;

    /// <summary>右侧人物通常需要水平镜像，使双方在战斗画面中面对彼此。</summary>
    public bool MirrorHorizontally { get; set; }

    /// <summary>
    /// 设置人物；人物变化时重置为待机状态。
    /// </summary>
    public void SetUnit(UnitModel? unit)
    {
        _unit = unit;
        Play(CharacterAnimationState.Idle);
    }

    /// <summary>
    /// 切换战斗动画状态并从第一帧开始播放。
    /// </summary>
    public void Play(CharacterAnimationState state)
    {
        _state = state;
        _stateElapsed = 0.0f;
        QueueRedraw();
    }

    /// <summary>
    /// 推进当前动画计时并持续重绘。
    /// </summary>
    public override void _Process(double delta)
    {
        _stateElapsed += (float)delta;
        QueueRedraw();
    }

    /// <summary>
    /// 绘制当前战斗人物状态。
    /// </summary>
    public override void _Draw()
    {
        if (_unit is null)
        {
            return;
        }

        Vector2 fallbackOffset = ResolveFallbackOffset();
        float fallbackOpacity = ResolveFallbackOpacity();
        int frameCount = CharacterAssetResolver.GetBattleFrameCount(_unit, _state);
        Texture2D? texture = null;

        if (frameCount > 0)
        {
            int frameIndex = ResolveFrameIndex(frameCount);
            texture = CharacterAssetResolver.TryLoadBattleFrame(_unit, _state, frameIndex);
        }

        texture ??= CharacterAssetResolver.TryLoad(_unit, CharacterArtSlot.Battle)
                    ?? CharacterAssetResolver.TryLoad(_unit, CharacterArtSlot.Portrait);

        if (texture is not null)
        {
            DrawBattleTexture(texture, fallbackOffset, fallbackOpacity);
            return;
        }

        DrawProceduralFigure(fallbackOffset, fallbackOpacity);
    }

    /// <summary>
    /// 根据当前状态选择战斗序列帧。
    /// 待机循环播放；攻击、受击、闪避、施法和倒下停在最后一帧，直到状态机切换。
    /// </summary>
    private int ResolveFrameIndex(int frameCount)
    {
        float fps = _state switch
        {
            CharacterAnimationState.Attack => 12.0f,
            CharacterAnimationState.Cast => 10.0f,
            CharacterAnimationState.Hit => 10.0f,
            CharacterAnimationState.Dodge => 12.0f,
            CharacterAnimationState.Defeat => 8.0f,
            _ => 4.0f
        };

        int rawIndex = (int)MathF.Floor(_stateElapsed * fps);
        if (_state == CharacterAnimationState.Idle)
        {
            return rawIndex % frameCount;
        }

        return Math.Clamp(rawIndex, 0, frameCount - 1);
    }

    /// <summary>
    /// 在缺少正式状态帧时提供最基本的动作反馈。
    /// 有序列帧时该偏移仍可作为轻微强化，但不会改变人物逻辑状态。
    /// </summary>
    private Vector2 ResolveFallbackOffset()
    {
        float direction = MirrorHorizontally ? -1.0f : 1.0f;
        return _state switch
        {
            CharacterAnimationState.Attack => new Vector2(
                direction * Mathf.Sin(Mathf.Clamp(_stateElapsed / 0.32f, 0.0f, 1.0f) * Mathf.Pi) * 34.0f,
                0),
            CharacterAnimationState.Cast => new Vector2(0, -Mathf.Abs(Mathf.Sin(_stateElapsed * 12.0f)) * 5.0f),
            CharacterAnimationState.Dodge => new Vector2(
                -direction * Mathf.Sin(Mathf.Clamp(_stateElapsed / 0.25f, 0.0f, 1.0f) * Mathf.Pi) * 28.0f,
                0),
            CharacterAnimationState.Hit => new Vector2(Mathf.Sin(_stateElapsed * 55.0f) * 6.0f, 0),
            CharacterAnimationState.Defeat => new Vector2(0, Mathf.Min(52.0f, _stateElapsed * 70.0f)),
            _ => new Vector2(0, Mathf.Sin(_stateElapsed * 3.0f) * 1.5f)
        };
    }

    /// <summary>
    /// 倒下状态逐渐淡出；其他状态保持完全可见。
    /// </summary>
    private float ResolveFallbackOpacity()
    {
        return _state == CharacterAnimationState.Defeat
            ? Mathf.Clamp(1.0f - _stateElapsed * 0.75f, 0.12f, 1.0f)
            : 1.0f;
    }

    /// <summary>
    /// 绘制正式战斗纹理，并根据左右站位做水平镜像。
    /// </summary>
    private void DrawBattleTexture(Texture2D texture, Vector2 offset, float opacity)
    {
        Rect2 target = new(
            new Vector2(18, 12) + offset,
            new Vector2(Mathf.Max(1, Size.X - 36), Mathf.Max(1, Size.Y - 24)));
        Color modulate = new(1, 1, 1, opacity);

        if (MirrorHorizontally)
        {
            DrawSetTransform(new Vector2(Size.X, 0), 0.0f, new Vector2(-1, 1));
            Rect2 mirroredTarget = new(
                new Vector2(18 - offset.X, 12 + offset.Y),
                target.Size);
            DrawTextureRect(texture, mirroredTarget, false, modulate);
            DrawSetTransform(Vector2.Zero, 0.0f, Vector2.One);
            return;
        }

        DrawTextureRect(texture, target, false, modulate);
    }

    /// <summary>
    /// 没有任何正式人物素材时绘制原创程序占位战斗人物。
    /// </summary>
    private void DrawProceduralFigure(Vector2 offset, float opacity)
    {
        if (_unit is null)
        {
            return;
        }

        CharacterAppearanceDefinition appearance = CharacterAppearanceCatalog.Get(_unit);
        Color outfit = appearance.OutfitColor;
        Color hair = appearance.HairColor;
        Color skin = appearance.SkinColor;
        Color accent = appearance.AccentColor;
        outfit.A *= opacity;
        hair.A *= opacity;
        skin.A *= opacity;
        accent.A *= opacity;

        Vector2 center = new(Size.X * 0.5f, Size.Y * 0.42f) + offset;
        DrawRect(new Rect2(center + new Vector2(-46, 26), new Vector2(92, 92)), outfit, true);
        DrawCircle(center, 34.0f, skin);
        DrawCircle(center + new Vector2(0, -15), 36.0f, hair);
        DrawRect(new Rect2(center + new Vector2(-31, 2), new Vector2(62, 26)), skin, true);
        DrawRect(new Rect2(center + new Vector2(-48, 78), new Vector2(96, 9)), accent, true);
    }
}
