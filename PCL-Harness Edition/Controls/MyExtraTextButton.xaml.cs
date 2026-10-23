using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using PCL.Core.App;
using PCL.Core.UI.Theme;

namespace PCL;

[ContentProperty("Inlines")]
public partial class MyExtraTextButton
{
    public delegate void ClickEventHandler(object sender, MouseButtonEventArgs e); // 自定义事件

    // 自定义事件
    // 务必放在 IsMouseDown 更新之后
    private const int animationColorIn = 120;
    private const int animationColorOut = 150;

    // 实心强调主题：浅色模式蓝底白字，深色模式黑底白字，跟随启动器深浅模式自动切换。
    // 开启后底色改用 Application.xaml 里的 ColorBrushSolidBack* 固定色组，不再参与主题色计算。
    public static readonly DependencyProperty SolidThemeProperty = DependencyProperty.Register(
        nameof(SolidTheme), typeof(bool), typeof(MyExtraTextButton),
        new PropertyMetadata(false, (sender, e) => ((MyExtraTextButton)sender).RefreshColor()));

    public bool SolidTheme
    {
        get => (bool)GetValue(SolidThemeProperty);
        set => SetValue(SolidThemeProperty, value);
    }

    // 字体族穿透：本控件不是 Control，没有 FontFamily，只能手动透传给 LabText。
    public static readonly DependencyProperty TextFontFamilyProperty = DependencyProperty.Register(
        nameof(TextFontFamily), typeof(FontFamily), typeof(MyExtraTextButton),
        new PropertyMetadata(null,
            (sender, e) => ((MyExtraTextButton)sender).LabText.FontFamily = (FontFamily)e.NewValue));

