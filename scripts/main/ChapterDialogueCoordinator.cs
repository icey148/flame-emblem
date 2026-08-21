using FlameEmblem.Game;
using FlameEmblem.Visual;
using Godot;
using System.Reflection;

namespace FlameEmblem.Main;

/// <summary>
/// 在战斗场景中播放章节开场与胜利对话。
/// 文本由 ChapterDialogueCatalog 提供；本协调器只负责触发、双侧头像绑定、输入屏蔽和逐句推进，不参与战斗规则。
/// </summary>
public partial class ChapterDialogueCoordinator : Node
{
    /// <summary>当前战斗主节点。</summary>
    private Node? _battleHost;

    /// <summary>MainGame 全部单位字段，用于把 speaker_id 绑定到现有头像系统。</summary>
    private FieldInfo? _unitsField;

    /// <summary>MainGame 当前阶段字段，用于检测 Victory。</summary>
    private FieldInfo? _phaseField;

    /// <summary>整套对话 UI 根节点。</summary>
    private Control? _dialogueRoot;

    /// <summary>左侧头像。</summary>
    private CharacterPortraitControl? _leftPortrait;

    /// <summary>右侧头像。</summary>
    private CharacterPortraitControl? _rightPortrait;

    /// <summary>左侧当前保留的对话角色；换说话人时才更新，不会因对方开口而被清空。</summary>
    private UnitModel? _leftConversationUnit;

    /// <summary>右侧当前保留的对话角色；换说话人时才更新，不会因对方开口而被清空。</summary>
    private UnitModel? _rightConversationUnit;

    /// <summary>说话者姓名。</summary>
    private Label? _speakerLabel;

    /// <summary>当前台词正文。</summary>
    private Label? _textLabel;

    /// <summary>当前句数提示。</summary>
    private Label? _progressLabel;

    /// <summary>继续按钮。</summary>
    private Button? _nextButton;

    /// <summary>当前正在播放的对话序列。</summary>
    private ChapterDialogueSequence? _activeSequence;

    /// <summary>当前台词索引。</summary>
    private int _lineIndex;

    /// <summary>本场战斗是否已经处理过开场对话。</summary>
    private bool _startHandled;

    /// <summary>本场战斗是否已经处理过胜利对话。</summary>
    private bool _victoryHandled;

    /// <summary>当前是否有剧情对话覆盖战斗界面。</summary>
    public bool IsDialogueActive => _activeSequence is not null;

    /// <summary>缓存 MainGame 状态并创建隐藏的对话层。</summary>
    public override void _Ready()
    {
        // ChapterFlowCoordinator(-500) 先准备正确章节；这里随后读取章节 ID 并决定是否播放开场。
        ProcessPriority = -400;
        _battleHost = GetParent();
        if (_battleHost is null)
        {
            GD.PushWarning("ChapterDialogueCoordinator 找不到 MainGame，章节对话不会启动。");
            SetProcess(false);
            return;
        }

        BindingFlags members = BindingFlags.Instance | BindingFlags.NonPublic;
        Type hostType = _battleHost.GetType();
        _unitsField = hostType.GetField("_units", members);
        _phaseField = hostType.GetField("_phase", members);
        if (_unitsField is null || _phaseField is null)
        {
            GD.PushWarning("ChapterDialogueCoordinator 无法读取 MainGame 单位或阶段状态，章节对话已停用。");
            SetProcess(false);
            return;
        }

        CreateDialogueInterface();
    }

    /// <summary>第一帧触发开场对话，并在胜利时触发战后对话。</summary>
    public override void _Process(double delta)
    {
        IReadOnlyList<UnitModel> units = ReadUnits();
        if (!_startHandled && units.Count > 0)
        {
            _startHandled = true;

            // 跨章节读档会在同一场景切换流程中恢复精确战斗快照，不重复播放章节开场。
            // 已经完成过的章节再次从世界地图进入时也视为重打，不强制重复开场剧情。
            if (!SaveGameService.HasPendingSceneRestore &&
                !CampaignState.IsChapterCompleted(CampaignState.CurrentChapterId))
            {
                TryStartEvent("start");
            }
        }

        bool victory = CurrentPhaseName().Equals("Victory", StringComparison.Ordinal);
        if (victory && !_victoryHandled)
        {
            _victoryHandled = true;
            TryStartEvent("victory");
        }
    }

