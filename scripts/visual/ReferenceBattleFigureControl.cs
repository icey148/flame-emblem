using FlameEmblem.Game;
using Godot;
using System.Reflection;

namespace FlameEmblem.Visual;

/// <summary>
/// 按当前正式战斗标准稿绘制原创硬边像素人物。
/// 本层只负责视觉：人物采用更短的 2.5～3 头身、宽肩、明显甲片/披风/武器轮廓，
/// 并继续读取现有战斗时间线，不修改命中、伤害、反击、经验或回合规则。
/// </summary>
public partial class ReferenceBattleFigureControl : Control
{
    /// <summary>实际由战斗协调器驱动的原人物节点。</summary>
    private AnimatedBattleCharacterControl? _source;

    /// <summary>缓存原人物当前单位字段。</summary>
    private FieldInfo? _unitField;

    /// <summary>缓存原人物当前动画状态字段。</summary>
    private FieldInfo? _stateField;

    /// <summary>缓存原人物当前状态时间字段。</summary>
    private FieldInfo? _elapsedField;

    /// <summary>一个逻辑像素固定绘制成 4×4 屏幕像素，外层 0.75 倍后正好得到 3×3。</summary>
    private const float PixelScale = 4.0f;

    /// <summary>物理攻击时间与现有战斗时间线保持一致。</summary>
    private const float AttackDuration = 0.34f;

    /// <summary>施法时间与现有战斗时间线保持一致。</summary>
    private const float CastDuration = 0.50f;

    /// <summary>人物逻辑中心线；所有水平镜像都围绕这里完成。</summary>
    private const int CenterX = 48;

    /// <summary>把表现层绑定到现有动画人物，只读取状态、不改变规则。</summary>
    public void Bind(AnimatedBattleCharacterControl source)
    {
        _source = source;
        BindingFlags members = BindingFlags.Instance | BindingFlags.NonPublic;
        Type sourceType = typeof(AnimatedBattleCharacterControl);
        _unitField = sourceType.GetField("_unit", members);
        _stateField = sourceType.GetField("_state", members);
        _elapsedField = sourceType.GetField("_stateElapsed", members);

        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = TextureFilterEnum.Nearest;
        SetProcess(true);
        QueueRedraw();
    }

    /// <summary>跟随原战斗时间线逐帧重绘。</summary>
    public override void _Process(double delta)
    {
        QueueRedraw();
    }

    /// <summary>绘制当前战斗人物；原细长程序人物在本层启用时始终隐藏。</summary>
    public override void _Draw()
    {
        UnitModel? unit = ReadUnit();
        if (_source is null || unit is null)
        {
            return;
        }

        // 这一层就是当前战斗标准人物，因此不再让旧细长回退模板透出来。
        _source.SelfModulate = new Color(1, 1, 1, 0);
        HideLegacyFigureLayers();

        CharacterAppearanceDefinition appearance = CharacterAppearanceCatalog.Get(unit);
        CharacterAnimationState state = ReadState();
        float elapsed = ReadElapsed();
        int side = _source.MirrorHorizontally ? -1 : 1;
        DrawReferenceFigure(unit, appearance, state, elapsed, side);
    }

    /// <summary>隐藏旧细节层和旧程序战斗人物层，保证画面只有一套人物。</summary>
    private void HideLegacyFigureLayers()
    {
        if (_source is null)
        {
            return;
        }

        foreach (BattleCharacterDetailOverlayControl overlay in _source.GetChildren().OfType<BattleCharacterDetailOverlayControl>())
        {
            overlay.Visible = false;
        }

        foreach (RefinedBattleFigureControl refined in _source.GetChildren().OfType<RefinedBattleFigureControl>())
        {
            refined.Visible = false;
        }

        foreach (CinematicBattleFigureControl cinematic in _source.GetChildren().OfType<CinematicBattleFigureControl>())
        {
            cinematic.Visible = false;
        }
    }

