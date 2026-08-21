using Godot;
using System.Reflection;

namespace FlameEmblem.Visual;

/// <summary>
/// 给标准稿式战斗界面执行最终构图校正。
/// 所有位置与尺寸统一读取 ReferenceBattleLayout，避免战斗协调器、HUD 和表现层分别写死坐标造成再次漂移。
/// 本协调器只调整表现节点，不参与战斗结算、目标选择或回合推进。
/// </summary>
public partial class BattlePresentationPolishCoordinator : Node
{
    /// <summary>现有战斗动画协调器。</summary>
    private RetroBattleAnimationCoordinator? _battleCoordinator;

    /// <summary>战斗动画协调器的全屏遮罩字段。</summary>
    private FieldInfo? _blockerField;

    /// <summary>左侧战斗人物字段。</summary>
    private FieldInfo? _leftCharacterField;

    /// <summary>右侧战斗人物字段。</summary>
    private FieldInfo? _rightCharacterField;

    /// <summary>中央战斗结果文字字段。</summary>
    private FieldInfo? _resultLabelField;

    /// <summary>底部双状态框字段。</summary>
    private FieldInfo? _statusHudField;

    /// <summary>战斗命中与武器特效字段。</summary>
    private FieldInfo? _effectControlField;

    /// <summary>是否已经完成一次性表现整理。</summary>
    private bool _applied;

    /// <summary>缓存现有战斗表现字段。</summary>
    public override void _Ready()
    {
        ProcessPriority = 350;
        _battleCoordinator = GetNodeOrNull<RetroBattleAnimationCoordinator>("../RetroBattleAnimationCoordinator");
        if (_battleCoordinator is null)
        {
            GD.PushWarning("BattlePresentationPolishCoordinator 找不到战斗演出协调器。");
            SetProcess(false);
            return;
        }

        BindingFlags members = BindingFlags.Instance | BindingFlags.NonPublic;
        Type type = typeof(RetroBattleAnimationCoordinator);
        _blockerField = type.GetField("_blocker", members);
        _leftCharacterField = type.GetField("_leftCharacter", members);
        _rightCharacterField = type.GetField("_rightCharacter", members);
        _resultLabelField = type.GetField("_resultLabel", members);
        _statusHudField = type.GetField("_statusHud", members);
        _effectControlField = type.GetField("_effectControl", members);
    }

    /// <summary>等待动态战斗 UI 创建完成后执行一次最终像素整理。</summary>
    public override void _Process(double delta)
    {
        if (_applied || _battleCoordinator is null)
        {
            return;
        }

        _applied = TryApplyPolish();
        if (_applied)
        {
            SetProcess(false);
        }
    }

