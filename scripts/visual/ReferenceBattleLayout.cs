using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 集中保存用户最终确认的 1280×720 战斗界面规格。
/// 最终标准以最后确认的金边战斗设计图为准：上半区是大尺寸正式人物，
/// 下半区左右信息面板无缝衔接，外侧放大头像，中央信息框压在人物与 HUD 的交界处。
/// </summary>
public static class ReferenceBattleLayout
{
    /// <summary>战斗界面的固定设计分辨率。</summary>
    public static readonly Vector2 ViewportSize = new(1280, 720);

    /// <summary>整屏金色外框与画面边缘之间的距离。</summary>
    public const float FrameInset = 10.0f;

    /// <summary>上半区人物脚下的装饰地面线高度。</summary>
    public const float GroundLineY = 382.0f;

    /// <summary>左侧敌军正式人物区域。</summary>
    public static readonly Vector2 LeftCharacterPosition = new(32, 12);

    /// <summary>右侧我方正式人物区域。</summary>
    public static readonly Vector2 RightCharacterPosition = new(738, 12);

    /// <summary>单侧正式人物控件尺寸。</summary>
    public static readonly Vector2 CharacterControlSize = new(510, 370);

    /// <summary>人物脚底在人物控件内部的统一基准线。</summary>
    public const float CharacterGroundY = 362.0f;

    /// <summary>正式人物在单侧区域中的最大绘制宽度。</summary>
    public const float CharacterMaximumWidth = 500.0f;

    /// <summary>正式人物在单侧区域中的最大绘制高度。</summary>
    public const float CharacterMaximumHeight = 350.0f;

    /// <summary>人物与武器特效允许出现的上半区高度。</summary>
    public const float EffectRegionHeight = 392.0f;

    /// <summary>双状态框整体位置。</summary>
    public static readonly Vector2 StatusHudPosition = new(12, 392);

    /// <summary>双状态框整体尺寸。</summary>
    public static readonly Vector2 StatusHudSize = new(1256, 316);

    /// <summary>单侧状态框宽度。</summary>
    public const float PanelWidth = 628.0f;

    /// <summary>最终定稿的左右面板在中线直接相接，不再保留上一版的大间隔。</summary>
    public const float PanelGap = 0.0f;

    /// <summary>单侧状态框高度。</summary>
    public const float PanelHeight = 316.0f;

    /// <summary>单侧头像区域宽度。</summary>
    public const float PortraitWidth = 252.0f;

    /// <summary>身份信息区的参考高度；头像本身贯穿整个面板。</summary>
    public const float IdentityHeight = 116.0f;

    /// <summary>中央伤害/攻击提示框位置。</summary>
    public static readonly Vector2 ResultPanelPosition = new(466, 326);

    /// <summary>中央提示框尺寸。</summary>
    public static readonly Vector2 ResultPanelSize = new(348, 86);

    /// <summary>中央提示文字内部尺寸。</summary>
    public static readonly Vector2 ResultLabelMinimumSize = new(340, 78);
}
