using Godot;
using System.Reflection;

namespace FlameEmblem.Visual;

/// <summary>
/// 给标准稿式战斗界面挂载最终程序人物层，并强制所有战斗人物使用整数坐标与最近邻过滤。
/// 旧版本会加入蓝灰战场背景；当前版本明确保留纯黑背景，不再插入任何额外舞台图层。
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

    /// <summary>锁定纯黑背景、整数站位和最近邻人物绘制。</summary>
    private bool TryApplyPolish()
    {
        if (_battleCoordinator is null ||
            _blockerField?.GetValue(_battleCoordinator) is not Control blocker)
        {
            return false;
        }

        // 参考稿使用纯黑底；旧蓝灰背景在这里被明确覆盖，避免后续主题层重新染色。
        ColorRect? fullBackdrop = blocker.GetChildren().OfType<ColorRect>().FirstOrDefault();
        if (fullBackdrop is not null)
        {
            fullBackdrop.Color = Colors.Black;
        }

        if (_leftCharacterField?.GetValue(_battleCoordinator) is not AnimatedBattleCharacterControl leftCharacter ||
            _rightCharacterField?.GetValue(_battleCoordinator) is not AnimatedBattleCharacterControl rightCharacter)
        {
            return false;
        }

        // 所有坐标、尺寸和缩放都保持整数值，人物本身不再进行非整数缩放。
        ConfigureBattleCharacter(leftCharacter, new Vector2(100, 4), false, "LeftCinematicBattleFigure");
        ConfigureBattleCharacter(rightCharacter, new Vector2(760, 4), true, "RightCinematicBattleFigure");

        if (_resultLabelField?.GetValue(_battleCoordinator) is Label resultLabel)
        {
            resultLabel.AddThemeColorOverride("font_color", new Color("f3f3ef"));
            resultLabel.AddThemeColorOverride("font_shadow_color", Colors.Black);
            resultLabel.AddThemeConstantOverride("shadow_offset_x", 2);
            resultLabel.AddThemeConstantOverride("shadow_offset_y", 2);
            resultLabel.AddThemeFontSizeOverride("font_size", 28);
        }

        return true;
    }

    /// <summary>统一设置单侧战斗人物的像素安全参数，并挂载最终程序人物。</summary>
    private static void ConfigureBattleCharacter(
        AnimatedBattleCharacterControl character,
        Vector2 position,
        bool mirrored,
        string overlayName)
    {
        character.Position = new Vector2(Mathf.Round(position.X), Mathf.Round(position.Y));
        character.Size = new Vector2(420, 390);
        character.Scale = Vector2.One;
        character.MirrorHorizontally = mirrored;
        character.Modulate = Colors.White;
        character.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        AttachCinematicFigure(character, overlayName);
    }

    /// <summary>
    /// 给现有动画人物挂载三段式程序战斗人物层。
    /// 正式 battle PNG 或正式状态帧存在时，新层会自动让位。
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