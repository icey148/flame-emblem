using Godot;

namespace FlameEmblem.Game;

/// <summary>
/// 使用固定规则驱动敌军回合。
/// 这里的“敌军逻辑”完全是传统游戏算法，不包含机器学习、LLM 或任何联网 AI 服务。
/// </summary>
public static class EnemyTurnController
{
    /// <summary>
    /// 为一个敌军单位选择本回合最优先接近的玩家单位。
    /// 第一版规则非常明确：选择曼哈顿距离最近的存活玩家单位。
    /// </summary>
    public static UnitModel? FindNearestPlayer(UnitModel enemy, IEnumerable<UnitModel> units)
    {
        UnitModel? nearest = null;
        int bestDistance = int.MaxValue;

        foreach (UnitModel candidate in units)
        {
            // 只把仍然存活的玩家单位当作可追踪目标。
            if (!candidate.IsAlive || candidate.Team != UnitTeam.Player)
            {
                continue;
            }

            int distance = CombatRules.GridDistance(enemy.GridPosition, candidate.GridPosition);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = candidate;
            }
        }

        return nearest;
    }

    /// <summary>
    /// 在敌军可到达的格子中，选择最接近目标的位置。
    /// 如果多个格子距离相同，则保留遍历时先遇到的格子，使行为稳定可预测。
    /// </summary>
    public static Vector2I ChooseMoveDestination(
        UnitModel enemy,
        UnitModel target,
        IReadOnlyCollection<Vector2I> reachableCells)
    {
        Vector2I bestCell = enemy.GridPosition;
        int bestDistance = CombatRules.GridDistance(enemy.GridPosition, target.GridPosition);

        foreach (Vector2I cell in reachableCells)
        {
            int distance = CombatRules.GridDistance(cell, target.GridPosition);

            // 敌军只会选择真正更接近目标的位置，避免无意义来回移动。
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestCell = cell;
            }
        }

        return bestCell;
    }
}
