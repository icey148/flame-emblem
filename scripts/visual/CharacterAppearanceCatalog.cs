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
            // 主角保持蓝灰轻甲，但提高中间色亮度，让棕发、披风与金属边在亮战场上仍然分层清楚。
            return new CharacterAppearanceDefinition(
                new Color("684630"),
                new Color("486a98"),
                new Color("d0aa62"),
                new Color("e8b78f"),
                true,
                CharacterWeaponSilhouette.Sword,
                CharacterBodySilhouette.Light);
        }

        if (id == "celine")
        {
            // 弓手使用更柔和的橙棕发与偏灰森林绿，减少原先过暗导致弓和身体粘在一起的问题。
            return new CharacterAppearanceDefinition(
                new Color("b97b48"),
                new Color("557b62"),
                new Color("dec982"),
                new Color("efc09b"),
                false,
                CharacterWeaponSilhouette.Bow,
                CharacterBodySilhouette.Light);
        }

        if (id == "rowan")
        {
            // 重甲提升钢蓝主体亮度，保留暗红识别色，让肩甲、胸甲和枪杆更容易从背景中分离。
            return new CharacterAppearanceDefinition(
                new Color("443832"),
                new Color("788493"),
                new Color("a85a49"),
                new Color("d9a982"),
                false,
                CharacterWeaponSilhouette.Spear,
                CharacterBodySilhouette.Armored);
        }

        if (id == "mira")
        {
            // 法师的紫色改成稍亮的灰紫层级，强调色更柔和，避免袍摆在深色背景中变成一个大色块。
            return new CharacterAppearanceDefinition(
                new Color("76527c"),
                new Color("5c5789"),
                new Color("c084d4"),
                new Color("edbf9a"),
                true,
                CharacterWeaponSilhouette.Tome,
                CharacterBodySilhouette.Robed);
        }

        // 通用敌军继续比玩家整体更暗，保证战斗中双方阵营可以只看轮廓和明度就快速区分。
        return unit.ClassDefinition.Id.ToLowerInvariant() switch
        {
            "guard" => new CharacterAppearanceDefinition(
                new Color("403833"),
                new Color("756555"),
                new Color("bc8258"),
                new Color("d2a07d"),
                false,
                CharacterWeaponSilhouette.Spear,
                CharacterBodySilhouette.Armored),
            "captain" => new CharacterAppearanceDefinition(
                new Color("2e2927"),
                new Color("783838"),
                new Color("c9a258"),
                new Color("d5a37f"),
                true,
                CharacterWeaponSilhouette.Sword,
                CharacterBodySilhouette.Armored),
            _ => new CharacterAppearanceDefinition(
                new Color("5d4437"),
                new Color("70473f"),
                new Color("b66d4c"),
                new Color("d3a17e"),
                false,
                CharacterWeaponSilhouette.Sword,
                CharacterBodySilhouette.Light)
        };
    }
}
