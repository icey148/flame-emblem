using FlameEmblem.Main;
using Godot;
using System.Reflection;

namespace FlameEmblem.Visual;

/// <summary>
/// 把章节对话界面整理成最终定稿的金边深色大头像结构。
/// 本类只调整 ChapterDialogueCoordinator 已经创建的表现节点，不读取或修改剧情文本、触发条件和推进逻辑。
/// </summary>
public partial class ApprovedDialoguePresentationCoordinator : Node
{
    /// <summary>现有章节对话协调器。</summary>
    private ChapterDialogueCoordinator? _dialogueCoordinator;

    /// <summary>整套对话 UI 根节点字段。</summary>
    private FieldInfo? _dialogueRootField;

    /// <summary>左侧正式头像字段。</summary>
    private FieldInfo? _leftPortraitField;

    /// <summary>右侧正式头像字段。</summary>
    private FieldInfo? _rightPortraitField;

    /// <summary>说话者姓名字段。</summary>
    private FieldInfo? _speakerLabelField;

    /// <summary>台词正文文字字段。</summary>
    private FieldInfo? _textLabelField;

    /// <summary>进度文字字段。</summary>
    private FieldInfo? _progressLabelField;

    /// <summary>继续按钮字段，用于定位底部命令行。</summary>
    private FieldInfo? _nextButtonField;

    /// <summary>是否已经完成一次性布局整理。</summary>
    private bool _applied;

    /// <summary>缓存章节对话协调器和需要读取的表现字段。</summary>
    public override void _Ready()
    {
        ProcessPriority = 360;
        _dialogueCoordinator = GetNodeOrNull<ChapterDialogueCoordinator>("../ChapterDialogueCoordinator");
        if (_dialogueCoordinator is null)
        {
            GD.PushWarning("ApprovedDialoguePresentationCoordinator 找不到章节对话协调器。");
            SetProcess(false);
            return;
        }

        BindingFlags members = BindingFlags.Instance | BindingFlags.NonPublic;
        Type type = typeof(ChapterDialogueCoordinator);
        _dialogueRootField = type.GetField("_dialogueRoot", members);
        _leftPortraitField = type.GetField("_leftPortrait", members);
        _rightPortraitField = type.GetField("_rightPortrait", members);
        _speakerLabelField = type.GetField("_speakerLabel", members);
        _textLabelField = type.GetField("_textLabel", members);
        _progressLabelField = type.GetField("_progressLabel", members);
        _nextButtonField = type.GetField("_nextButton", members);
    }

    /// <summary>等待原对话 UI 完成动态创建后，再执行一次最终定稿整理。</summary>
    public override void _Process(double delta)
    {
        if (_applied || _dialogueCoordinator is null)
        {
            return;
        }

        _applied = TryApplyPresentation();
        if (_applied)
        {
            SetProcess(false);
        }
    }

