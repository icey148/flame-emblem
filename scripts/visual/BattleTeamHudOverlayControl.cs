using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 兼容旧场景的战斗 HUD 阵营叠层。
/// 新版 RetroBattleStatusHudControl 已经直接绘制深蓝/粉红状态框，因此本节点不再额外绘制任何边框，
/// 只保留绑定接口，避免旧协调器的反射挂载逻辑因为类型缺失而报错。
/// </summary>
public partial class BattleTeamHudOverlayControl : Control
{
    /// <summary>被装饰的状态 HUD；仅用于同步尺寸。</summary>
    private RetroBattleStatusHudControl? _source;

    /// <summary>绑定新版状态 HUD。</summary>
    public void Bind(RetroBattleStatusHudControl source)
    {
        _source = source;
        Position = Vector2.Zero;
        Size = source.Size;
        MouseFilter = MouseFilterEnum.Ignore;
        SetProcess(true);
    }

    /// <summary>保持和来源 HUD 同尺寸，但不再进行二次绘制。</summary>
    public override void _Process(double delta)
    {
        if (_source is not null && Size != _source.Size)
        {
            Size = _source.Size;
        }
    }
}