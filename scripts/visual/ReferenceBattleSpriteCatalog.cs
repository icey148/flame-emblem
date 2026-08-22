using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 战斗画面人物资源的唯一入口。
/// 已完成角色优先读取最终 approved_battle.webp；尚未完成最终全身像的角色由 ApprovedCharacterArtCatalog
/// 临时读取已有彩色 battle_ref.png，确保正常战斗中不会再出现纯黑剪影、空人物或程序方块人。
/// </summary>
public static class ReferenceBattleSpriteCatalog
{
    /// <summary>
    /// 返回当前单位可显示的战斗人物纹理。
    /// 正式美术补齐后会自动覆盖临时彩色后备，不需要修改战斗演出代码。
    /// </summary>
    public static Texture2D? TryLoad(UnitModel unit)
    {
        return ApprovedCharacterArtCatalog.TryLoadBattle(unit);
    }
}