    /// <summary>组合身体、装备和动作，形成与标准稿一致的短身宽肩侧视轮廓。</summary>
    private void DrawReferenceFigure(
        UnitModel unit,
        CharacterAppearanceDefinition appearance,
        CharacterAnimationState state,
        float elapsed,
        int side)
    {
        Pose pose = ResolvePose(state, elapsed);
        Vector2 motion = ResolveMotion(appearance, state, elapsed, side);
        Vector2 origin = SnapScreenMotion(motion);
        float opacity = ResolveOpacity(state, elapsed);

        Color outline = Fade(new Color("15151b"), opacity);
        Color skin = Fade(appearance.SkinColor, opacity);
        Color skinLight = Fade(appearance.SkinColor.Lightened(0.12f), opacity);
        Color skinShadow = Fade(appearance.SkinColor.Darkened(0.22f), opacity);
        Color cloth = Fade(appearance.OutfitColor, opacity);
        Color clothLight = Fade(appearance.OutfitColor.Lightened(0.18f), opacity);
        Color clothShadow = Fade(appearance.OutfitColor.Darkened(0.30f), opacity);
        Color accent = Fade(appearance.AccentColor, opacity);
        Color hair = Fade(appearance.HairColor, opacity);
        Color hairLight = Fade(appearance.HairColor.Lightened(0.18f), opacity);
        Color hairShadow = Fade(appearance.HairColor.Darkened(0.30f), opacity);
        Color leather = Fade(new Color("55392c"), opacity);
        Color leatherLight = Fade(new Color("8b5b37"), opacity);
        Color metal = Fade(new Color("aeb9c5"), opacity);
        Color metalLight = Fade(new Color("eef2f3"), opacity);
        Color metalShadow = Fade(new Color("4d5868"), opacity);

        DrawGround(origin, unit, opacity);
        DrawCape(origin, appearance, pose, side, outline, clothShadow, accent);
        DrawLegs(origin, appearance, pose, side, outline, cloth, clothLight, clothShadow, leather, metalShadow);
        DrawTorso(origin, appearance, pose, side, outline, cloth, clothLight, clothShadow, accent, leather, metal, metalShadow);
        DrawHead(origin, unit, appearance, pose, side, outline, skin, skinLight, skinShadow, hair, hairLight, hairShadow, metal, metalShadow, accent);
        DrawArms(origin, appearance, pose, state, side, outline, cloth, clothShadow, skin, leather, metalShadow);
        DrawWeapon(origin, unit, appearance, pose, state, side, outline, accent, leatherLight, metal, metalLight, metalShadow);
    }

    /// <summary>绘制低矮阴影和阵营细线，让人物脚底明确落在状态框上方。</summary>
    private void DrawGround(Vector2 origin, UnitModel unit, float opacity)
    {
        Pixel(origin, 32, 89, 33, 2, new Color(0.04f, 0.04f, 0.06f, 0.50f * opacity));
        Pixel(origin, 39, 91, 19, 1, Fade(TeamVisualPalette.Primary(unit.Team), opacity));
    }

    /// <summary>披风改成大块三层轮廓，不再像旧模板的小竖条。</summary>
    private void DrawCape(
        Vector2 origin,
        CharacterAppearanceDefinition appearance,
        Pose pose,
        int side,
        Color outline,
        Color shadow,
        Color accent)
    {
        if (!appearance.HasCape)
        {
            return;
        }

        int sweep = pose == Pose.Strike ? 7 : pose == Pose.Windup ? 2 : pose == Pose.Recover ? 4 : 0;
        int back = -side;
        int x = side > 0 ? 35 : 47;

        Pixel(origin, MirrorX(x, 11, side), 39, 11, 5, outline);
        Pixel(origin, MirrorX(x + back, 13, side), 43, 13, 18, outline);
        Pixel(origin, MirrorX(x + back * (2 + sweep), 16, side), 52, 16, 18, outline);
        Pixel(origin, MirrorX(x + back + 1, 11, side), 44, 11, 15, shadow);
        Pixel(origin, MirrorX(x + back * (2 + sweep) + 1, 14, side), 53, 14, 15, accent.Darkened(0.22f));
        Pixel(origin, MirrorX(x + back * (3 + sweep), 8, side), 67, 8, 4, accent.Darkened(0.34f));
    }