    /// <summary>调整对话主框、头像、文字与按钮到最终金边设计。</summary>
    private bool TryApplyPresentation()
    {
        if (_dialogueCoordinator is null ||
            _dialogueRootField?.GetValue(_dialogueCoordinator) is not Control root ||
            _leftPortraitField?.GetValue(_dialogueCoordinator) is not CharacterPortraitControl leftPortrait ||
            _rightPortraitField?.GetValue(_dialogueCoordinator) is not CharacterPortraitControl rightPortrait ||
            _speakerLabelField?.GetValue(_dialogueCoordinator) is not Label speakerLabel ||
            _textLabelField?.GetValue(_dialogueCoordinator) is not Label textLabel)
        {
            return false;
        }

        PanelContainer? panel = FindFirstDescendant<PanelContainer>(root);
        if (panel is null)
        {
            return false;
        }

        // 对话框使用与战斗 HUD 相同的暗底金色双框，并略微上移扩大头像区域。
        panel.Position = new Vector2(30, 428);
        panel.Size = new Vector2(1220, 272);
        panel.AddThemeStyleboxOverride("panel", BuildDialoguePanelStyle());

        ColorRect? blocker = root.GetChildren().OfType<ColorRect>().FirstOrDefault();
        if (blocker is not null)
        {
            blocker.Color = new Color(0.0f, 0.0f, 0.0f, 0.62f);
        }

        HBoxContainer? dialogueRow = leftPortrait.GetParent() as HBoxContainer;
        if (dialogueRow is not null)
        {
            dialogueRow.CustomMinimumSize = new Vector2(1180, 202);
            dialogueRow.AddThemeConstantOverride("separation", 12);
        }

        ConfigurePortrait(leftPortrait);
        ConfigurePortrait(rightPortrait);

        if (speakerLabel.GetParent() is Control textColumn)
        {
            textColumn.CustomMinimumSize = new Vector2(700, 202);
            textColumn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        }

        speakerLabel.CustomMinimumSize = new Vector2(690, 42);
        speakerLabel.AddThemeFontOverride("font", BattlePixelFontCatalog.Font);
        speakerLabel.AddThemeFontSizeOverride("font_size", 24);
        speakerLabel.AddThemeColorOverride("font_color", new Color("e0bd78"));
        speakerLabel.AddThemeColorOverride("font_shadow_color", Colors.Black);
        speakerLabel.AddThemeConstantOverride("shadow_offset_x", 2);
        speakerLabel.AddThemeConstantOverride("shadow_offset_y", 2);

        textLabel.CustomMinimumSize = new Vector2(690, 142);
        textLabel.AddThemeFontOverride("font", BattlePixelFontCatalog.Font);
        textLabel.AddThemeFontSizeOverride("font_size", 20);
        textLabel.AddThemeColorOverride("font_color", new Color("eee7dc"));
        textLabel.AddThemeColorOverride("font_shadow_color", Colors.Black);
        textLabel.AddThemeConstantOverride("shadow_offset_x", 1);
        textLabel.AddThemeConstantOverride("shadow_offset_y", 1);

        if (_progressLabelField?.GetValue(_dialogueCoordinator) is Label progressLabel)
        {
            progressLabel.AddThemeFontOverride("font", BattlePixelFontCatalog.Font);
            progressLabel.AddThemeFontSizeOverride("font_size", 15);
            progressLabel.AddThemeColorOverride("font_color", new Color("9d8255"));
        }

        if (_nextButtonField?.GetValue(_dialogueCoordinator) is Button nextButton &&
            nextButton.GetParent() is HBoxContainer commandRow)
        {
            commandRow.CustomMinimumSize = new Vector2(1180, 46);
            commandRow.AddThemeConstantOverride("separation", 8);
        }

        foreach (Button button in FindDescendants<Button>(root))
        {
            ApplyButtonStyle(button);
        }

        return true;
    }

    /// <summary>把对话头像扩大到最终卡片比例，并使用高分辨率正式素材的平滑过滤。</summary>
    private static void ConfigurePortrait(CharacterPortraitControl portrait)
    {
        portrait.CustomMinimumSize = new Vector2(218, 202);
        portrait.TextureFilter = CanvasItem.TextureFilterEnum.Linear;
    }

    /// <summary>创建与最终战斗 HUD 一致的深色金边对话面板。</summary>
    private static StyleBoxFlat BuildDialoguePanelStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color("080b10"),
            BorderColor = new Color("b8833f"),
            BorderWidthLeft = 3,
            BorderWidthTop = 3,
            BorderWidthRight = 3,
            BorderWidthBottom = 3,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
            ContentMarginLeft = 10,
            ContentMarginTop = 8,
            ContentMarginRight = 10,
            ContentMarginBottom = 8
        };
    }

    /// <summary>给继续/跳过按钮套用克制的暗底金边样式。</summary>
    private static void ApplyButtonStyle(Button button)
    {
        button.AddThemeFontOverride("font", BattlePixelFontCatalog.Font);
        button.AddThemeFontSizeOverride("font_size", 16);
        button.AddThemeColorOverride("font_color", new Color("e0bd78"));
        button.AddThemeColorOverride("font_hover_color", new Color("f2d99d"));
        button.AddThemeStyleboxOverride("normal", BuildButtonStyle(new Color("0c1119"), new Color("6f4a26")));
        button.AddThemeStyleboxOverride("hover", BuildButtonStyle(new Color("151c28"), new Color("b8833f")));
        button.AddThemeStyleboxOverride("pressed", BuildButtonStyle(new Color("07090d"), new Color("e0bd78")));
    }

    /// <summary>创建按钮状态使用的统一暗底金边样式。</summary>
    private static StyleBoxFlat BuildButtonStyle(Color background, Color border)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2
        };
    }

    /// <summary>从节点树中找到第一个指定类型的后代节点。</summary>
    private static T? FindFirstDescendant<T>(Node root) where T : Node
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is T typed)
            {
                return typed;
            }

            T? nested = FindFirstDescendant<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    /// <summary>收集节点树中所有指定类型的后代节点。</summary>
    private static IEnumerable<T> FindDescendants<T>(Node root) where T : Node
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is T typed)
            {
                yield return typed;
            }

            foreach (T nested in FindDescendants<T>(child))
            {
                yield return nested;
            }
        }
    }
}
