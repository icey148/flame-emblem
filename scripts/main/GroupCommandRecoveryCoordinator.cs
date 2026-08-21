using Godot;
using System.Reflection;

namespace FlameEmblem.Main;

/// <summary>
/// 在“全体进攻”批量模式结束后恢复 MainGame 原生按钮可用状态。
/// 批量模式本身仍由 RightCommandCoordinator 驱动；本节点只处理从批量模式退出时的 UI 收尾。
/// </summary>
public partial class GroupCommandRecoveryCoordinator : Node
{
    /// <summary>战棋主节点。</summary>
    private Node? _battleHost;

    /// <summary>右侧批量指令协调器。</summary>
    private RightCommandCoordinator? _rightCommands;

    /// <summary>读取批量进攻是否正在执行的私有字段。</summary>
    private FieldInfo? _groupAttackActiveField;

    /// <summary>调用 MainGame 原生按钮状态刷新方法。</summary>
    private MethodInfo? _updateActionButtonsMethod;

    /// <summary>上一帧是否处于批量进攻，用于只在 true -> false 的瞬间恢复按钮。</summary>
    private bool _wasGroupAttackActive;

    /// <summary>缓存需要的字段与方法。</summary>
    public override void _Ready()
    {
        ProcessPriority = 520;
        _battleHost = GetParent();
        _rightCommands = GetNodeOrNull<RightCommandCoordinator>("../RightCommandCoordinator");

        BindingFlags members = BindingFlags.Instance | BindingFlags.NonPublic;
        _groupAttackActiveField = typeof(RightCommandCoordinator).GetField("_groupAttackActive", members);
        _updateActionButtonsMethod = _battleHost?.GetType().GetMethod("UpdateActionButtons", members);

        if (_battleHost is null ||
            _rightCommands is null ||
            _groupAttackActiveField is null ||
            _updateActionButtonsMethod is null)
        {
            GD.PushWarning("GroupCommandRecoveryCoordinator 无法读取批量指令状态，按钮恢复保护不会启动。");
            SetProcess(false);
            return;
        }

        _wasGroupAttackActive = ReadGroupAttackActive();
    }

    /// <summary>检测批量模式结束瞬间，并让 MainGame 按真实阶段/选择状态重新决定按钮是否可用。</summary>
    public override void _Process(double delta)
    {
        bool active = ReadGroupAttackActive();
        if (_wasGroupAttackActive && !active)
        {
            _updateActionButtonsMethod?.Invoke(_battleHost, null);
        }

        _wasGroupAttackActive = active;
    }

    /// <summary>读取 RightCommandCoordinator 当前批量进攻状态。</summary>
    private bool ReadGroupAttackActive()
    {
        return _rightCommands is not null &&
               _groupAttackActiveField?.GetValue(_rightCommands) is bool active &&
               active;
    }
}
