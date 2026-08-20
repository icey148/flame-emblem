using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 横向战斗演出底部的复古状态 HUD。
/// 该控件只消费已经结算好的战斗结果，按动画顺序重放 HP 变化，并在战后播放 EXP 增长与升级反馈。
/// </summary>
public partial class RetroBattleStatusHudControl : Control
{
    /// <summary>左侧固定战斗单位。</summary>
    private UnitModel? _leftUnit;

    /// <summary>右侧固定战斗单位。</summary>
    private UnitModel? _rightUnit;

    /// <summary>本场实际获得经验的玩家单位；没有实际攻击时为空。</summary>
    private UnitModel? _experienceUnit;

    /// <summary>左侧当前演出 HP。</summary>
    private int _leftHp;

    /// <summary>右侧当前演出 HP。</summary>
    private int _rightHp;

    /// <summary>左侧开战时最大 HP。</summary>
    private int _leftMaxHp = 1;

    /// <summary>右侧开战时最大 HP。</summary>
    private int _rightMaxHp = 1;

    /// <summary>玩家获得经验前的等级。</summary>
    private int _experienceLevelBefore = 1;

    /// <summary>玩家获得经验前的 EXP。</summary>
    private int _experienceBefore;

    /// <summary>本场实际获得的经验值。</summary>
    private int _experienceGained;

    /// <summary>当前是否已经进入战后经验展示阶段。</summary>
    private bool _showExperienceResult;

    /// <summary>EXP 条是否正在从旧值滚动到新值。</summary>
    private bool _experienceAnimating;

    /// <summary>EXP 动画已经播放的秒数。</summary>
    private float _experienceAnimationElapsed;

    /// <summary>EXP 动画总时长。</summary>
    private float _experienceAnimationDuration = 0.8f;

    /// <summary>
    /// 把“等级 + EXP”转换成连续总经验后的动画起点。
    /// 使用 (等级 - 1) × 100，确保 Lv.1 EXP 0 对应总值 0。
    /// </summary>
    private int _experienceAnimationStartTotal;

    /// <summary>连续总经验动画终点。</summary>
    private int _experienceAnimationEndTotal;

    /// <summary>当前动画正在展示的连续总经验值。</summary>
    private int _animatedExperienceTotal;

    /// <summary>左侧姓名与武器文本。</summary>
    private Label? _leftIdentityLabel;

    /// <summary>右侧姓名与武器文本。</summary>
    private Label? _rightIdentityLabel;

    /// <summary>左侧 HP 数字。</summary>
    private Label? _leftHpLabel;

    /// <summary>右侧 HP 数字。</summary>
    private Label? _rightHpLabel;

    /// <summary>中央本击伤害/结果文字。</summary>
    private Label? _damageLabel;

    /// <summary>中央经验值与升级文字。</summary>
    private Label? _experienceLabel;

