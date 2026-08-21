using Godot;
using System.Reflection;

namespace FlameEmblem.Visual;

/// <summary>
/// 对现有横向战斗演出做纯表现优化：提亮底色、加入像素战场舞台、调整人物站位和文字对比。
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

    /// <summary>提亮遮罩和战斗框，插入新的像素舞台，并把人物稍微向中央收拢。</summary>
    private bool TryApplyPolish()
    {
        if (_battleCoordinator is null ||
            _blockerField?.GetValue(_battleCoordinator) is not Control blocker)
        {
            return false;
        }

        // 全屏底色从近黑改成偏蓝灰，仍然压暗地图但不再让战斗界面显得沉闷。
        ColorRect? fullBackdrop = blocker.GetChildren().OfType<ColorRect>().FirstOrDefault();
        if (fullBackdrop is not null)
        {
            fullBackdrop.Color = new Color(0.10f, 0.13f, 0.16f, 0.94f);
        }

        PanelContainer? battlePanel = blocker.GetChildren().OfType<PanelContainer>().FirstOrDefault();
        if (battlePanel is null)
        {
            return false;
        }

        battlePanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            // 面板本身只做浅暗蓝灰框架，真正的战场颜色由舞台背景承担。
            BgColor = new Color(0.14f, 0.17f, 0.18f, 1.0f),
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

        // 新舞台放在 stage 的第一个子节点，保证人物、特效、结果文字和底部 HUD 全部绘制在它上面。
        RetroBattleStageBackdropControl stageBackdrop = new()
        {
            Name = "PolishedBattleStageBackdrop",
            Position = new Vector2(8, 46),
            Size = new Vector2(1084, 384),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        stage.AddChild(stageBackdrop);
        stage.MoveChild(stageBackdrop, 0);

        // 把双方人物稍微向中央收拢；仍保持整数坐标和原始 1:1 Control 缩放，避免像素重新变糊。
        if (_leftCharacterField?.GetValue(_battleCoordinator) is AnimatedBattleCharacterControl leftCharacter)
        {
            leftCharacter.Position = new Vector2(48, 48);
            leftCharacter.Scale = Vector2.One;
            leftCharacter.Modulate = new Color(1.04f, 1.04f, 1.02f, 1.0f);
        }

        if (_rightCharacterField?.GetValue(_battleCoordinator) is AnimatedBattleCharacterControl rightCharacter)
        {
            rightCharacter.Position = new Vector2(632, 48);
            rightCharacter.Scale = Vector2.One;
            rightCharacter.Modulate = new Color(1.04f, 1.04f, 1.02f, 1.0f);
        }

        // 中央结果文字改成亮米黄色并增强阴影，在亮背景上也能保持清晰。
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
}
