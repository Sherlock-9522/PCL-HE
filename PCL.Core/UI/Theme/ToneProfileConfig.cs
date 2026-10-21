namespace PCL.Core.UI.Theme;

public record ToneProfileConfig
{
    public static readonly ToneProfile DefaultLight = new();

    /// <summary>
    /// 深色主题的配色模板。<br/>
    /// C5 与 C7 决定背景渐变（ColorBrushBg0 / ColorBrushBg1）的两个端点，这里把它们归零，
    /// 于是深色主题的底色与渐变是中性灰黑，而 C2~C4 仍沿用主题色，强调元素不会跟着变灰。
    /// </summary>
    public static readonly ToneProfile DefaultDark = new(
        L1: 0.96, L2: 0.75, L3: 0.6, L4: 0.65,
        L5: 0.55, L6: 0.3, L7: 0.3, L8: 0.22,
        LBackground: 0.3, LForeground: 1, LWhite: 0.275,
        C5: 0, C7: 0
    );

    public ToneProfile Light { get => field ?? DefaultLight; init; } = null!;

    public ToneProfile Dark { get => field ?? DefaultDark; init; } = null!;
}