    /// <summary>腿部明显缩短并加宽，使整体接近标准稿的约三头身比例。</summary>
    private void DrawLegs(
        Vector2 origin,
        CharacterAppearanceDefinition appearance,
        Pose pose,
        int side,
        Color outline,
        Color cloth,
        Color light,
        Color shadow,
        Color leather,
        Color metalShadow)
    {
        if (appearance.BodySilhouette == CharacterBodySilhouette.Robed)
        {
            Pixel(origin, 37, 57, 24, 6, outline);
            Pixel(origin, 39, 58, 20, 5, cloth);
            Pixel(origin, 36, 62, 13, 18, outline);
            Pixel(origin, 48, 62, 13, 18, outline);
            Pixel(origin, 38, 63, 10, 15, cloth);
            Pixel(origin, 50, 63, 9, 15, shadow);
            Pixel(origin, 42, 64, 3, 13, light);
            Pixel(origin, 45, 67, 2, 12, outline);
            Pixel(origin, 39, 79, 8, 9, outline);
            Pixel(origin, 51, 79, 8, 9, outline);
            Pixel(origin, 40, 80, 6, 7, leather);
            Pixel(origin, 52, 80, 6, 7, leather);
            Pixel(origin, 37, 87, 11, 3, outline);
            Pixel(origin, 50, 87, 11, 3, outline);
            return;
        }

        int stride = pose == Pose.Strike && appearance.WeaponSilhouette is CharacterWeaponSilhouette.Sword or CharacterWeaponSilhouette.Spear ? 4 : 0;
        int frontX = side > 0 ? 49 + stride : 40 - stride;
        int backX = side > 0 ? 40 - stride / 2 : 49 + stride / 2;
        Color legFill = appearance.BodySilhouette == CharacterBodySilhouette.Armored ? metalShadow : shadow;

        Pixel(origin, frontX, 61, 8, 14, outline);
        Pixel(origin, frontX + 1, 62, 6, 12, legFill);
        Pixel(origin, backX, 61, 8, 14, outline);
        Pixel(origin, backX + 1, 62, 6, 12, cloth.Darkened(0.10f));
        Pixel(origin, frontX, 74, 8, 6, outline);
        Pixel(origin, frontX + 1, 75, 6, 4, appearance.BodySilhouette == CharacterBodySilhouette.Armored ? cloth : leather);
        Pixel(origin, backX, 74, 8, 6, outline);
        Pixel(origin, backX + 1, 75, 6, 4, appearance.BodySilhouette == CharacterBodySilhouette.Armored ? cloth.Darkened(0.12f) : leather);
        Pixel(origin, frontX, 79, 7, 9, outline);
        Pixel(origin, frontX + 1, 80, 5, 7, appearance.BodySilhouette == CharacterBodySilhouette.Armored ? metalShadow : leather);
        Pixel(origin, backX, 79, 7, 9, outline);
        Pixel(origin, backX + 1, 80, 5, 7, appearance.BodySilhouette == CharacterBodySilhouette.Armored ? metalShadow.Darkened(0.08f) : leather.Darkened(0.08f));
        Pixel(origin, frontX - (side < 0 ? 2 : 0), 87, 11, 3, outline);
        Pixel(origin, backX - (side < 0 ? 2 : 0), 87, 11, 3, outline);
    }

