using FlameEmblem.Game;
using Godot;
using System.Reflection;

namespace FlameEmblem.Visual;

/// <summary>
/// 把人物地图小人和人物详情面板挂到现有 MainGame 场景上。
/// 当前主战斗场景尚未暴露只读 BattleView 接口，因此这个过渡层只在启动时通过反射缓存必要字段；后续重构主场景 API 后会移除反射。
/// </summary>
public partial class CharacterVisualCoordinator : Node
{
    /// <summary>当前战斗主节点。</summary>
    private Node? _battleHost;

    /// <summary>MainGame 中单位集合字段的缓存反射信息。</summary>
    private FieldInfo? _unitsField;

    /// <summary>MainGame 中当前选中单位字段的缓存反射信息。</summary>
    private FieldInfo? _selectedUnitField;

    /// <summary>地图上的程序化人物小人层。</summary>
    private UnitCharacterLayer? _unitCharacterLayer;

    /// <summary>人物详情头像控件。</summary>
    private CharacterPortraitControl? _portrait;

    /// <summary>人物详情身份与装备文本。</summary>
    private Label? _identityLabel;

    /// <summary>人物详情八维属性文本。</summary>
    private Label? _statsLabel;

    /// <summary>上一次面板状态签名，用于避免无意义地每帧刷新文字。</summary>
    private string _lastPanelState = string.Empty;

    /// <summary>
    /// 节点进入场景树时缓存战斗主节点字段，并创建人物表现层。
    /// </summary>
    public override void _Ready()
    {
        _battleHost = GetParent();
        if (_battleHost is null)
        {
            GD.PushWarning("CharacterVisualCoordinator 找不到战斗主节点，人物表现层不会启动。");
            SetProcess(false);
            return;
        }

        Type hostType = _battleHost.GetType();
        _unitsField = hostType.GetField("_units", BindingFlags.Instance | BindingFlags.NonPublic);
        _selectedUnitField = hostType.GetField("_selectedUnit", BindingFlags.Instance | BindingFlags.NonPublic);

        if (_unitsField is null || _selectedUnitField is null)
        {
            GD.PushWarning("CharacterVisualCoordinator 无法读取 MainGame 的人物状态字段。请在重构 MainGame 时同步更新人物表现接口。");
            SetProcess(false);
            return;
        }

        CreateMapCharacterLayer();
        CreateCharacterDetailsPanel();
    }

    /// <summary>
    /// 每帧读取当前战斗人物状态并同步纯表现层。
    /// 反射 FieldInfo 已在 _Ready 缓存，因此不会每帧重新查找字段定义。
    /// </summary>
    public override void _Process(double delta)
    {
        IReadOnlyList<UnitModel> units = ReadUnits();
        UnitModel? selectedUnit = ReadSelectedUnit();

        if (_unitCharacterLayer is not null)
        {
            _unitCharacterLayer.Units = units;
            _unitCharacterLayer.SelectedUnit = selectedUnit;
            _unitCharacterLayer.QueueRedraw();
        }

        RefreshDetailsPanel(selectedUnit);
    }

    /// <summary>
    /// 创建覆盖在原型单位圆点之上的程序化 2D 人物层。
    /// </summary>
    private void CreateMapCharacterLayer()
    {
        _unitCharacterLayer = new UnitCharacterLayer
        {
            BoardOrigin = new Vector2(40, 100),
            CellSize = 52,
            ZIndex = 20
        };
        AddChild(_unitCharacterLayer);
    }

    /// <summary>
    /// 创建地图底部的人物头像与属性详情面板。
    /// </summary>
    private void CreateCharacterDetailsPanel()
    {
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

        _portrait = new CharacterPortraitControl
        {
            CustomMinimumSize = new Vector2(82, 68)
        };
        row.AddChild(_portrait);

        VBoxContainer textColumn = new()
        {
            CustomMinimumSize = new Vector2(680, 68)
        };
        row.AddChild(textColumn);

        _identityLabel = new Label
        {
            Text = "人物详情：选择一个单位查看。"
        };
        textColumn.AddChild(_identityLabel);

        _statsLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Text = "当前人物小人和头像为程序绘制占位模型，后续可直接替换正式 2D 美术。"
        };
        textColumn.AddChild(_statsLabel);
    }

    /// <summary>
    /// 读取主战斗节点的单位集合；读取失败时返回空集合，避免表现层影响游戏运行。
    /// </summary>
    private IReadOnlyList<UnitModel> ReadUnits()
    {
        if (_battleHost is null || _unitsField is null)
        {
            return Array.Empty<UnitModel>();
        }

        return _unitsField.GetValue(_battleHost) as IReadOnlyList<UnitModel>
               ?? Array.Empty<UnitModel>();
    }

    /// <summary>
    /// 读取当前选中的人物。
    /// </summary>
    private UnitModel? ReadSelectedUnit()
    {
        return _battleHost is null || _selectedUnitField is null
            ? null
            : _selectedUnitField.GetValue(_battleHost) as UnitModel;
    }

    /// <summary>
    /// 根据选中人物刷新头像、职业、装备和八维属性。
    /// </summary>
    private void RefreshDetailsPanel(UnitModel? unit)
    {
        string state = BuildPanelState(unit);
        if (state == _lastPanelState)
        {
            return;
        }

        _lastPanelState = state;
        _portrait?.SetUnit(unit);

        if (_identityLabel is null || _statsLabel is null)
        {
            return;
        }

        if (unit is null)
        {
            _identityLabel.Text = "人物详情：选择一个单位查看。";
            _statsLabel.Text = "当前人物小人和头像为程序绘制占位模型，后续可直接替换正式 2D 美术。";
            return;
        }

        WeaponDefinition equipped = unit.EquippedWeapon;
        string damageType = equipped.DamageType == WeaponDamageType.Magical ? "魔法" : "物理";
        _identityLabel.Text =
            $"{unit.DisplayName}  |  Lv.{unit.Level} {unit.ClassDefinition.DisplayName}  |  {equipped.DisplayName}（{damageType}）";
        _statsLabel.Text =
            $"HP {unit.CurrentHp}/{unit.MaxHp}  力 {unit.Strength}  魔 {unit.Magic}  技 {unit.Skill}  速 {unit.Speed}  " +
            $"运 {unit.Luck}  防 {unit.Defense}  魔防 {unit.Resistance}  EXP {unit.Experience}/100";
    }

    /// <summary>
    /// 生成人物详情面板的状态签名，只在可见数据变化时更新 Label。
    /// </summary>
    private static string BuildPanelState(UnitModel? unit)
    {
        if (unit is null)
        {
            return "none";
        }

        return $"{unit.Id}:{unit.Level}:{unit.Experience}:{unit.CurrentHp}:{unit.MaxHp}:" +
               $"{unit.Strength}:{unit.Magic}:{unit.Skill}:{unit.Speed}:{unit.Luck}:" +
               $"{unit.Defense}:{unit.Resistance}:{unit.EquippedWeapon.Id}:{unit.HasActed}";
    }
}