    /// <summary>把战斗画面锁到已经确认的唯一构图规格与像素安全参数。</summary>
    private bool TryApplyPolish()
    {
        if (_battleCoordinator is null ||
            _blockerField?.GetValue(_battleCoordinator) is not Control blocker)
        {
            return false;
        }

        // 正式规格使用纯黑底，不插入蓝灰舞台、地形图层或渐变。
        ColorRect? fullBackdrop = blocker.GetChildren().OfType<ColorRect>().FirstOrDefault();
        if (fullBackdrop is not null)
        {
            fullBackdrop.Color = Colors.Black;
        }

        if (_leftCharacterField?.GetValue(_battleCoordinator) is not AnimatedBattleCharacterControl leftCharacter ||
            _rightCharacterField?.GetValue(_battleCoordinator) is not AnimatedBattleCharacterControl rightCharacter ||
            _statusHudField?.GetValue(_battleCoordinator) is not RetroBattleStatusHudControl statusHud)
        {
            return false;
        }

        // 程序人物以 4px 为逻辑像素，0.75 倍后正好得到 3px 屏幕像素。
        // 内部绘制和节点偏移都经过补偿，因此最终边缘仍落在整数屏幕像素上。
        ConfigureBattleCharacter(
            leftCharacter,
            ReferenceBattleLayout.LeftCharacterPosition + new Vector2(0.5f, 0.25f),
            false,
            "LeftCinematicBattleFigure");
        ConfigureBattleCharacter(
            rightCharacter,
            ReferenceBattleLayout.RightCharacterPosition + new Vector2(0.5f, 0.25f),
            true,
            "RightCinematicBattleFigure");

        // 双状态框、人物区域和中央信息框全部读取同一份规格。
        statusHud.Position = ReferenceBattleLayout.StatusHudPosition;
        statusHud.Size = ReferenceBattleLayout.StatusHudSize;
        statusHud.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;

        if (_effectControlField?.GetValue(_battleCoordinator) is RetroBattleEffectControl effectControl)
        {
            effectControl.Position = Vector2.Zero;
            effectControl.Size = new Vector2(
                ReferenceBattleLayout.ViewportSize.X,
                ReferenceBattleLayout.EffectRegionHeight);
            effectControl.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        }

        if (_resultLabelField?.GetValue(_battleCoordinator) is Label resultLabel)
        {
            if (resultLabel.GetParent() is PanelContainer resultPanel)
            {
                resultPanel.Position = ReferenceBattleLayout.ResultPanelPosition;
                resultPanel.Size = ReferenceBattleLayout.ResultPanelSize;
            }

            resultLabel.CustomMinimumSize = ReferenceBattleLayout.ResultLabelMinimumSize;
            resultLabel.AddThemeColorOverride("font_color", new Color("f3f3ef"));
            resultLabel.AddThemeColorOverride("font_shadow_color", Colors.Black);
            resultLabel.AddThemeConstantOverride("shadow_offset_x", 2);
            resultLabel.AddThemeConstantOverride("shadow_offset_y", 2);
            resultLabel.AddThemeFontSizeOverride("font_size", 28);
        }

        // 战斗窗口文字全部使用关闭抗锯齿与次像素定位的共享字体。
        ApplyPixelFontRecursive(blocker);
        return true;
    }

    /// <summary>递归给战斗窗口中的 Label 和 Button 应用同一套硬边系统字体。</summary>
    private static void ApplyPixelFontRecursive(Node node)
    {
        if (node is Label label)
        {
            label.AddThemeFontOverride("font", BattlePixelFontCatalog.Font);
        }
        else if (node is Button button)
        {
            button.AddThemeFontOverride("font", BattlePixelFontCatalog.Font);
        }

        foreach (Node child in node.GetChildren())
        {
            ApplyPixelFontRecursive(child);
        }
    }

    /// <summary>统一设置单侧战斗人物的 3× 逻辑像素缩放，并挂载最终程序人物层。</summary>
    private static void ConfigureBattleCharacter(
        AnimatedBattleCharacterControl character,
        Vector2 position,
        bool mirrored,
        string overlayName)
    {
        character.Position = position;
        character.Size = ReferenceBattleLayout.CharacterControlSize;
        character.Scale = new Vector2(0.75f, 0.75f);
        character.MirrorHorizontally = mirrored;
        character.Modulate = Colors.White;
        character.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        AttachCinematicFigure(character, overlayName);
    }

    /// <summary>
    /// 给现有动画人物挂载三段式程序战斗人物层。
    /// 正式 battle PNG 或正式状态帧存在时本层自动让位；缺素材时才显示原创程序人物。
    /// </summary>
    private static void AttachCinematicFigure(AnimatedBattleCharacterControl character, string overlayName)
    {
        if (character.GetChildren().OfType<CinematicBattleFigureControl>().Any())
        {
            return;
        }

        CinematicBattleFigureControl overlay = new()
        {
            Name = overlayName,
            Position = Vector2.Zero,
            Size = character.Size,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest
        };
        character.AddChild(overlay);
        overlay.Bind(character);
    }
}
