using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Title;

/// <summary>
/// 游戏启动标题画面。
/// 提供新游戏、继续游戏和退出；继续入口会根据存档位置进入战斗或世界地图。
/// </summary>
public partial class TitleScreen : Node2D
{
    /// <summary>新游戏按钮；有旧存档时第一次点击只进入覆盖确认状态。</summary>
    private Button? _newGameButton;

    /// <summary>继续游戏按钮；没有有效存档时禁用。</summary>
    private Button? _continueButton;

    /// <summary>显示单槽位摘要与错误信息。</summary>
    private Label? _statusLabel;

    /// <summary>是否已经进行过一次新游戏覆盖确认。</summary>
    private bool _overwriteArmed;

    /// <summary>创建标题菜单并读取单槽位摘要。</summary>
    public override void _Ready()
    {
        CreateInterface();
        RefreshSaveSummary();
        QueueRedraw();
    }

    /// <summary>绘制原创复古边境标题背景。</summary>
    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, new Vector2(1280, 720)), new Color("172330"), true);
        DrawRect(new Rect2(0, 430, 1280, 290), new Color("354b3f"), true);
        DrawRect(new Rect2(0, 505, 1280, 215), new Color("4d6043"), true);

        // 硬边远山保持与战斗地图一致的像素化视觉语言。
        for (int x = -100; x < 1300; x += 180)
        {
            DrawPolygon(
                new[]
                {
                    new Vector2(x, 475),
                    new Vector2(x + 90, 300),
                    new Vector2(x + 185, 475)
                },
                new[] { new Color("293b43") });
        }

        // 远处残破城堡作为东境战事的原创视觉主题。
        Color castle = new("283034");
        DrawRect(new Rect2(535, 360, 210, 130), castle, true);
        DrawRect(new Rect2(510, 320, 50, 170), castle, true);
        DrawRect(new Rect2(720, 332, 50, 158), castle, true);
        DrawRect(new Rect2(595, 300, 88, 190), castle, true);
        DrawRect(new Rect2(621, 432, 38, 58), new Color("111a20"), true);

        // 土路把视觉中心从菜单引向远处章节地图。
        DrawPolygon(
            new[]
            {
                new Vector2(500, 720),
                new Vector2(780, 720),
                new Vector2(690, 485),
                new Vector2(595, 485)
            },
            new[] { new Color("9b875e") });

        DrawFlameEmblem();
    }

    /// <summary>绘制原创铜色火焰徽记。</summary>
    private void DrawFlameEmblem()
    {
        Vector2 center = new(640, 155);
        DrawColoredPolygon(
            new[]
            {
                center + new Vector2(0, -66),
                center + new Vector2(34, -16),
                center + new Vector2(23, 35),
                center + new Vector2(0, 64),
                center + new Vector2(-31, 31),
                center + new Vector2(-37, -11)
            },
            new Color("80542d"));
        DrawColoredPolygon(
            new[]
            {
                center + new Vector2(3, -45),
                center + new Vector2(22, -9),
                center + new Vector2(11, 32),
                center + new Vector2(-2, 46),
                center + new Vector2(-19, 20),
                center + new Vector2(-17, -5)
            },
            new Color("c48a45"));
        DrawColoredPolygon(
            new[]
            {
                center + new Vector2(2, -24),
                center + new Vector2(11, 2),
                center + new Vector2(4, 27),
                center + new Vector2(-8, 14),
                center + new Vector2(-6, -4)
            },
            new Color("e1b866"));
    }

    /// <summary>创建标题、菜单和存档摘要。</summary>
    private void CreateInterface()
    {
        Label title = new()
        {
            Text = "FLAME EMBLEM",
            Position = new Vector2(330, 205),
            Size = new Vector2(620, 72),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 42);
        title.AddThemeColorOverride("font_color", new Color("ead9a2"));
        AddChild(title);

        Label subtitle = new()
        {
            Text = "东境战记",
            Position = new Vector2(390, 265),
            Size = new Vector2(500, 38),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        subtitle.AddThemeFontSizeOverride("font_size", 20);
        AddChild(subtitle);

        PanelContainer panel = new()
        {
            Position = new Vector2(455, 325),
            Size = new Vector2(370, 300)
        };
        AddChild(panel);

        VBoxContainer menu = new()
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        panel.AddChild(menu);

        _newGameButton = new Button
        {
            Text = "新游戏",
            CustomMinimumSize = new Vector2(320, 46)
        };
        _newGameButton.Pressed += RequestNewGame;
        menu.AddChild(_newGameButton);

        _continueButton = new Button
        {
            Text = "继续游戏",
            CustomMinimumSize = new Vector2(320, 46)
        };
        _continueButton.Pressed += ContinueGame;
        menu.AddChild(_continueButton);

        Button quitButton = new()
        {
            Text = "退出游戏",
            CustomMinimumSize = new Vector2(320, 42)
        };
        quitButton.Pressed += () => GetTree().Quit();
        menu.AddChild(quitButton);

        _statusLabel = new Label
        {
            Text = "读取本地存档状态中……",
            CustomMinimumSize = new Vector2(330, 92),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        menu.AddChild(_statusLabel);
    }

    /// <summary>读取单槽位但不改变战役状态，只更新继续按钮和摘要。</summary>
    private void RefreshSaveSummary()
    {
        if (_continueButton is null || _statusLabel is null)
        {
            return;
        }

        if (!SaveGameService.HasSave)
        {
            _continueButton.Disabled = true;
            _continueButton.Text = "继续游戏（无存档）";
            _statusLabel.Text = "没有本地存档。选择“新游戏”开始序章。";
            return;
        }

        if (!SaveGameService.TryLoad(out SaveGameData? data, out string message) || data is null)
        {
            _continueButton.Disabled = true;
            _continueButton.Text = "继续游戏（存档异常）";
            _statusLabel.Text = message;
            return;
        }

        _continueButton.Disabled = false;
        if (data.Location == SaveLocation.WorldMap)
        {
            _continueButton.Text = "继续游戏 · 世界地图";
            _statusLabel.Text = $"槽位 1：世界地图\n已完成章节：{data.Campaign.CompletedChapterIds.Count}";
        }
        else
        {
            _continueButton.Text = "继续游戏 · 战斗";
            _statusLabel.Text = $"槽位 1：{data.ChapterId}\n第 {Math.Max(1, data.Round)} 回合";
        }
    }

    /// <summary>请求开始新游戏；已有存档时要求连续确认两次才真正覆盖。</summary>
    private void RequestNewGame()
    {
        if (SaveGameService.HasSave && !_overwriteArmed)
        {
            _overwriteArmed = true;
            if (_newGameButton is not null)
            {
                _newGameButton.Text = "再次点击确认新游戏";
            }

            if (_statusLabel is not null)
            {
                _statusLabel.Text = "再次点击“新游戏”会清除槽位 1 并从序章重新开始。";
            }
            return;
        }

        StartNewGame();
    }

    /// <summary>清空旧战役和本地槽位，然后进入序章。</summary>
    private void StartNewGame()
    {
        CampaignState.ResetCampaign();
        SaveGameService.TryDeleteSave(out _);
        CampaignState.BeginChapter(CampaignState.DefaultChapterId, CampaignState.DefaultChapterPath);

        Error error = GetTree().ChangeSceneToFile("res://scenes/main/Main.tscn");
        if (error != Error.Ok && _statusLabel is not null)
        {
            _statusLabel.Text = $"无法开始新游戏：{error}。";
        }
    }

    /// <summary>从单槽位恢复战役，并按存档位置进入世界地图或具体战斗。</summary>
    private void ContinueGame()
    {
        if (!SaveGameService.TryLoad(out SaveGameData? data, out string message) || data is null)
        {
            if (_statusLabel is not null)
            {
                _statusLabel.Text = message;
            }
            RefreshSaveSummary();
            return;
        }

        CampaignState.RestoreSaveSnapshot(data.Campaign, data.ChapterId, data.ChapterPath);
        if (data.Location == SaveLocation.WorldMap)
        {
            ChangeScene("res://scenes/world/WorldMap.tscn", "世界地图");
            return;
        }

        CampaignState.BeginChapter(data.ChapterId, data.ChapterPath);
        SaveGameService.QueuePendingSceneRestore(data);
        ChangeScene("res://scenes/main/Main.tscn", data.ChapterId);
    }

    /// <summary>统一执行场景切换并在失败时显示可读错误。</summary>
    private void ChangeScene(string scenePath, string destination)
    {
        Error error = GetTree().ChangeSceneToFile(scenePath);
        if (error != Error.Ok && _statusLabel is not null)
        {
            _statusLabel.Text = $"无法进入 {destination}：{error}。";
        }
    }
}