    /// <summary>躯干使用宽肩、收腰和清晰胸甲层级，避免旧版“细柱”观感。</summary>
    private void DrawTorso(
        Vector2 origin,
        CharacterAppearanceDefinition appearance,
        Pose pose,
        int side,
        Color outline,
        Color cloth,
        Color light,
        Color shadow,
        Color accent,
        Color leather,
        Color metal,
        Color metalShadow)
    {
        int lean = pose == Pose.Strike && appearance.WeaponSilhouette is CharacterWeaponSilhouette.Sword or CharacterWeaponSilhouette.Spear ? side * 2 : 0;

        Pixel(origin, 34 + lean, 38, 29, 8, outline);
        Pixel(origin, 36 + lean, 39, 25, 7, cloth);
        Pixel(origin, 38 + lean, 45, 21, 15, outline);
        Pixel(origin, 39 + lean, 46, 19, 13, cloth);
        Pixel(origin, 40 + lean, 47, 5, 10, light);
        Pixel(origin, 54 + lean, 49, 4, 9, shadow);
        Pixel(origin, 39 + lean, 57, 19, 6, outline);
        Pixel(origin, 41 + lean, 58, 15, 4, leather);
        Pixel(origin, 47 + lean, 58, 3, 3, accent);

        // 双肩甲片始终存在；重甲职业再加胸甲和腰侧护片。
        Pixel(origin, 31 + lean, 39, 8, 8, outline);
        Pixel(origin, 32 + lean, 40, 7, 5, appearance.BodySilhouette == CharacterBodySilhouette.Armored ? metalShadow : clothShadowOr(cloth, 0.15f));
        Pixel(origin, 59 + lean, 39, 8, 8, outline);
        Pixel(origin, 59 + lean, 40, 7, 5, appearance.BodySilhouette == CharacterBodySilhouette.Armored ? metalShadow : clothShadowOr(cloth, 0.15f));
        Pixel(origin, 33 + lean, 40, 5, 2, accent);
        Pixel(origin, 60 + lean, 40, 5, 2, accent);

        if (appearance.BodySilhouette == CharacterBodySilhouette.Armored)
        {
            Pixel(origin, 38 + lean, 44, 21, 7, metalShadow);
            Pixel(origin, 40 + lean, 45, 17, 5, metal);
            Pixel(origin, 41 + lean, 45, 6, 2, metal.Lightened(0.16f));
            Pixel(origin, 40 + lean, 51, 18, 3, accent.Darkened(0.14f));
            Pixel(origin, 35 + lean, 55, 7, 9, outline);
            Pixel(origin, 36 + lean, 56, 5, 7, metalShadow);
            Pixel(origin, 56 + lean, 55, 7, 9, outline);
            Pixel(origin, 57 + lean, 56, 5, 7, metalShadow);
        }
    }

    /// <summary>头部扩大并加入头发/兜帽/头盔分层，建立清楚的角色身份轮廓。</summary>
    private void DrawHead(
        Vector2 origin,
        UnitModel unit,
        CharacterAppearanceDefinition appearance,
        Pose pose,
        int side,
        Color outline,
        Color skin,
        Color skinLight,
        Color skinShadow,
        Color hair,
        Color hairLight,
        Color hairShadow,
        Color metal,
        Color metalShadow,
        Color accent)
    {
        int lean = pose == Pose.Strike ? side : 0;
        Pixel(origin, 40 + lean, 18, 18, 20, outline);
        Pixel(origin, 42 + lean, 20, 14, 17, skin);
        Pixel(origin, side > 0 ? 53 + lean : 42 + lean, 27, 4, 7, skinShadow);
        Pixel(origin, side > 0 ? 50 + lean : 45 + lean, 27, 3, 3, outline);
        Pixel(origin, side > 0 ? 51 + lean : 46 + lean, 27, 1, 1, Colors.White);
        Pixel(origin, 46 + lean, 34, 7, 2, skinLight);

        string classId = unit.ClassDefinition.Id.ToLowerInvariant();
        bool hooded = classId.Contains("raider", StringComparison.OrdinalIgnoreCase);
        bool helmeted = classId.Contains("guard", StringComparison.OrdinalIgnoreCase);

        if (hooded)
        {
            Pixel(origin, 38 + lean, 14, 22, 8, outline);
            Pixel(origin, 40 + lean, 15, 18, 6, appearance.OutfitColor);
            Pixel(origin, 37 + lean, 20, 7, 17, outline);
            Pixel(origin, 38 + lean, 21, 5, 14, appearance.OutfitColor.Darkened(0.14f));
            Pixel(origin, 56 + lean, 20, 6, 17, outline);
            Pixel(origin, 57 + lean, 21, 4, 14, appearance.OutfitColor.Darkened(0.20f));
            Pixel(origin, 41 + lean, 15, 11, 2, appearance.OutfitColor.Lightened(0.20f));
            return;
        }

        if (helmeted)
        {
            Pixel(origin, 38 + lean, 15, 22, 8, outline);
            Pixel(origin, 40 + lean, 16, 18, 6, metalShadow);
            Pixel(origin, 42 + lean, 17, 14, 2, metal);
            Pixel(origin, 47 + lean, 12, 4, 5, outline);
            Pixel(origin, 48 + lean, 13, 2, 4, accent);
            Pixel(origin, 39 + lean, 22, 5, 7, metalShadow);
            return;
        }

        Pixel(origin, 38 + lean, 14, 22, 10, outline);
        Pixel(origin, 40 + lean, 15, 18, 8, hair);
        Pixel(origin, 43 + lean, 14, 8, 2, hairLight);
        Pixel(origin, 37 + lean, 20, 6, 11, outline);
        Pixel(origin, 39 + lean, 21, 4, 9, hairShadow);
        Pixel(origin, side > 0 ? 55 + lean : 40 + lean, 18, 7, 8, outline);
        Pixel(origin, side > 0 ? 56 + lean : 41 + lean, 19, 5, 6, hair);
    }

