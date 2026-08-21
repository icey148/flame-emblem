using Godot;
using System.Reflection;

namespace FlameEmblem.Visual;

/// <summary>
/// 等待 CharacterVisualCoordinator 创建 UnitCharacterLayer 后挂载新的地图人物绘制层。
/// 只替换表现，不改变单位路径、朝向、逻辑坐标或输入。
/// </summary>
public partial class MapFigurePolishCoordinator : Node
{
    /// <summary>现有人物表现协调器。</summary>
    private CharacterVisualCoordinator? _visualCoordinator;

    /// <summary>人物表现协调器中的地图人物层字段。</summary>
    private FieldInfo? _unitLayerField;

    /// <summary>是否已经成功挂载。</summary>
    private bool _applied;

    /// <summary>缓存必要反射字段。</summary>
    public override void _Ready()
    {
        ProcessPriority = 360;
        _visualCoordinator = GetNodeOrNull<CharacterVisualCoordinator>("../CharacterVisualCoordinator");
        _unitLayerField = typeof(CharacterVisualCoordinator).GetField(
            "_unitCharacterLayer",
            BindingFlags.Instance | BindingFlags.NonPublic);

        if (_visualCoordinator is null || _unitLayerField is null)
        {
            GD.PushWarning("MapFigurePolishCoordinator 找不到地图人物层，修长地图人物不会启动。");
            SetProcess(false);
        }
    }

    /// <summary>等动态人物层存在后挂载一次新的绘制层。</summary>
    public override void _Process(double delta)
    {
        if (_applied || _visualCoordinator is null || _unitLayerField is null)
        {
            return;
        }

        if (_unitLayerField.GetValue(_visualCoordinator) is not UnitCharacterLayer source)
        {
            return;
        }

        if (source.GetChildren().OfType<RefinedMapFigureLayer>().Any())
        {
            _applied = true;
            SetProcess(false);
            return;
        }

        RefinedMapFigureLayer refinedLayer = new()
        {
            Name = "RefinedMapFigureLayer"
        };
        source.AddChild(refinedLayer);
        refinedLayer.Bind(source);

        _applied = true;
        SetProcess(false);
    }
}
