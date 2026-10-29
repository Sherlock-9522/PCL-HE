using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using PCL.Core.UI.Controls;

using PCL.Core.App.Localization;
namespace PCL;

/// <summary>
///     「自助安装 Java」的教程弹窗。
///     <para>
///         与普通文本弹窗的区别只有一处：右下角的「确定」会先倒计时一段时间才可点击，
///         让用户有时间把上面的说明读完，而不是下意识地点掉。
///     </para>
/// </summary>
public partial class MyMsgJavaGuide
{
    /// <summary>确定按钮解锁前的等待时间。</summary>
    private static readonly TimeSpan ConfirmDelay = TimeSpan.FromSeconds(30);

    private readonly ModMain.MyMsgBoxConverter myConverter;
    private readonly int uuid = ModBase.GetUuid();
    private readonly DispatcherTimer countdownTimer;
    private int remainingSeconds = (int)ConfirmDelay.TotalSeconds;

    public MyMsgJavaGuide(ModMain.MyMsgBoxConverter converter)
    {
        try
        {
            InitializeComponent();
            AppendUniqueNameSuffix(Btn1);
            AppendUniqueNameSuffix(Btn2);
            myConverter = converter;
            LabTitle.Text = converter.Title;
            LabTarget.Text = converter.Text;
            Btn2.Text = string.IsNullOrEmpty(converter.Button2) ? Lang.Text("Common.Action.Cancel") : converter.Button2;
            ShapeLine.StrokeThickness = ModBase.GetWPFSize(1d);

            // 倒计时期间按钮保持禁用，避免用户不看说明直接点过
            Btn1.IsEnabled = false;
            Btn1.Text = $"{converter.Button1}（{remainingSeconds}）";
            countdownTimer = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            countdownTimer.Tick += CountdownTick;
        }

        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "Java 安装说明弹窗初始化失败",
                ModBase.LogLevel.Hint,
                userSummary: Lang.Text("Application.Control.MessageBox.Error.OperationFailed"));
        }

        Loaded += Load;
        Unloaded += (_, _) => countdownTimer?.Stop();
    }

    private void AppendUniqueNameSuffix(FrameworkElement element)
    {
        element.Name += ModBase.GetUuid();
    }

    private void CountdownTick(object sender, EventArgs e)
    {
        remainingSeconds--;
        if (remainingSeconds > 0)
        {
            Btn1.Text = $"{myConverter.Button1}（{remainingSeconds}）";
            return;
        }

        countdownTimer.Stop();
        Btn1.Text = myConverter.Button1;
        Btn1.IsEnabled = true;
    }

    private void Load(object sender, RoutedEventArgs e)
    {
        try
        {
            ComponentDispatcher.PushModal();
            ModAnimation.AniStart(ModAnimation.AaColor(ModMain.frmMain.PanMsgBackground, BlurBorder.BackgroundProperty,
                new ModBase.MyColor(90d, 0d, 0d, 0d) - ModMain.frmMain.PanMsgBackground.Background, 200,
                ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)),
                "PanMsgBackground Background");
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaOpacity(this, 1d, 120, 60),
                    ModAnimation.AaDouble(i => TransformPos.Y += (double)i,
                        -TransformPos.Y, 300, 60, new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
                    ModAnimation.AaDouble(i => TransformRotate.Angle += (double)i,
                        -TransformRotate.Angle, 300, 60,
                        new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak))
                }, "MyMsgBox " + uuid);
            countdownTimer?.Start();
            ModBase.Log("[Control] Java 安装说明弹窗：" + LabTitle.Text);
        }

        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "Java 安装说明弹窗加载失败",
                ModBase.LogLevel.Hint,
                userSummary: Lang.Text("Application.Control.MessageBox.Error.OperationFailed"));
        }
    }

    private void Close()
    {
        countdownTimer?.Stop();
        if (myConverter.ForceWait || !string.IsNullOrEmpty(myConverter.Button2))
            myConverter.WaitFrame.Continue = false;
        ComponentDispatcher.PopModal();
        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaCode(() =>
            {
                if (!ModMain.WaitingMyMsgBox.Any())
                    ModAnimation.AniStart(ModAnimation.AaColor(ModMain.frmMain.PanMsgBackground,
                        BlurBorder.BackgroundProperty,
                        new ModBase.MyColor(0d, 0d, 0d, 0d) - ModMain.frmMain.PanMsgBackground.Background, 200,
                        ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)));
            }, 30),
            ModAnimation.AaOpacity(this, -Opacity, 80, 20),
            ModAnimation.AaDouble(i => TransformPos.Y += (double)i, 20d - TransformPos.Y,
                150, 0, new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaDouble(i => TransformRotate.Angle += (double)i,
                6d - TransformRotate.Angle, 150, 0, new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)),
            ModAnimation.AaCode(() => ((Grid)Parent).Children.Remove(this), after: true)
        }, "MyMsgBox " + uuid);
    }

    public void Btn1_Click(object? sender = null, MouseButtonEventArgs? e = null)
    {
        if (myConverter.IsExited || !Btn1.IsEnabled)
            return;
        myConverter.IsExited = true;
        myConverter.Result = 1;
        Close();
    }

    public void Btn2_Click(object? sender = null, MouseButtonEventArgs? e = null)
    {
        if (myConverter.IsExited)
            return;
        myConverter.IsExited = true;
        myConverter.Result = 2;
        Close();
    }
}
