using Godot;

namespace FlameEmblem.Game;

/// <summary>
/// 保存当前游戏进程中的战役进度与玩家队伍状态。
/// 世界地图和战斗场景会被 Godot 反复切换，因此不能把跨章节状态只留在某个场景节点中。
/// </summary>
public static class CampaignState
{
    /// <summary>没有从世界地图选择章节时默认进入序章。</summary>
    public const string DefaultChapterId = "chapter_01";

    /// <summary>序章的默认数据路径。</summary>
    public const string DefaultChapterPath = "res://data/chapter_01.json";

    /// <summary>当前准备进入或正在进行的章节 ID。</summary>
    private static string _currentChapterId = DefaultChapterId;

    /// <summary>当前准备进入或正在进行的章节 JSON 路径。</summary>
    private static string _currentChapterPath = DefaultChapterPath;

    /// <summary>已经完成的章节 ID；用于世界地图节点解锁。</summary>
    private static readonly HashSet<string> CompletedChapters = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>按角色稳定 ID 保存的跨场景玩家状态。</summary>
    private static readonly Dictionary<string, CampaignUnitState> PlayerRoster = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>当前章节 ID。</summary>
    public static string CurrentChapterId => _currentChapterId;

    /// <summary>当前章节数据路径。</summary>
    public static string CurrentChapterPath => _currentChapterPath;

    /// <summary>是否已经拥有可跨场景恢复的玩家队伍。</summary>
    public static bool HasPlayerRoster => PlayerRoster.Count > 0;

    /// <summary>
    /// 从世界地图选择一个战斗节点。
    /// 这里只记录目标章节；真正切换场景由世界地图节点负责。
    /// </summary>
    public static void BeginChapter(string chapterId, string chapterPath)
    {
        if (string.IsNullOrWhiteSpace(chapterId) || string.IsNullOrWhiteSpace(chapterPath))
        {
            throw new ArgumentException("章节 ID 和章节路径不能为空。");
        }

        _currentChapterId = chapterId;
        _currentChapterPath = chapterPath;
    }

    /// <summary>
    /// 战斗场景实际加载完章节后同步真实章节 ID。
    /// 这样即使数据文件重命名，后续完成记录仍以 JSON 内的稳定 ID 为准。
    /// </summary>
    public static void ConfirmLoadedChapter(string chapterId, string chapterPath)
    {
        _currentChapterId = string.IsNullOrWhiteSpace(chapterId) ? DefaultChapterId : chapterId;
        _currentChapterPath = string.IsNullOrWhiteSpace(chapterPath) ? DefaultChapterPath : chapterPath;
    }

    /// <summary>
    /// 在章节胜利时记录完成状态，并保存所有玩家角色的长期成长状态。
    /// </summary>
    public static void CompleteChapter(string chapterId, IEnumerable<UnitModel> units)
    {
        if (!string.IsNullOrWhiteSpace(chapterId))
        {
            CompletedChapters.Add(chapterId);
        }

        CapturePlayerRoster(units);
    }

    /// <summary>判断指定章节是否已经完成。</summary>
    public static bool IsChapterCompleted(string chapterId)
    {
        return !string.IsNullOrWhiteSpace(chapterId) && CompletedChapters.Contains(chapterId);
    }

    /// <summary>判断世界地图节点要求的前置章节是否全部完成。</summary>
    public static bool AreRequirementsMet(IEnumerable<string> requiredChapterIds)
    {
        return requiredChapterIds.All(IsChapterCompleted);
    }

    /// <summary>
    /// 把当前章节中的玩家角色状态写入跨场景队伍缓存。
    /// 地图坐标和本回合行动标记属于单场战斗状态，因此不会跨章节保存。
    /// </summary>
    public static void CapturePlayerRoster(IEnumerable<UnitModel> units)
    {
        foreach (UnitModel unit in units.Where(unit => unit.Team == UnitTeam.Player))
        {
            PlayerRoster[unit.Id] = new CampaignUnitState(
                unit.ClassDefinition.Id,
                unit.EquippedWeapon.Id,
                unit.Level,
                unit.Experience,
                unit.MaxHp,
                unit.CurrentHp,
                unit.Strength,
                unit.Magic,
                unit.Skill,
                unit.Speed,
                unit.Luck,
                unit.Defense,
                unit.Resistance);
        }
    }