    /// <summary>手臂按武器姿势调整，但保持比旧版更厚的 5～7 像素轮廓。</summary>
    private void DrawArms(
        Vector2 origin,
        CharacterAppearanceDefinition appearance,
        Pose pose,
        CharacterAnimationState state,
        int side,
        Color outline,
        Color cloth,
        Color shadow,
        Color skin,
        Color leather,
        Color metalShadow)
    {
        bool action = state is CharacterAnimationState.Attack or CharacterAnimationState.Cast;
        int frontBase = side > 0 ? 60 : 31;
        int backBase = side > 0 ? 31 : 60;
        int reach = action && pose == Pose.Strike ? 10 : action && pose == Pose.Windup ? 4 : 2;

        Pixel(origin, backBase, 45, 7, 14, outline);
        Pixel(origin, backBase + 1, 46, 5, 11, shadow);
        Pixel(origin, backBase, 56, 7, 5, outline);
        Pixel(origin, backBase + 1, 57, 5, 3, leather);

        int frontX = side > 0 ? frontBase : frontBase - reach;
        Pixel(origin, frontX, 43, 7 + reach, 7, outline);
        Pixel(origin, frontX + 1, 44, 5 + reach, 5, appearance.BodySilhouette == CharacterBodySilhouette.Armored ? metalShadow : cloth);
        Pixel(origin, side > 0 ? frontX + 6 + reach : frontX, 44, 4, 5, skin);
    }

    /// <summary>根据职业绘制更粗、更清楚的剑、枪、弓或法书。</summary>
    private void DrawWeapon(
        Vector2 origin,
        UnitModel unit,
        CharacterAppearanceDefinition appearance,
        Pose pose,
        CharacterAnimationState state,
        int side,
        Color outline,
        Color accent,
        Color leather,
        Color metal,
        Color metalLight,
        Color metalShadow)
    {
        switch (appearance.WeaponSilhouette)
        {
            case CharacterWeaponSilhouette.Spear:
                DrawSpear(origin, pose, state == CharacterAnimationState.Attack, side, leather, metal, metalLight, metalShadow);
                break;
            case CharacterWeaponSilhouette.Bow:
                DrawBow(origin, pose, state == CharacterAnimationState.Attack, side, outline, accent, leather, metalLight);
                break;
            case CharacterWeaponSilhouette.Tome:
                DrawTome(origin, pose, state == CharacterAnimationState.Cast, side, outline, accent, leather, metalLight);
                break;
            default:
                // 掠夺者继续使用游戏规则中的近战武器，但视觉上给短刃/砍刀更紧凑的轮廓。
                bool shortBlade = unit.ClassDefinition.Id.Contains("raider", StringComparison.OrdinalIgnoreCase);
                DrawSword(origin, pose, state == CharacterAnimationState.Attack, side, shortBlade, accent, leather, metal, metalLight, metalShadow);
                break;
        }
    }

