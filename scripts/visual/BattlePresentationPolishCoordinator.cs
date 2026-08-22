using Godot;
using System.Reflection;

namespace FlameEmblem.Visual;

/// <summary>
/// 把动态创建的战斗窗口锁定到用户最终确认的定稿图构图。
/// 本协调器只调整表现节点，不参与战斗结算、目标选择或回合推进。
/// </summary>
public partial class BattlePresentationPolishCoordinator : Node
{
    /// <summary>现有战斗动画协调器。</summary>
    private RetroBattleAnimationCoordinator? _battleCoordinator;

    /// <summary>战斗窗口全屏根节点字段。</summary>
    private FieldInfo? _blockerField;

    /// <summary>左侧隐藏状态人物字段。</summary>
    private FieldInfo? _leftCharacterField;

    /// <summary>右侧隐藏状态人物字段。</summary>
    private FieldInfo? _rightCharacterField;

    /// <summary>中央结果文字字段。</summary>
    private FieldInfo? _resultLabelField;

    /// <summary>底部状态 HUD 字段。</summary>
    private FieldInfo? _statusHudField;

    /// <summary>战斗特效字段。</summary>
    private FieldInfo? _effectControlField;

    /// <summary>是否已经完成一次性整理。</summary>
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

    /// <summary>等待动态战斗 UI 创建完成后执行一次最终整理。</summary>
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

    /// <summary>把人物、HUD、结果框、金色外框和特效层放到最终定稿位置。</summary>
    private bool TryApplyPolish()
    {
        if (_battleCoordinator is null ||
            _blockerField?.GetValue(_battleCoordinator) is not Control blocker)
        {
            return false;
        }

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

        EnsureApprovedFrame(blocker);

        ConfigureBattleCharacter(
            leftCharacter,
            ReferenceBattleLayout.LeftCharacterPosition,
            false,
            "LeftBattleSpriteFigure");
        ConfigureBattleCharacter(
            rightCharacter,
            ReferenceBattleLayout.RightCharacterPosition,
            true,
            "RightBattleSpriteFigure");

        statusHud.Position = ReferenceBattleLayout.StatusHudPosition;
        statusHud.Size = ReferenceBattleLayout.StatusHudSize;
        statusHud.ZIndex = 30;
        statusHud.TextureFilter = CanvasItem.TextureFilterEnum.Linear;

        if (_effectControlField?.GetValue(_battleCoordinator) is RetroBattleEffectControl effectControl)
        {
            effectControl.Position = Vector2.Zero;
            effectControl.Size = new Vector2(
                ReferenceBattleLayout.ViewportSize.X,
                ReferenceBattleLayout.EffectRegionHeight);
            effectControl.ZIndex = 20;
            effectControl.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        }

        if (_resultLabelField?.GetValue(_battleCoordinator) is Label resultLabel)
        {
            if (resultLabel.GetParent() is PanelContainer resultPanel)
            {
                resultPanel.Position = ReferenceBattleLayout.ResultPanelPosition;
                resultPanel.Size = ReferenceBattleLayout.ResultPanelSize;
                resultPanel.ZIndex = 40;
                resultPanel.AddThemeStyleboxOverride("panel", BuildApprovedResultStyle());
            }

            resultLabel.CustomMinimumSize = ReferenceBattleLayout.ResultLabelMinimumSize;
            resultLabel.AddThemeColorOverride("font_color", new Color("e0bd78"));
            resultLabel.AddThemeColorOverride("font_shadow_color", Colors.Black);
            resultLabel.AddThemeConstantOverride("shadow_offset_x", 2);
            resultLabel.AddThemeConstantOverride("shadow_offset_y", 2);
            resultLabel.AddThemeFontSizeOverride("font_size", 32);
        }

        ApplyPixelFontRecursive(blocker);
        return true;
    }

    /// <summary>确保最终整屏金色装饰框只创建一次，并始终位于人物下方、黑底上方。</summary>
    private static void EnsureApprovedFrame(Control blocker)
    {
        ApprovedBattleFrameControl? existing = blocker.GetNodeOrNull<ApprovedBattleFrameControl>("ApprovedBattleFrame");
        if (existing is not null)
        {
            existing.Position = Vector2.Zero;
            existing.Size = ReferenceBattleLayout.ViewportSize;
            existing.ZIndex = 5;
            existing.Visible = true;
            existing.QueueRedraw();
            return;
        }

        ApprovedBattleFrameControl frame = new()
        {
            Name = "ApprovedBattleFrame",
            Position = Vector2.Zero,
            Size = ReferenceBattleLayout.ViewportSize,
            ZIndex = 5,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        blocker.AddChild(frame);
    }

    /// <summary>创建最终定稿使用的深色金边中央提示框。</summary>
    private static StyleBoxFlat BuildApprovedResultStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color("07090d"),
            BorderColor = new Color("b8833f"),
            BorderWidthLeft = 3,
            BorderWidthTop = 3,
            BorderWidthRight = 3,
            BorderWidthBottom = 3,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4
        };
    }

    /// <summary>递归给战斗窗口文字应用共享字体。</summary>
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

    /// <summary>
    /// 旧 AnimatedBattleCharacterControl 只保留单位与动作计时；真正可见的人物由独立正式设计稿层绘制。
    /// </summary>
    private static void ConfigureBattleCharacter(
        AnimatedBattleCharacterControl character,
        Vector2 position,
        bool mirrored,
        string overlayName)
    {
        character.Position = position;
        character.Size = ReferenceBattleLayout.CharacterControlSize;
        character.Scale = Vector2.One;
        character.MirrorHorizontally = mirrored;
        character.Modulate = Colors.White;
        character.TextureFilter = CanvasItem.TextureFilterEnum.Linear;

        AttachDetachedBattleSprite(character, overlayName);

        // 隐藏旧绘制节点，但保留其 _Process 与 Play/SetUnit 状态更新。
        character.Visible = false;
    }

    /// <summary>把正式设计稿人物作为隐藏状态节点的同级节点挂到战斗舞台。</summary>
    private static void AttachDetachedBattleSprite(AnimatedBattleCharacterControl character, string overlayName)
    {
        Node? parent = character.GetParent();
        if (parent is null)
        {
            return;
        }

        foreach (ReferenceBattleFigureControl oldReference in parent.GetChildren().OfType<ReferenceBattleFigureControl>())
        {
            oldReference.Visible = false;
            oldReference.SetProcess(false);
            oldReference.QueueFree();
        }

        foreach (CinematicBattleFigureControl oldCinematic in character.GetChildren().OfType<CinematicBattleFigureControl>())
        {
            oldCinematic.Visible = false;
            oldCinematic.SetProcess(false);
        }

        BattleSpriteFigureControl? existing = parent.GetNodeOrNull<BattleSpriteFigureControl>(overlayName);
        if (existing is not null)
        {
            existing.Position = character.Position;
            existing.Size = character.Size;
            existing.Scale = Vector2.One;
            existing.ZIndex = 10;
            existing.Visible = true;
            existing.TextureFilter = CanvasItem.TextureFilterEnum.Linear;
            existing.Bind(character);
            return;
        }

        BattleSpriteFigureControl overlay = new()
        {
            Name = overlayName,
            Position = character.Position,
            Size = character.Size,
            Scale = Vector2.One,
            ZIndex = 10,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            TextureFilter = CanvasItem.TextureFilterEnum.Linear
        };
        parent.AddChild(overlay);
        overlay.Bind(character);
    }
}
