using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 为当前角色选择原创程序像素外观。
/// 阵营主体色优先保证战棋可读性：我方统一使用深蓝系，敌方统一使用粉红/玫红系；
/// 角色个人差异继续由头发、金属层级、披风、武器和饰边承担。
/// </summary>
public static class CharacterAppearanceCatalog
{
    /// <summary>我方基础深蓝；所有玩家职业都围绕这个色相变化。</summary>
    private static readonly Color PlayerNavy = new("29466f");

    /// <summary>我方晋升或重甲使用的亮一档蓝钢色。</summary>
    private static readonly Color PlayerSteelBlue = new("38577d");

    /// <summary>敌方基础粉红；地图和战斗中需要能和深蓝立即分开。</summary>
    private static readonly Color EnemyPink = new("b95778");

    /// <summary>敌方高阶/重甲使用的深玫红。</summary>
    private static readonly Color EnemyRose = new("a54668");

    /// <summary>
    /// 根据角色实例 ID 和当前职业返回程序回退外观。
    /// 玩家角色优先使用个人发色与职业轮廓；阵营主体衣色保持统一，避免只靠小底座辨认敌我。
    /// </summary>
    public static CharacterAppearanceDefinition Get(UnitModel unit)
    {
        string id = unit.Id.ToLowerInvariant();
        string classId = unit.ClassDefinition.Id.ToLowerInvariant();

        if (id == "adrian")
        {
            if (classId == "sword_lord")
            {
                // 亚德里安晋升后使用蓝钢甲片和旧金饰边；发型、剑与披风继续承担个人识别。
                return new CharacterAppearanceDefinition(
                    new Color("5b4439"),
                    PlayerSteelBlue,
                    new Color("d4b36a"),
                    new Color("e4b590"),
                    true,
                    CharacterWeaponSilhouette.Sword,
                    CharacterBodySilhouette.Armored);
            }

            // 基础剑士使用纯正深蓝外衣，不再和其他我方角色分散成不同阵营色。
            return new CharacterAppearanceDefinition(
                new Color("5b4439"),
                PlayerNavy,
                new Color("c6a15d"),
                new Color("e4b590"),
                true,
                CharacterWeaponSilhouette.Sword,
                CharacterBodySilhouette.Light);
        }

        if (id == "celine")
        {
            if (classId == "bow_knight")
            {
                // 塞琳晋升后仍以深蓝为主体，米金与棕皮革保留弓手轻装气质。
                return new CharacterAppearanceDefinition(
                    new Color("ad7d4d"),
                    new Color("315077"),
                    new Color("d0ba78"),
                    new Color("e8ba96"),
                    true,
                    CharacterWeaponSilhouette.Bow,
                    CharacterBodySilhouette.Light);
            }

            // 弓手不再使用森林绿主体；深蓝衣服确保地图缩小时仍被立即识别为我方。
            return new CharacterAppearanceDefinition(
                new Color("ad7d4d"),
                new Color("26436b"),
                new Color("c8ae70"),
                new Color("e8ba96"),
                false,
                CharacterWeaponSilhouette.Bow,
                CharacterBodySilhouette.Light);
        }

        if (id == "rowan")
        {
            if (classId == "royal_lancer")
            {
                // 罗文晋升重甲使用偏冷的亮蓝钢，黄铜只作为甲片边缘和披风扣。
                return new CharacterAppearanceDefinition(
                    new Color("4a3e37"),
                    new Color("435f82"),
                    new Color("bea05f"),
                    new Color("d8aa86"),
                    true,
                    CharacterWeaponSilhouette.Spear,
                    CharacterBodySilhouette.Armored);
            }

            // 基础枪兵使用更沉的蓝钢，仍然保持和轻装角色同一阵营色相。
            return new CharacterAppearanceDefinition(
                new Color("4a3e37"),
                new Color("334e70"),
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
                // 米拉晋升后的长袍仍然是蓝色阵营主体，仅用柔和紫金饰边保留法师身份。
                return new CharacterAppearanceDefinition(
                    new Color("79677f"),
                    new Color("38517a"),
                    new Color("c2a0c9"),
                    new Color("e8b997"),
                    true,
                    CharacterWeaponSilhouette.Tome,
                    CharacterBodySilhouette.Robed);
            }

            // 基础法师使用深靛蓝长袍；紫色只留在头发、法术与小面积饰边上。
            return new CharacterAppearanceDefinition(
                new Color("706079"),
                new Color("2d456e"),
                new Color("ae91be"),
                new Color("e8b997"),
                true,
                CharacterWeaponSilhouette.Tome,
                CharacterBodySilhouette.Robed);
        }

        // 未来新增的我方角色即使暂时没有个人外观，也必须保持深蓝阵营主体，不能掉进敌方粉红回退。
        if (unit.Team == UnitTeam.Player)
        {
            CharacterWeaponSilhouette weapon = unit.EquippedWeapon.DamageType == DamageType.Magical
                ? CharacterWeaponSilhouette.Tome
                : classId.Contains("arch", StringComparison.OrdinalIgnoreCase) || classId.Contains("bow", StringComparison.OrdinalIgnoreCase)
                    ? CharacterWeaponSilhouette.Bow
                    : classId.Contains("spear", StringComparison.OrdinalIgnoreCase) || classId.Contains("lancer", StringComparison.OrdinalIgnoreCase)
                        ? CharacterWeaponSilhouette.Spear
                        : CharacterWeaponSilhouette.Sword;
            CharacterBodySilhouette body = weapon == CharacterWeaponSilhouette.Tome
                ? CharacterBodySilhouette.Robed
                : classId.Contains("guard", StringComparison.OrdinalIgnoreCase) || classId.Contains("armor", StringComparison.OrdinalIgnoreCase)
                    ? CharacterBodySilhouette.Armored
                    : CharacterBodySilhouette.Light;

            return new CharacterAppearanceDefinition(
                new Color("59463c"),
                PlayerNavy,
                new Color("c5a56a"),
                new Color("dfaF8c"),
                false,
                weapon,
                body);
        }

        // 通用敌军全部使用粉红/玫红主体。职业区别仍由身体轮廓、金属量和武器决定。
        return classId switch
        {
            "guard" => new CharacterAppearanceDefinition(
                new Color("403733"),
                new Color("b75c7b"),
                new Color("d19aab"),
                new Color("d2a07d"),
                false,
                CharacterWeaponSilhouette.Spear,
                CharacterBodySilhouette.Armored),
            "captain" => new CharacterAppearanceDefinition(
                new Color("302925"),
                EnemyRose,
                new Color("d6a16d"),
                new Color("d5a37f"),
                true,
                CharacterWeaponSilhouette.Sword,
                CharacterBodySilhouette.Armored),
            _ => new CharacterAppearanceDefinition(
                new Color("584237"),
                EnemyPink,
                new Color("d28ca1"),
                new Color("d3a17e"),
                false,
                CharacterWeaponSilhouette.Sword,
                CharacterBodySilhouette.Light)
        };
    }
}