    /// <summary>剑/砍刀提供明显的待机、后上蓄势、水平斩和收势四种方向。</summary>
    private void DrawSword(
        Vector2 origin,
        Pose pose,
        bool attacking,
        int side,
        bool shortBlade,
        Color accent,
        Color leather,
        Color metal,
        Color metalLight,
        Color metalShadow)
    {
        int length = shortBlade ? 14 : 22;
        int handX = side > 0 ? 68 : 28;
        int handY = 49;

        if (!attacking || pose == Pose.Idle)
        {
            DrawSteppedLine(origin, handX, handY, handX + side * length, handY + 12, metalShadow, 3);
            DrawSteppedLine(origin, handX, handY - 1, handX + side * length, handY + 11, metalLight, 1);
        }
        else if (pose == Pose.Windup)
        {
            DrawSteppedLine(origin, handX, handY, handX - side * (length - 2), handY - 20, metal, 3);
        }
        else if (pose == Pose.Strike)
        {
            DrawSteppedLine(origin, handX, handY - 2, handX + side * (length + 10), handY - 4, metalLight, 3);
        }
        else
        {
            DrawSteppedLine(origin, handX, handY, handX + side * length, handY + 18, metal, 3);
        }

        Pixel(origin, side > 0 ? handX - 3 : handX - 2, handY - 2, 6, 2, accent);
        Pixel(origin, side > 0 ? handX - 1 : handX, handY, 3, 6, leather);
    }

    /// <summary>长枪使用连续横向枪杆和大枪头，出手时长度明显增加。</summary>
    private void DrawSpear(Vector2 origin, Pose pose, bool attacking, int side, Color leather, Color metal, Color metalLight, Color metalShadow)
    {
        int y = attacking && pose == Pose.Strike ? 49 : attacking && pose == Pose.Windup ? 54 : 51;
        int start = side > 0 ? 27 : 18;
        int length = attacking && pose == Pose.Strike ? 68 : 52;
        Pixel(origin, start, y, length, 2, leather);
        int tip = side > 0 ? start + length - 1 : start;
        Pixel(origin, side > 0 ? tip - 1 : tip - 8, y - 3, 9, 7, metalShadow);
        Pixel(origin, side > 0 ? tip : tip - 7, y - 2, 7, 5, metal);
        Pixel(origin, side > 0 ? tip + 4 : tip - 7, y - 1, 4, 3, metalLight);
    }

    /// <summary>弓体宽度和拉弦幅度加大，远距离也能一眼识别职业。</summary>
    private void DrawBow(Vector2 origin, Pose pose, bool attacking, int side, Color outline, Color accent, Color leather, Color metalLight)
    {
        int x = side > 0 ? 74 : 22;
        int bend = attacking && pose == Pose.Strike ? 6 : attacking && pose == Pose.Windup ? 3 : 2;
        Color wood = accent.Darkened(0.24f);
        Pixel(origin, x, 30, 3, 8, wood);
        Pixel(origin, x + side * bend, 37, 3, 10, wood);
        Pixel(origin, x + side * bend, 47, 3, 10, wood);
        Pixel(origin, x, 56, 3, 8, wood);

        int pull = attacking && pose == Pose.Strike ? (side > 0 ? 53 : 43) : attacking && pose == Pose.Windup ? (side > 0 ? 59 : 37) : x;
        DrawSteppedLine(origin, x, 31, pull, 46, outline, 1);
        DrawSteppedLine(origin, pull, 46, x, 63, outline, 1);
        if (attacking && pose is Pose.Windup or Pose.Strike)
        {
            int arrowX = side > 0 ? pull : pull - 28;
            Pixel(origin, arrowX, 46, 28, 1, metalLight);
        }
    }

