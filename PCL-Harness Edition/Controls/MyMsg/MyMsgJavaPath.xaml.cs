using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.Core.UI.Controls;

namespace PCL;

/// <summary>
///     「自助安装 Java」的安装位置选择弹窗。
///     <para>
///         三选一：Adoptium 的默认目录（Program Files）、启动器目录下的 <c>PCL\Adoptium JRE</c>、
///         或者用户自己挑一个文件夹。选中的路径会写回传进来的
///         <see cref="ModJavaInstall.InstallPathContext.SelectedDirectory" />。
///     </para>
/// </summary>
public partial class MyMsgJavaPath
{
    private readonly ModMain.MyMsgBoxConverter myConverter;
    private readonly int uuid = ModBase.GetUuid();
    private readonly ModJavaInstall.InstallPathContext? pathContext;

    public MyMsgJavaPath(ModMain.MyMsgBoxConverter converter)
    {
        try
        {
            InitializeComponent();
            AppendUniqueNameSuffix(Btn1);
            AppendUniqueNameSuffix(Btn2);
            myConverter = converter;
            pathContext = converter.Content as ModJavaInstall.InstallPathContext;

            LabTitle.Text = converter.Title;
            LabTarget.Text = converter.Text;
            Btn2.Text = string.IsNullOrEmpty(converter.Button2) ? Lang.Text("Common.Action.Cancel") : converter.Button2;
            ShapeLine.StrokeThickness = ModBase.GetWPFSize(1d);

            if (pathContext is null)
            {
                // 没拿到上下文就没什么可选的，直接当作用户取消，避免弹出一个空壳
                myConverter.IsExited = true;
                myConverter.Result = 2;
                ModBase.Log("[Java] 安装位置弹窗缺少上下文，已按取消处理", ModBase.LogLevel.Debug);
            }
            else
            {
                TextSystem.Text = pathContext.SystemDirectory;
                TextLauncher.Text = pathContext.LauncherDirectory;
                TextCustom.Text = pathContext.SystemDirectory;
                if (pathContext.RecommendSystem)
                    RadioSystem.IsChecked = true;
                else
                    RadioLauncher.IsChecked = true;
                UpdateCustomState();
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "Java 安装位置弹窗初始化失败",
                ModBase.LogLevel.Hint,
                userSummary: Lang.Text("Application.Control.MessageBox.Error.OperationFailed"));
        }

        Loaded += Load;
    }

    private void AppendUniqueNameSuffix(FrameworkElement element)
    {
        element.Name += ModBase.GetUuid();
    }

    private void UpdateCustomState()
    {
        var enabled = RadioCustom.IsChecked == true;
        TextCustom.IsEnabled = enabled;
        BtnBrowse.IsEnabled = enabled;
    }

    private void Radio_Checked(object? sender = null, RoutedEventArgs? e = null)
    {
        try
        {
            UpdateCustomState();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "切换 Java 安装位置选项失败", ModBase.LogLevel.Debug);
        }
    }

    private void BtnBrowse_Click(object? sender = null, MouseButtonEventArgs? e = null)
    {
        try
        {
            var start = pathContext?.LauncherDirectory is { Length: > 0 } launcher && Directory.Exists(launcher)
                ? launcher
                : ModBase.exePath;
            var selected = SystemDialogs.SelectFolder(Lang.Text("Tools.Java.Path.BrowseTitle"), start);
            if (string.IsNullOrEmpty(selected)) return;
            TextCustom.Text = selected;
            RadioCustom.IsChecked = true;
            UpdateCustomState();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "选择 Java 安装文件夹失败", ModBase.LogLevel.Hint,
                userSummary: Lang.Text("Application.Control.MessageBox.Error.OperationFailed"));
        }
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
            ModBase.Log("[Control] Java 安装位置弹窗：" + LabTitle.Text);
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "Java 安装位置弹窗加载失败",
                ModBase.LogLevel.Hint,
                userSummary: Lang.Text("Application.Control.MessageBox.Error.OperationFailed"));
        }
    }

    private void Close()
    {
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
        if (myConverter.IsExited || pathContext is null) return;

        var target = RadioSystem.IsChecked == true ? pathContext.SystemDirectory
            : RadioLauncher.IsChecked == true ? pathContext.LauncherDirectory
            : TextCustom.Text.Trim();

        var error = ValidateDirectory(target);
        if (error is not null)
        {
            HintService.Hint(error, HintType.Warning);
            return;
        }

        pathContext.SelectedDirectory = target.TrimEnd(Path.DirectorySeparatorChar);
        myConverter.IsExited = true;
        myConverter.Result = 1;
        Close();
    }

    /// <summary>检查用户挑的目录能不能装，返回 <c>null</c> 表示没问题。</summary>
    private static string? ValidateDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return Lang.Text("Tools.Java.Path.Empty");
        if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0) return Lang.Text("Tools.Java.Path.Invalid");
        try
        {
            var full = Path.GetFullPath(path);
            var root = Path.GetPathRoot(full);
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return Lang.Text("Tools.Java.Path.NoDrive");
            // 只允许装进某个文件夹：直接装到盘符根目录既难管理，也容易被权限拦住
            if (string.Equals(full.TrimEnd(Path.DirectorySeparatorChar), root.TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
                return Lang.Text("Tools.Java.Path.RootNotAllowed");
            return null;
        }
        catch (Exception)
        {
            return Lang.Text("Tools.Java.Path.Invalid");
        }
    }

    public void Btn2_Click(object? sender = null, MouseButtonEventArgs? e = null)
    {
        if (myConverter.IsExited) return;
        myConverter.IsExited = true;
        myConverter.Result = 2;
        Close();
    }
}
