using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 为当前角色选择原创程序像素外观。
/// 个人配色、身体轮廓和武器轮廓与战斗数据分离，后续接入正式原创素材时可以直接替换这一层。
/// </summary>
public static class CharacterAppearanceCatalog
{
    /// <summary>
    /// 根据角色实例 ID 和职业返回当前使用的外观。
    /// 玩家角色优先使用个人外观，通用敌军则按职业复用外观。
    /// </summary>
    public static CharacterAppearanceDefinition Get(UnitModel unit)
    {
        string id = unit.Id.ToLowerInvariant();
        if (id == "adrian")
        {
            return new CharacterAppearanceDefinition(
                new Color("5b3a29"),
                new Color("315b8a"),
                new Color("d6b45a"),
                new Color("e8b78f"),
                true,
                CharacterWeaponSilhouette.Sword,
                CharacterBodySilhouette.Light);
        }

        if (id == "celine")
        {
            return new CharacterAppearanceDefinition(
                new Color("b77a45"),
                new Color("47765c"),
                new Color("e4d08b"),
                new Color("efc09b"),
                false,
                CharacterWeaponSilhouette.Bow,
                CharacterBodySilhouette.Light);
        }

        if (id == "rowan")
        {
            return new CharacterAppearanceDefinition(
                new Color("3c322c"),
                new Color("6d7481"),
                new Color("a94f3f"),
                new Color("d9a982"),
                false,
                CharacterWeaponSilhouette.Spear,
                CharacterBodySilhouette.Armored);
        }

        if (id == "mira")
        {
            return new CharacterAppearanceDefinition(
                new Color("6b416f"),
                new Color("4c477b"),
                new Color("b96fd4"),
                new Color("edbf9a"),
                true,
                CharacterWeaponSilhouette.Tome,
                CharacterBodySilhouette.Robed);
        }

        // 通用敌军按职业给出不同身体/武器轮廓，避免所有敌军只靠颜色区分。
        return unit.ClassDefinition.Id.ToLowerInvariant() switch
        {
            "guard" => new CharacterAppearanceDefinition(
                new Color("403833"),
                new Color("7c6454"),
                new Color("c1895b"),
                new Color("d2a07d"),
                false,
                CharacterWeaponSilhouette.Spear,
                CharacterBodySilhouette.Armored),
            "captain" => new CharacterAppearanceDefinition(
                new Color("2e2927"),
                new Color("7c3131"),
                new Color("d2aa55"),
                new Color("d5a37f"),
                true,
                CharacterWeaponSilhouette.Sword,
                CharacterBodySilhouette.Armored),
            _ => new CharacterAppearanceDefinition(
                new Color("5d4437"),
                new Color("70423b"),
                new Color("b66d4c"),
                new Color("d3a17e"),
                false,
                CharacterWeaponSilhouette.Sword,
                CharacterBodySilhouette.Light)
        };
    }
}
