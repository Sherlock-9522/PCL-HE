using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using PCL.Core.App;
using PCL.Core.App.Essentials;
using PCL.Core.App.IoC;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.UI.Controls;
using PCL.Core.Utils;
using PCL.Core.Utils.OS;

namespace PCL;

public partial class Application
{
    public Application()
    {
        // 注册生命周期事件
        Lifecycle.When(LifecycleState.Loaded, _ApplicationStartup);
        Lifecycle.When(LifecycleState.WindowCreated, _ShowEnvironmentWarning);
        SessionEnding += _ApplicationSessionEnding;
    }

    // 开始
    private static void _ApplicationStartup()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // 创建自定义跟踪监听器，用于检测是否存在 Binding 失败
            PresentationTraceSources.DataBindingSource.Listeners.Add(new BindingErrorTraceListener());
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            StartupValidation.EnsureWpfFont();

            // 检查参数调用
            var args = Basics.CommandLineArguments;
            if (args.Length > 0)
                if (args[0] == "--gpu")
                    // 调整显卡设置
                    try
                    {
                        // --gpu 后面必须跟显卡首选项名；缺参数时不能直接取 args[1]（越界），
                        // 否则会被下面的 catch 静默吞掉、进程无声退出，用户看不到任何原因。
                        if (args.Length < 2)
                        {
                            ModMain.MyMsgBox("启动参数 --gpu 缺少显卡首选项名称，未做任何更改。", isWarn: true);
                            Environment.Exit((int)ModBase.ProcessReturnValues.Fail);
                        }

                        ModMain.SetGPUPreference(args[1].Trim('"'));
                        Environment.Exit((int)ModBase.ProcessReturnValues.TaskDone);
                    }
                    catch (Exception ex)
                    {
                        ModMain.MyMsgBox($"设置显卡首选项失败：{ex.Message}", isWarn: true);
                        Environment.Exit((int)ModBase.ProcessReturnValues.Fail);
                    }

            // 初始化文件结构
            Directory.CreateDirectory(ModBase.exePath + @"PCL\Pictures");
            Directory.CreateDirectory(ModBase.exePath + @"PCL\Musics");
            Directory.CreateDirectory(Path.Combine(ModBase.pathTemp, "Cache"));
            Directory.CreateDirectory(Path.Combine(ModBase.pathTemp, "Download"));
            Directory.CreateDirectory(ModBase.pathAppdata);

            // 设置 ToolTipService 默认值
            ToolTipService.InitialShowDelayProperty.OverrideMetadata(typeof(DependencyObject),
                new FrameworkPropertyMetadata(100));
            Tooltip.Enable();

