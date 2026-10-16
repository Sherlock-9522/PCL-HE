using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PCL;

/// <summary>
///     PCL-DSH 开屏动画窗口（原创实现）。
///     替换 PCL CE 原生的静态 <see cref="System.Windows.SplashScreen" />，提供带动画的开屏体验。
///     本类为 PCL-DSH 项目的独立第三方添加内容，不包含 PCL CE 的既有实现。
/// </summary>
public partial class FormSplash
{
    /// <summary>
    ///     开屏的最短可见时长（毫秒）。启动器在快机器上可能几十毫秒内就完成主窗体加载，
    ///     若此时立刻关闭开屏，入场动画会被整体跳过、用户只看到一闪。这里保证动画至少
    ///     播完一轮关键节点（logo 弹入 → 标题浮现 → 光带扫掠起势），又不至于明显拖慢启动。
    /// </summary>
    private const double MinVisibleMs = 900d;

    private enum EaseKind
    {
        OutCubic,
        OutBack
    }

    private volatile bool _isFading;
    private bool _isIntroPlayed;
    private DateTime _shownUtc = DateTime.UtcNow;

    public FormSplash()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            _shownUtc = DateTime.UtcNow;
            PlayIntro();
            StartFrameCaptureIfRequested();
        };
    }

    /// <summary>
    ///     播放开屏入场动画。仅在窗口首次加载时执行一次。
    /// </summary>
    private void PlayIntro()
    {
        if (_isIntroPlayed) return;
        _isIntroPlayed = true;

        try
        {
            var speed = GetAnimationSpeed();

            // 整体淡入 + 轻微放大（柔和，不使用回弹，避免「砸」出来的突兀感）
            Anim(RootGrid, UIElement.OpacityProperty, 0d, 1d, 260d, 0d, speed, EaseKind.OutCubic);
            Anim(RootScale, ScaleTransform.ScaleXProperty, 0.96d, 1d, 420d, 0d, speed, EaseKind.OutCubic);
            Anim(RootScale, ScaleTransform.ScaleYProperty, 0.96d, 1d, 420d, 0d, speed, EaseKind.OutCubic);

            // Logo 上浮 + 淡入
            Anim(LogoBox, UIElement.OpacityProperty, 0d, 1d, 400d, 140d, speed, EaseKind.OutCubic);
            Anim(LogoShift, TranslateTransform.YProperty, 10d, 0d, 520d, 140d, speed, EaseKind.OutCubic);

            // 主标题、副标题依次浮现
            Anim(LabTitle, UIElement.OpacityProperty, 0d, 1d, 320d, 380d, speed, EaseKind.OutCubic);
            Anim(LabSub, UIElement.OpacityProperty, 0d, 1d, 320d, 520d, speed, EaseKind.OutCubic);

            // 进度条浮现，光带无限循环扫掠
            Anim(ProgressTrack, UIElement.OpacityProperty, 0d, 1d, 300d, 620d, speed, EaseKind.OutCubic);
            ProgressShift.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation
            {
                From = -100d,
                To = 200d,
                Duration = new Duration(TimeSpan.FromMilliseconds(1400d / speed)),
                BeginTime = TimeSpan.FromMilliseconds(700d / speed),
                RepeatBehavior = RepeatBehavior.Forever
            });
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "播放开屏动画失败，已直接显示静态开屏", ModBase.LogLevel.Hint);
            TryDumpIntroFailure(ex);
            // 兜底：至少让窗口可见，不影响启动流程
            RootGrid.Opacity = 1d;
            LogoBox.Opacity = 1d;
            LabTitle.Opacity = 1d;
            LabSub.Opacity = 1d;
            ProgressTrack.Opacity = 1d;
        }
    }

    /// <summary>
    ///     诊断用：当环境变量 <c>DSH_SPLASH_SHOT</c> 指向一个目录时，把开屏动画的若干帧
    ///     渲染为 PNG 落盘，随后结束进程。仅用于自动化视觉验证；正常启动时该变量不存在，
    ///     本方法立即返回，不产生任何开销与副作用。
    /// </summary>
    private void StartFrameCaptureIfRequested()
    {
        var dir = Environment.GetEnvironmentVariable("DSH_SPLASH_SHOT");
        if (string.IsNullOrWhiteSpace(dir)) return;

        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "anispeed.txt"),
                $"raw={RawAnimationSpeed()} effective={GetAnimationSpeed()}");
        }
        catch
        {
            return;
        }

        // 诊断模式下改用不透明底色，消除 alpha 合成对帧分析的干扰
        RootGrid.Background = new SolidColorBrush(Color.FromRgb(0x12, 0x16, 0x1E));

        double[] checkpoints = [0d, 80d, 180d, 300d, 420d, 560d, 700d, 850d];
        var index = 0;
        var started = DateTime.UtcNow;
        var timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(30d) };

        timer.Tick += (_, _) =>
        {
            if (index >= checkpoints.Length)
            {
                timer.Stop();
                try
                {
                    Application.Current?.Shutdown();
                }
                catch
                {
                    // 进程已在退出流程中，忽略
                }

                return;
            }

            if ((DateTime.UtcNow - started).TotalMilliseconds < checkpoints[index]) return;

            SaveFrame(Path.Combine(dir, $"frame{index}_{(int)checkpoints[index]}ms.png"));
            TryDumpProperties(dir, checkpoints[index]);
            index++;
        };

        timer.Start();
    }

    /// <summary>
    ///     把当前窗口的视觉树渲染为 PNG。使用 <see cref="RenderTargetBitmap" />，
    ///     因此不依赖桌面会话与屏幕捕获权限。
    /// </summary>
    private void SaveFrame(string path)
    {
        try
        {
            UpdateLayout();

            var width = (int)Math.Ceiling(ActualWidth);
            var height = (int)Math.Ceiling(ActualHeight);
            if (width <= 0 || height <= 0) return;

            var bitmap = new RenderTargetBitmap(width, height, 96d, 96d, PixelFormats.Pbgra32);
            bitmap.Render(this);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using var stream = File.Create(path);
            encoder.Save(stream);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "导出开屏动画帧失败", ModBase.LogLevel.Hint);
        }
    }

    /// <summary>
    ///     诊断用：记录该采样点上关键动画属性的实际值，用于判断动画是否真正在逐帧推进。
    /// </summary>
    private void TryDumpProperties(string dir, double ms)
    {
        try
        {
            File.AppendAllText(Path.Combine(dir, "props.txt"),
                $"{ms}ms root={RootGrid.Opacity:F3} " +
                $"logo={LogoBox.Opacity:F3} shiftY={LogoShift.Y:F2} title={LabTitle.Opacity:F3} " +
                $"sub={LabSub.Opacity:F3} track={ProgressTrack.Opacity:F3} win={Opacity:F3} " +
                $"fading={_isFading}" + Environment.NewLine);
        }
        catch
        {
            // 诊断写入失败不影响启动流程
        }
    }

    /// <summary>
    ///     诊断用：把入场动画的异常原文写入帧导出目录，便于自动化排查。
    /// </summary>
    private static void TryDumpIntroFailure(Exception ex)
    {
        try
        {
            var dir = Environment.GetEnvironmentVariable("DSH_SPLASH_SHOT");
            if (string.IsNullOrWhiteSpace(dir)) return;
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "intro-failed.txt"), ex + Environment.NewLine);
        }
        catch
        {
            // 诊断写入失败不影响启动流程
        }
    }

    /// <summary>
    ///     淡出并关闭开屏窗口。语义等价于原生 <c>SplashScreen.Close(TimeSpan)</c>，
    ///     供主窗体就绪后调用；若动画尚未播满 <see cref="MinVisibleMs" />，则延后到播满再淡出。
    /// </summary>
    public void FadeOutAndClose(TimeSpan duration)
    {
        if (_isFading) return;
        _isFading = true;

        var waitMs = MinVisibleMs - (DateTime.UtcNow - _shownUtc).TotalMilliseconds;
        if (waitMs <= 1d)
        {
            BeginFadeOut(duration);
            return;
        }

        try
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(waitMs) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                BeginFadeOut(duration);
            };
            timer.Start();
        }
        catch
        {
            BeginFadeOut(duration);
        }
    }

    private void BeginFadeOut(TimeSpan duration)
    {
        if (!IsLoaded)
        {
            CloseSafely();
            return;
        }

        var ms = duration.TotalMilliseconds;
        if (double.IsNaN(ms) || double.IsInfinity(ms) || ms < 16d) ms = 200d;
        ms = Math.Min(ms, 3000d);

        try
        {
            var fade = new DoubleAnimation
            {
                From = Opacity,
                To = 0d,
                Duration = new Duration(TimeSpan.FromMilliseconds(ms)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            fade.Completed += (_, _) => CloseSafely();
            BeginAnimation(OpacityProperty, fade);
        }
        catch
        {
            CloseSafely();
        }
    }

    private void CloseSafely()
    {
        try
        {
            Close();
        }
        catch
        {
            // 窗口可能已被关闭，忽略
        }
    }

    /// <summary>
    ///     对单个目标属性施加一段补间动画。
    ///     使用 <see cref="IAnimatable.BeginAnimation" /> 直接施加，而非 Storyboard：
    ///     Storyboard 依赖目标解析，实测在本窗口上会把动画直接推到终值、导致入场动画被跳过。
    /// </summary>
    private static void Anim(DependencyObject target, DependencyProperty prop,
        double from, double to, double ms, double delayMs, double speed, EaseKind ease)
    {
        if (target is not IAnimatable animatable) return;

        var anim = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = new Duration(TimeSpan.FromMilliseconds(Math.Max(1d, ms / speed))),
            BeginTime = TimeSpan.FromMilliseconds(Math.Max(0d, delayMs / speed)),
            EasingFunction = ease switch
            {
                EaseKind.OutBack => new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.55d },
                _ => new CubicEase { EasingMode = EasingMode.EaseOut }
            },
            FillBehavior = FillBehavior.HoldEnd
        };

        animatable.BeginAnimation(prop, anim);
    }

    /// <summary>
    ///     读取启动器的动画速度设置，使开屏动画与全局动画速度保持一致。
    /// </summary>
    private static double GetAnimationSpeed()
    {
        try
        {
            var speed = ModAnimation.aniSpeed;
            if (double.IsNaN(speed) || double.IsInfinity(speed) || speed <= 0.01d) return 1d;
            return Math.Clamp(speed, 0.2d, 5d);
        }
        catch
        {
            return 1d;
        }
    }

    /// <summary>
    ///     诊断用：返回未经处理的动画速度原值，便于排查开屏节奏问题。
    /// </summary>
    private static double RawAnimationSpeed()
    {
        try
        {
            return ModAnimation.aniSpeed;
        }
        catch
        {
            return double.NaN;
        }
    }
}
