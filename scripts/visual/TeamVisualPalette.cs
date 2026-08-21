using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 统一提供敌我阵营视觉色。
/// 战棋缩小时优先保证阵营识别，因此我方固定深蓝、敌方固定粉红；
/// 角色个人色只作为头发、饰边、金属与武器细节存在。
/// </summary>
public static class TeamVisualPalette
{
    /// <summary>我方主要深蓝。</summary>
    public static readonly Color PlayerPrimary = new("24456f");

    /// <summary>我方高亮蓝。</summary>
    public static readonly Color PlayerHighlight = new("4f78a7");

    /// <summary>敌方主要粉红。</summary>
    public static readonly Color EnemyPrimary = new("c65d85");

    /// <summary>敌方高亮粉红。</summary>
    public static readonly Color EnemyHighlight = new("e59ab3");

    /// <summary>返回指定阵营的主要识别色。</summary>
    public static Color Primary(UnitTeam team)
    {
        return team == UnitTeam.Player ? PlayerPrimary : EnemyPrimary;
    }

    /// <summary>返回指定阵营的高亮识别色。</summary>
    public static Color Highlight(UnitTeam team)
    {
        return team == UnitTeam.Player ? PlayerHighlight : EnemyHighlight;
    }
}
