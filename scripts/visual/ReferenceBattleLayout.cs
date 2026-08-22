using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 集中保存用户最终确认的 1280×720 战斗界面规格。
/// 新标准以定稿图为准：上半区展示大尺寸人物，下半区是带头像的红/蓝信息面板，
/// 中央金边结果框压在人物与状态区交界处。
/// </summary>
public static class ReferenceBattleLayout
{
    /// <summary>战斗界面的设计分辨率。</summary>
    public static readonly Vector2 ViewportSize = new(1280, 720);

    /// <summary>左侧敌军正式人物区域。</summary>
    public static readonly Vector2 LeftCharacterPosition = new(70, 20);

    /// <summary>右侧我方正式人物区域。</summary>
    public static readonly Vector2 RightCharacterPosition = new(700, 20);

    /// <summary>单侧正式人物控件尺寸。</summary>
    public static readonly Vector2 CharacterControlSize = new(510, 390);

    /// <summary>人物与武器特效允许出现的上半区高度。</summary>
    public const float EffectRegionHeight = 430.0f;

    /// <summary>双状态框整体位置。</summary>
    public static readonly Vector2 StatusHudPosition = new(20, 430);

    /// <summary>双状态框整体尺寸。</summary>
    public static readonly Vector2 StatusHudSize = new(1240, 270);

    /// <summary>单侧状态框宽度。</summary>
    public const float PanelWidth = 610.0f;

    /// <summary>左右状态框之间的间隔。</summary>
    public const float PanelGap = 20.0f;

    /// <summary>单侧状态框高度。</summary>
    public const float PanelHeight = 270.0f;

    /// <summary>身份信息区的参考高度；实际头像会贯穿整个面板。</summary>
    public const float IdentityHeight = 112.0f;

    /// <summary>中央伤害/攻击提示框位置。</summary>
    public static readonly Vector2 ResultPanelPosition = new(455, 386);

    /// <summary>中央提示框尺寸。</summary>
    public static readonly Vector2 ResultPanelSize = new(370, 78);

    /// <summary>中央提示文字内部尺寸。</summary>
    public static readonly Vector2 ResultLabelMinimumSize = new(362, 70);
}
