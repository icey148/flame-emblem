using FlameEmblem.Game;
using Godot;
using System.Reflection;

namespace FlameEmblem.Visual;

/// <summary>
/// 把人物地图小人、人物详情、装备切换、完整详情页和战斗演出预览挂到现有 MainGame 场景上。
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

    /// <summary>MainGame 中当前锁定攻击目标字段的缓存反射信息。</summary>
    private FieldInfo? _pendingAttackTargetField;

    /// <summary>地图上的人物小人层。</summary>
    private UnitCharacterLayer? _unitCharacterLayer;

    /// <summary>人物详情头像控件。</summary>
    private CharacterPortraitControl? _portrait;

    /// <summary>人物详情身份与装备文本。</summary>
    private Label? _identityLabel;

    /// <summary>人物详情八维属性文本。</summary>
    private Label? _statsLabel;

    /// <summary>装备切换提示文本。</summary>
    private Label? _equipmentLabel;

    /// <summary>循环切换当前人物备用装备的按钮。</summary>
    private Button? _cycleEquipmentButton;

    /// <summary>打开完整人物详情页的按钮。</summary>
    private Button? _openDetailsButton;

    /// <summary>完整人物详情页。</summary>
    private CharacterDetailOverlay? _characterDetailOverlay;

    /// <summary>锁定攻击目标时显示的独立战斗演出预览层。</summary>
    private BattleDuelPreviewControl? _duelPreview;

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
        _pendingAttackTargetField = hostType.GetField("_pendingAttackTarget", BindingFlags.Instance | BindingFlags.NonPublic);

        if (_unitsField is null || _selectedUnitField is null || _pendingAttackTargetField is null)
        {
            GD.PushWarning("CharacterVisualCoordinator 无法读取 MainGame 的人物状态字段。请在重构 MainGame 时同步更新人物表现接口。");
            SetProcess(false);
            return;
        }

        CreateMapCharacterLayer();
        CreateBattleDuelPreview();
        CreateCharacterDetailsPanel();
        CreateCharacterDetailOverlay();
    }

    /// <summary>
    /// 每帧读取当前战斗人物状态并同步纯表现层。
    /// 反射 FieldInfo 已在 _Ready 缓存，因此不会每帧重新查找字段定义。
    /// </summary>
    public override void _Process(double delta)
    {
        IReadOnlyList<UnitModel> units = ReadUnits();
        UnitModel? selectedUnit = ReadSelectedUnit();
        UnitModel? pendingTarget = ReadPendingAttackTarget();

        if (_unitCharacterLayer is not null)
        {
            _unitCharacterLayer.Units = units;
            _unitCharacterLayer.SelectedUnit = selectedUnit;
        }

        _duelPreview?.SetCombatants(selectedUnit, pendingTarget);
        RefreshDetailsPanel(selectedUnit, pendingTarget);
    }

    /// <summary>
    /// 创建覆盖在原型单位圆点之上的 2D 人物层。
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
    /// 创建锁定攻击目标时出现的左右战斗演出层。
    /// </summary>
    private void CreateBattleDuelPreview()
    {
        CanvasLayer battleLayer = new()
        {
            Layer = 10
        };
        AddChild(battleLayer);

        _duelPreview = new BattleDuelPreviewControl
        {
            Position = new Vector2(105, 335),
            Size = new Vector2(650, 245)
        };
        battleLayer.AddChild(_duelPreview);
    }

    /// <summary>
    /// 创建地图底部的人物头像、属性和装备操作面板。
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
            Position = new Vector2(40, 585),
            Size = new Vector2(780, 110)
        };
        detailsLayer.AddChild(panel);

        HBoxContainer row = new();
        panel.AddChild(row);

        _portrait = new CharacterPortraitControl
        {
            CustomMinimumSize = new Vector2(105, 100)
        };
        row.AddChild(_portrait);

        VBoxContainer textColumn = new()
        {
            CustomMinimumSize = new Vector2(500, 100)
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
            Text = "当前人物小人和头像支持正式 PNG 自动替换；没有素材时使用程序绘制占位模型。"
        };
        textColumn.AddChild(_statsLabel);

        _equipmentLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        textColumn.AddChild(_equipmentLabel);

        VBoxContainer actionColumn = new()
        {
            CustomMinimumSize = new Vector2(145, 100),
            Alignment = BoxContainer.AlignmentMode.Center
        };
        row.AddChild(actionColumn);

        _cycleEquipmentButton = new Button
        {
            Text = "切换装备"
        };
        _cycleEquipmentButton.Pressed += OnCycleEquipmentPressed;
        actionColumn.AddChild(_cycleEquipmentButton);

        _openDetailsButton = new Button
        {
            Text = "人物详情"
        };
        _openDetailsButton.Pressed += OnOpenDetailsPressed;
        actionColumn.AddChild(_openDetailsButton);
    }

    /// <summary>
    /// 创建覆盖在战场之上的完整人物详情页。
    /// </summary>
    private void CreateCharacterDetailOverlay()
    {
        CanvasLayer overlayLayer = new()
        {
            Layer = 30
        };
        AddChild(overlayLayer);

        _characterDetailOverlay = new CharacterDetailOverlay
        {
            Position = new Vector2(210, 85),
            Size = new Vector2(860, 540)
        };
        overlayLayer.AddChild(_characterDetailOverlay);
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
    /// 读取当前战斗预测锁定的敌军目标。
    /// </summary>
    private UnitModel? ReadPendingAttackTarget()
    {
        return _battleHost is null || _pendingAttackTargetField is null
            ? null
            : _pendingAttackTargetField.GetValue(_battleHost) as UnitModel;
    }

    /// <summary>
    /// 循环切换当前玩家单位的备用装备。
    /// 已经锁定攻击目标时禁止切换，保证主 HUD 的预测数值不会与实际装备产生短暂不一致。
    /// </summary>
    private void OnCycleEquipmentPressed()
    {
        UnitModel? unit = ReadSelectedUnit();
        UnitModel? pendingTarget = ReadPendingAttackTarget();
        if (unit is null || unit.Team != UnitTeam.Player || pendingTarget is not null)
        {
            return;
        }

        UnitLoadoutCatalog.CycleNext(unit);
        _lastPanelState = string.Empty;

        // 装备射程变化会影响地图攻击范围，因此要求主 Canvas 重新绘制。
        if (_battleHost is CanvasItem canvasItem)
        {
            canvasItem.QueueRedraw();
        }
    }

    /// <summary>
    /// 打开当前选中人物的完整详情页。
    /// </summary>
    private void OnOpenDetailsPressed()
    {
        UnitModel? unit = ReadSelectedUnit();
        if (unit is null)
        {
            return;
        }

        _characterDetailOverlay?.ShowUnit(unit);
    }

    /// <summary>
    /// 根据选中人物刷新头像、职业、装备、八维属性和操作按钮。
    /// </summary>
    private void RefreshDetailsPanel(UnitModel? unit, UnitModel? pendingTarget)
    {
        string state = BuildPanelState(unit, pendingTarget);
        if (state == _lastPanelState)
        {
            return;
        }

        _lastPanelState = state;
        _portrait?.SetUnit(unit);

        if (_identityLabel is null || _statsLabel is null || _equipmentLabel is null ||
            _cycleEquipmentButton is null || _openDetailsButton is null)
        {
            return;
        }

        if (unit is null)
        {
            _identityLabel.Text = "人物详情：选择一个单位查看。";
            _statsLabel.Text = "当前人物小人和头像支持正式 PNG 自动替换；没有素材时使用程序绘制占位模型。";
            _equipmentLabel.Text = "装备：选择我方人物后可以查看和切换备用装备。";
            _cycleEquipmentButton.Disabled = true;
            _openDetailsButton.Disabled = true;
            return;
        }

        WeaponDefinition equipped = unit.EquippedWeapon;
        string damageType = equipped.DamageType == DamageType.Magical ? "魔法" : "物理";
        _identityLabel.Text =
            $"{unit.DisplayName}  |  Lv.{unit.Level} {unit.ClassDefinition.DisplayName}  |  {equipped.DisplayName}（{damageType}）";
        _statsLabel.Text =
            $"HP {unit.CurrentHp}/{unit.MaxHp}  力 {unit.Strength}  魔 {unit.Magic}  技 {unit.Skill}  速 {unit.Speed}  " +
            $"运 {unit.Luck}  防 {unit.Defense}  魔防 {unit.Resistance}  EXP {unit.Experience}/100";

        IReadOnlyList<WeaponDefinition> available = UnitLoadoutCatalog.GetAvailableWeapons(unit);
        string equipmentNames = string.Join(" / ", available.Select(weapon => weapon.DisplayName));
        _equipmentLabel.Text =
            $"装备：{equipmentNames}  |  当前射程 {equipped.MinRange}-{equipped.MaxRange}  " +
            $"威力 {equipped.Might} 命中 {equipped.Hit} 必杀 {equipped.Critical}" +
            (equipped.HpCost > 0 ? $" HP消耗 {equipped.HpCost}" : string.Empty);

        bool canCycle = unit.Team == UnitTeam.Player && available.Count > 1 && pendingTarget is null && !unit.HasActed;
        _cycleEquipmentButton.Disabled = !canCycle;
        _cycleEquipmentButton.Text = pendingTarget is not null ? "已锁定目标" : "切换装备";
        _openDetailsButton.Disabled = false;
    }

    /// <summary>
    /// 生成人物详情面板的状态签名，只在可见数据变化时更新 Label。
    /// </summary>
    private static string BuildPanelState(UnitModel? unit, UnitModel? pendingTarget)
    {
        if (unit is null)
        {
            return "none";
        }

        return $"{unit.Id}:{unit.Level}:{unit.Experience}:{unit.CurrentHp}:{unit.MaxHp}:" +
               $"{unit.Strength}:{unit.Magic}:{unit.Skill}:{unit.Speed}:{unit.Luck}:" +
               $"{unit.Defense}:{unit.Resistance}:{unit.EquippedWeapon.Id}:{unit.HasActed}:" +
               $"{pendingTarget?.Id ?? "no-target"}";
    }
}
