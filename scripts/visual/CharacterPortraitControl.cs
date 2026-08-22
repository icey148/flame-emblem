using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 对话/HUD 使用的正式人物头像控件。
/// 头像只读取用户已经确认的正式设计稿；尚未完成正式头像的角色显示干净的空框，
/// 不再回退到旧程序胸像或旧图集头像。
/// </summary>
public partial class CharacterPortraitControl : Control
{
    /// <summary>当前需要展示的单位。</summary>
    private UnitModel? _unit;

    /// <summary>提供给阵营边框和对话层读取的只读人物引用。</summary>
    public UnitModel? DisplayedUnit => _unit;

    /// <summary>启用平滑过滤；正式设计稿本身分辨率足够，不再用最近邻放大旧低分辨率占位图。</summary>
    public override void _Ready()
    {
        TextureFilter = CanvasItem.TextureFilterEnum.Linear;
    }

    /// <summary>切换当前人物并请求重绘。</summary>
    public void SetUnit(UnitModel? unit)
    {
        _unit = unit;
        QueueRedraw();
    }

    /// <summary>绘制深色底、金色内框和正式人物头像。</summary>
    public override void _Draw()
    {
        Rect2 bounds = new(Vector2.Zero, Size);
        DrawRect(bounds, new Color("07090d"), true);
        DrawRect(bounds.Grow(-2), new Color("c9a76a"), false, 2.0f);
        DrawRect(bounds.Grow(-5), new Color("332719"), false, 1.0f);

        if (_unit is null)
        {
            return;
        }

        Texture2D? portrait = ApprovedCharacterArtCatalog.TryLoadPortrait(_unit);
        if (portrait is null)
        {
            // 正式素材缺失时保持空框，避免再次出现和设计稿不一致的程序头像。
            return;
        }

        DrawPortraitFitted(portrait, bounds.Grow(-7));
    }

    /// <summary>等比裁切填满头像框，保持人物脸部和肩部比例不被横向拉伸。</summary>
    private void DrawPortraitFitted(Texture2D texture, Rect2 bounds)
    {
        float sourceWidth = Math.Max(1, texture.GetWidth());
        float sourceHeight = Math.Max(1, texture.GetHeight());
        float scale = MathF.Max(bounds.Size.X / sourceWidth, bounds.Size.Y / sourceHeight);
        Vector2 targetSize = new(sourceWidth * scale, sourceHeight * scale);
        Vector2 targetPosition = bounds.Position + (bounds.Size - targetSize) * 0.5f;

        DrawTextureRect(
            texture,
            new Rect2(
                new Vector2(Mathf.Round(targetPosition.X), Mathf.Round(targetPosition.Y)),
                new Vector2(Mathf.Round(targetSize.X), Mathf.Round(targetSize.Y))),
            false);
    }
}
