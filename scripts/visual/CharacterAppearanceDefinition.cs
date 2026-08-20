using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 描述角色在当前程序像素模板中的视觉外观。
/// 这里只保存配色、身体轮廓和装备轮廓，不包含任何战斗逻辑；未来换成正式原创 PNG/序列帧时规则层无需修改。
/// </summary>
public sealed class CharacterAppearanceDefinition
{
    /// <summary>
    /// 创建一份角色外观定义。
    /// bodySilhouette 放在最后并提供默认值，已有调用方可以平滑迁移。
    /// </summary>
    public CharacterAppearanceDefinition(
        Color hairColor,
        Color outfitColor,
        Color accentColor,
        Color skinColor,
        bool hasCape,
        CharacterWeaponSilhouette weaponSilhouette,
        CharacterBodySilhouette bodySilhouette = CharacterBodySilhouette.Light)
    {
        HairColor = hairColor;
        OutfitColor = outfitColor;
        AccentColor = accentColor;
        SkinColor = skinColor;
        HasCape = hasCape;
        WeaponSilhouette = weaponSilhouette;
        BodySilhouette = bodySilhouette;
    }

    /// <summary>头发主色。</summary>
    public Color HairColor { get; }

    /// <summary>服装/护甲主色。</summary>
    public Color OutfitColor { get; }

    /// <summary>披风、饰边、法术核心等强调色。</summary>
    public Color AccentColor { get; }

    /// <summary>程序像素模型使用的肤色。</summary>
    public Color SkinColor { get; }

    /// <summary>是否绘制披风轮廓。</summary>
    public bool HasCape { get; }

    /// <summary>地图和战斗人物显示的武器轮廓类型。</summary>
    public CharacterWeaponSilhouette WeaponSilhouette { get; }

    /// <summary>决定轻装、重甲或长袍的身体外轮廓。</summary>
    public CharacterBodySilhouette BodySilhouette { get; }
}

/// <summary>
/// 程序绘制战棋人物时使用的武器轮廓分类。
/// 这不是武器规则本身，只负责让不同职业在视觉上快速区分。
/// </summary>
public enum CharacterWeaponSilhouette
{
    Sword,
    Spear,
    Bow,
    Tome
}

/// <summary>
/// 程序像素职业模板的身体轮廓分类。
/// Light 用于剑士/弓手等轻装职业；Armored 用于枪兵/守卫；Robed 用于法师类职业。
/// </summary>
public enum CharacterBodySilhouette
{
    Light,
    Armored,
    Robed
}
