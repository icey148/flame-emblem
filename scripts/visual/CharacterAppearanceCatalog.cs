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
    /// 根据角色实例 ID 和当前职业返回程序回退外观。
    /// 玩家角色优先使用个人外观，并在晋升后切换更高阶轮廓；通用敌军则按职业复用外观。
    /// </summary>
    public static CharacterAppearanceDefinition Get(UnitModel unit)
    {
        string id = unit.Id.ToLowerInvariant();
        string classId = unit.ClassDefinition.Id.ToLowerInvariant();

        if (id == "adrian")
        {
            if (classId == "sword_lord")
            {
                // 晋升后增加明亮钢蓝甲片和更强金色饰边，身体轮廓从轻装升级为披风重甲剑士。
                return new CharacterAppearanceDefinition(
                    new Color("684630"),
                    new Color("647fa8"),
                    new Color("e0bd69"),
                    new Color("e8b78f"),
                    true,
                    CharacterWeaponSilhouette.Sword,
                    CharacterBodySilhouette.Armored);
            }

            // 主角基础职业保持蓝灰轻甲，让棕发、披风与金属边在亮战场上分层清楚。
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
            if (classId == "bow_knight")
            {
                // 晋升弓骑士使用更深的森林绿和金属肩甲色，保留弓手纤细轮廓但提高高级职业辨识度。
                return new CharacterAppearanceDefinition(
                    new Color("b97b48"),
                    new Color("66826d"),
                    new Color("e4c878"),
                    new Color("efc09b"),
                    true,
                    CharacterWeaponSilhouette.Bow,
                    CharacterBodySilhouette.Light);
            }

            // 弓手使用柔和橙棕发与偏灰森林绿，避免弓和身体粘在一起。
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
            if (classId == "royal_lancer")
            {
                // 晋升枪卫强化亮钢蓝胸甲和金色披风扣，使重甲层次明显高于基础士兵。
                return new CharacterAppearanceDefinition(
                    new Color("443832"),
                    new Color("8a98a8"),
                    new Color("c69a58"),
                    new Color("d9a982"),
                    true,
                    CharacterWeaponSilhouette.Spear,
                    CharacterBodySilhouette.Armored);
            }

            // 基础重甲使用钢蓝主体和暗红识别色。
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
            if (classId == "sage")
            {
                // 贤者使用更明亮的灰紫长袍与金紫饰边，让施法者晋升后在地图和战斗画面都能立即看出变化。
                return new CharacterAppearanceDefinition(
                    new Color("80608a"),
                    new Color("706aa0"),
                    new Color("e0b56c"),
                    new Color("edbf9a"),
                    true,
                    CharacterWeaponSilhouette.Tome,
                    CharacterBodySilhouette.Robed);
            }

            // 基础法师使用偏灰紫色，保持长袍和头发层次。
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
        return classId switch
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