            // 设置初始窗口（PCL-HE：改用带动画的自定义开屏窗口）
            if (Config.Preference.ShowStartupLogo)
                try
                {
                    ModMain.frmStart = new FormSplash();
                    ModMain.frmStart.Show();
                    // 先把开屏的静态画面画出来再继续启动链：紧随其后的主窗体首次布局会独占
                    // UI 线程数百毫秒，这段时间里任何补间都不会出帧，用户会看到一张空白窗口。
                    ModMain.frmStart.ShowStaticPreview();
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                        System.Windows.Threading.DispatcherPriority.Render, new Action(() => { }));
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "创建开屏动画窗口失败，已跳过开屏", ModBase.LogLevel.Hint);
                    ModMain.frmStart = null;
                }

            // 设置初始化
            _ = Config.Debug.Enabled;
            // 真正应用动画速度：原先只是读取预热，导致 ModAnimation.aniSpeed 要等
            // ModSetup.ApplyAll 才被赋值，开屏窗口永远读到默认值 1，与用户设置脱节。
            ModSetup.SystemDebugAnim(Config.Debug.AnimationSpeed);
            _ = Config.Network.HttpProxy.CustomAddress;
            _ = Config.Network.HttpProxy.CustomUsername;
            _ = Config.Network.HttpProxy.Type;
            _ = Config.Download.ThreadLimit;
            _ = Config.Download.SpeedLimit;
            _ = Config.Preference.Font;
            var updateBranchCfg = Config.Update.UpdateChannelConfig;
            if (updateBranchCfg.IsDefault())
                updateBranchCfg.SetValue(ModBase.versionBaseName.Contains("beta")
                    ? Core.App.UpdateChannel.Beta
                    : Core.App.UpdateChannel.Release);

            // 删除旧日志
            for (var i = 1; i <= 5; i++)
            {
                var oldLogFile = $@"{ModBase.exePath}PCL\Log-CE{i}.log";
                if (File.Exists(oldLogFile))
                    File.Delete(oldLogFile);
            }

            // 计时
            ModBase.Log("[Start] 第一阶段加载用时：" + (TimeUtils.GetTimeTick() - ModBase.applicationStartTick) + " ms");
            ModBase.applicationStartTick = TimeUtils.GetTimeTick();
            ModAnimation.AniControlEnabled += 1;
        }
        catch (Exception ex)
        {
            var filePath = Basics.ExecutablePath;
            var summary = Lang.Text("Application.InitializationError.Path",
                string.IsNullOrEmpty(filePath)
                    ? Lang.Text("Application.InitializationError.PathUnavailable")
                    : filePath);
            MessageBox.Show(
                ExceptionDetails.Compose(summary, ex),
                Lang.Text("SystemDialog.Startup.InitializationTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            FormMain.EndProgramForce(ModBase.ProcessReturnValues.Exception);
        }
    }

    // 检测异常环境
    private static void _ShowEnvironmentWarning()
    {
        var problemList = new List<string>();
        var currentOsVersion = NtInterop.GetCurrentOsVersion();
        if (currentOsVersion.Build < 17763)
            problemList.Add(Lang.Text("Application.EnvironmentWarning.WindowsVersion"));
        if (SystemInfo.Is32BitSystem)
            problemList.Add(Lang.Text("Application.EnvironmentWarning.System32Bit"));
        if (ModBase.exePath.Contains(Path.GetTempPath()) || ModBase.exePath.Contains(@"AppData\Local\Temp\"))
            problemList.Add(Lang.Text("Application.EnvironmentWarning.TempFolder"));
        if (ModBase.exePath.ContainsF("wechat_files", true) || ModBase.exePath.ContainsF("WeChat Files", true) ||
            ModBase.exePath.ContainsF("Tencent Files", true))
            problemList.Add(Lang.Text("Application.EnvironmentWarning.SocialSoftwareFolder"));
        if (problemList.Count == 0) return;

        ModMain.MyMsgBox(
            Lang.Text("Application.EnvironmentWarning.Message", problemList.Join("\r\n")),
            Lang.Text("Application.EnvironmentWarning.Title"),
            Lang.Text("Application.EnvironmentWarning.IKnow"),
            isWarn: true);
    }

    // 结束
    private static void _ApplicationSessionEnding(object sender, SessionEndingCancelEventArgs e)
    {
        // 系统注销/关机时主窗体可能尚未创建（例如启动阶段就被注销），直接解引用会 NRE；
        // 此时也没有需要收尾的界面与联机进程，直接返回即可。
        if (ModMain.frmMain is null) return;
        ModMain.frmMain.EndProgram(false);
    }

    /**
     * Error handling for unhandled exceptions
     */
    private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            e.Handled = true;
            if (ModBase.isProgramEnded) return;

            ModBase.FeedbackInfo();

            var detail = e.Exception.ToString();

            // Automatic error analysis for environment issues
            if (detail.Contains("System.Windows.Threading.Dispatcher.Invoke") ||
                detail.Contains("MS.Internal.AppModel.ITaskbarList.HrInit") ||
                detail.Contains("未能加载文件或程序集"))
            {
                ModBase.OpenWebsite("https://get.dot.net/10");
                LogWrapper.Error(
                    e.Exception,
                    Lang.Text("SystemDialog.Startup.DotNetRuntimeOutdated.Message"));
            }
            else
            {
                LogWrapper.Error(e.Exception, Lang.Text("SystemDialog.Error.Unexpected.Message"));
            }
        }
        catch
        {
            // Equivalent to On Error Resume Next for safety in the global handler
        }
    }

    // Win32 API declaration for DLL directory configuration
    [DllImport("kernel32", EntryPoint = "SetDllDirectoryA", CharSet = CharSet.Ansi)]
    private static extern bool _SetDllDirectory(string lpPathName);
    // 切换窗口

    // 控件模板事件
    private void _MyIconButtonClick(object sender, EventArgs e)
    {
    }

    // 自定义监听器类
    public class BindingErrorTraceListener : TraceListener
    {
        public override void Write(string message)
        {
            ModBase.Log($"警告，检测到 Binding 失败：{message}");
        }

        public override void WriteLine(string message)
        {
            ModBase.Log($"警告，检测到 Binding 失败：{message}");
        }
    }
}