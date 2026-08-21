using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.World;

/// <summary>
/// 原创复古世界地图界面。
/// 节点位置、类型和解锁要求来自 world_map.json；本脚本只负责绘制路线和处理节点点击。
/// </summary>
public partial class WorldMapScreen : Node2D
{
    /// <summary>世界地图数据。</summary>
    private WorldMapDefinition? _map;

    /// <summary>底部状态文本，用于显示节点说明和祠堂结果。</summary>
    private Label? _statusLabel;

    /// <summary>世界地图按钮列表，切换状态时统一刷新。</summary>
    private readonly Dictionary<string, Button> _nodeButtons = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>进入场景后加载数据并创建节点按钮。</summary>
    public override void _Ready()
    {
        try
        {
            _map = WorldMapCatalog.Load();
            CreateInterface();
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            CreateErrorInterface(exception.Message);
        }

        QueueRedraw();
    }

    /// <summary>绘制原创明亮像素风区域地图、河流、山体和路线。</summary>
    public override void _Draw()
    {
        // 世界地图采用比战斗 HUD 更亮的草绿/土黄基调，让章节切换有明显的空间感变化。
        DrawRect(new Rect2(Vector2.Zero, new Vector2(1280, 720)), new Color("91b66d"), true);
        DrawRect(new Rect2(0, 0, 1280, 82), new Color("597a68"), true);
        DrawRect(new Rect2(0, 612, 1280, 108), new Color("49645f"), true);

        DrawRiver();
        DrawMountains();
        DrawRoads();
        DrawNodeMarkers();
    }

    /// <summary>绘制从北向南穿过地图的浅蓝河流。</summary>
    private void DrawRiver()
    {
        Color deepWater = new("4d8296");
        Color lightWater = new("6ba5b0");
        Vector2[] river =
        {
            new(420, 82), new(472, 82),
            new(505, 175), new(480, 265),
            new(515, 360), new(500, 465),
            new(548, 612), new(492, 612)
        };
        DrawColoredPolygon(river, deepWater);

        // 离散亮色水纹保持硬边，不使用平滑渐变。
        for (int y = 125; y < 590; y += 58)
        {
            int offset = (y / 58) % 2 == 0 ? 0 : 18;
            DrawRect(new Rect2(478 + offset, y, 34, 4), lightWater, true);
        }
    }

    /// <summary>绘制地图北侧和东侧的阶梯状山脉轮廓。</summary>
    private void DrawMountains()
    {
        Color mountainDark = new("65704f");
        Color mountainLight = new("899069");

        for (int x = 90; x <= 340; x += 76)
        {
            DrawPolygon(
                new[]
                {
                    new Vector2(x, 165),
                    new Vector2(x + 34, 112),
                    new Vector2(x + 68, 165)
                },
                new[] { mountainDark });
            DrawPolygon(
                new[]
                {
                    new Vector2(x + 27, 154),
                    new Vector2(x + 34, 124),
                    new Vector2(x + 45, 154)
                },
                new[] { mountainLight });
        }

        for (int x = 930; x <= 1160; x += 72)
        {
            DrawPolygon(
                new[]
                {
                    new Vector2(x, 235),
                    new Vector2(x + 30, 185),
                    new Vector2(x + 62, 235)
                },
                new[] { mountainDark });
        }
    }

    /// <summary>按节点数据顺序绘制连接路线。</summary>
    private void DrawRoads()
    {
        if (_map is null || _map.Nodes.Count < 2)
        {
            return;
        }

        Color roadShadow = new("76694b");
        Color road = new("c3aa74");
        for (int index = 0; index < _map.Nodes.Count - 1; index++)
        {
            Vector2 from = _map.Nodes[index].Position;
            Vector2 to = _map.Nodes[index + 1].Position;
            DrawLine(from, to, roadShadow, 12.0f, false);
            DrawLine(from, to, road, 6.0f, false);
        }
    }

    /// <summary>绘制按钮背后的地图节点圆盘，并用明度区分锁定状态。</summary>
    private void DrawNodeMarkers()
    {
        if (_map is null)
        {
            return;
        }

        foreach (WorldMapNodeDefinition node in _map.Nodes)
        {
            bool unlocked = node.IsUnlocked;
            Color outer = unlocked ? new Color("503f31") : new Color("4d5552");
            Color inner = unlocked ? new Color("d1b46e") : new Color("77807b");
            DrawCircle(node.Position, 31.0f, outer);
            DrawCircle(node.Position, 22.0f, inner);

            if (node.Type == WorldMapNodeType.Shrine)
            {
                // 祠堂以十字石纹表示；纯几何原创标记避免依赖外部图标素材。
                DrawRect(new Rect2(node.Position + new Vector2(-4, -14), new Vector2(8, 28)), outer, true);
                DrawRect(new Rect2(node.Position + new Vector2(-12, -5), new Vector2(24, 8)), outer, true);
            }
            else
            {
                DrawRect(new Rect2(node.Position + new Vector2(-10, -8), new Vector2(20, 17)), outer, true);
                DrawRect(new Rect2(node.Position + new Vector2(-14, 7), new Vector2(28, 7)), outer, true);
            }
        }
    }

