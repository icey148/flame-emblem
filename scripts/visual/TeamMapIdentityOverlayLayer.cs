using FlameEmblem.Game;
using Godot;
using System.Reflection;

namespace FlameEmblem.Visual;

/// <summary>
/// 在地图人物脚下覆盖统一敌我识别条。
/// 原人物层仍负责路径和视觉中心，本层只读取位置并以深蓝/粉红覆盖旧的亮蓝/亮红底座。
/// </summary>
public partial class TeamMapIdentityOverlayLayer : Node2D
{
    /// <summary>现有地图人物层。</summary>
    private UnitCharacterLayer? _source;

    /// <summary>读取移动插值后视觉中心的方法。</summary>
    private MethodInfo? _resolveVisualCenterMethod;

    /// <summary>绑定原地图人物层并缓存视觉中心方法。</summary>
    public void Bind(UnitCharacterLayer source)
    {
        _source = source;
        _resolveVisualCenterMethod = typeof(UnitCharacterLayer).GetMethod(
            "ResolveVisualCenter",
            BindingFlags.Instance | BindingFlags.NonPublic);
        ZIndex = 3;
        SetProcess(true);
        QueueRedraw();
    }

    /// <summary>移动中每帧重新绘制，确保识别条跟随逐格行走动画。</summary>
    public override void _Process(double delta)
    {
        QueueRedraw();
    }

    /// <summary>给所有存活单位绘制硬边阵营条。</summary>
    public override void _Draw()
    {
        if (_source is null || _resolveVisualCenterMethod is null)
        {
            return;
        }

        foreach (UnitModel unit in _source.Units.Where(unit => unit.IsAlive))
        {
            Vector2 center = ReadVisualCenter(unit);
            Color team = TeamVisualPalette.Primary(unit.Team);
            Color highlight = TeamVisualPalette.Highlight(unit.Team);
            if (unit.HasActed)
            {
                team = team.Darkened(0.30f);
                highlight = highlight.Darkened(0.30f);
            }

            // 先用完整暗底覆盖旧底座，再叠一条阵营主色和中央高光，保证复杂地形上也清楚。
            DrawRect(
                new Rect2(center + new Vector2(-17, 19), new Vector2(34, 6)),
                new Color("171c24"),
                true);
            DrawRect(
                new Rect2(center + new Vector2(-14, 20), new Vector2(28, 4)),
                team,
                true);
            DrawRect(
                new Rect2(center + new Vector2(-9, 20), new Vector2(18, 1)),
                highlight,
                true);
        }
    }

    /// <summary>通过原人物层读取已经包含移动插值的视觉中心，并锁到整数像素。</summary>
    private Vector2 ReadVisualCenter(UnitModel unit)
    {
        if (_source is null || _resolveVisualCenterMethod is null)
        {
            return Vector2.Zero;
        }

        object? value = _resolveVisualCenterMethod.Invoke(_source, new object[] { unit });
        if (value is not Vector2 center)
        {
            return Vector2.Zero;
        }

        return new Vector2(Mathf.Round(center.X), Mathf.Round(center.Y));
    }
}
