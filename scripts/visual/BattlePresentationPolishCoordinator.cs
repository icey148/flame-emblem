using Godot;
using System.Reflection;

namespace FlameEmblem.Visual;

/// <summary>
/// 给标准稿式战斗界面执行最终构图校正。
/// 所有位置与尺寸统一读取 ReferenceBattleLayout，避免战斗协调器、HUD 和表现层分别写死坐标造成再次漂移。
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

    /// <summary>把战斗画面锁到已经确认的唯一构图规格与像素安全参数。</summary>
    private bool TryApplyPolish()
    {
        if (_battleCoordinator is null ||
            _blockerField?.GetValue(_battleCoordinator) is not Control blocker)
        {
            return false;
        }

        // 正式规格使用纯黑底，不插入蓝灰舞台、地形图层或渐变。
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

        // 标准人物内部继续以 4px 为逻辑像素，外层 0.75 倍后得到严格的 3px 屏幕像素。
        // 旧 AnimatedBattleCharacterControl 仅保留人物与时间线状态，不再参与实际绘制。
        ConfigureBattleCharacter(
            leftCharacter,
            ReferenceBattleLayout.LeftCharacterPosition + new Vector2(0.5f, 0.25f),
            false,
            "LeftReferenceBattleFigure");
        ConfigureBattleCharacter(
            rightCharacter,
            ReferenceBattleLayout.RightCharacterPosition + new Vector2(0.5f, 0.25f),
            true,
            "RightReferenceBattleFigure");

        // 双状态框、人物区域和中央信息框全部读取同一份规格。
        statusHud.Position = ReferenceBattleLayout.StatusHudPosition;
        statusHud.Size = ReferenceBattleLayout.StatusHudSize;
        statusHud.ZIndex = 30;
        statusHud.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;

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
            }

            resultLabel.CustomMinimumSize = ReferenceBattleLayout.ResultLabelMinimumSize;
            resultLabel.AddThemeColorOverride("font_color", new Color("f3f3ef"));
            resultLabel.AddThemeColorOverride("font_shadow_color", Colors.Black);
            resultLabel.AddThemeConstantOverride("shadow_offset_x", 2);
            resultLabel.AddThemeConstantOverride("shadow_offset_y", 2);
            resultLabel.AddThemeFontSizeOverride("font_size", 28);
        }

        // 战斗窗口文字全部使用关闭抗锯齿与次像素定位的共享字体。
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

    /// <summary>
    /// 统一设置单侧人物状态节点，并把真正显示的人物改为舞台同级标准人物层。
    /// 旧人物节点仍继续推进动画状态，但自身完全隐藏，因此不可能再把旧细长人物画到屏幕上。
    /// </summary>
    private static void ConfigureBattleCharacter(
        AnimatedBattleCharacterControl character,
        Vector2 position,
        bool mirrored,
        string overlayName)
    {
        character.Position = position;
        character.Size = ReferenceBattleLayout.CharacterControlSize;
        character.Scale = new Vector2(0.75f, 0.75f);
        character.MirrorHorizontally = mirrored;
        character.Modulate = Colors.White;
        character.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;

        AttachDetachedReferenceFigure(character, overlayName);

        // 隐藏整个旧 CanvasItem，只保留它的 _Process 与 Play/SetUnit 状态更新。
        // 标准人物作为同级节点绘制，因此不会受到旧节点 Visible 的继承影响。
        character.Visible = false;
    }

    /// <summary>
    /// 把标准人物作为旧人物节点的同级节点挂到舞台上。
    /// 这样父节点可见性、SelfModulate 和旧程序绘制都无法再影响标准人物。
    /// </summary>
    private static void AttachDetachedReferenceFigure(AnimatedBattleCharacterControl character, string overlayName)
    {
        Node? parent = character.GetParent();
        if (parent is null)
        {
            return;
        }

        // 清掉上一版曾挂在旧人物内部的标准人物，避免升级后出现重复绘制。
        foreach (ReferenceBattleFigureControl nested in character.GetChildren().OfType<ReferenceBattleFigureControl>())
        {
            nested.Visible = false;
            nested.SetProcess(false);
            nested.QueueFree();
        }

        // 同一舞台已经存在正确的独立人物层时只重新绑定布局，不再重复创建。
        ReferenceBattleFigureControl? existing = parent.GetNodeOrNull<ReferenceBattleFigureControl>(overlayName);
        if (existing is not null)
        {
            existing.Position = character.Position;
            existing.Size = character.Size;
            existing.Scale = character.Scale;
            existing.ZIndex = 10;
            existing.Visible = true;
            existing.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
            existing.Bind(character);
            return;
        }

        ReferenceBattleFigureControl overlay = new()
        {
            Name = overlayName,
            Position = character.Position,
            Size = character.Size,
            Scale = character.Scale,
            ZIndex = 10,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest
        };
        parent.AddChild(overlay);
        overlay.Bind(character);
    }
}
