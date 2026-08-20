using FlameEmblem.Game;
using Godot;
using System.Reflection;

namespace FlameEmblem.Visual;

/// <summary>
/// 在横向战斗演出期间暂停地图人物移动动画。
/// MainGame 当前仍会同步计算后续敌军位置，因此这个协调器保证那些位置变化不会在战斗遮罩后面把视觉动画提前播完。
/// </summary>
public partial class BattleMovementPauseCoordinator : Node
{
    /// <summary>人物表现协调器。</summary>
    private CharacterVisualCoordinator? _characterVisualCoordinator;

    /// <summary>CharacterVisualCoordinator 内部地图人物层字段。</summary>
    private FieldInfo? _unitCharacterLayerField;

    /// <summary>缓存到的地图人物绘制/移动节点。</summary>
    private UnitCharacterLayer? _unitCharacterLayer;

    /// <summary>上一帧是否处于横向战斗演出。</summary>
    private bool _wasPlaybackActive;

    /// <summary>
    /// 优先于普通表现节点执行，以尽量在同一帧开始战斗时就冻结地图移动。
    /// </summary>
    public override void _Ready()
    {
        ProcessPriority = -100;
        _characterVisualCoordinator = GetNodeOrNull<CharacterVisualCoordinator>("../CharacterVisualCoordinator");
        if (_characterVisualCoordinator is null)
        {
            GD.PushWarning("BattleMovementPauseCoordinator 找不到 CharacterVisualCoordinator。");
            SetProcess(false);
            return;
        }

        _unitCharacterLayerField = typeof(CharacterVisualCoordinator).GetField(
            "_unitCharacterLayer",
            BindingFlags.Instance | BindingFlags.NonPublic);

        if (_unitCharacterLayerField is null)
        {
            GD.PushWarning("BattleMovementPauseCoordinator 无法读取地图人物层字段。");
            SetProcess(false);
        }
    }

    /// <summary>
    /// 横向演出开始时暂停 UnitCharacterLayer 的 _Process；结束后恢复。
    /// 停止的是纯视觉推进，不会修改任何 UnitModel 逻辑坐标或战斗数值。
    /// </summary>
    public override void _Process(double delta)
    {
        ResolveUnitLayerIfNeeded();
        if (_unitCharacterLayer is null)
        {
            return;
        }

        bool playbackActive = BattleAnimationBus.IsPlaybackActive;
        if (playbackActive == _wasPlaybackActive)
        {
            return;
        }

        _wasPlaybackActive = playbackActive;
        _unitCharacterLayer.SetProcess(!playbackActive);

        if (!playbackActive)
        {
            // 恢复后立即请求重绘；下一次 _Process 会检测战斗期间积累的逻辑格变化并创建移动路径。
            _unitCharacterLayer.QueueRedraw();
        }
    }

    /// <summary>第一次使用时从人物协调器中取出地图人物层。</summary>
    private void ResolveUnitLayerIfNeeded()
    {
        if (_unitCharacterLayer is not null ||
            _characterVisualCoordinator is null ||
            _unitCharacterLayerField is null)
        {
            return;
        }

        _unitCharacterLayer = _unitCharacterLayerField.GetValue(_characterVisualCoordinator) as UnitCharacterLayer;
    }

    /// <summary>离开场景时确保地图人物层恢复处理。</summary>
    public override void _ExitTree()
    {
        _unitCharacterLayer?.SetProcess(true);
    }
}
