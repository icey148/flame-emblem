using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 战斗画面正式人物资源入口。
/// 旧 battle_v3 占位图已经退出正式显示链；这里只转交到用户确认过的 ApprovedCharacterArtCatalog。
/// </summary>
public static class ReferenceBattleSpriteCatalog
{
    /// <summary>
    /// 返回当前单位的正式战斗人物。
    /// 正式设计稿尚未制作的角色返回空，调用方宁可不画人物，也不再显示旧方块人。
    /// </summary>
    public static Texture2D? TryLoad(UnitModel unit)
    {
        return ApprovedCharacterArtCatalog.TryLoadBattle(unit);
    }
}
