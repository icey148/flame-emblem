using Godot;
using System.Reflection;

namespace FlameEmblem.Visual;

/// <summary>
/// 给标准稿式战斗界面执行最终构图校正。
/// 目标不是“接近”参考稿，而是把黑底、人物占比、左右状态框、中央信息框和像素缩放统一到已确认的同一规格。
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

    /// <summary>把战斗画面锁到已确认参考稿的构图比例与像素安全参数。</summary>
    private bool TryApplyPolish()
    {
        if (_battleCoordinator is null ||
            _blockerField?.GetValue(_battleCoordinator) is not Control blocker)
        {
            return false;
        }

        // 参考稿背景是纯黑色，不插入任何蓝灰舞台、地形或渐变层。
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

        // 原程序人物以 4px 为一个逻辑像素；0.75 倍正好得到 3px 整数像素块。
        // x 使用 .5、y 使用 .25 是对原内部 (18,5) 偏移的补偿，最终像素边缘仍落在整数屏幕像素上。
        ConfigureBattleCharacter(
            leftCharacter,
            new Vector2(210.5f, 40.25f),
            false,
            "LeftCinematicBattleFigure");
        ConfigureBattleCharacter(
            rightCharacter,
            new Vector2(700.5f, 40.25f),
            true,
            "RightCinematicBattleFigure");

        // 双状态框按参考图比例放在画面下半部：左右各 540px，中间只保留 30px 缝隙。
        statusHud.Position = new Vector2(85, 305);
        statusHud.Size = new Vector2(1110, 380);
        statusHud.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;

        // 特效只占人物区域，不允许旧效果覆盖到底部状态框。
        if (_effectControlField?.GetValue(_battleCoordinator) is RetroBattleEffectControl effectControl)
        {
            effectControl.Position = Vector2.Zero;
            effectControl.Size = new Vector2(1280, 305);
            effectControl.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        }

        // 中央信息框覆盖两边身份区下沿，位置和宽高直接按参考图 1280×720 比例校正。
        if (_resultLabelField?.GetValue(_battleCoordinator) is Label resultLabel)
        {
            if (resultLabel.GetParent() is PanelContainer resultPanel)
            {
                resultPanel.Position = new Vector2(355, 405);
                resultPanel.Size = new Vector2(570, 105);
            }

            resultLabel.CustomMinimumSize = new Vector2(562, 97);
            resultLabel.AddThemeColorOverride("font_color", new Color("f3f3ef"));
            resultLabel.AddThemeColorOverride("font_shadow_color", Colors.Black);
            resultLabel.AddThemeConstantOverride("shadow_offset_x", 2);
            resultLabel.AddThemeConstantOverride("shadow_offset_y", 2);
            resultLabel.AddThemeFontSizeOverride("font_size", 28);
        }

        // 战斗 UI 全部使用关闭抗锯齿与次像素定位的共享字体，减少中文和英文在像素框里发虚。
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
        character.Size = new Vector2(420, 390);
        character.Scale = new Vector2(0.75f, 0.75f);
        character.MirrorHorizontally = mirrored;
        character.Modulate = Colors.White;
        character.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        AttachCinematicFigure(character, overlayName);
    }

    /// <summary>
    /// 给现有动画人物挂载三段式程序战斗人物层。
    /// 正式 battle PNG 或正式状态帧存在时，新层会自动让位；程序人物与正式 96px 素材都会在 0.75 缩放后形成清晰的 3× 像素显示。
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