using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 为当前角色选择原创程序像素外观。
/// 配色采用低饱和蓝灰、森林绿、银钢、柔和紫与酒红敌军色，保持古典日式战争幻想气质；人物轮廓与具体服装结构仍为原创。
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
                // 晋升后使用更亮的蓝钢与旧金饰边，保留主角蓝灰识别但避免高饱和塑料感。
                return new CharacterAppearanceDefinition(
                    new Color("5b4439"),
                    new Color("7385a3"),
                    new Color("d9b86c"),
                    new Color("e4b590"),
                    true,
                    CharacterWeaponSilhouette.Sword,
                    CharacterBodySilhouette.Armored);
            }

            // 主角基础职业使用低饱和蓝灰、棕发和黄铜点缀。
            return new CharacterAppearanceDefinition(
                new Color("5b4439"),
                new Color("596d91"),
                new Color("cdaa67"),
                new Color("e4b590"),
                true,
                CharacterWeaponSilhouette.Sword,
                CharacterBodySilhouette.Light);
        }

        if (id == "celine")
        {
            if (classId == "bow_knight")
            {
                // 晋升弓骑士使用偏灰深林绿与旧金，保持纤细远程职业的轻盈层次。
                return new CharacterAppearanceDefinition(
                    new Color("ad7d4d"),
                    new Color("70856f"),
                    new Color("d7bf78"),
                    new Color("e8ba96"),
                    true,
                    CharacterWeaponSilhouette.Bow,
                    CharacterBodySilhouette.Light);
            }

            // 基础弓手使用森林绿、米金和棕皮革气质，降低橙色与绿色的对比强度。
            return new CharacterAppearanceDefinition(
                new Color("ad7d4d"),
                new Color("5d7561"),
                new Color("cdb875"),
                new Color("e8ba96"),
                false,
                CharacterWeaponSilhouette.Bow,
                CharacterBodySilhouette.Light);
        }

        if (id == "rowan")
        {
            if (classId == "royal_lancer")
            {
                // 晋升枪卫提高银蓝钢明度并保留暖黄铜饰边，强调重甲但不做成纯亮银。
                return new CharacterAppearanceDefinition(
                    new Color("4a3e37"),
                    new Color("909ba8"),
                    new Color("bea05f"),
                    new Color("d8aa86"),
                    true,
                    CharacterWeaponSilhouette.Spear,
                    CharacterBodySilhouette.Armored);
            }

            // 基础重甲使用冷灰蓝钢主体和暗黄铜识别色。
            return new CharacterAppearanceDefinition(
                new Color("4a3e37"),
                new Color("798693"),
                new Color("ad8a57"),
                new Color("d8aa86"),
                false,
                CharacterWeaponSilhouette.Spear,
                CharacterBodySilhouette.Armored);
        }

        if (id == "mira")
        {
            if (classId == "sage")
            {
                // 贤者使用更亮的灰紫与米金饰边，保持柔和神秘感而不使用荧光紫。
                return new CharacterAppearanceDefinition(
                    new Color("79677f"),
                    new Color("7b7194"),
                    new Color("d1b16e"),
                    new Color("e8b997"),
                    true,
                    CharacterWeaponSilhouette.Tome,
                    CharacterBodySilhouette.Robed);
            }

            // 基础法师使用低饱和紫灰长袍、灰紫头发和暖米金点缀。
            return new CharacterAppearanceDefinition(
                new Color("706079"),
                new Color("625d7f"),
                new Color("bda474"),
                new Color("e8b997"),
                true,
                CharacterWeaponSilhouette.Tome,
                CharacterBodySilhouette.Robed);
        }

        // 通用敌军使用低饱和酒红、褐铁和暗金，和我方冷色系形成稳定阵营区分。
        return classId switch
        {
            "guard" => new CharacterAppearanceDefinition(
                new Color("403733"),
                new Color("665954"),
                new Color("a77c58"),
                new Color("d2a07d"),
                false,
                CharacterWeaponSilhouette.Spear,
                CharacterBodySilhouette.Armored),
            "captain" => new CharacterAppearanceDefinition(
                new Color("302925"),
                new Color("6b3438"),
                new Color("b99458"),
                new Color("d5a37f"),
                true,
                CharacterWeaponSilhouette.Sword,
                CharacterBodySilhouette.Armored),
            _ => new CharacterAppearanceDefinition(
                new Color("584237"),
                new Color("65423f"),
                new Color("9f684f"),
                new Color("d3a17e"),
                false,
                CharacterWeaponSilhouette.Sword,
                CharacterBodySilhouette.Light)
        };
    }
}