    /// <summary>创建全屏输入屏蔽与底部复古双头像对话框。</summary>
    private void CreateDialogueInterface()
    {
        CanvasLayer layer = new()
        {
            Layer = 80
        };
        AddChild(layer);

        _dialogueRoot = new Control
        {
            Position = Vector2.Zero,
            Size = new Vector2(1280, 720),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        layer.AddChild(_dialogueRoot);

        // 半透明全屏层负责吞掉战斗地图与右侧 HUD 的鼠标输入；点击空白区域也可以推进一句。
        ColorRect blocker = new()
        {
            Position = Vector2.Zero,
            Size = new Vector2(1280, 720),
            Color = new Color(0.03f, 0.045f, 0.055f, 0.38f),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        blocker.GuiInput += OnBlockerGuiInput;
        _dialogueRoot.AddChild(blocker);

        PanelContainer panel = new()
        {
            Position = new Vector2(45, 462),
            Size = new Vector2(1190, 223),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("182231"),
            BorderColor = new Color("8b7757"),
            BorderWidthLeft = 3,
            BorderWidthTop = 3,
            BorderWidthRight = 3,
            BorderWidthBottom = 3,
            CornerRadiusTopLeft = 0,
            CornerRadiusTopRight = 0,
            CornerRadiusBottomLeft = 0,
            CornerRadiusBottomRight = 0
        });
        _dialogueRoot.AddChild(panel);

        VBoxContainer outerColumn = new();
        panel.AddChild(outerColumn);

        HBoxContainer dialogueRow = new()
        {
            CustomMinimumSize = new Vector2(1160, 168)
        };
        outerColumn.AddChild(dialogueRow);

        _leftPortrait = new CharacterPortraitControl
        {
            CustomMinimumSize = new Vector2(170, 164),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        dialogueRow.AddChild(_leftPortrait);
        AttachTeamFrame(_leftPortrait, "LeftTeamFrame");

        VBoxContainer textColumn = new()
        {
            CustomMinimumSize = new Vector2(805, 164),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        dialogueRow.AddChild(textColumn);

        _speakerLabel = new Label
        {
            Text = "",
            CustomMinimumSize = new Vector2(780, 36)
        };
        _speakerLabel.AddThemeFontSizeOverride("font_size", 22);
        _speakerLabel.AddThemeColorOverride("font_shadow_color", new Color(0.02f, 0.03f, 0.05f, 0.95f));
        _speakerLabel.AddThemeConstantOverride("shadow_offset_x", 2);
        _speakerLabel.AddThemeConstantOverride("shadow_offset_y", 2);
        textColumn.AddChild(_speakerLabel);

        _textLabel = new Label
        {
            Text = "",
            CustomMinimumSize = new Vector2(780, 108),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            VerticalAlignment = VerticalAlignment.Center
        };
        _textLabel.AddThemeFontSizeOverride("font_size", 19);
        _textLabel.AddThemeColorOverride("font_color", new Color("eee8d8"));
        textColumn.AddChild(_textLabel);

        _rightPortrait = new CharacterPortraitControl
        {
            CustomMinimumSize = new Vector2(170, 164),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        dialogueRow.AddChild(_rightPortrait);
        AttachTeamFrame(_rightPortrait, "RightTeamFrame");

        HBoxContainer commandRow = new()
        {
            CustomMinimumSize = new Vector2(1160, 40),
            Alignment = BoxContainer.AlignmentMode.End
        };
        outerColumn.AddChild(commandRow);

        _progressLabel = new Label
        {
            Text = "",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        _progressLabel.AddThemeColorOverride("font_color", new Color("b8bdc6"));
        commandRow.AddChild(_progressLabel);

        Button skipButton = new()
        {
            Text = "跳过",
            CustomMinimumSize = new Vector2(105, 34)
        };
        skipButton.Pressed += FinishSequence;
        commandRow.AddChild(skipButton);

        _nextButton = new Button
        {
            Text = "继续",
            CustomMinimumSize = new Vector2(125, 34)
        };
        _nextButton.Pressed += AdvanceLine;
        commandRow.AddChild(_nextButton);
    }

    /// <summary>给一个对话头像挂载深蓝/粉红阵营边框。</summary>
    private static void AttachTeamFrame(CharacterPortraitControl portrait, string name)
    {
        TeamPortraitFrameControl frame = new()
        {
            Name = name,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        portrait.AddChild(frame);
        frame.Bind(portrait);
    }

    /// <summary>尝试播放当前章节指定事件；没有数据时静默跳过。</summary>
    private void TryStartEvent(string eventId)
    {
        if (IsDialogueActive)
        {
            return;
        }

        ChapterDialogueSequence? sequence = ChapterDialogueCatalog.TryGet(
            CampaignState.CurrentChapterId,
            eventId);
        if (sequence is null || sequence.Lines.Count == 0)
        {
            return;
        }

        _activeSequence = sequence;
        _lineIndex = 0;
        PrepareConversationPortraits(sequence);
        if (_dialogueRoot is not null)
        {
            _dialogueRoot.Visible = true;
        }

        ShowCurrentLine();
    }

    /// <summary>
    /// 在第一句出现前先找到左右两侧各自最早出现的角色。
    /// 这样即使第一句只有左侧说话，右侧也已经存在对话对象，不再出现一边空头像。
    /// </summary>
    private void PrepareConversationPortraits(ChapterDialogueSequence sequence)
    {
        _leftConversationUnit = ResolveFirstSpeakerOnSide(sequence, "left");
        _rightConversationUnit = ResolveFirstSpeakerOnSide(sequence, "right");
        _leftPortrait?.SetUnit(_leftConversationUnit);
        _rightPortrait?.SetUnit(_rightConversationUnit);
    }

    /// <summary>寻找一段对话中指定侧最早出现并能绑定到当前战场单位的角色。</summary>
    private UnitModel? ResolveFirstSpeakerOnSide(ChapterDialogueSequence sequence, string side)
    {
        foreach (ChapterDialogueLine line in sequence.Lines)
        {
            if (!line.Side.Equals(side, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            UnitModel? speaker = ResolveSpeaker(line.SpeakerId);
            if (speaker is not null)
            {
                return speaker;
            }
        }

        return null;
    }

    /// <summary>把当前台词、双侧头像、姓名和进度写入 UI。</summary>
    private void ShowCurrentLine()
    {
        if (_activeSequence is null ||
            _lineIndex < 0 ||
            _lineIndex >= _activeSequence.Lines.Count)
        {
            FinishSequence();
            return;
        }

        ChapterDialogueLine line = _activeSequence.Lines[_lineIndex];
        UnitModel? speaker = ResolveSpeaker(line.SpeakerId);
        bool rightSide = line.Side.Equals("right", StringComparison.OrdinalIgnoreCase);

        // 当前侧换人时只更新当前侧；另一侧继续保留它最后一个角色，而不是被设成 null。
        if (rightSide)
        {
            _rightConversationUnit = speaker ?? _rightConversationUnit;
        }
        else
        {
            _leftConversationUnit = speaker ?? _leftConversationUnit;
        }

        _leftPortrait?.SetUnit(_leftConversationUnit);
        _rightPortrait?.SetUnit(_rightConversationUnit);
        ApplySpeakerEmphasis(rightSide);

        if (_speakerLabel is not null)
        {
            _speakerLabel.Text = string.IsNullOrWhiteSpace(line.SpeakerName)
                ? speaker?.DisplayName ?? line.SpeakerId
                : line.SpeakerName;
            _speakerLabel.HorizontalAlignment = rightSide
                ? HorizontalAlignment.Right
                : HorizontalAlignment.Left;
            _speakerLabel.AddThemeColorOverride(
                "font_color",
                speaker is null
                    ? new Color("eee2c7")
                    : TeamVisualPalette.Highlight(speaker.Team));
        }

        if (_textLabel is not null)
        {
            _textLabel.Text = line.Text;
        }

        if (_progressLabel is not null)
        {
            _progressLabel.Text = $"{_lineIndex + 1} / {_activeSequence.Lines.Count}   点击空白区域也可继续";
        }

        if (_nextButton is not null)
        {
            _nextButton.Text = _lineIndex >= _activeSequence.Lines.Count - 1
                ? "结束对话"
                : "继续";
        }
    }

    /// <summary>当前说话者保持明亮，另一侧保留可见但压暗，形成明确的对话焦点。</summary>
    private void ApplySpeakerEmphasis(bool rightSide)
    {
        Color active = Colors.White;
        Color inactive = new(0.56f, 0.58f, 0.62f, 1.0f);

        if (_leftPortrait is not null)
        {
            _leftPortrait.Modulate = rightSide ? inactive : active;
        }

        if (_rightPortrait is not null)
        {
            _rightPortrait.Modulate = rightSide ? active : inactive;
        }
    }

    /// <summary>按稳定 speaker_id 找到当前战场单位。</summary>
    private UnitModel? ResolveSpeaker(string speakerId)
    {
        return ReadUnits().FirstOrDefault(unit =>
            unit.Id.Equals(speakerId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>推进到下一句；最后一句后关闭对话层。</summary>
    private void AdvanceLine()
    {
        if (_activeSequence is null)
        {
            return;
        }

        _lineIndex++;
        if (_lineIndex >= _activeSequence.Lines.Count)
        {
            FinishSequence();
            return;
        }

        ShowCurrentLine();
    }

    /// <summary>结束当前对话并释放地图与 HUD 输入。</summary>
    private void FinishSequence()
    {
        _activeSequence = null;
        _lineIndex = 0;
        _leftConversationUnit = null;
        _rightConversationUnit = null;
        _leftPortrait?.SetUnit(null);
        _rightPortrait?.SetUnit(null);

        if (_leftPortrait is not null)
        {
            _leftPortrait.Modulate = Colors.White;
        }

        if (_rightPortrait is not null)
        {
            _rightPortrait.Modulate = Colors.White;
        }

        if (_dialogueRoot is not null)
        {
            _dialogueRoot.Visible = false;
        }
    }

    /// <summary>点击对话框外的遮罩时也推进一句，并阻止该点击落到战棋地图。</summary>
    private void OnBlockerGuiInput(InputEvent @event)
    {
        if (!IsDialogueActive)
        {
            return;
        }

        if (@event is InputEventMouseButton mouseButton &&
            mouseButton.ButtonIndex == MouseButton.Left &&
            mouseButton.Pressed)
        {
            AdvanceLine();
        }
    }

    /// <summary>读取当前战场全部单位。</summary>
    private IReadOnlyList<UnitModel> ReadUnits()
    {
        if (_battleHost is null || _unitsField is null)
        {
            return Array.Empty<UnitModel>();
        }

        return _unitsField.GetValue(_battleHost) as IReadOnlyList<UnitModel>
               ?? Array.Empty<UnitModel>();
    }

    /// <summary>读取 MainGame 私有 BattlePhase 的枚举名称。</summary>
    private string CurrentPhaseName()
    {
        return _battleHost is not null
            ? _phaseField?.GetValue(_battleHost)?.ToString() ?? string.Empty
            : string.Empty;
    }
}