    /// <summary>法书和符文光点保持小范围硬边像素，不遮挡人物轮廓。</summary>
    private void DrawTome(Vector2 origin, Pose pose, bool casting, int side, Color outline, Color accent, Color leather, Color metalLight)
    {
        int x = side > 0 ? 63 : 28;
        Pixel(origin, x, 48, 10, 8, outline);
        Pixel(origin, x + 1, 49, 4, 6, leather);
        Pixel(origin, x + 5, 49, 4, 6, accent.Darkened(0.18f));
        Pixel(origin, x + 4, 49, 1, 6, accent.Lightened(0.28f));

        if (!casting)
        {
            return;
        }

        int radius = pose == Pose.Strike ? 10 : pose == Pose.Windup ? 6 : 4;
        Pixel(origin, CenterX - 1, 27 - radius / 2, 3, 3, accent.Lightened(0.42f));
        Pixel(origin, CenterX - radius, 34, 3, 3, accent);
        Pixel(origin, CenterX + radius - 2, 34, 3, 3, accent.Lightened(0.22f));
        Pixel(origin, CenterX, 41, 2, 2, metalLight);
    }

    /// <summary>把攻击/施法状态转换成四个固定关键姿势。</summary>
    private static Pose ResolvePose(CharacterAnimationState state, float elapsed)
    {
        if (state == CharacterAnimationState.Attack)
        {
            float t = Mathf.Clamp(elapsed / AttackDuration, 0.0f, 1.0f);
            return t < 0.30f ? Pose.Windup : t < 0.62f ? Pose.Strike : Pose.Recover;
        }

        if (state == CharacterAnimationState.Cast)
        {
            float t = Mathf.Clamp(elapsed / CastDuration, 0.0f, 1.0f);
            return t < 0.36f ? Pose.Windup : t < 0.72f ? Pose.Strike : Pose.Recover;
        }

        return Pose.Idle;
    }

    /// <summary>动作位移保持短促，避免人物在黑底上大幅滑行。</summary>
    private static Vector2 ResolveMotion(CharacterAppearanceDefinition appearance, CharacterAnimationState state, float elapsed, int side)
    {
        if (state == CharacterAnimationState.Attack)
        {
            float t = Mathf.Clamp(elapsed / AttackDuration, 0.0f, 1.0f);
            float distance = appearance.WeaponSilhouette switch
            {
                CharacterWeaponSilhouette.Spear => 10.0f,
                CharacterWeaponSilhouette.Bow => 0.0f,
                CharacterWeaponSilhouette.Tome => 0.0f,
                _ => 13.0f
            };
            float pulse = t < 0.30f ? -EaseOut(t / 0.30f) * 2.0f : t < 0.62f ? EaseOut((t - 0.30f) / 0.32f) * distance : (1.0f - EaseInOut((t - 0.62f) / 0.38f)) * distance;
            return new Vector2(side * pulse, 0);
        }

        if (state == CharacterAnimationState.Cast)
        {
            float t = Mathf.Clamp(elapsed / CastDuration, 0.0f, 1.0f);
            return new Vector2(0, -Mathf.Sin(t * Mathf.Pi) * 2.0f);
        }

        if (state == CharacterAnimationState.Dodge)
        {
            float t = Mathf.Clamp(elapsed / 0.28f, 0.0f, 1.0f);
            return new Vector2(-side * Mathf.Sin(t * Mathf.Pi) * 16.0f, 0);
        }

        if (state == CharacterAnimationState.Hit)
        {
            float t = Mathf.Clamp(elapsed / 0.24f, 0.0f, 1.0f);
            return new Vector2(-side * Mathf.Sin(t * Mathf.Pi) * 6.0f, Mathf.Sin(t * Mathf.Pi) * 2.0f);
        }

        if (state == CharacterAnimationState.Defeat)
        {
            float t = Mathf.Clamp(elapsed / 0.90f, 0.0f, 1.0f);
            return new Vector2(-side * EaseOut(t) * 8.0f, EaseInOut(t) * 28.0f);
        }

        return Vector2.Zero;
    }

