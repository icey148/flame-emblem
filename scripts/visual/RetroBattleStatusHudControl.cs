using FlameEmblem.Game;
using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 横向战斗演出底部的复古状态 HUD。
/// 该控件只消费已经结算好的战斗结果，按动画顺序重放 HP 变化，并在收尾阶段展示玩家获得的经验值。
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
    private int _experienceLevelBefore;

    /// <summary>玩家获得经验前的 EXP。</summary>
    private int _experienceBefore;

    /// <summary>当前是否已经进入战后经验展示阶段。</summary>
    private bool _showExperienceResult;

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

    /// <summary>中央经验值文字。</summary>
    private Label? _experienceLabel;

    /// <summary>
    /// 创建固定文字控件并使用最近邻画面策略。
    /// HUD 本身全部使用整数坐标矩形，不绘制圆角或抗锯齿装饰。
    /// </summary>
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        CreateLabels();
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
        _experienceLevelBefore = experienceLevelBefore;
        _experienceBefore = Math.Clamp(experienceBefore, 0, 99);
        _showExperienceResult = false;

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
    /// 战斗所有攻击结束后读取玩家当前等级/经验，和事件触发瞬间保存的旧值比较。
    /// MainGame 会在 ResolveExchange 返回以后发放经验，因此此时可以得到真实最终结果。
    /// </summary>
    public int ShowExperienceResult()
    {
        _showExperienceResult = true;
        int gained = CalculateExperienceGain();

        if (_experienceLabel is not null)
        {
            if (_experienceUnit is null)
            {
                _experienceLabel.Text = "EXP  --";
            }
            else if (_experienceUnit.Level > _experienceLevelBefore)
            {
                _experienceLabel.Text = $"LEVEL UP  Lv.{_experienceUnit.Level}   EXP +{gained}";
            }
            else
            {
                _experienceLabel.Text = $"EXP +{gained}   {_experienceUnit.Experience}/100";
            }
        }

        QueueRedraw();
        return gained;
    }

    /// <summary>清空中央结果信息，供下一场战斗重新使用。</summary>
    public void ResetBattle()
    {
        _leftUnit = null;
        _rightUnit = null;
        _experienceUnit = null;
        _showExperienceResult = false;
        _leftHp = 0;
        _rightHp = 0;

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
            int expValue = _showExperienceResult ? _experienceUnit.Experience : _experienceBefore;
            DrawExperienceBar(
                new Rect2(new Vector2(425, 88), new Vector2(180, 10)),
                expValue,
                barBack,
                new Color(0.86f, 0.72f, 0.27f, 1.0f));
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

        _experienceLabel = CreateLabel(new Vector2(420, 56), new Vector2(190, 30), HorizontalAlignment.Center, 14);
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
