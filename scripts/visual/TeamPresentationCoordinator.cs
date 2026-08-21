using FlameEmblem.Game;
using Godot;
using System.Reflection;

namespace FlameEmblem.Visual;

/// <summary>
/// 统一地图 HUD、回合条和战斗特效的阵营表现。
/// 正式战斗状态框已经直接绘制敌方粉红/我方深蓝，因此本协调器不再给战斗 HUD 叠加额外边框。
/// </summary>
public partial class TeamPresentationCoordinator : Node
{
    /// <summary>战棋主节点。</summary>
    private Node? _battleHost;

    /// <summary>MainGame 当前选中玩家单位字段。</summary>
    private FieldInfo? _selectedUnitField;

    /// <summary>MainGame 当前攻击目标字段。</summary>
    private FieldInfo? _pendingTargetField;

    /// <summary>MainGame 章节标题控件字段。</summary>
    private FieldInfo? _titleLabelField;

    /// <summary>MainGame 状态控件字段。</summary>
    private FieldInfo? _statusLabelField;

    /// <summary>MainGame 当前回合阶段字段。</summary>
    private FieldInfo? _phaseField;

    /// <summary>地图右侧主 HUD 面板。</summary>
    private PanelContainer? _mainHudPanel;

    /// <summary>复古 HUD 协调器，用于读取顶部回合文字。</summary>
    private RetroHudCoordinator? _retroHudCoordinator;

    /// <summary>顶部回合文字私有字段。</summary>
    private FieldInfo? _phaseLabelField;

    /// <summary>横向战斗协调器。</summary>
    private RetroBattleAnimationCoordinator? _battleCoordinator;

    /// <summary>横向战斗程序特效字段。</summary>
    private FieldInfo? _effectControlField;

    /// <summary>是否已经成功挂载新的紧凑战斗特效层。</summary>
    private bool _battleEffectOverlayAttached;

    /// <summary>上一次应用到地图主 HUD 的阵营；避免每帧重复创建 StyleBox。</summary>
    private UnitTeam? _lastMapHudTeam;

    /// <summary>上一次地图主 HUD 是否处于攻击目标强调状态。</summary>
    private bool _lastMapHudTargeting;

    /// <summary>缓存需要的表现字段。</summary>
    public override void _Ready()
    {
        // 放在现有 HUD/人物表现协调器之后执行，确保同一帧最终颜色以阵营统一层为准。
        ProcessPriority = 420;
        _battleHost = GetParent();
        if (_battleHost is null)
        {
            GD.PushWarning("TeamPresentationCoordinator 找不到 MainGame，阵营统一表现不会启动。");
            SetProcess(false);
            return;
        }

        BindingFlags members = BindingFlags.Instance | BindingFlags.NonPublic;
        Type hostType = _battleHost.GetType();
        _selectedUnitField = hostType.GetField("_selectedUnit", members);
        _pendingTargetField = hostType.GetField("_pendingAttackTarget", members);
        _titleLabelField = hostType.GetField("_titleLabel", members);
        _statusLabelField = hostType.GetField("_statusLabel", members);
        _phaseField = hostType.GetField("_phase", members);

        _retroHudCoordinator = GetNodeOrNull<RetroHudCoordinator>("../RetroHudCoordinator");
        _phaseLabelField = typeof(RetroHudCoordinator).GetField("_phaseLabel", members);

        // 战斗状态框已经自带阵营颜色；这里只保留紧凑像素特效的挂载入口。
        _battleCoordinator = GetNodeOrNull<RetroBattleAnimationCoordinator>("../RetroBattleAnimationCoordinator");
        _effectControlField = typeof(RetroBattleAnimationCoordinator).GetField("_effectControl", members);
    }

    /// <summary>持续同步地图 HUD、回合条，并等待动态战斗特效控件创建完成。</summary>
    public override void _Process(double delta)
    {
        RefreshMapHudAccent();
        RefreshPhaseAccent();
        TryAttachBattleEffectOverlay();
    }