    /// <summary>倒下时逐渐淡出；其他状态保持完全不透明。</summary>
    private static float ResolveOpacity(CharacterAnimationState state, float elapsed)
    {
        if (state != CharacterAnimationState.Defeat)
        {
            return 1.0f;
        }

        float t = Mathf.Clamp(elapsed / 1.05f, 0.0f, 1.0f);
        return Mathf.Lerp(1.0f, 0.20f, EaseInOut(t));
    }

    /// <summary>读取原人物当前单位。</summary>
    private UnitModel? ReadUnit()
    {
        return _source is null ? null : _unitField?.GetValue(_source) as UnitModel;
    }

    /// <summary>读取原人物当前动画状态。</summary>
    private CharacterAnimationState ReadState()
    {
        return _source is not null && _stateField?.GetValue(_source) is CharacterAnimationState state ? state : CharacterAnimationState.Idle;
    }

    /// <summary>读取原人物当前状态时间。</summary>
    private float ReadElapsed()
    {
        return _source is not null && _elapsedField?.GetValue(_source) is float elapsed ? elapsed : 0.0f;
    }

    /// <summary>围绕人物中心线镜像一个矩形的逻辑 X 坐标。</summary>
    private static int MirrorX(int x, int width, int side)
    {
        return side > 0 ? x : CenterX - (x + width - CenterX);
    }

    /// <summary>绘制逻辑像素矩形；所有位置和尺寸都保持整数。</summary>
    private void Pixel(Vector2 origin, int x, int y, int width, int height, Color color)
    {
        DrawRect(
            new Rect2(
                origin + new Vector2(x * PixelScale, y * PixelScale),
                new Vector2(width * PixelScale, height * PixelScale)),
            color,
            true);
    }

    /// <summary>使用离散逻辑像素连接两点，可指定线宽，不启用抗锯齿。</summary>
    private void DrawSteppedLine(Vector2 origin, int fromX, int fromY, int toX, int toY, Color color, int thickness)
    {
        int dx = toX - fromX;
        int dy = toY - fromY;
        int steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
        if (steps <= 0)
        {
            Pixel(origin, fromX, fromY, thickness, thickness, color);
            return;
        }

        for (int index = 0; index <= steps; index++)
        {
            float t = index / (float)steps;
            int x = (int)MathF.Round(fromX + dx * t);
            int y = (int)MathF.Round(fromY + dy * t);
            Pixel(origin, x, y, thickness, thickness, color);
        }
    }

    /// <summary>把小幅动作位移锁到 4px 屏幕栅格，外层缩放后仍是整数像素。</summary>
    private static Vector2 SnapScreenMotion(Vector2 value)
    {
        return new Vector2(Mathf.Round(value.X / PixelScale) * PixelScale, Mathf.Round(value.Y / PixelScale) * PixelScale);
    }

    /// <summary>给颜色统一应用动作透明度。</summary>
    private static Color Fade(Color color, float opacity)
    {
        return new Color(color.R, color.G, color.B, color.A * opacity);
    }

    /// <summary>返回略暗的同色，用于轻装肩甲。</summary>
    private static Color clothShadowOr(Color cloth, float amount)
    {
        return cloth.Darkened(amount);
    }

    /// <summary>二次缓出，用于快速出手。</summary>
    private static float EaseOut(float t)
    {
        float clamped = Mathf.Clamp(t, 0.0f, 1.0f);
        return 1.0f - (1.0f - clamped) * (1.0f - clamped);
    }

    /// <summary>平滑起落，用于收势和倒下。</summary>
    private static float EaseInOut(float t)
    {
        float clamped = Mathf.Clamp(t, 0.0f, 1.0f);
        return clamped * clamped * (3.0f - 2.0f * clamped);
    }

    /// <summary>标准稿人物的四个关键动作姿势。</summary>
    private enum Pose
    {
        /// <summary>待机。</summary>
        Idle,
        /// <summary>蓄势。</summary>
        Windup,
        /// <summary>出手。</summary>
        Strike,
        /// <summary>收势。</summary>
        Recover
    }
}
