using FlameEmblem.Game;
using FlameEmblem.Visual;
using Godot;

namespace FlameEmblem.Main;

/// <summary>
/// MainGame 的人物表现扩展。
/// 这一部分只负责地图人物层和人物详情 HUD，不参与移动、回合或战斗结算。
/// </summary>
public partial class MainGame
{
    /// <summary>绘制在地图单位之上的程序化人物小人层。</summary>
    private UnitCharacterLayer? _unitCharacterLayer;

    /// <summary>底部人物详情面板中的程序化头像。</summary>
    private CharacterPortraitControl? _characterPortrait;

    /// <summary>人物详情面板的身份/装备文本。</summary>
    private Label? _characterIdentityLabel;

    /// <summary>人物详情面板的核心属性文本。</summary>
    private Label? _characterStatsLabel;

    /// <summary>上一次用于刷新人物详情面板的状态签名。</summary>
    private string _characterPanelState = string.Empty;

    /// <summary>
    /// 每帧同步人物表现层。
    /// 战斗状态仍然由主文件维护，这里只读取状态并刷新显示，因此不会改变原有战斗流程。
    /// </summary>
    public override void _Process(double delta)
    {
        EnsureCharacterVisualLayer();

        if (_unitCharacterLayer is not null)
        {
            _unitCharacterLayer.Units = _units;
            _unitCharacterLayer.SelectedUnit = _selectedUnit;
            _unitCharacterLayer.QueueRedraw();
        }

        RefreshCharacterPanelIfNeeded();
    }

    /// <summary>
    /// 延迟创建人物地图层和底部详情面板。
    /// 使用延迟初始化可以避免改动主场景的 _Ready 方法，让人物表现模块保持可拔插。
    /// </summary>
    private void EnsureCharacterVisualLayer()
    {
        if (_unitCharacterLayer is not null)
        {
            return;
        }

        _unitCharacterLayer = new UnitCharacterLayer
        {
            BoardOrigin = BoardOrigin,
            CellSize = CellSize,
            ZIndex = 20
        };
        AddChild(_unitCharacterLayer);

        CanvasLayer detailsLayer = new()
        {
            Layer = 12
        };
        AddChild(detailsLayer);

        PanelContainer panel = new()
        {
            Position = new Vector2(40, 625),
            Size = new Vector2(780, 72)
        };
        detailsLayer.AddChild(panel);

        HBoxContainer row = new();
        panel.AddChild(row);

        _characterPortrait = new CharacterPortraitControl
        {
            CustomMinimumSize = new Vector2(82, 68)
        };
        row.AddChild(_characterPortrait);

        VBoxContainer textColumn = new
        {
            CustomMinimumSize = new Vector2(680, 68)
        };
        row.AddChild(textColumn);

        _characterIdentityLabel = new Label
        {
            Text = "人物详情：选择一个单位查看。"
        };
        textColumn.AddChild(_characterIdentityLabel);

        _characterStatsLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        textColumn.AddChild(_characterStatsLabel);
    }

    /// <summary>
    /// 只有选中单位或可见数值变化时才刷新人物详情文本，避免每帧重复设置 Label 内容。
    /// </summary>
    private void RefreshCharacterPanelIfNeeded()
    {
        string state = BuildCharacterPanelState();
        if (state == _characterPanelState)
        {
            return;
        }

        _characterPanelState = state;

        if (_characterPortrait is null || _characterIdentityLabel is null || _characterStatsLabel is null)
        {
            return;
        }

        _characterPortrait.SetUnit(_selectedUnit);
        if (_selectedUnit is null)
        {
            _characterIdentityLabel.Text = "人物详情：选择一个单位查看。";
            _characterStatsLabel.Text = "地图人物目前为原创程序绘制占位模型，后续可直接替换正式 2D 小人和立绘素材。";
            return;
        }

        UnitModel unit = _selectedUnit;
        WeaponDefinition weapon = unit.EquippedWeapon;
        string weaponType = weapon.DamageType == WeaponDamageType.Magical ? "魔法" : "物理";

        _characterIdentityLabel.Text =
            $"{unit.DisplayName}  |  Lv.{unit.Level} {unit.ClassDefinition.DisplayName}  |  {weapon.DisplayName}（{weaponType}）";
        _characterStatsLabel.Text =
            $"HP {unit.CurrentHp}/{unit.MaxHp}  力 {unit.Strength}  魔 {unit.Magic}  技 {unit.Skill}  速 {unit.Speed}  " +
            $"运 {unit.Luck}  防 {unit.Defense}  魔防 {unit.Resistance}  EXP {unit.Experience}/100";
    }

    /// <summary>
    /// 构造人物面板当前状态的轻量签名，用于判断是否需要刷新 UI。
    /// </summary>
    private string BuildCharacterPanelState()
    {
        if (_selectedUnit is null)
        {
            return "none";
        }

        UnitModel unit = _selectedUnit;
        return string.Join(
            ':',
            unit.Id,
            unit.Level,
            unit.Experience,
            unit.CurrentHp,
            unit.MaxHp,
            unit.Strength,
            unit.Magic,
            unit.Skill,
            unit.Speed,
            unit.Luck,
            unit.Defense,
            unit.Resistance,
            unit.EquippedWeapon.Id,
            unit.HasActed);
    }
}
