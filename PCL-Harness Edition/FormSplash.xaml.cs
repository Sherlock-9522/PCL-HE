using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PCL;

/// <summary>
///     PCL-HE 开屏动画窗口（原创实现）。
///     替换 PCL CE 原生的静态 <see cref="System.Windows.SplashScreen" />，提供带动画的开屏体验。
///     本类为 PCL-HE 项目的独立第三方添加内容，不包含 PCL CE 的既有实现。
/// </summary>
public partial class FormSplash
{
    /// <summary>
    ///     开屏的最短可见时长（毫秒）。启动器在快机器上可能几十毫秒内就完成主窗体加载，
    ///     若此时立刻关闭开屏，入场动画会被整体跳过、用户只看到一闪。这里保证动画至少
    ///     播完一轮关键节点（logo 弹入 → 标题浮现 → 光带扫掠起势），又不至于明显拖慢启动。
    /// </summary>
    private const double MinVisibleMs = 700d;

    private enum EaseKind
    {
        OutCubic,
        OutBack
    }

    private volatile bool _isFading;
    private bool _isIntroPlayed;
    private DateTime _shownUtc = DateTime.UtcNow;
    private readonly DateTime _createdUtc = DateTime.UtcNow;

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

            // 启动链和主窗体首次布局会独占 UI 线程数百毫秒，这段时间里任何补间都不会出帧，
            // 入场动画只会在阻塞结束后一次性快进到终值，看起来就是「一闪而过」。
            // 这种情况直接保留静态预览，只让底部光带扫起来——比播一段被压扁的动画更稳。
            var lagMs = (DateTime.UtcNow - _createdUtc).TotalMilliseconds;
            if (lagMs > 400d)
            {
                ShowStaticPreview();
                StartProgressSweep(speed);
                ModBase.Log($"[Start] 开屏入场动画被启动耗时推迟 {lagMs:F0} ms，已改为静态呈现");
                return;
            }

            // 整体淡入 + 轻微放大（柔和，不使用回弹，避免「砸」出来的突兀感）
            // 节奏表必须整体塞进 MinVisibleMs 之内：原先进度条 620+300=920ms 才浮现完，
            // 而淡出在 900ms 就开始，等于「刚出现就被抹掉」。现在全部入场在 680ms 收尾。
            Anim(RootGrid, UIElement.OpacityProperty, 0d, 1d, 220d, 0d, speed, EaseKind.OutCubic);
            Anim(RootScale, ScaleTransform.ScaleXProperty, 0.96d, 1d, 360d, 0d, speed, EaseKind.OutCubic);
            Anim(RootScale, ScaleTransform.ScaleYProperty, 0.96d, 1d, 360d, 0d, speed, EaseKind.OutCubic);

            // Logo 上浮 + 淡入
            Anim(LogoBox, UIElement.OpacityProperty, 0d, 1d, 340d, 80d, speed, EaseKind.OutCubic);
            Anim(LogoShift, TranslateTransform.YProperty, 10d, 0d, 440d, 80d, speed, EaseKind.OutCubic);

            // 主标题、副标题依次浮现
            Anim(LabTitle, UIElement.OpacityProperty, 0d, 1d, 280d, 240d, speed, EaseKind.OutCubic);
            Anim(LabSub, UIElement.OpacityProperty, 0d, 1d, 280d, 340d, speed, EaseKind.OutCubic);

            // 进度条浮现，光带循环扫掠（淡出时会被主动停掉，避免与淡出抢帧）
            Anim(ProgressTrack, UIElement.OpacityProperty, 0d, 1d, 260d, 420d, speed, EaseKind.OutCubic);
            StartProgressSweep(speed, 480d);
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
    ///     立即呈现完整的静态画面（不做任何入场动画）。
    ///     供启动链在「主窗体首次布局会独占 UI 线程」的场景下提前显示品牌画面，
    ///     避免用户看到一张空白窗口。
    /// </summary>
    public void ShowStaticPreview()
    {
        try
        {
            RootGrid.Opacity = 1d;
            LogoBox.Opacity = 1d;
            LabTitle.Opacity = 1d;
            LabSub.Opacity = 1d;
            ProgressTrack.Opacity = 1d;
            RootScale.ScaleX = 1d;
            RootScale.ScaleY = 1d;
            LogoShift.Y = 0d;
        }
        catch
        {
            // 视觉兜底失败不影响启动
        }
    }

    /// <summary>
    ///     启动底部光带的循环扫掠。
    /// </summary>
    private void StartProgressSweep(double speed, double delayMs = 0d)
    {
        try
        {
            ProgressShift.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation
            {
                From = -100d,
                To = 200d,
                Duration = new Duration(TimeSpan.FromMilliseconds(420d / speed)),
                BeginTime = TimeSpan.FromMilliseconds(Math.Max(0d, delayMs) / speed),
                RepeatBehavior = RepeatBehavior.Forever
            });
        }
        catch
        {
            // 光带失败不影响其余动画
        }
    }

    /// <summary>
    ///     诊断用：当环境变量 <c>HE_SPLASH_SHOT</c> 指向一个目录时，把开屏动画的若干帧
    ///     渲染为 PNG 落盘，随后结束进程。仅用于自动化视觉验证；正常启动时该变量不存在，
    ///     本方法立即返回，不产生任何开销与副作用。
    /// </summary>
    private void StartFrameCaptureIfRequested()
    {
        var dir = Environment.GetEnvironmentVariable("HE_SPLASH_SHOT");
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
            var dir = Environment.GetEnvironmentVariable("HE_SPLASH_SHOT");
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
            // 立刻停掉无限循环的光带：它在淡出期间仍会让整窗每帧重绘（本窗口是软件渲染路径），
            // 与正在看的淡出抢帧。
            ProgressShift.BeginAnimation(TranslateTransform.XProperty, null);

            var fade = new DoubleAnimation
            {
                From = RootGrid.Opacity,
                To = 0d,
                Duration = new Duration(TimeSpan.FromMilliseconds(ms)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            fade.Completed += (_, _) => CloseSafely();
            // 淡出作用在 RootGrid 而不是窗口 Opacity：窗口级不透明度不参与离屏渲染，
            // 诊断截图（RenderTargetBitmap.Render(this)）拍不到，视觉等价但这里可被观测。
            RootGrid.BeginAnimation(UIElement.OpacityProperty, fade);
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
