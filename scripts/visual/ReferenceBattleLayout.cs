using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 集中保存已经确认的 1280×720 战斗界面规格。
/// 任何战斗人物、状态框、中央提示框或特效层都必须从这里读取尺寸，避免多个协调器各自写死坐标后再次发生视觉漂移。
/// </summary>
public static class ReferenceBattleLayout
{
    /// <summary>战斗界面的设计分辨率。</summary>
    public static readonly Vector2 ViewportSize = new(1280, 720);

    /// <summary>左侧敌军人物控件位置。</summary>
    public static readonly Vector2 LeftCharacterPosition = new(210, 40);

    /// <summary>右侧我方人物控件位置。</summary>
    public static readonly Vector2 RightCharacterPosition = new(700, 40);

    /// <summary>单侧人物控件尺寸。</summary>
    public static readonly Vector2 CharacterControlSize = new(420, 390);

    /// <summary>人物和武器特效允许出现的上半区高度。</summary>
    public const float EffectRegionHeight = 305.0f;

    /// <summary>双状态框整体位置。</summary>
    public static readonly Vector2 StatusHudPosition = new(85, 305);

    /// <summary>双状态框整体尺寸。</summary>
    public static readonly Vector2 StatusHudSize = new(1110, 380);

    /// <summary>单侧状态框宽度。</summary>
    public const float PanelWidth = 540.0f;

    /// <summary>左右状态框之间的间隔。</summary>
    public const float PanelGap = 30.0f;

    /// <summary>单侧状态框高度。</summary>
    public const float PanelHeight = 380.0f;

    /// <summary>黑色姓名/职业/LV 身份区高度。</summary>
    public const float IdentityHeight = 172.0f;

    /// <summary>中央攻击提示框位置。</summary>
    public static readonly Vector2 ResultPanelPosition = new(355, 405);

    /// <summary>中央攻击提示框尺寸。</summary>
    public static readonly Vector2 ResultPanelSize = new(570, 105);

    /// <summary>中央攻击提示文字内部尺寸。</summary>
    public static readonly Vector2 ResultLabelMinimumSize = new(562, 97);
}
