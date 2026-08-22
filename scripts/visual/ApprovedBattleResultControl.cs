using Godot;

namespace FlameEmblem.Visual;

/// <summary>
/// 把现有战斗结果 Label 的文字重新排成最终定稿的金色文字与红色伤害数字。
/// 本控件只镜像源 Label.Text，不修改战斗结算、伤害值或时间线。
/// </summary>
public partial class ApprovedBattleResultControl : Control
{
    /// <summary>仍由 RetroBattleAnimationCoordinator 更新的原始结果文字。</summary>
    private Label? _sourceLabel;

    /// <summary>结果文字前半段。</summary>
    private Label? _prefixLabel;

    /// <summary>伤害数字；没有伤害数字时保持隐藏。</summary>
    private Label? _numberLabel;

    /// <summary>结果文字后半段。</summary>
    private Label? _suffixLabel;

    /// <summary>上一次已经同步过的原始文字。</summary>
    private string _lastText = string.Empty;

    /// <summary>最终界面主金色。</summary>
    private static readonly Color GoldLight = new("e0bd78");

    /// <summary>伤害数字使用的深红高亮。</summary>
    private static readonly Color DamageRed = new("d23b32");

    /// <summary>绑定原始 Label，并让原始 Label 只作为数据源存在。</summary>
    public void Bind(Label sourceLabel)
    {
        _sourceLabel = sourceLabel;
        _sourceLabel.Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
        SetProcess(true);
        EnsureLabels();
        RefreshText(force: true);
    }

    /// <summary>原协调器随战斗阶段修改 Text，因此每帧只在文字变化时重新拆分。</summary>
    public override void _Process(double delta)
    {
        RefreshText(force: false);
    }

    /// <summary>创建居中的三段结果文字。</summary>
    private void EnsureLabels()
    {
        if (_prefixLabel is not null)
        {
            return;
        }

        HBoxContainer row = new()
        {
            Name = "ApprovedResultTextRow",
            Position = Vector2.Zero,
            Size = ReferenceBattleLayout.ResultLabelMinimumSize,
            CustomMinimumSize = ReferenceBattleLayout.ResultLabelMinimumSize,
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(row);

        _prefixLabel = CreateSegmentLabel(30, GoldLight);
        _numberLabel = CreateSegmentLabel(38, DamageRed);
        _suffixLabel = CreateSegmentLabel(30, GoldLight);

        row.AddChild(_prefixLabel);
        row.AddChild(_numberLabel);
        row.AddChild(_suffixLabel);
    }

    /// <summary>创建一段统一字体、居中高度和黑色阴影的文字。</summary>
    private static Label CreateSegmentLabel(int fontSize, Color color)
    {
        Label label = new()
        {
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        label.AddThemeFontOverride("font", BattlePixelFontCatalog.Font);
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_shadow_color", Colors.Black);
        label.AddThemeConstantOverride("shadow_offset_x", 2);
        label.AddThemeConstantOverride("shadow_offset_y", 2);
        return label;
    }

    /// <summary>同步源文字；只有“造成 N 伤害”结构会把 N 独立变成红色大数字。</summary>
    private void RefreshText(bool force)
    {
        if (_sourceLabel is null || _prefixLabel is null || _numberLabel is null || _suffixLabel is null)
        {
            return;
        }

        string text = _sourceLabel.Text ?? string.Empty;
        if (!force && text == _lastText)
        {
            return;
        }

        _lastText = text;
        if (!TrySplitDamageText(text, out string prefix, out string number, out string suffix))
        {
            _prefixLabel.Text = text;
            _numberLabel.Text = string.Empty;
            _suffixLabel.Text = string.Empty;
            return;
        }

        _prefixLabel.Text = prefix;
        _numberLabel.Text = number;
        _suffixLabel.Text = suffix;
    }

    /// <summary>从结果文字中找到“造成”之后的第一个连续数字区间。</summary>
    private static bool TrySplitDamageText(string text, out string prefix, out string number, out string suffix)
    {
        prefix = text;
        number = string.Empty;
        suffix = string.Empty;

        int damageWordIndex = text.IndexOf("造成", StringComparison.Ordinal);
        if (damageWordIndex < 0)
        {
            return false;
        }

        int searchStart = damageWordIndex + 2;
        int numberStart = -1;
        for (int index = searchStart; index < text.Length; index++)
        {
            if (char.IsDigit(text[index]))
            {
                numberStart = index;
                break;
            }
        }

        if (numberStart < 0)
        {
            return false;
        }

        int numberEnd = numberStart;
        while (numberEnd < text.Length && char.IsDigit(text[numberEnd]))
        {
            numberEnd++;
        }

        prefix = text[..numberStart];
        number = text[numberStart..numberEnd];
        suffix = text[numberEnd..];
        return true;
    }
}