    /// <summary>
    /// 把跨场景队伍状态应用到新章节生成的玩家单位。
    /// 新章节的部署坐标仍由该章节 JSON 决定，只恢复职业、装备、成长属性和生命值。
    /// </summary>
    public static void ApplyPlayerRoster(IEnumerable<UnitModel> units)
    {
        if (PlayerRoster.Count == 0)
        {
            return;
        }

        foreach (UnitModel unit in units.Where(unit => unit.Team == UnitTeam.Player))
        {
            if (!PlayerRoster.TryGetValue(unit.Id, out CampaignUnitState? state))
            {
                continue;
            }

            UnitClassDefinition? classDefinition = UnitClassCatalog.TryGet(state.ClassId);
            WeaponDefinition? weapon = UnitLoadoutCatalog.TryGetWeapon(state.WeaponId);
            if (classDefinition is null || weapon is null)
            {
                // 数据版本变化时保留章节本身生成的安全状态，不把坏的跨场景引用写进运行时模型。
                GD.PushWarning($"无法恢复 {unit.DisplayName} 的战役状态：职业或装备定义不存在。");
                continue;
            }

            unit.RestoreRuntimeState(
                unit.GridPosition,
                classDefinition,
                weapon,
                state.Level,
                state.Experience,
                state.MaxHp,
                state.CurrentHp,
                state.Strength,
                state.Magic,
                state.Skill,
                state.Speed,
                state.Luck,
                state.Defense,
                state.Resistance,
                false);
        }
    }

    /// <summary>
    /// 在祠堂休整时回复所有仍然存活角色的 HP。
    /// 已经阵亡的角色保持 0 HP，不通过普通休整复活。
    /// </summary>
    public static string RestAtShrine()
    {
        if (PlayerRoster.Count == 0)
        {
            return "当前还没有可休整的队伍。";
        }

        int healedUnits = 0;
        foreach (CampaignUnitState state in PlayerRoster.Values)
        {
            if (state.CurrentHp <= 0 || state.CurrentHp >= state.MaxHp)
            {
                continue;
            }

            state.CurrentHp = state.MaxHp;
            healedUnits++;
        }

        return healedUnits > 0
            ? $"祠堂休整完成：{healedUnits} 名角色的 HP 已完全恢复。"
            : "队伍状态良好，不需要额外回复。";
    }

    /// <summary>
    /// 开发阶段需要重新开始时清空战役状态。
    /// 后续标题画面的“新游戏”会直接调用这个入口。
    /// </summary>
    public static void ResetCampaign()
    {
        _currentChapterId = DefaultChapterId;
        _currentChapterPath = DefaultChapterPath;
        CompletedChapters.Clear();
        PlayerRoster.Clear();
    }

    /// <summary>一个玩家角色在章节之间需要保留的长期状态。</summary>
    private sealed class CampaignUnitState
    {
        /// <summary>创建长期角色状态快照。</summary>
        public CampaignUnitState(
            string classId,
            string weaponId,
            int level,
            int experience,
            int maxHp,
            int currentHp,
            int strength,
            int magic,
            int skill,
            int speed,
            int luck,
            int defense,
            int resistance)
        {
            ClassId = classId;
            WeaponId = weaponId;
            Level = level;
            Experience = experience;
            MaxHp = maxHp;
            CurrentHp = currentHp;
            Strength = strength;
            Magic = magic;
            Skill = skill;
            Speed = speed;
            Luck = luck;
            Defense = defense;
            Resistance = resistance;
        }

        /// <summary>当前职业 ID。</summary>
        public string ClassId { get; }

        /// <summary>当前装备 ID。</summary>
        public string WeaponId { get; }

        /// <summary>当前等级。</summary>
        public int Level { get; }

        /// <summary>当前等级内经验。</summary>
        public int Experience { get; }

        /// <summary>最大 HP。</summary>
        public int MaxHp { get; }

        /// <summary>当前 HP；祠堂休整会修改这一项。</summary>
        public int CurrentHp { get; set; }

        /// <summary>力量。</summary>
        public int Strength { get; }

        /// <summary>魔力。</summary>
        public int Magic { get; }

        /// <summary>技巧。</summary>
        public int Skill { get; }

        /// <summary>速度。</summary>
        public int Speed { get; }

        /// <summary>幸运。</summary>
        public int Luck { get; }

        /// <summary>防御。</summary>
        public int Defense { get; }

        /// <summary>魔防。</summary>
        public int Resistance { get; }
    }
}
