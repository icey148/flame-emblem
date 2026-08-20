using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 描述角色在当前程序绘制原型中的视觉外观。
/// 这里故意只保存“外观参数”，不包含任何战斗逻辑；未来换成正式 PNG、序列帧或 Spine 动画时，战斗系统无需修改。
/// </summary>
public sealed class CharacterAppearanceDefinition
{
    /// <summary>
    /// 创建一份角色外观定义。
    /// </summary>
    public CharacterAppearanceDefinition(
        Color hairColor,
        Color outfitColor,
        Color accentColor,
        Color skinColor,
        bool hasCape,
        CharacterWeaponSilhouette weaponSilhouette)
    {
        HairColor = hairColor;
        OutfitColor = outfitColor;
        AccentColor = accentColor;
        SkinColor = skinColor;
        HasCape = hasCape;
        WeaponSilhouette = weaponSilhouette;
    }

    /// <summary>头发主色。</summary>
    public Color HairColor { get; }

    /// <summary>服装/护甲主色。</summary>
    public Color OutfitColor { get; }

    /// <summary>披风、饰边、法术核心等强调色。</summary>
    public Color AccentColor { get; }

    /// <summary>当前程序绘制占位模型使用的肤色。</summary>
    public Color SkinColor { get; }

    /// <summary>是否绘制披风轮廓。</summary>
    public bool HasCape { get; }

    /// <summary>地图小人上显示的武器轮廓类型。</summary>
    public CharacterWeaponSilhouette WeaponSilhouette { get; }
}

/// <summary>
/// 程序绘制战棋小人时使用的武器轮廓分类。
/// 这不是武器规则本身，只负责让不同职业在地图上一眼能区分。
/// </summary>
public enum CharacterWeaponSilhouette
{
    Sword,
    Spear,
    Bow,
    Tome
}