    public FontFamily TextFontFamily
    {
        get => (FontFamily)GetValue(TextFontFamilyProperty);
        set => SetValue(TextFontFamilyProperty, value);
    }

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string),
        typeof(MyExtraTextButton), new PropertyMetadata((sender, e) =>
        {
            ((MyExtraTextButton)sender)?.LabText.Text = (string)e.NewValue;
        }));

    // 鼠标点击判定（务必放在点击事件之后，以使得 Button_MouseUp 先于 Button_MouseLeave 执行）
    private bool isLeftMouseHeld;

    // 自定义属性
    public int Uuid = ModBase.GetUuid();

    public MyExtraTextButton()
    {
        InitializeComponent();
        RefreshIconHostVisibility();

        Loaded += (_, _) => RefreshColor();
        IsEnabledChanged += (_, _) => RefreshColor();
        // 主题是静态事件，必须成对注销；Loaded 可能重复触发，故用标志位防止重复订阅。
        Loaded += (_, _) =>
        {
            if (themeHooked) return;
            themeHooked = true;
            ThemeService.ColorModeChanged += OnColorModeChanged;
        };
        Unloaded += (_, _) =>
        {
            themeHooked = false;
            ThemeService.ColorModeChanged -= OnColorModeChanged;
        };
        PanClick.MouseLeftButtonDown += Button_LeftMouseDown;
        PanClick.MouseLeftButtonUp += Button_LeftMouseUp;
        PanClick.MouseLeave += Button_MouseLeave;
        PanClick.MouseRightButtonUp += Button_RightMouseUp;
        PanClick.MouseEnter += (sender, e) => RefreshColor();

        // 浮钮不留描边：模板里那圈白色渐变描边（模拟顶部打光）在高饱和实心底色上会显得脏。
        // 只置空 BorderBrush、不动 BorderThickness，避免 Border 的布局让内容位移 2px。
        PanColor.BorderBrush = null;
    }

    public string Logo
    {
        get;
        set
        {
            if ((value ?? "") == (field ?? ""))
                return;
            field = value ?? string.Empty;
            Path.Data = string.IsNullOrWhiteSpace(value)
                ? null
                : (Geometry)new GeometryConverter().ConvertFromString(value);
            SvgIconControlHelper.ApplyVisibility(Path, ShapeSvgIcon, IsUsingSvgIcon);
            RefreshIconHostVisibility();
        }
    } = string.Empty;

    public string SvgIcon
    {
        get;
        set
        {
            value ??= string.Empty;
            if (value == field)
                return;
            field = value;
            if (Path is null || ShapeSvgIcon is null)
                return;
            SvgIconControlHelper.ApplyIcon(Path, ShapeSvgIcon, field);
            ApplyLogoScale();
            RefreshIconHostVisibility();
        }
    } = string.Empty;

    private bool IsUsingSvgIcon => SvgIconControlHelper.HasSvgIcon(SvgIcon);

    private double EffectiveLogoScale => IsUsingSvgIcon ? 1D : LogoScale;

    private bool HasAnyIcon => IsUsingSvgIcon || !string.IsNullOrWhiteSpace(Logo);

    private void ApplyLogoScale()
    {
        IconHost?.RenderTransform = new ScaleTransform
        {
            ScaleX = EffectiveLogoScale,
            ScaleY = EffectiveLogoScale
        };
    }

    private void RefreshIconHostVisibility()
    {
        if (IconHost is null || LabText is null)
            return;

        if (HasAnyIcon)
        {
            IconHost.Visibility = Visibility.Visible;
            IconHost.Width = 16;
            IconHost.Margin = new Thickness(2, 12, 0, 12);
            LabText.Margin = new Thickness(12, 0, 0, 0.8);
        }
        else
        {
            IconHost.Visibility = Visibility.Collapsed;
            IconHost.Width = 0;
            IconHost.Margin = new Thickness(0, 12, 0, 12);
            LabText.Margin = new Thickness(0, 0, 0, 0.8);
        }
    }

    public double LogoScale
    {
        get;
        set
        {
            field = value;
            ApplyLogoScale();
        }
    } = 1d;

    // 显示文本
    public InlineCollection Inlines => LabText.Inlines;

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set
        {
            if (value is null) return;
            SetValue(TextProperty, value);
        }
    }

    public bool Show
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            ModBase.RunInUi(() =>
            {
                if (value)
                {
                    // 有了
                    Opacity = 0d;
                    ModAnimation.AniStart(
                        new[]
                        {
                            ModAnimation.AaOpacity(this, 1d - Opacity, 80, 50),
                            ModAnimation.AaScaleTransform(this, 0.15d - ((ScaleTransform)RenderTransform).ScaleX, 400,
                                50, new ModAnimation.AniEaseOutBack()),
                            ModAnimation.AaScaleTransform(this, 0.85d, 160, 50, new ModAnimation.AniEaseOutFluent())
                        }, "MyExtraTextButton MainScale " + Uuid);
                }
                else
                {
                    // 没了
                    ModAnimation.AniStart(
                        new[]
                        {
                            ModAnimation.AaOpacity(this, -Opacity, 50, 50),
                            ModAnimation.AaScaleTransform(this, -((ScaleTransform)RenderTransform).ScaleX, 100,
                                ease: new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak))
                        }, "MyExtraTextButton MainScale " + Uuid);
                }

                IsHitTestVisible = value; // 防止缩放动画中依然可以点进去
            });
        }
    }

    // 声明
    public event ClickEventHandler? Click;

    private void StartScaleAnimation(double targetScale, double reboundScale, int reboundDuration = 60)
    {
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaScaleTransform(PanScale, targetScale - ((ScaleTransform)PanScale.RenderTransform).ScaleX,
                    800, ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong)),
                ModAnimation.AaScaleTransform(PanScale, reboundScale, reboundDuration,
                    ease: new ModAnimation.AniEaseOutFluent())
            }, "MyExtraTextButton Scale " + Uuid);
    }

    private void RefreshScaleAfterRelease()
    {
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaScaleTransform(PanScale, 1d - ((ScaleTransform)PanScale.RenderTransform).ScaleX, 300,
                    ease: new ModAnimation.AniEaseOutBack())
            }, "MyExtraTextButton Scale " + Uuid);
    }

    // 触发点击事件
    private void Button_LeftMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!isLeftMouseHeld) return;
        ModBase.Log("[Control] 按下附加图标按钮：" + Text);
        Click?.Invoke(sender, e);
        e.Handled = true;
        ModMain.RaiseCustomEvent(this);
        Button_LeftMouseUp();
    }

    private void Button_LeftMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!isLeftMouseHeld)
            StartScaleAnimation(0.85d, -0.05d);
        isLeftMouseHeld = true;
        Focus();
    }

    private void Button_LeftMouseUp()
    {
        RefreshScaleAfterRelease();
        isLeftMouseHeld = false;
        RefreshColor(); // 直接刷新颜色以判断是否已触发 MouseLeave
    }

    private void Button_RightMouseUp(object sender, MouseEventArgs e)
    {
        if (!isLeftMouseHeld)
            RefreshScaleAfterRelease();
        RefreshColor(); // 直接刷新颜色以判断是否已触发 MouseLeave
    }

    private void Button_MouseLeave(object sender, MouseEventArgs e)
    {
        isLeftMouseHeld = false;
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaScaleTransform(PanScale, 1d - ((ScaleTransform)PanScale.RenderTransform).ScaleX, 500,
                    ease: new ModAnimation.AniEaseOutFluent())
            }, "MyExtraTextButton Scale " + Uuid);
        RefreshColor(); // 直接刷新颜色以判断是否已触发 MouseLeave
    }

    private bool themeHooked;

    private void OnColorModeChanged(bool isDarkMode, ColorTheme theme)
    {
        if (!SolidTheme) return;
        Dispatcher.Invoke(() => RefreshColor());
    }

    // 实心态下的底色资源键：浅色模式蓝底白字，深色模式黑底白字，跟随启动器深浅模式自动切换。
    // 深色黑底取窗口标题栏色（ColorBrushTitleBar #20242C），悬停取标题栏描边色（#3A4049），与标题栏同色系。
    private string GetSolidBackgroundResourceKey()
    {
        if (!IsEnabled) return "ColorBrushHalfWhite";
        return ThemeManager.IsDarkMode
            ? (IsMouseOver ? "ColorBrushSolidBackDarkHover" : "ColorBrushSolidBackDark")
            : (IsMouseOver ? "ColorBrushSolidBackHover" : "ColorBrushSolidBack");
    }

    // 当前状态应使用的底色资源键：实心态走固定色组，其余沿用主题色组。
    private string GetBackgroundResourceKey()
    {
        if (SolidTheme) return GetSolidBackgroundResourceKey();
        return !IsEnabled ? "ColorBrushGray4" : IsMouseOver ? "ColorBrush4" : "ColorBrush3";
    }

    public void RefreshColor()
    {
        try
        {
            var colorKey = GetBackgroundResourceKey();
            // 离开与禁用用进入时长，指向用进入时长，回到普通态用退出时长
            var colorDuration = IsEnabled && !IsMouseOver ? animationColorOut : animationColorIn;
            if (ControlVisualHelpers.ShouldAnimate(this)) // 防止默认属性变更触发动画
            {
                ModAnimation.AniStart(
                    ModAnimation.AaColor(PanColor, BackgroundProperty, colorKey, colorDuration),
                    "MyExtraTextButton Color " + Uuid);
            }

            else
            {
                ControlVisualHelpers.AnimateColorOrSetResource(PanColor, BackgroundProperty, colorKey,
                    colorDuration, "MyExtraTextButton Color " + Uuid, false);
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "刷新附加图标按钮颜色出错");
        }
    }
}