using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 对话/HUD 使用的人物头像控件。
/// 已完成角色优先显示用户确认的正式头像；尚未补齐正式头像时由 ApprovedCharacterArtCatalog 返回安全图集头像，
/// 保证对话与战斗 HUD 不再出现黑块或空脸。
/// </summary>
public partial class CharacterPortraitControl : Control
{
    /// <summary>当前需要展示的单位。</summary>
    private UnitModel? _unit;

    /// <summary>低于等于这个尺寸的头像视为角色图集回退，需要最近邻显示。</summary>
    private const int LowResolutionPortraitThreshold = 96;

    /// <summary>提供给阵营边框和对话层读取的只读人物引用。</summary>
    public UnitModel? DisplayedUnit => _unit;

    /// <summary>默认使用平滑过滤；实际绘制时会根据头像分辨率切换。</summary>
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

    /// <summary>绘制深色底、金色内框和当前人物头像。</summary>
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
            return;
        }

        // 正式高清头像使用平滑过滤；64×64 左右的临时图集头像使用最近邻，避免放大后发糊。
        TextureFilter = portrait.GetWidth() <= LowResolutionPortraitThreshold &&
                        portrait.GetHeight() <= LowResolutionPortraitThreshold
            ? CanvasItem.TextureFilterEnum.Nearest
            : CanvasItem.TextureFilterEnum.Linear;

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