    /// <summary>
    /// 创建固定文字控件并使用最近邻画面策略。
    /// HUD 全部使用整数坐标矩形，不绘制圆角或抗锯齿装饰。
    /// </summary>
    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        CreateLabels();
        SetProcess(false);
        QueueRedraw();
    }

    /// <summary>
    /// 开始一场新的战斗 HUD。
    /// HP 必须传入 CombatResolver 保存的开战快照，不能直接读取已经被整场结算修改后的 CurrentHp。
    /// </summary>
    public void BeginBattle(
        UnitModel leftUnit,
        int leftHp,
        int leftMaxHp,
        UnitModel rightUnit,
        int rightHp,
        int rightMaxHp,
        UnitModel? experienceUnit,
        int experienceLevelBefore,
        int experienceBefore)
    {
        _leftUnit = leftUnit;
        _rightUnit = rightUnit;
        _leftHp = Math.Clamp(leftHp, 0, Math.Max(1, leftMaxHp));
        _rightHp = Math.Clamp(rightHp, 0, Math.Max(1, rightMaxHp));
        _leftMaxHp = Math.Max(1, leftMaxHp);
        _rightMaxHp = Math.Max(1, rightMaxHp);
        _experienceUnit = experienceUnit;
        _experienceLevelBefore = Math.Max(1, experienceLevelBefore);
        _experienceBefore = Math.Clamp(experienceBefore, 0, 99);
        _experienceGained = 0;
        _showExperienceResult = false;
        _experienceAnimating = false;
        _experienceAnimationElapsed = 0.0f;
        _experienceAnimationStartTotal = ToTotalExperience(_experienceLevelBefore, _experienceBefore);
        _experienceAnimationEndTotal = _experienceAnimationStartTotal;
        _animatedExperienceTotal = _experienceAnimationStartTotal;
        SetProcess(false);

        if (_leftIdentityLabel is not null)
        {
            _leftIdentityLabel.Text = $"{leftUnit.DisplayName}  Lv.{leftUnit.Level}\n{leftUnit.EquippedWeapon.DisplayName}";
        }

        if (_rightIdentityLabel is not null)
        {
            _rightIdentityLabel.Text = $"{rightUnit.DisplayName}  Lv.{rightUnit.Level}\n{rightUnit.EquippedWeapon.DisplayName}";
        }

        if (_damageLabel is not null)
        {
            _damageLabel.Text = "READY";
        }

        RefreshLabels();
        QueueRedraw();
    }

    /// <summary>
    /// 按一击真实结果推进 HUD 数值。
    /// 法术 HP 成本先从攻击方扣除；命中后再从防守方扣除伤害，顺序与实际结算一致。
    /// </summary>
    public void ApplyStrike(CombatStrikeResult strike)
    {
        if (_leftUnit is null || _rightUnit is null)
        {
            return;
        }

        if (ReferenceEquals(strike.Attacker, _leftUnit))
        {
            _leftHp = Math.Max(0, _leftHp - strike.HpCostPaid);
        }
        else if (ReferenceEquals(strike.Attacker, _rightUnit))
        {
            _rightHp = Math.Max(0, _rightHp - strike.HpCostPaid);
        }

        if (strike.Hit)
        {
            if (ReferenceEquals(strike.Defender, _leftUnit))
            {
                _leftHp = Math.Max(0, _leftHp - strike.Damage);
            }
            else if (ReferenceEquals(strike.Defender, _rightUnit))
            {
                _rightHp = Math.Max(0, _rightHp - strike.Damage);
            }
        }

        if (_damageLabel is not null)
        {
            if (!strike.Hit)
            {
                _damageLabel.Text = "MISS";
            }
            else if (strike.Critical)
            {
                _damageLabel.Text = $"CRITICAL  {strike.Damage}";
            }
            else
            {
                _damageLabel.Text = $"DAMAGE  {strike.Damage}";
            }
        }

        RefreshLabels();
        QueueRedraw();
    }

    /// <summary>
    /// 战斗所有攻击结束后启动 EXP 增长动画。
    /// MainGame 会在 ResolveExchange 返回以后发放经验，因此这里读取到的是已经真实结算后的最终等级与 EXP。
    /// </summary>
    public int ShowExperienceResult()
    {
        _showExperienceResult = true;
        _experienceGained = CalculateExperienceGain();

        if (_experienceUnit is null)
        {
            _experienceAnimating = false;
            SetProcess(false);
            SetExperienceLabel("EXP  --");
            QueueRedraw();
            return 0;
        }

        _experienceAnimationStartTotal = ToTotalExperience(_experienceLevelBefore, _experienceBefore);
        _experienceAnimationEndTotal = _experienceAnimationStartTotal + _experienceGained;
        _animatedExperienceTotal = _experienceAnimationStartTotal;
        _experienceAnimationElapsed = 0.0f;

        // 小额经验也至少播放半秒；高经验最多约一秒，保证不拖慢战斗节奏。
        _experienceAnimationDuration = Math.Clamp(
            0.58f + _experienceGained * 0.006f,
            0.58f,
            1.02f);
        _experienceAnimating = _experienceGained > 0;
        SetProcess(_experienceAnimating);

        if (!_experienceAnimating)
        {
            _animatedExperienceTotal = _experienceAnimationEndTotal;
            RefreshExperienceLabel(true);
        }
        else
        {
            RefreshExperienceLabel(false);
        }

        QueueRedraw();
        return _experienceGained;
    }

    /// <summary>
    /// EXP 动画按连续总经验推进。
    /// 跨过 100 时条会自然从满格回到 0，并同步把显示等级提升一级。
    /// </summary>
    public override void _Process(double delta)
    {
        if (!_experienceAnimating)
        {
            return;
        }

        _experienceAnimationElapsed += Math.Max(0.0f, (float)delta);
        float t = Mathf.Clamp(
            _experienceAnimationElapsed / Math.Max(0.01f, _experienceAnimationDuration),
            0.0f,
            1.0f);

        // 使用轻微 ease-out，让 EXP 前段增长更有反馈，末尾停得更稳。
        float eased = 1.0f - (1.0f - t) * (1.0f - t);
        _animatedExperienceTotal = (int)MathF.Round(Mathf.Lerp(
            _experienceAnimationStartTotal,
            _experienceAnimationEndTotal,
            eased));

        if (t >= 1.0f)
        {
            _animatedExperienceTotal = _experienceAnimationEndTotal;
            _experienceAnimating = false;
            SetProcess(false);
            RefreshExperienceLabel(true);
        }
        else
        {
            RefreshExperienceLabel(false);
        }

        QueueRedraw();
    }

    /// <summary>清空中央结果信息，供下一场战斗重新使用。</summary>
    public void ResetBattle()
    {
        _leftUnit = null;
        _rightUnit = null;
        _experienceUnit = null;
        _showExperienceResult = false;
        _experienceAnimating = false;
        _experienceGained = 0;
        _leftHp = 0;
        _rightHp = 0;
        SetProcess(false);

        if (_damageLabel is not null)
        {
            _damageLabel.Text = string.Empty;
        }

        if (_experienceLabel is not null)
        {
            _experienceLabel.Text = string.Empty;
        }

        RefreshLabels();
        QueueRedraw();
    }

    /// <summary>
    /// 绘制双方深色状态框、青铜边线、HP 条以及中央 EXP 条。
    /// 所有尺寸使用整数，保持复古界面边缘干净。
    /// </summary>
    public override void _Draw()
    {
        Color panel = new(0.055f, 0.065f, 0.085f, 0.97f);
        Color border = new(0.62f, 0.49f, 0.28f, 1.0f);
        Color barBack = new(0.08f, 0.075f, 0.075f, 1.0f);
        Color hpLeft = HpColor(_leftHp, _leftMaxHp);
        Color hpRight = HpColor(_rightHp, _rightMaxHp);

        Rect2 leftPanel = new(new Vector2(0, 0), new Vector2(400, 116));
        Rect2 centerPanel = new(new Vector2(410, 0), new Vector2(210, 116));
        Rect2 rightPanel = new(new Vector2(630, 0), new Vector2(400, 116));

        DrawRect(leftPanel, panel, true);
        DrawRect(centerPanel, panel, true);
        DrawRect(rightPanel, panel, true);
        DrawRect(leftPanel, border, false, 3.0f);
        DrawRect(centerPanel, border, false, 3.0f);
        DrawRect(rightPanel, border, false, 3.0f);

        DrawHpBar(new Rect2(new Vector2(18, 76), new Vector2(364, 14)), _leftHp, _leftMaxHp, barBack, hpLeft);
        DrawHpBar(new Rect2(new Vector2(648, 76), new Vector2(364, 14)), _rightHp, _rightMaxHp, barBack, hpRight);

        if (_experienceUnit is not null)
        {
            int expValue = CurrentDisplayedExperience();
            DrawExperienceBar(
                new Rect2(new Vector2(425, 88), new Vector2(180, 10)),
                expValue,
                barBack,
                new Color(0.86f, 0.72f, 0.27f, 1.0f));

            // EXP 每 10 点增加一个小刻度，强化老式战棋 HUD 的离散读数感。
            for (int mark = 1; mark < 10; mark++)
            {
                float x = 425 + mark * 18;
                DrawLine(
                    new Vector2(x, 88),
                    new Vector2(x, 98),
                    new Color(0.15f, 0.13f, 0.10f, 0.72f),
                    1.0f);
            }
        }
    }

    /// <summary>创建 HUD 内部所有文本标签。</summary>
    private void CreateLabels()
    {
        _leftIdentityLabel = CreateLabel(new Vector2(16, 8), new Vector2(368, 54), HorizontalAlignment.Left, 18);
        AddChild(_leftIdentityLabel);

        _rightIdentityLabel = CreateLabel(new Vector2(646, 8), new Vector2(368, 54), HorizontalAlignment.Right, 18);
        AddChild(_rightIdentityLabel);

        _leftHpLabel = CreateLabel(new Vector2(18, 90), new Vector2(364, 22), HorizontalAlignment.Left, 16);
        AddChild(_leftHpLabel);

        _rightHpLabel = CreateLabel(new Vector2(648, 90), new Vector2(364, 22), HorizontalAlignment.Right, 16);
        AddChild(_rightHpLabel);

        _damageLabel = CreateLabel(new Vector2(420, 14), new Vector2(190, 40), HorizontalAlignment.Center, 20);
        _damageLabel.VerticalAlignment = VerticalAlignment.Center;
        AddChild(_damageLabel);

        _experienceLabel = CreateLabel(new Vector2(416, 54), new Vector2(198, 32), HorizontalAlignment.Center, 14);
        _experienceLabel.VerticalAlignment = VerticalAlignment.Center;
        AddChild(_experienceLabel);
    }

    /// <summary>创建统一颜色和字号的战斗 HUD 标签。</summary>
    private static Label CreateLabel(
        Vector2 position,
        Vector2 size,
        HorizontalAlignment alignment,
        int fontSize)
    {
        Label label = new()
        {
            Position = position,
            Size = size,
            HorizontalAlignment = alignment,
            MouseFilter = MouseFilterEnum.Ignore
        };
        label.AddThemeColorOverride("font_color", new Color(0.94f, 0.91f, 0.82f));
        label.AddThemeColorOverride("font_shadow_color", new Color(0.02f, 0.02f, 0.025f, 0.86f));
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
        label.AddThemeFontSizeOverride("font_size", fontSize);
        return label;
    }

    /// <summary>刷新双方 HP 数字和默认经验信息。</summary>
    private void RefreshLabels()
    {
        if (_leftHpLabel is not null)
        {
            _leftHpLabel.Text = _leftUnit is null ? string.Empty : $"HP {_leftHp}/{_leftMaxHp}";
        }

        if (_rightHpLabel is not null)
        {
            _rightHpLabel.Text = _rightUnit is null ? string.Empty : $"HP {_rightHp}/{_rightMaxHp}";
        }

        if (_experienceLabel is not null && !_showExperienceResult)
        {
            _experienceLabel.Text = _experienceUnit is null
                ? "EXP  --"
                : $"EXP {_experienceBefore}/100";
        }
    }

    /// <summary>刷新 EXP 动画阶段的文字，包括跨级时的 LEVEL UP 提示。</summary>
    private void RefreshExperienceLabel(bool finalFrame)
    {
        if (_experienceLabel is null || _experienceUnit is null)
        {
            return;
        }

        int displayedLevel = CurrentDisplayedLevel();
        int displayedExperience = CurrentDisplayedExperience();
        bool hasLeveled = displayedLevel > _experienceLevelBefore;

        if (hasLeveled)
        {
            _experienceLabel.Text = finalFrame
                ? $"LEVEL UP  Lv.{_experienceUnit.Level}  +{_experienceGained}"
                : $"LEVEL UP  Lv.{displayedLevel}  {displayedExperience}/100";
            _experienceLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.86f, 0.38f));
        }
        else
        {
            _experienceLabel.Text = finalFrame
                ? $"EXP +{_experienceGained}   {_experienceUnit.Experience}/100"
                : $"EXP +{_experienceGained}   {displayedExperience}/100";
            _experienceLabel.AddThemeColorOverride("font_color", new Color(0.94f, 0.91f, 0.82f));
        }
    }

    /// <summary>直接设置 EXP 文本并恢复普通颜色。</summary>
    private void SetExperienceLabel(string text)
    {
        if (_experienceLabel is null)
        {
            return;
        }

        _experienceLabel.Text = text;
        _experienceLabel.AddThemeColorOverride("font_color", new Color(0.94f, 0.91f, 0.82f));
    }

    /// <summary>绘制一条按当前生命比例缩放的硬边 HP 条。</summary>
    private void DrawHpBar(Rect2 rect, int hp, int maxHp, Color background, Color foreground)
    {
        DrawRect(rect, background, true);
        float ratio = Mathf.Clamp((float)hp / Math.Max(1, maxHp), 0.0f, 1.0f);
        int fillWidth = (int)MathF.Round(rect.Size.X * ratio);
        if (fillWidth > 0)
        {
            DrawRect(new Rect2(rect.Position, new Vector2(fillWidth, rect.Size.Y)), foreground, true);
        }
    }

    /// <summary>绘制 0~99 的玩家经验条。</summary>
    private void DrawExperienceBar(Rect2 rect, int experience, Color background, Color foreground)
    {
        DrawRect(rect, background, true);
        float ratio = Mathf.Clamp(Math.Clamp(experience, 0, 99) / 100.0f, 0.0f, 1.0f);
        int fillWidth = (int)MathF.Round(rect.Size.X * ratio);
        if (fillWidth > 0)
        {
            DrawRect(new Rect2(rect.Position, new Vector2(fillWidth, rect.Size.Y)), foreground, true);
        }
    }

    /// <summary>根据剩余生命比例选择绿、黄、红三段 HP 颜色。</summary>
    private static Color HpColor(int hp, int maxHp)
    {
        float ratio = Mathf.Clamp((float)hp / Math.Max(1, maxHp), 0.0f, 1.0f);
        if (ratio <= 0.25f)
        {
            return new Color(0.78f, 0.20f, 0.18f, 1.0f);
        }

        if (ratio <= 0.50f)
        {
            return new Color(0.86f, 0.70f, 0.20f, 1.0f);
        }

        return new Color(0.28f, 0.72f, 0.32f, 1.0f);
    }

    /// <summary>把等级和当前 EXP 转换成可以连续动画的总经验值。</summary>
    private static int ToTotalExperience(int level, int experience)
    {
        return Math.Max(0, level - 1) * 100 + Math.Clamp(experience, 0, 99);
    }

    /// <summary>返回当前 EXP 动画应该显示的等级。</summary>
    private int CurrentDisplayedLevel()
    {
        return Math.Max(1, _animatedExperienceTotal / 100 + 1);
    }

    /// <summary>返回当前 EXP 动画应该显示的 0~99 EXP。</summary>
    private int CurrentDisplayedExperience()
    {
        return Math.Clamp(_animatedExperienceTotal % 100, 0, 99);
    }

    /// <summary>
    /// 通过等级差与当前 EXP 计算本场实际获得经验。
    /// 每级固定需要 100 EXP，因此跨级也能正确累计。
    /// </summary>
    private int CalculateExperienceGain()
    {
        if (_experienceUnit is null)
        {
            return 0;
        }

        int levelDifference = Math.Max(0, _experienceUnit.Level - _experienceLevelBefore);
        int gained = levelDifference * 100 + _experienceUnit.Experience - _experienceBefore;
        return Math.Max(0, gained);
    }
}
