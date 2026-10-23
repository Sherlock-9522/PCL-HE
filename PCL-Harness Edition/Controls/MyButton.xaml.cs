using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using PCL.Core.App;
using PCL.Core.UI.Theme;

namespace PCL;

[ContentProperty("Inlines")]
public partial class MyButton
{
    public delegate void ClickEventHandler(object sender, MouseButtonEventArgs e); // 自定义事件

    public enum ColorState
    {
        Normal = 0,
        Highlight = 1,
        Red = 2,

        /// <summary>
        /// 实心强调按钮：浅色模式蓝底白字，深色模式黑底白字，随启动器深浅模式自动切换。
        /// </summary>
        Solid = 3
    }

    // 自定义事件
    private const int animationColorIn = 100;
    private const int animationColorOut = 200;

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string),
        typeof(MyButton), new PropertyMetadata((sender, e) =>
        {
            if (sender is not null) ((MyButton)sender).LabText.Text = (string)e.NewValue;
        }));

    // 属性穿透
    public new static readonly DependencyProperty PaddingProperty = DependencyProperty.Register("Padding",
        typeof(Thickness), typeof(MyButton), new PropertyMetadata((sender, e) =>
        {
            if (sender is not null) ((MyButton)sender).PanFore.Padding = (Thickness)e.NewValue;
        }));

    public static readonly DependencyProperty TextFontFamilyProperty = DependencyProperty.Register("TextFontFamily",
        typeof(FontFamily), typeof(MyButton), new PropertyMetadata((sender, e) =>
        {
            if (sender is not null) ((MyButton)sender).LabText.FontFamily = (FontFamily)e.NewValue;
        }));

    public static readonly DependencyProperty TextFontWeightProperty = DependencyProperty.Register("TextFontWeight",
        typeof(FontWeight), typeof(MyButton), new PropertyMetadata((sender, e) =>
        {
            if (sender is not null) ((MyButton)sender).LabText.FontWeight = (FontWeight)e.NewValue;
        }));
    
    private ColorState _ColorType = ColorState.Normal; // 配色方案

    // 鼠标点击判定（务必放在点击事件之后，以使得 Button_MouseUp 先于 Button_MouseLeave 执行）
    

    // 自定义属性
    public int Uuid = ModBase.GetUuid();

    public MyButton()
    {
        InitializeComponent();

        MouseEnter += RefreshColor;
        MouseLeave += RefreshColor;
        Loaded += RefreshColor;
        IsEnabledChanged += (_, _) => RefreshColor();
        MouseLeftButtonUp += Button_MouseUp;
        MouseLeftButtonDown += Button_MouseDown;
        MouseEnter += (_, _) => Button_MouseEnter();
        MouseLeftButtonUp += (_, _) => Button_MouseUp();
        MouseLeave += (_, _) => Button_MouseLeave();
        Loaded += (_, _) => ThemeService.ColorModeChanged += OnColorModeChanged;
        Unloaded += (_, _) => ThemeService.ColorModeChanged -= OnColorModeChanged;
    }

    /// <summary>
    /// 实心按钮的取色依赖启动器深浅模式，主题变化时需要重新取色。
    /// ThemeService.ColorModeChanged 是静态事件，必须成对注销，否则会永久持有控件并泄漏整棵可视树。
    /// </summary>
    private void OnColorModeChanged(bool isDarkMode, ColorTheme theme)
    {
        if (_ColorType != ColorState.Solid)
            return;
        Dispatcher.Invoke(() => RefreshColor());
    }

    public InlineCollection Inlines => LabText.Inlines;

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    } // 显示文本

    public Thickness TextPadding
    {
        get => LabText.Padding;
        set => LabText.Padding = value;
    }

    public FontFamily TextFontFamily
    {
        get => (FontFamily)GetValue(TextFontFamilyProperty);
        set => SetValue(TextFontFamilyProperty, value);
    } // 字体

    public FontWeight TextFontWeight
    {
        get => (FontWeight)GetValue(TextFontWeightProperty);
        set => SetValue(TextFontWeightProperty, value);
    } // 字重

    public ColorState ColorType
    {
        get => _ColorType;
        set
        {
            _ColorType = value;
            RefreshColor();
        }
    }

    public new Thickness Padding
    {
        get => PanFore.Padding;
        set => PanFore.Padding = value;
    }

    public Transform RealRenderTransform
    {
        get => PanFore.RenderTransform;
        set => PanFore.RenderTransform = value;
    }

    // 声明
    public event ClickEventHandler? Click;

    private string GetBorderBrushResourceKey()
    {
        return ColorType switch
        {
            ColorState.Normal => IsMouseOver ? "ColorBrush3" : "ColorBrush1",
            ColorState.Highlight => IsMouseOver ? "ColorBrush3" : "ColorBrush2",
            ColorState.Red => IsMouseOver ? "ColorBrushRedLight" : "ColorBrushRedDark",
            // 实心按钮：文字色跟随边框色，因此这里取纯白；1px 边框本身由 BorderThickness=0 隐去
            ColorState.Solid => "ColorBrushSolidFore",
            _ => "ColorBrush1"
        };
    }

    /// <summary>
    /// 实心按钮的底色：浅色模式蓝底、深色模式黑底，文字色由 GetBorderBrushResourceKey 给出（纯白）。
    /// </summary>
    private string GetSolidBackgroundResourceKey(bool hover)
    {
        return ThemeManager.IsDarkMode
            ? hover ? "ColorBrushSolidBackDarkHover" : "ColorBrushSolidBackDark"
            : hover ? "ColorBrushSolidBackHover" : "ColorBrushSolidBack";
    }

    private void RefreshSolidBackground()
    {
        ModAnimation.AniStop("MyButton Background " + Uuid);
        PanFore.SetResourceReference(BackgroundProperty,
            IsEnabled ? GetSolidBackgroundResourceKey(false) : "ColorBrushHalfWhite");
    }

    private void StartBorderBrushAnimation(string resourceKey, int duration)
    {
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaColor(PanFore, BorderBrushProperty, resourceKey, duration)
            }, "MyButton Color " + Uuid);
    }

    private void RefreshColor(object obj = null, object e = null)
    {
        try
        {
            if (_ColorType == ColorState.Solid)
            {
                // 实心按钮：不显示 1px 边框，边框色只作为纯白文字色使用
                PanFore.BorderThickness = new Thickness(0);
                if (!IsMouseOver)
                    RefreshSolidBackground();
            }
            else
            {
                PanFore.BorderThickness = new Thickness(1);
            }

            if (ControlVisualHelpers.ShouldAnimate(this)) // 防止默认属性变更触发动画
            {
                if (IsEnabled)
                    StartBorderBrushAnimation(GetBorderBrushResourceKey(), IsMouseOver ? animationColorIn : animationColorOut);
                else
                    // 不可用（Gray 4）
                    ModAnimation.AniStart(
                        new[]
                        {
                            ModAnimation.AaColor(PanFore, BorderBrushProperty,
                                ThemeManager.colorGray4 - PanFore.BorderBrush, animationColorOut)
                        }, "MyButton Color " + Uuid);
            }
            else
            {
                ModAnimation.AniStop("MyButton Color " + Uuid);
                if (IsEnabled)
                    PanFore.SetResourceReference(BorderBrushProperty, GetBorderBrushResourceKey());
                else
                    PanFore.BorderBrush = ThemeManager.colorGray4;
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "刷新按钮颜色出错");
        }
    }

    // 实现自定义事件
    private bool isMouseDown = false;
    private void Button_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!isMouseDown)
            return;
        ModBase.Log("[Control] 按下按钮：" + Text);
        Click?.Invoke(sender, e);
        ModMain.RaiseCustomEvent(this);
    }

    private void Button_MouseDown(object sender, MouseButtonEventArgs e)
    {
        isMouseDown = true;
        Focus();
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaScaleTransform(PanFore, 0.955d - ((ScaleTransform)PanFore.RenderTransform).ScaleX, 80,
                    ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.ExtraStrong)),
                ModAnimation.AaScaleTransform(PanFore, -0.01d, 700, ease: new ModAnimation.AniEaseOutFluent())
            }, "MyButton Scale " + Uuid);
    }

    private void Button_MouseEnter()
    {
        ModAnimation.AniStart(
            ModAnimation.AaColor(PanFore, BackgroundProperty,
                _ColorType == ColorState.Solid
                    ? GetSolidBackgroundResourceKey(true)
                    : _ColorType == ColorState.Red
                        ? "ColorBrushRedBack"
                        : "ColorBrush7",
                animationColorIn),
            "MyButton Background " + Uuid);
    }

    private void Button_MouseUp()
    {
        if (!isMouseDown)
            return;
        isMouseDown = false;
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaScaleTransform(PanFore, 1d - ((ScaleTransform)PanFore.RenderTransform).ScaleX, 300, 10,
                    new ModAnimation.AniEaseOutFluent())
            }, "MyButton Scale " + Uuid);
    }

    private void Button_MouseLeave()
    {
        ModAnimation.AniStart(
            ModAnimation.AaColor(PanFore, BackgroundProperty,
                _ColorType == ColorState.Solid ? GetSolidBackgroundResourceKey(false) : "ColorBrushHalfWhite",
                animationColorOut),
            "MyButton Background " + Uuid);
        if (!isMouseDown)
            return;
        isMouseDown = false;
        ModAnimation.AniStart(
            ModAnimation.AaScaleTransform(PanFore, 1d - ((ScaleTransform)PanFore.RenderTransform).ScaleX, 800,
                ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong)), "MyButton Scale " + Uuid);
    }
}
