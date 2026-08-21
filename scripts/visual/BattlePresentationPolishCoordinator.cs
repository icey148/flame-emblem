using Godot;
using System.Reflection;

namespace FlameEmblem.Visual;

/// <summary>
/// 对现有横向战斗演出做纯表现优化：提亮底色、加入像素战场舞台、调整人物站位，
/// 并在缺少正式素材时挂载三段式战斗人物动画层。
/// 不修改战斗时间线和数值逻辑，避免美术调整影响结算稳定性。
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

    /// <summary>只需要成功应用一次舞台结构。</summary>
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

    /// <summary>等待战斗协调器完成动态 UI 创建后，再执行一次表现重排。</summary>
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

    /// <summary>提亮遮罩和战斗框，插入新的像素舞台，并把人物收进更适合冲刺/突刺的站位。</summary>
    private bool TryApplyPolish()
    {
        if (_battleCoordinator is null ||
            _blockerField?.GetValue(_battleCoordinator) is not Control blocker)
        {
            return false;
        }

        // 全屏底色使用偏蓝灰，让深蓝我方和粉红敌方都能从背景上稳定分离。
        ColorRect? fullBackdrop = blocker.GetChildren().OfType<ColorRect>().FirstOrDefault();
        if (fullBackdrop is not null)
        {
            fullBackdrop.Color = new Color(0.09f, 0.12f, 0.16f, 0.94f);
        }

        PanelContainer? battlePanel = blocker.GetChildren().OfType<PanelContainer>().FirstOrDefault();
        if (battlePanel is null)
        {
            return false;
        }

        battlePanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            // 面板只保留蓝灰框架；真正的战场颜色由舞台背景承担。
            BgColor = new Color(0.13f, 0.17f, 0.20f, 1.0f),
            BorderColor = new Color(0.70f, 0.54f, 0.30f, 1.0f),
            BorderWidthLeft = 3,
            BorderWidthTop = 3,
            BorderWidthRight = 3,
            BorderWidthBottom = 3,
            CornerRadiusTopLeft = 0,
            CornerRadiusTopRight = 0,
            CornerRadiusBottomLeft = 0,
            CornerRadiusBottomRight = 0
        });

        if (battlePanel.GetChildCount() == 0 || battlePanel.GetChild(0) is not Control stage)
        {
            return false;
        }

        // 新舞台放在第一个子节点，人物、武器特效、结果文字和底部 HUD 都在它上面。
        RetroBattleStageBackdropControl stageBackdrop = new()
        {
            Name = "PolishedBattleStageBackdrop",
            Position = new Vector2(8, 46),
            Size = new Vector2(1084, 384),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        stage.AddChild(stageBackdrop);
        stage.MoveChild(stageBackdrop, 0);

        // 双方稍向画面中央靠拢，同时给快速前冲动作保留足够空间。
        if (_leftCharacterField?.GetValue(_battleCoordinator) is AnimatedBattleCharacterControl leftCharacter)
        {
            leftCharacter.Position = new Vector2(48, 46);
            leftCharacter.Scale = Vector2.One;
            leftCharacter.Modulate = new Color(1.03f, 1.03f, 1.02f, 1.0f);
            AttachCinematicFigure(leftCharacter, "LeftCinematicBattleFigure");
        }

        if (_rightCharacterField?.GetValue(_battleCoordinator) is AnimatedBattleCharacterControl rightCharacter)
        {
            rightCharacter.Position = new Vector2(632, 46);
            rightCharacter.Scale = Vector2.One;
            rightCharacter.Modulate = new Color(1.03f, 1.03f, 1.02f, 1.0f);
            AttachCinematicFigure(rightCharacter, "RightCinematicBattleFigure");
        }

        // 中央结果文字使用亮米黄色并增强阴影，在亮背景上也保持清晰。
        if (_resultLabelField?.GetValue(_battleCoordinator) is Label resultLabel)
        {
            resultLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.93f, 0.72f));
            resultLabel.AddThemeColorOverride("font_shadow_color", new Color(0.08f, 0.07f, 0.06f, 0.92f));
            resultLabel.AddThemeConstantOverride("shadow_offset_x", 2);
            resultLabel.AddThemeConstantOverride("shadow_offset_y", 2);
            resultLabel.AddThemeFontSizeOverride("font_size", 21);
        }

        return true;
    }

    /// <summary>
    /// 给现有动画人物挂载新的三段式程序战斗人物层。
    /// 原控件继续负责 SetUnit/Play/动作计时；新层只读取状态并替换旧方块身体。
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
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        character.AddChild(overlay);
        overlay.Bind(character);
    }
}
