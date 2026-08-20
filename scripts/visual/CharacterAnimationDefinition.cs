namespace FlameEmblem.Visual;

/// <summary>
/// 地图人物可以播放的动画状态。
/// 目前地图层实际使用待机和行走；攻击、受击、闪避、倒下和施法先定义好，后续战斗演出可以复用同一命名体系。
/// </summary>
public enum CharacterAnimationState
{
    Idle,
    Walk,
    Attack,
    Hit,
    Dodge,
    Defeat,
    Cast
}

/// <summary>
/// 地图人物面朝的四个正交方向。
/// 文件名使用这些方向名的小写形式，例如 walk_left_0.png。
/// </summary>
public enum CharacterFacing
{
    Down,
    Left,
    Right,
    Up
}

/// <summary>
/// 保存一段人物动画在运行时需要的基础参数。
/// 当前主要用于统一待机与行走帧率，后续可扩展成数据驱动配置。
/// </summary>
public sealed class CharacterAnimationDefinition
{
    /// <summary>
    /// 创建一段人物动画定义。
    /// </summary>
    public CharacterAnimationDefinition(CharacterAnimationState state, float framesPerSecond, bool loop)
    {
        State = state;
        FramesPerSecond = Math.Max(0.1f, framesPerSecond);
        Loop = loop;
    }

    /// <summary>动画状态。</summary>
    public CharacterAnimationState State { get; }

    /// <summary>每秒播放帧数。</summary>
    public float FramesPerSecond { get; }

    /// <summary>播放到末帧后是否循环。</summary>
    public bool Loop { get; }
}
