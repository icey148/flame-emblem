using FlameEmblem.Game;
using Godot;
using System.Reflection;

namespace FlameEmblem.Visual;

/// <summary>
/// 把 MainGame 现有 HUD 统一成复古战棋界面，并额外提供顶部回合条与鼠标悬停地形信息。
/// 本类只修改 Control 样式和读取战斗状态，不接管按钮逻辑，因此现有攻击/等待/结束回合行为保持不变。
/// </summary>
public partial class RetroHudCoordinator : Node
{
    /// <summary>战棋地图左上角，用于把鼠标位置转换成地形格。</summary>
    private static readonly Vector2 BoardOrigin = new(40, 100);

    /// <summary>逻辑格尺寸与 MainGame 保持一致。</summary>
    private const int CellSize = 52;

    /// <summary>当前战斗主节点。</summary>
    private Node? _battleHost;

    /// <summary>HUD 是否已经完成一次性样式应用。</summary>
    private bool _hudStyled;

    /// <summary>顶部回合提示文本。</summary>
    private Label? _phaseLabel;

    /// <summary>顶部鼠标悬停地形文本。</summary>
    private Label? _hoverTerrainLabel;

    /// <summary>上一次回合状态签名，避免每帧重复写 Label。</summary>
    private string _lastPhaseState = string.Empty;

    /// <summary>上一次鼠标地形状态签名。</summary>
    private string _lastTerrainState = string.Empty;

    /// <summary>MainGame 当前回合字段。</summary>
    private FieldInfo? _roundField;

    /// <summary>MainGame 当前阶段字段。</summary>
    private FieldInfo? _phaseField;

    /// <summary>MainGame 地形表字段。</summary>
    private FieldInfo? _terrainField;

    /// <summary>MainGame 地图宽度字段。</summary>
    private FieldInfo? _gridWidthField;

    /// <summary>MainGame 地图高度字段。</summary>
    private FieldInfo? _gridHeightField;

    /// <summary>MainGame HUD 标题字段。</summary>
    private FieldInfo? _titleLabelField;

    /// <summary>MainGame HUD 状态字段。</summary>
    private FieldInfo? _statusLabelField;

    /// <summary>MainGame HUD 地形字段。</summary>
    private FieldInfo? _terrainLabelField;

    /// <summary>MainGame HUD 预测字段。</summary>
    private FieldInfo? _forecastLabelField;

    /// <summary>MainGame HUD 战斗日志字段。</summary>
    private FieldInfo? _battleLogLabelField;

    /// <summary>MainGame HUD 单位列表字段。</summary>
    private FieldInfo? _unitListLabelField;

    /// <summary>MainGame 确认攻击按钮字段。</summary>
    private FieldInfo? _confirmAttackButtonField;

    /// <summary>MainGame 取消目标按钮字段。</summary>
    private FieldInfo? _cancelAttackButtonField;

    /// <summary>MainGame 等待按钮字段。</summary>
    private FieldInfo? _waitButtonField;

    /// <summary>MainGame 结束回合按钮字段。</summary>
    private FieldInfo? _endTurnButtonField;

    /// <summary>
    /// 缓存主场景字段并创建独立顶部状态条。
    /// MainGame 的 _Ready 在子节点之后执行，因此现有 HUD 的具体控件引用会在后续 _Process 中延迟解析。
    /// </summary>
    public override void _Ready()
    {
        _battleHost = GetParent();
        if (_battleHost is null)
        {
            GD.PushWarning("RetroHudCoordinator 找不到战斗主节点，复古 HUD 不会启动。");
            SetProcess(false);
            return;
        }

        Type hostType = _battleHost.GetType();
        BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        _roundField = hostType.GetField("_round", fields);
        _phaseField = hostType.GetField("_phase", fields);
        _terrainField = hostType.GetField("_terrain", fields);
        _gridWidthField = hostType.GetField("_gridWidth", fields);
        _gridHeightField = hostType.GetField("_gridHeight", fields);
        _titleLabelField = hostType.GetField("_titleLabel", fields);
        _statusLabelField = hostType.GetField("_statusLabel", fields);
        _terrainLabelField = hostType.GetField("_terrainLabel", fields);
        _forecastLabelField = hostType.GetField("_forecastLabel", fields);
        _battleLogLabelField = hostType.GetField("_battleLogLabel", fields);
        _unitListLabelField = hostType.GetField("_unitListLabel", fields);
        _confirmAttackButtonField = hostType.GetField("_confirmAttackButton", fields);
        _cancelAttackButtonField = hostType.GetField("_cancelAttackButton", fields);
        _waitButtonField = hostType.GetField("_waitButton", fields);
        _endTurnButtonField = hostType.GetField("_endTurnButton", fields);

        CreateTopBar();
    }

    /// <summary>
    /// 首帧等待 MainGame 创建 HUD 后应用样式，之后持续刷新回合条和悬停地形。
    /// </summary>
    public override void _Process(double delta)
    {
        if (!_hudStyled)
        {
            _hudStyled = TryStyleExistingHud();
        }

        RefreshPhaseBar();
        RefreshHoverTerrain();
    }