    /// <summary>
    /// 右侧主 HUD 在普通选择时跟随我方深蓝；锁定攻击目标时切成目标敌军粉红边框。
    /// 这样玩家不用读文字也能知道当前处于“操作自己”还是“瞄准敌人”状态。
    /// </summary>
    private void RefreshMapHudAccent()
    {
        if (_battleHost is null)
        {
            return;
        }

        Label? title = _titleLabelField?.GetValue(_battleHost) as Label;
        Label? status = _statusLabelField?.GetValue(_battleHost) as Label;
        if (title is null || status is null)
        {
            return;
        }

        _mainHudPanel ??= title.GetParent()?.GetParent() as PanelContainer;
        UnitModel? selected = _selectedUnitField?.GetValue(_battleHost) as UnitModel;
        UnitModel? target = _pendingTargetField?.GetValue(_battleHost) as UnitModel;
        UnitModel? accentUnit = target ?? selected;
        bool targeting = target is not null;

        if (accentUnit is null)
        {
            if (_lastMapHudTeam is not null)
            {
                ApplyNeutralMapHud(title, status);
                _lastMapHudTeam = null;
                _lastMapHudTargeting = false;
            }
            return;
        }

        if (_lastMapHudTeam == accentUnit.Team && _lastMapHudTargeting == targeting)
        {
            return;
        }

        _lastMapHudTeam = accentUnit.Team;
        _lastMapHudTargeting = targeting;
        Color primary = TeamVisualPalette.Primary(accentUnit.Team);
        Color highlight = TeamVisualPalette.Highlight(accentUnit.Team);

        if (_mainHudPanel is not null)
        {
            _mainHudPanel.AddThemeStyleboxOverride("panel", BuildHudPanelStyle(primary, targeting));
        }

        title.AddThemeColorOverride("font_color", highlight.Lightened(targeting ? 0.12f : 0.04f));
        status.AddThemeColorOverride("font_color", targeting
            ? highlight.Lightened(0.18f)
            : new Color(0.84f, 0.87f, 0.82f));
    }

    /// <summary>没有选中人物时恢复中性的青铜 HUD，避免整块界面一直带阵营色。</summary>
    private void ApplyNeutralMapHud(Label title, Label status)
    {
        if (_mainHudPanel is not null)
        {
            _mainHudPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(0.045f, 0.055f, 0.072f, 0.98f),
                BorderColor = new Color(0.55f, 0.39f, 0.20f),
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2
            });
        }

        title.AddThemeColorOverride("font_color", new Color(0.95f, 0.82f, 0.46f));
        status.AddThemeColorOverride("font_color", new Color(0.84f, 0.87f, 0.82f));
    }

    /// <summary>顶部回合文字使用和人物完全相同的深蓝/粉红阵营色。</summary>
    private void RefreshPhaseAccent()
    {
        if (_battleHost is null || _retroHudCoordinator is null)
        {
            return;
        }

        if (_phaseLabelField?.GetValue(_retroHudCoordinator) is not Label phaseLabel)
        {
            return;
        }

        string phase = _phaseField?.GetValue(_battleHost)?.ToString() ?? "Player";
        Color color = phase switch
        {
            "Enemy" => TeamVisualPalette.EnemyHighlight,
            "Victory" => new Color(0.95f, 0.84f, 0.38f),
            "Defeat" => new Color(0.72f, 0.72f, 0.72f),
            _ => TeamVisualPalette.PlayerHighlight
        };
        phaseLabel.AddThemeColorOverride("font_color", color);
    }

    /// <summary>等待原战斗特效层创建完成后挂载新的紧凑像素特效。</summary>
    private void TryAttachBattleEffectOverlay()
    {
        if (_battleEffectOverlayAttached || _battleCoordinator is null)
        {
            return;
        }

        if (_effectControlField?.GetValue(_battleCoordinator) is not RetroBattleEffectControl effectControl)
        {
            return;
        }

        if (effectControl.GetChildren().OfType<CinematicBattleEffectControl>().Any())
        {
            _battleEffectOverlayAttached = true;
            return;
        }

        CinematicBattleEffectControl overlay = new()
        {
            Name = "CinematicBattleEffectOverlay",
            Position = Vector2.Zero,
            Size = effectControl.Size,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest
        };
        effectControl.AddChild(overlay);
        overlay.Bind(effectControl);
        _battleEffectOverlayAttached = true;
    }

    /// <summary>创建地图右侧 HUD 的阵营色硬边样式。</summary>
    private static StyleBoxFlat BuildHudPanelStyle(Color primary, bool targeting)
    {
        Color background = targeting
            ? primary.Darkened(0.72f)
            : primary.Darkened(0.78f);
        Color border = targeting ? primary.Lightened(0.22f) : primary;

        return new StyleBoxFlat
        {
            BgColor = new Color(background.R, background.G, background.B, 0.98f),
            BorderColor = border,
            BorderWidthLeft = targeting ? 4 : 3,
            BorderWidthTop = targeting ? 4 : 3,
            BorderWidthRight = targeting ? 4 : 3,
            BorderWidthBottom = targeting ? 4 : 3
        };
    }
}