    /// <summary>创建标题、说明和每个世界地图节点的按钮。</summary>
    private void CreateInterface()
    {
        if (_map is null)
        {
            return;
        }

        Label title = new()
        {
            Text = $"FLAME EMBLEM   {_map.Title}",
            Position = new Vector2(42, 24),
            Size = new Vector2(760, 42)
        };
        // Godot 4 使用主题覆盖接口设置字号，避免依赖不存在的对象初始化属性。
        title.AddThemeFontSizeOverride("font_size", 26);
        AddChild(title);

        Label hint = new()
        {
            Text = "选择地图节点。完成战斗会解锁新的路线与休整地点。",
            Position = new Vector2(820, 30),
            Size = new Vector2(410, 34),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        AddChild(hint);

        foreach (WorldMapNodeDefinition node in _map.Nodes)
        {
            Button button = new()
            {
                Position = node.Position + new Vector2(-90, 40),
                Size = new Vector2(180, 38),
                FocusMode = Control.FocusModeEnum.All
            };
            button.Pressed += () => ActivateNode(node);
            AddChild(button);
            _nodeButtons[node.Id] = button;
        }

        PanelContainer statusPanel = new()
        {
            Position = new Vector2(70, 628),
            Size = new Vector2(1140, 66)
        };
        AddChild(statusPanel);

        _statusLabel = new Label
        {
            Text = CampaignState.HasPlayerRoster
                ? "序章战果已经带回世界地图。可以休整、重返旧战场，或查看下一条路线。"
                : "世界地图准备完成。当前没有跨章节队伍数据。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        statusPanel.AddChild(_statusLabel);

        RefreshNodeButtons();
    }

    /// <summary>根据战役完成状态刷新所有世界地图节点。</summary>
    private void RefreshNodeButtons()
    {
        if (_map is null)
        {
            return;
        }

        foreach (WorldMapNodeDefinition node in _map.Nodes)
        {
            if (!_nodeButtons.TryGetValue(node.Id, out Button? button))
            {
                continue;
            }

            bool unlocked = node.IsUnlocked;
            button.Disabled = !unlocked;
            string completed = !string.IsNullOrWhiteSpace(node.ChapterId) &&
                               CampaignState.IsChapterCompleted(node.ChapterId)
                ? " 已完成"
                : string.Empty;
            string future = node.Type == WorldMapNodeType.FutureBattle ? " 准备中" : string.Empty;
            button.Text = unlocked
                ? $"{node.DisplayName}{completed}{future}"
                : $"{node.DisplayName} 未解锁";
        }

        QueueRedraw();
    }

    /// <summary>处理一个已解锁地图节点。</summary>
    private void ActivateNode(WorldMapNodeDefinition node)
    {
        if (!node.IsUnlocked)
        {
            SetStatus($"{node.DisplayName} 还没有解锁。");
            return;
        }

        switch (node.Type)
        {
            case WorldMapNodeType.Battle:
                EnterBattleNode(node);
                break;
            case WorldMapNodeType.Shrine:
                SetStatus(CampaignState.RestAtShrine());
                break;
            case WorldMapNodeType.FutureBattle:
                SetStatus("东境道路已经开放。第二章战斗数据将在下一阶段接入，这个节点结构已经可以直接承载新章节。");
                break;
        }

        RefreshNodeButtons();
    }

    /// <summary>记录目标章节并切回统一战斗场景。</summary>
    private void EnterBattleNode(WorldMapNodeDefinition node)
    {
        if (string.IsNullOrWhiteSpace(node.ChapterId) || string.IsNullOrWhiteSpace(node.ChapterPath))
        {
            SetStatus($"{node.DisplayName} 没有配置可进入的章节数据。");
            return;
        }

        CampaignState.BeginChapter(node.ChapterId, node.ChapterPath);
        Error error = GetTree().ChangeSceneToFile("res://scenes/main/Main.tscn");
        if (error != Error.Ok)
        {
            SetStatus($"无法进入 {node.DisplayName}：{error}。");
        }
    }

    /// <summary>显示世界地图状态文本。</summary>
    private void SetStatus(string message)
    {
        if (_statusLabel is not null)
        {
            _statusLabel.Text = message;
        }
    }

    /// <summary>世界地图数据加载失败时仍显示可读错误。</summary>
    private void CreateErrorInterface(string message)
    {
        Label error = new()
        {
            Text = $"世界地图加载失败：{message}",
            Position = new Vector2(120, 280),
            Size = new Vector2(1040, 100),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        AddChild(error);
    }
}