    /// <summary>创建地图上方的回合提示与鼠标地形提示。</summary>
    private void CreateTopBar()
    {
        CanvasLayer layer = new()
        {
            Layer = 6
        };
        AddChild(layer);

        PanelContainer phasePanel = new()
        {
            Position = new Vector2(40, 28),
            Size = new Vector2(310, 54),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        phasePanel.AddThemeStyleboxOverride("panel", MakePanelStyle(
            new Color(0.055f, 0.075f, 0.11f, 0.97f),
            new Color(0.67f, 0.49f, 0.24f)));
        layer.AddChild(phasePanel);

        _phaseLabel = new Label
        {
            Text = "我方回合  |  ROUND 1",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _phaseLabel.AddThemeColorOverride("font_color", new Color(0.93f, 0.88f, 0.72f));
        _phaseLabel.AddThemeFontSizeOverride("font_size", 20);
        phasePanel.AddChild(_phaseLabel);

        PanelContainer terrainPanel = new()
        {
            Position = new Vector2(365, 28),
            Size = new Vector2(455, 54),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        terrainPanel.AddThemeStyleboxOverride("panel", MakePanelStyle(
            new Color(0.075f, 0.085f, 0.075f, 0.96f),
            new Color(0.40f, 0.37f, 0.25f)));
        layer.AddChild(terrainPanel);

        _hoverTerrainLabel = new Label
        {
            Text = "移动鼠标到战场格查看地形",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _hoverTerrainLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.84f, 0.73f));
        _hoverTerrainLabel.AddThemeFontSizeOverride("font_size", 15);
        terrainPanel.AddChild(_hoverTerrainLabel);
    }

    /// <summary>
    /// 尝试取得 MainGame 已经创建的 Label/Button，并应用统一暗色面板、青铜边框和像素感按钮。
    /// 返回 false 表示父节点 HUD 还未创建完成，下帧会重试。
    /// </summary>
    private bool TryStyleExistingHud()
    {
        Label? title = ReadControl<Label>(_titleLabelField);
        Label? status = ReadControl<Label>(_statusLabelField);
        Label? terrain = ReadControl<Label>(_terrainLabelField);
        Label? forecast = ReadControl<Label>(_forecastLabelField);
        Label? battleLog = ReadControl<Label>(_battleLogLabelField);
        Label? unitList = ReadControl<Label>(_unitListLabelField);
        Button? confirm = ReadControl<Button>(_confirmAttackButtonField);
        Button? cancel = ReadControl<Button>(_cancelAttackButtonField);
        Button? wait = ReadControl<Button>(_waitButtonField);
        Button? endTurn = ReadControl<Button>(_endTurnButtonField);

        if (title is null || status is null || terrain is null || forecast is null ||
            battleLog is null || unitList is null || confirm is null || cancel is null ||
            wait is null || endTurn is null)
        {
            return false;
        }

        // 从标题控件向上找到原有 PanelContainer，并直接替换默认 Godot 面板样式。
        PanelContainer? panel = title.GetParent()?.GetParent() as PanelContainer;
        if (panel is not null)
        {
            panel.AddThemeStyleboxOverride("panel", MakePanelStyle(
                new Color(0.045f, 0.055f, 0.072f, 0.98f),
                new Color(0.55f, 0.39f, 0.20f)));
        }

        StyleLabel(title, new Color(0.95f, 0.82f, 0.46f), 21);
        StyleLabel(status, new Color(0.84f, 0.87f, 0.82f), 14);
        StyleLabel(terrain, new Color(0.72f, 0.82f, 0.66f), 14);
        StyleLabel(forecast, new Color(0.91f, 0.86f, 0.72f), 14);
        StyleLabel(battleLog, new Color(0.76f, 0.82f, 0.88f), 13);
        StyleLabel(unitList, new Color(0.80f, 0.82f, 0.80f), 13);

        StyleButton(confirm, new Color(0.23f, 0.38f, 0.57f), new Color(0.73f, 0.56f, 0.27f));
        StyleButton(cancel, new Color(0.28f, 0.27f, 0.25f), new Color(0.49f, 0.43f, 0.31f));
        StyleButton(wait, new Color(0.25f, 0.32f, 0.30f), new Color(0.45f, 0.55f, 0.42f));
        StyleButton(endTurn, new Color(0.42f, 0.19f, 0.20f), new Color(0.66f, 0.39f, 0.28f));
        return true;
    }

    /// <summary>刷新顶部“我方/敌方回合 + ROUND”状态。</summary>
    private void RefreshPhaseBar()
    {
        if (_battleHost is null || _phaseLabel is null)
        {
            return;
        }

        string phase = _phaseField?.GetValue(_battleHost)?.ToString() ?? "Player";
        int round = _roundField?.GetValue(_battleHost) is int value ? value : 1;
        string state = $"{phase}:{round}";
        if (state == _lastPhaseState)
        {
            return;
        }

        _lastPhaseState = state;
        _phaseLabel.Text = phase switch
        {
            "Enemy" => $"敌军回合  |  ROUND {round}",
            "Victory" => "胜  利",
            "Defeat" => "战斗结束",
            _ => $"我方回合  |  ROUND {round}"
        };

        Color phaseColor = phase switch
        {
            "Enemy" => new Color(0.92f, 0.55f, 0.50f),
            "Victory" => new Color(0.95f, 0.84f, 0.38f),
            "Defeat" => new Color(0.72f, 0.72f, 0.72f),
            _ => new Color(0.68f, 0.82f, 1.0f)
        };
        _phaseLabel.AddThemeColorOverride("font_color", phaseColor);
    }

    /// <summary>
    /// 根据鼠标所在战场格显示地形移动、防御和回避信息。
    /// 这比只显示“当前选中人物脚下地形”更适合规划移动路线。
    /// </summary>
    private void RefreshHoverTerrain()
    {
        if (_battleHost is null || _hoverTerrainLabel is null)
        {
            return;
        }

        Vector2 local = GetViewport().GetMousePosition() - BoardOrigin;
        int width = _gridWidthField?.GetValue(_battleHost) is int w ? w : 15;
        int height = _gridHeightField?.GetValue(_battleHost) is int h ? h : 10;
        if (local.X < 0 || local.Y < 0)
        {
            SetHoverText("移动鼠标到战场格查看地形");
            return;
        }

        Vector2I cell = new((int)(local.X / CellSize), (int)(local.Y / CellSize));
        if (cell.X < 0 || cell.X >= width || cell.Y < 0 || cell.Y >= height)
        {
            SetHoverText("移动鼠标到战场格查看地形");
            return;
        }

        IReadOnlyDictionary<Vector2I, TerrainType> terrain =
            _terrainField?.GetValue(_battleHost) as IReadOnlyDictionary<Vector2I, TerrainType>
            ?? new Dictionary<Vector2I, TerrainType>();
        TerrainType type = terrain.TryGetValue(cell, out TerrainType explicitType)
            ? explicitType
            : TerrainType.Plain;
        TerrainDefinition definition = TerrainRules.Get(type);
        string passable = definition.Passable ? "可通行" : "不可通行";
        SetHoverText(
            $"{definition.DisplayName}  |  移动 {definition.MoveCost}  |  防御 +{definition.DefenseBonus}  |  " +
            $"回避 +{definition.AvoidBonus}  |  {passable}");
    }

    /// <summary>只在悬停文本变化时更新 Label。</summary>
    private void SetHoverText(string text)
    {
        if (_hoverTerrainLabel is null || text == _lastTerrainState)
        {
            return;
        }

        _lastTerrainState = text;
        _hoverTerrainLabel.Text = text;
    }

    /// <summary>应用复古 HUD 标签颜色和字号。</summary>
    private static void StyleLabel(Label label, Color color, int fontSize)
    {
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_shadow_color", new Color(0.02f, 0.02f, 0.025f, 0.85f));
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
        label.AddThemeFontSizeOverride("font_size", fontSize);
    }

    /// <summary>给按钮应用无圆角、双状态边框的复古样式。</summary>
    private static void StyleButton(Button button, Color baseColor, Color borderColor)
    {
        button.CustomMinimumSize = new Vector2(button.CustomMinimumSize.X, 34);
        button.AddThemeStyleboxOverride("normal", MakeButtonStyle(baseColor, borderColor));
        button.AddThemeStyleboxOverride("hover", MakeButtonStyle(baseColor.Lightened(0.10f), borderColor.Lightened(0.14f)));
        button.AddThemeStyleboxOverride("pressed", MakeButtonStyle(baseColor.Darkened(0.13f), borderColor.Lightened(0.20f)));
        button.AddThemeStyleboxOverride("disabled", MakeButtonStyle(baseColor.Darkened(0.48f), borderColor.Darkened(0.40f)));
        button.AddThemeColorOverride("font_color", new Color(0.95f, 0.92f, 0.82f));
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_pressed_color", new Color(1.0f, 0.90f, 0.56f));
        button.AddThemeColorOverride("font_disabled_color", new Color(0.48f, 0.49f, 0.48f));
        button.AddThemeFontSizeOverride("font_size", 14);
    }

    /// <summary>创建 HUD 面板使用的无圆角青铜边框。</summary>
    private static StyleBoxFlat MakePanelStyle(Color background, Color border)
    {
        StyleBoxFlat style = new()
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2
        };
        return style;
    }

    /// <summary>创建按钮状态使用的无圆角样式。</summary>
    private static StyleBoxFlat MakeButtonStyle(Color background, Color border)
    {
        StyleBoxFlat style = new()
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2
        };
        return style;
    }

    /// <summary>安全读取 MainGame 中缓存的 Control 引用。</summary>
    private T? ReadControl<T>(FieldInfo? field) where T : Control
    {
        return _battleHost is null || field is null
            ? null
            : field.GetValue(_battleHost) as T;
    }
}
