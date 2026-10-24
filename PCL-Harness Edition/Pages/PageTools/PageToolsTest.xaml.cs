using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using PCL.Core.App;
using PCL.Core.App.Essentials;
using PCL.Core.App.Tools;
using PCL.Core.IO;
using PCL.Core.IO.Net;
using PCL.Core.UI;
using PCL.Core.Utils.OS;
using PCL.Core.Utils.Secret;
using PCL.Core.Utils.Validate;
using PCL.Network;
using PCL.Network.Loaders;
using PCL.Core.App.Localization;
using System.Globalization;

namespace PCL;

public partial class PageToolsTest
{
    private Bitmap currentSkinBitmap;
    private Bitmap generatedHeadBitmap;

    private int headSize = 64;
    private string skinPath = "";

    public PageToolsTest()
    {
        InitializeComponent();
        BtnSelectSkin.Click += BtnSelectSkin_Click;
        CmbHeadSize.SelectionChanged += CmbHeadSize_SelectionChanged;
        Loaded += (_, _) => MeLoaded();
        Loaded += (_, _) => RefreshMemoryStatus();
        #if DEBUG
        BtnCrash.Visibility = Visibility.Visible;
        #endif
    }

    #region 内存优化

    private bool memoryOptimizeRunning;

    private void RefreshMemoryStatus()
    {
        try
        {
            var snapshot = KernelInterop.GetMemorySnapshot();
            double totalGb = snapshot.TotalPhysical / 1024d / 1024d / 1024d;
            double availableGb = snapshot.AvailablePhysical / 1024d / 1024d / 1024d;
            double commitLimitGb = snapshot.CommitLimit / 1024d / 1024d / 1024d;
            double commitAvailableGb = snapshot.CommitAvailable / 1024d / 1024d / 1024d;
            TextMemoryStatus.Text = string.Format(Lang.Text("Tools.Memory.Status"),
                totalGb, availableGb, snapshot.MemoryLoadPercent, commitLimitGb, commitAvailableGb);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "读取内存状态失败", ModBase.LogLevel.Hint);
            TextMemoryStatus.Text = Lang.Text("Tools.Memory.StatusFailed");
        }
    }

    private void BtnMemoryRefresh_Click(object sender, MouseButtonEventArgs e) => RefreshMemoryStatus();

    private enum MemoryOptimizeKind
    {
        /// <summary>轻量护航：只整理启动器自身，不碰其他程序。</summary>
        Light,

        /// <summary>深度清理：经提权子进程清空全系统工作集。</summary>
        Deep,

        /// <summary>游戏内护航：绝不触碰游戏进程，只整理其他程序。</summary>
        Game,
    }

    /// <summary>轻量护航：只整理启动器自身的工作集，不触碰任何其他进程，不需要管理员授权。</summary>
    private void BtnMemoryLight_Click(object sender, MouseButtonEventArgs e) => RunMemoryOptimize(MemoryOptimizeKind.Light);

    /// <summary>深度清理：经提权子进程清空全系统工作集，需要一次管理员授权（UAC）。</summary>
    private void BtnMemoryDeep_Click(object sender, MouseButtonEventArgs e) => RunMemoryOptimize(MemoryOptimizeKind.Deep);

    /// <summary>游戏内护航：把游戏完整排除在外并提高它的内存优先级，游戏运行中随时可用，不需要管理员授权。</summary>
    private void BtnMemoryGame_Click(object sender, MouseButtonEventArgs e) => RunMemoryOptimize(MemoryOptimizeKind.Game);

    private void RunMemoryOptimize(MemoryOptimizeKind kind)
    {
        if (memoryOptimizeRunning) return;
        var deep = kind == MemoryOptimizeKind.Deep;
        var game = kind == MemoryOptimizeKind.Game;

        // 游戏内护航靠「排除游戏进程」实现，因此必须先知道游戏是谁；没有游戏也该能清后台，先问一句再跑。
        var gameProcessIds = new HashSet<uint>();
        if (game)
        {
            gameProcessIds = CollectGameProcessIds();
            if (gameProcessIds.Count == 0 &&
                ModMain.MyMsgBox(Lang.Text("Tools.Memory.Game.NoGame.Message"),
                    Lang.Text("Tools.Memory.Game.NoGame.Title"),
                    Lang.Text("Tools.Memory.Game.NoGame.Confirm"),
                    Lang.Text("Common.Action.Cancel")) != 1)
            {
                ModBase.Log("[Memory] 没有检测到正在运行的游戏，用户取消了本次游戏内护航", ModBase.LogLevel.Hint);
                return;
            }
        }

        // 深度清理会清空全系统工作集且命令本身无法排除任何进程：游戏在跑时执行必然让它从磁盘重读全部页面。
        // 游戏运行中不再一律拒绝：内核命令无法排除任何进程，游戏一定会被一起清到，
        // 所以把代价讲清楚、由用户自己决定要不要现在清（内存确实吃紧时这是唯一能腾出空间的办法）。
        if (deep && ModLaunch.mcLaunchProcess is { HasExited: false })
        {
            if (ModMain.MyMsgBox(Lang.Text("Tools.Memory.Deep.GameRunning.Message"),
                    Lang.Text("Tools.Memory.Deep.GameRunning.Title"),
                    Lang.Text("Tools.Memory.Deep.GameRunning.Confirm"),
                    Lang.Text("Common.Action.Cancel")) != 1)
            {
                ModBase.Log("[Memory] 游戏正在运行，用户取消了本次深度清理", ModBase.LogLevel.Hint);
                return;
            }
        }

        memoryOptimizeRunning = true;
        BtnMemoryLight.IsEnabled = false;
        BtnMemoryDeep.IsEnabled = false;
        BtnMemoryGame.IsEnabled = false;
        if (deep)
        {
            // 深度清理要等一次 UAC 授权，可能几十秒没有反馈，必须在界面上说明正在发生什么。
            TextMemoryDeepState.Text = Lang.Text("Tools.Memory.Deep.Running");
            TextMemoryDeepState.Visibility = Visibility.Visible;
        }
        else if (game)
        {
            TextMemoryGameState.Text = Lang.Text("Tools.Memory.Game.Running");
            TextMemoryGameState.Visibility = Visibility.Visible;
        }

        ModBase.RunInNewThread(() =>
        {
            try
            {
                var report = kind switch
                {
                    MemoryOptimizeKind.Deep => RunDeepOptimize(),
                    MemoryOptimizeKind.Game =>
                        PCL.MemoryOptimizer.MemoryOptimizer.RunGameBoost(gameProcessIds, CancellationToken.None),
                    _ => PCL.MemoryOptimizer.MemoryOptimizer.RunGuarded(CancellationToken.None),
                };
                if (report is null) return; // 已在内部给出提示
                string message;
                switch (kind)
                {
                    case MemoryOptimizeKind.Deep:
                        // 深度清理报告可用内存增量、耗时与原生状态码；失败时把提权子进程给出的原因带给用户。
                        var nativeStatus = $"NTSTATUS 0x{report.NativeStatus:X8}";
                        message = report.Succeeded
                            ? Lang.Text("Tools.Memory.Deep.Result", report.FreedAvailableMb, report.ElapsedMilliseconds) +
                              $" ({nativeStatus})"
                            : Lang.Text("Tools.Memory.Deep.Failed",
                                string.IsNullOrWhiteSpace(report.ErrorMessage) ? nativeStatus : report.ErrorMessage!);
                        break;
                    case MemoryOptimizeKind.Game:
                        // 游戏内护航的收益体现在全系统工作集上：游戏那部分一点没动，其余程序被换出了。
                        message = Lang.Text("Tools.Memory.Game.Result", report.WorkingSetReducedMb,
                            report.ProcessedCount, report.ElapsedMilliseconds);
                        if (report.PriorityRaisedCount > 0)
                        {
                            message += "\n" + Lang.Text("Tools.Memory.Game.PriorityRaised", report.PriorityRaisedCount);
                        }
                        break;
                    default:
                        message = Lang.Text("Tools.Memory.Light.Result", report.WorkingSetReducedMb,
                            report.ElapsedMilliseconds);
                        break;
                }
                ModBase.Log($"[Memory] {GetMemoryOptimizeKindName(kind)}完成：{message}（工作集减少 {report.WorkingSetReducedMb:F0} MB，" +
                            $"处理 {report.ProcessedCount} 个、跳过 {report.SkippedCount} 个、无法处理 {report.FailedCount} 个）");
                Dispatcher.Invoke(() =>
                {
                    HintService.Hint(message);
                    RefreshMemoryStatus();
                });
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "内存优化失败", ModBase.LogLevel.Hint,
                    userSummary: kind switch
                    {
                        MemoryOptimizeKind.Deep => string.Format(Lang.Text("Tools.Memory.Deep.Failed"), ex.Message),
                        MemoryOptimizeKind.Game => string.Format(Lang.Text("Tools.Memory.Game.Failed"), ex.Message),
                        _ => Lang.Text("Tools.Memory.ReleaseFailed"),
                    });
            }
            finally
            {
                Dispatcher.Invoke(() =>
                {
                    TextMemoryDeepState.Visibility = Visibility.Collapsed;
                    TextMemoryDeepState.Text = string.Empty;
                    TextMemoryGameState.Visibility = Visibility.Collapsed;
                    TextMemoryGameState.Text = string.Empty;
                    BtnMemoryLight.IsEnabled = true;
                    BtnMemoryDeep.IsEnabled = true;
                    BtnMemoryGame.IsEnabled = true;
                    memoryOptimizeRunning = false;
                });
            }
        }, "MemoryOptimize");
    }

    private static string GetMemoryOptimizeKindName(MemoryOptimizeKind kind) => kind switch
    {
        MemoryOptimizeKind.Deep => "深度清理",
        MemoryOptimizeKind.Game => "游戏内护航",
        _ => "轻量护航",
    };

    /// <summary>
    /// 收集要保护的游戏进程：当前启动的这个游戏，以及同名的其他实例
    /// （多开整合包时同一个 javaw 名字下可能同时跑着另一个游戏，不能把它清掉）。
    /// 没有正在运行的游戏时返回空集合。
    /// </summary>
    private static HashSet<uint> CollectGameProcessIds()
    {
        var result = new HashSet<uint>();
        var game = ModLaunch.mcLaunchProcess;
        if (game is not { HasExited: false })
        {
            return result;
        }

        try
        {
            result.Add((uint)game.Id);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Memory] 读取游戏进程 ID 失败", ModBase.LogLevel.Debug);
            return result;
        }

        try
        {
            foreach (var process in Process.GetProcessesByName(game.ProcessName))
            {
                try
                {
                    result.Add((uint)process.Id);
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "[Memory] 读取同名游戏进程 ID 失败", ModBase.LogLevel.Debug);
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Memory] 枚举同名游戏进程失败", ModBase.LogLevel.Debug);
        }

        return result;
    }

    /// <summary>
    /// 经提权子进程执行内核命令（信息类 80 / 命令 2）。
    /// 返回 null 表示未执行或执行失败，此时已经提示过用户。
    /// </summary>
    private static PCL.MemoryOptimizer.OptimizationReport? RunDeepOptimize()
    {
        PCL.MemoryOptimizer.OptimizationReport? report = null;
        using var done = new ManualResetEventSlim(false);
        PromoteService.Append("memory-deep-clean", result =>
        {
            try
            {
                if (!string.IsNullOrEmpty(result)) report = PCL.MemoryOptimizer.MemoryOptimizer.Deserialize(result);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "[Memory] 解析提权返回的内存报告失败", ModBase.LogLevel.Debug);
            }
            finally
            {
                done.Set();
            }
        });
        if (!PromoteService.Activate())
        {
            ModBase.Log("[Memory] 提权进程启动失败（可能是用户取消了授权）", ModBase.LogLevel.Hint,
                userSummary: Lang.Text("Tools.Memory.Deep.ElevationFailed"));
            return null;
        }
        done.Wait(TimeSpan.FromMinutes(2));
        if (report is null)
        {
            ModBase.Log("[Memory] 深度清理未在时限内返回结果", ModBase.LogLevel.Hint,
                userSummary: Lang.Text("Tools.Memory.Deep.Timeout"));
        }
        return report;
    }

    #endregion

    private void MeLoaded()
    {
        BtnDownloadStart.IsEnabled = false;

        TextDownloadFolder.Text = States.Tool.DownloadFolder;
        TextDownloadFolder.Validate();

        if (!string.IsNullOrEmpty(TextDownloadFolder.ValidateResult) || string.IsNullOrEmpty(TextDownloadFolder.Text))
            TextDownloadFolder.Text = ModBase.exePath + @"PCL\MyDownload\";

        TextDownloadFolder.Validate();
        TextDownloadName.Validate();
        TextDownloadUrl.Validate();
        StartButtonRefresh();
    }

    private void StartButtonRefresh()
    {
        BtnDownloadStart.IsEnabled = string.IsNullOrEmpty(TextDownloadFolder.ValidateResult) &&
                                     string.IsNullOrEmpty(TextDownloadUrl.ValidateResult) &&
                                     string.IsNullOrEmpty(TextDownloadName.ValidateResult);

        BtnDownloadOpen.IsEnabled = string.IsNullOrEmpty(TextDownloadFolder.ValidateResult);

        BtnAchievementPreview.IsEnabled = string.IsNullOrEmpty(AchievementBlockTextBox.ValidateResult) &&
                                          string.IsNullOrEmpty(AchievementTitleTextBox.ValidateResult) &&
                                          string.IsNullOrEmpty(AchievementString1TextBox.ValidateResult);

        BtnAchievementSave.IsEnabled = string.IsNullOrEmpty(AchievementBlockTextBox.ValidateResult) &&
                                       string.IsNullOrEmpty(AchievementTitleTextBox.ValidateResult) &&
                                       string.IsNullOrEmpty(AchievementString1TextBox.ValidateResult);
    }

    private void SaveCacheDownloadFolder(object sender, RoutedEventArgs e)
    {
        States.Tool.DownloadFolder = TextDownloadFolder.Text;
        TextDownloadName.Validate();
    }

    private static void DownloadState(ModLoader.LoaderCombo<int> loader)
    {
        try
        {
            switch (loader.State)
            {
                case ModBase.LoadState.Finished:
                {
                    HintService.Hint(Lang.Text("Tools.Test.CustomDownload.Finished", loader.name), HintType.Success);
                    Console.Beep();
                    break;
                }
                case ModBase.LoadState.Failed:
                {
                    ModBase.Log(
                        loader.Error,
                        $"{loader.name}失败",
                        ModBase.LogLevel.Msgbox,
                        userSummary: Lang.Text("Tools.Test.Error.OperationFailed"));
                    Console.Beep();
                    break;
                }
                case ModBase.LoadState.Aborted:
                {
                    HintService.Hint(Lang.Text("Tools.Test.CustomDownload.Aborted", loader.name));
                    break;
                }
            }
        }
        catch (Exception ex)
        {
        }
    }

    public static void StartCustomDownload(string url, string fileName, string folder = null, string userAgent = "")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                folder = SystemDialogs.SelectSaveFile(Lang.Text("Tools.Test.CustomDownload.SelectLocation"), fileName);
                if (!folder.Contains(@"\")) return;
                if (folder.EndsWith(fileName)) folder = folder[..^fileName.Length];
            }

            folder = folder.Replace("/", @"\").TrimEnd(new[] { '\\' }) + @"\";
            try
            {
                Directory.CreateDirectory(folder);
                ModBase.CheckPermissionWithException(folder);
            }
            catch (Exception ex)
            {
                ModBase.Log(
                    ex,
                    $"访问文件夹失败（{folder}）",
                    ModBase.LogLevel.Hint,
                    userSummary: Lang.Text("Tools.Test.Error.OperationFailed"));
                return;
            }

            ModBase.Log("[Download] 自定义下载文件名：" + fileName);
            ModBase.Log("[Download] 自定义下载文件目标：" + folder);
            var uuid = ModBase.GetUuid();
            ModLoader.LoaderBase loaderdownload;
            if (new HttpValidator().Validate(url).IsValid)
                loaderdownload = new LoaderDownload(Lang.Text("Tools.Test.CustomDownload.LoaderName", fileName),
                    new List<DownloadFile> { new(new[] { url }, folder + fileName, null, true, userAgent) });
            else // UNC 路径
                loaderdownload = new LoaderDownloadUnc(Lang.Text("Tools.Test.CustomDownload.LoaderName", fileName),
                    new Tuple<string, string>(url, folder + fileName));
            var loaderCombo = new ModLoader.LoaderCombo<int>(Lang.Text("Tools.Test.CustomDownload.LoaderTitle", uuid), new[] { loaderdownload })
                { OnStateChanged = a => DownloadState((ModLoader.LoaderCombo<int>)a) };
            loaderCombo.Start();
            ModLoader.LoaderTaskbarAdd(loaderCombo);
            ModMain.frmMain.BtnExtraDownload.ShowRefresh();
            ModMain.frmMain.BtnExtraDownload.Ribble();
        }

        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "开始自定义下载失败",
                ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Tools.Test.Error.OperationFailed"));
        }
    }

    public static void Jrrp()
    {
        var random = new Random(GenerateDailySeed());
        var luckValue = random.Next(0, 101);
        var rating = GetRating(luckValue);
        var title = Lang.Text("Tools.Test.Luck.MsgboxTitle", Lang.Date(DateTime.Now, "d"));

        if (luckValue >= 60)
            ModMain.MyMsgBox(Lang.Text("Tools.Test.Luck.MessageGood", luckValue, rating), title);
        else
            ModMain.MyMsgBox(Lang.Text("Tools.Test.Luck.MessageBad", luckValue, rating), title, isWarn: luckValue <= 30);
    }

    public static void RubbishClear()
    {
        ModBase.RunInUi(() =>
        {
            if (ModMain.frmToolsTest is not null && ModMain.frmToolsTest.BtnClear is not null)
                ModMain.frmToolsTest.BtnClear.IsEnabled = false;
        });
        // 只有当没有运行中的Minecraft游戏且启动器不在加载状态时才能清理

        // 清理的文件数量
        // 所有 Minecraft 文件夹


        // 寻找所有 Minecraft 文件夹

        // 删除 Minecraft 的缓存
        // 删除日志和崩溃报告并计数

        // 删除 Natives 文件

        // 删除 PCL 的缓存

        ModBase.RunInNewThread(() =>
        {
            try
            {
                if (!ModWatcher.hasRunningMinecraft && ModLaunch.mcLaunchLoader.State != ModBase.LoadState.Loading)
                {
                    if (ModNet.HasDownloadingTask())
                    {
                        HintService.Hint(Lang.Text("Tools.Test.Clean.WaitForDownload"));
                        return;
                    }

                    if (!ModFolder.mcFolderList.Any()) ModFolder.mcFolderListLoader.Start();
                    if (States.Hint.CleanJunkFile <= 2)
                    {
                        if (ModMain.MyMsgBox(
                                Lang.Text("Tools.Test.Clean.ConfirmMessage"),
                                Lang.Text("Tools.Test.Clean.ConfirmTitle"),
                                Lang.Text("Common.Action.Confirm"),
                                Lang.Text("Common.Action.Cancel")
                            ) == 2) return;
                        States.Hint.CleanJunkFile += 1;
                    }

                    var num = 0;
                    var cleanMcFolderList = new List<DirectoryInfo>();
                    if (!ModFolder.mcFolderList.Any()) ModFolder.mcFolderListLoader.WaitForExit();
                    foreach (var mcFolder in ModFolder.mcFolderList)
                    {
                        cleanMcFolderList.Add(new DirectoryInfo(mcFolder.Location));
                        var dirInfo = new DirectoryInfo(mcFolder.Location + "versions");
                        if (dirInfo.Exists)
                            foreach (var item in dirInfo.EnumerateDirectories())
                                cleanMcFolderList.Add(item);
                    }

                    foreach (var dirInfo in cleanMcFolderList)
                    {
                        num += ModBase.DeleteDirectory(
                            dirInfo.FullName + (dirInfo.FullName.EndsWith(@"\") ? "" : @"\") + @"crash-reports\", true);
                        num += ModBase.DeleteDirectory(
                            dirInfo.FullName + (dirInfo.FullName.EndsWith(@"\") ? "" : @"\") + @"logs\", true);
                        foreach (var fileInfo in dirInfo.EnumerateFiles("*"))
                            if (fileInfo.Name.StartsWith("hs_err_pid") || fileInfo.Name.EndsWith(".log") ||
                                fileInfo.Name == "WailaErrorOutput.txt")
                            {
                                fileInfo.Delete();
                                num += 1;
                            }

                        foreach (var dirInfo2 in dirInfo.EnumerateDirectories())
                            if ((dirInfo2.Name ?? "") == (dirInfo2.Name + "-natives" ?? "") ||
                                dirInfo2.Name == "natives-windows-x86_64")
                                num += ModBase.DeleteDirectory(dirInfo2.FullName, true);
                    }

                    num += ModBase.DeleteDirectory(ModBase.pathTemp, true);
                    num += ModBase.DeleteDirectory(Path.Combine(SystemPaths.DriveLetter, "ProgramData", "PCL"), true);
                    if (num != 0)
                    {
                        ModMain.MyMsgBox(Lang.Text("Tools.Test.Clean.ClearedMessage", num),
                            Lang.Text("Tools.Test.Clean.Cleared"), Lang.Text("Common.Action.Confirm"), "", "", false, true, true);
                        Process.Start(new ProcessStartInfo(Basics.ExecutablePath));
                        FormMain.EndProgramForce();
                    }
                    else
                    {
                        HintService.Hint(Lang.Text("Tools.Test.Clean.NoFiles"));
                    }
                }
                else
                {
                    HintService.Hint(Lang.Text("Tools.Test.Clean.CloseGameFirst"));
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(
                    ex,
                    "清理垃圾失败",
                    ModBase.LogLevel.Hint,
                    userSummary: Lang.Text("Tools.Test.Error.OperationFailed"));
            }
            finally
            {
                ModBase.RunInUiWait(() =>
                {
                    if (ModMain.frmToolsTest is not null && ModMain.frmToolsTest.BtnClear is not null)
                        ModMain.frmToolsTest.BtnClear.IsEnabled = true;
                });
            }
        }, "Rubbish Clear");
    }

    public static string GetRandomCave()
    {
        return Lang.Text("Tools.Test.CeNotice");
    }

    public static string GetRandomHint()
    {
        return Lang.Text("Tools.Test.CeNotice");
    }

    public static string GetRandomPresetHint()
    {
        return Lang.Text("Tools.Test.CeNotice");
    }

    private void TextDownloadUrl_TextChanged(object sender, TextChangedEventArgs e)
    {
        try
        {
            if (!string.IsNullOrEmpty(TextDownloadName.Text) || string.IsNullOrEmpty(TextDownloadUrl.Text)) return;
            TextDownloadName.Text = ModBase.GetFileNameFromPath(WebUtility.UrlDecode(TextDownloadUrl.Text));
            StartButtonRefresh();
        }
        catch
        {
        }
    }

    private void MyTextButton_Click(object sender, EventArgs e)
    {
        var text = SystemDialogs.SelectFolder();
        if (!string.IsNullOrEmpty(text)) TextDownloadFolder.Text = text;
    }

    private void BtnDownloadOpen_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            var text = TextDownloadFolder.Text;
            Directory.CreateDirectory(text);
            Basics.OpenPath(text);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "打开下载文件夹失败");
        }
    }

    private void BtnDownloadStart_Click(object sender, MouseButtonEventArgs e)
    {
        StartCustomDownload(TextDownloadUrl.Text, TextDownloadName.Text, TextDownloadFolder.Text);
        TextDownloadUrl.Text = "";
        TextDownloadUrl.Validate();
        TextDownloadUrl.ForceShowAsSuccess();
        TextDownloadName.Text = "";
        TextDownloadName.Validate();
        TextDownloadName.ForceShowAsSuccess();
        StartButtonRefresh();
    }

    private void TextDownloadUrl_ValidateChanged(object sender, RoutedEventArgs e)
    {
        StartButtonRefresh();
    }

    private void TextDownloadFolder_ValidateChanged(object sender, EventArgs e)
    {
        StartButtonRefresh();
    }

    private void TextDownloadName_ValidateChanged(object sender, EventArgs e)
    {
        StartButtonRefresh();
    }

    private void BtnClear_Click(object sender, MouseButtonEventArgs e)
    {
        RubbishClear();
    }

    // 下载正版玩家皮肤
    private void BtnSkinSave_Click(object sender, MouseButtonEventArgs e)
    {
        var id = TextSkinID.Text;
        HintService.Hint(Lang.Text("Tools.Test.Skin.Fetching"));
        ModBase.RunInNewThread(() =>
        {
            try
            {
                if (id.Length < 3)
                {
                    HintService.Hint(Lang.Text("Tools.Test.Skin.InvalidId"));
                }
                else
                {
                    var result = (string)ProfileUi.McLoginMojangUuid(id, true);
                    result = ModSkin.McSkinGetAddress(result, "Mojang");
                    result = ModSkin.McSkinDownload(result);
                    ModBase.RunInUi(() =>
                    {
                        var path = SystemDialogs.SelectSaveFile(Lang.Text("Tools.Test.Skin.Save"), $"{id}.png", Lang.Text("Tools.Test.Skin.FileFilter"));
                        ModBase.CopyFile(result, path);
                        HintService.Hint(Lang.Text("Tools.Test.Skin.Saved", id), HintType.Success);
                    });
                }
            }
            catch (Exception ex)
            {
                if (ex.ToString().Contains("429"))
                {
                    HintService.Hint(Lang.Text("Tools.Test.Skin.TooFrequent"), HintType.Error);
                    ModBase.Log($"获取正版皮肤失败（{id}）：获取皮肤太过频繁，请 5 分钟后再试！");
                }
                else
                {
                    ModBase.Log(ex, $"获取正版皮肤失败（{id}）");
                }
            }
        });
    }

    // 今日人品
    private void BtnLuck_Click(object sender, MouseButtonEventArgs e)
    {
        Jrrp();
    }

    public static int GenerateDailySeed()
    {
        var datePart = DateTime.Today.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        return DJB2Hash(datePart + Identify.LauncherId);
    }

    private static int DJB2Hash(string str)
    {
        var hash = 5381L;
        var prime = 33L;
        foreach (var c in str)
        {
            long charValue = c;
            hash = (hash * prime + charValue) % 0x100000000L;
        }

        return (int)(hash & 0x7FFFFFFFL);
    }

    public static string GetRating(int luckValue)
    {
        var key = luckValue switch
        {
            100 => "Tools.Test.Luck.Rating100",
            >= 95 => "Tools.Test.Luck.Rating95",
            >= 90 => "Tools.Test.Luck.Rating90",
            >= 60 => "Tools.Test.Luck.Rating60",
            >= 40 => "Tools.Test.Luck.Rating40",
            >= 30 => "Tools.Test.Luck.Rating30",
            >= 10 => "Tools.Test.Luck.Rating10",
            _ => "Tools.Test.Luck.Rating0"
        };

        return Lang.Text(key);
    }

    private void BtnCreateShortcut_Click(object sender, MouseButtonEventArgs e)
    {
        var shortcutName = Lang.Text("Tools.Test.Shortcut.FileName", ".lnk");
        var desktopName = Lang.Text("Tools.Test.Shortcut.Desktop");
        var startName = Lang.Text("Tools.Test.Shortcut.StartMenu");
        var desktop = Paths.GetSpecialPath(Environment.SpecialFolder.Desktop, shortcutName);
        var start = Paths.GetSpecialPath(Environment.SpecialFolder.StartMenu, @"Programs\" + shortcutName);
        var choice =
            ModMain.MyMsgBox(
                Lang.Text("Tools.Test.Shortcut.ConfirmMessage", desktopName, desktop, startName, start),
                Lang.Text("Tools.Test.Shortcut.SelectLocation"), Lang.Text("Common.Action.Cancel"), desktopName, startName);
        if (choice == 1)
            return;
        var shortcutPath = choice == 2 ? desktop : start;
        var locationName = choice == 2 ? desktopName : startName;
        Files.CreateShortcut(shortcutPath, Basics.ExecutablePath);
        HintService.Hint(Lang.Text("Tools.Test.Shortcut.Created", locationName), HintType.Success);
    }

    // 启动计数显示
    private void BtnLaunchCount_Click(object sender, MouseButtonEventArgs e)
    {
        ModMain.MyMsgBox(Lang.Text("Tools.Test.LaunchCount.Message", States.System.LaunchCount), Lang.Text("Tools.Test.LaunchCount.Title"));
    }

    private async void BtnAchievementPreview_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            var url = GetAchievementUrl();
            ModBase.Log("[Net] 获取网络结果" + url);
            await LoadImageAsync(url);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "预览成就图片失败", ModBase.LogLevel.Hint);
        }
    }

    private async Task LoadImageAsync(string imageUrl)
    {
        var client = NetworkService.GetClient();
        try
        {
            var response = await client.GetAsync(imageUrl);
            if (response.IsSuccessStatusCode)
                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.StreamSource = stream;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();

                    Dispatcher.Invoke(() =>
                    {
                        AchievementImage.Source = bitmapImage;
                        AchievementImage.Visibility = Visibility.Visible;
                    });
                }
            else if (response.StatusCode == HttpStatusCode.NotFound)
                Dispatcher.Invoke(() =>
                {
                    ModBase.Log("获取成就图片失败（404）");
                    HintService.Hint(Lang.Text("Tools.Test.Achievement.FetchFailed"), HintType.Error);
                });

            else
                Dispatcher.Invoke(() => ModBase.Log("获取成就图片失败（" + (int)response.StatusCode + "）"));
        }

        catch (Exception ex)
        {
            Dispatcher.Invoke(() => ModBase.Log(ex, "获取成就图片失败"));
        }
    }

    private async void BtnAchievementSave_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            var url = GetAchievementUrl();
            await DownloadImageToLocalAsync(url);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "保存成就图片失败", ModBase.LogLevel.Hint);
        }
    }

    private async Task DownloadImageToLocalAsync(string imageUrl)
    {
        var savePath = ModBase.pathTemp + @"Download\" + ModBase.GetHash(imageUrl) + ".png";
        var client = NetworkService.GetClient();
        try
        {
            // 异步发送 GET 请求
            var response = await client.GetAsync(imageUrl);

            // 如果响应状态码是成功的，则继续
            if (response.IsSuccessStatusCode)
            {
                // 异步读取响应内容为字节流
                var imageBytes = await response.Content.ReadAsByteArrayAsync();

                // 将字节写入本地文件
                File.WriteAllBytes(savePath, imageBytes);

                var path =
                    SystemDialogs.SelectSaveFile(Lang.Text("Tools.Test.Achievement.Save"), AchievementTitleTextBox.Text + ".png", Lang.Text("Tools.Test.Achievement.FileFilter"));
                if (string.IsNullOrEmpty(path))
                {
                    ModBase.Log("用户取消了保存操作");
                    File.Delete(savePath);
                    return;
                }

                ModBase.CopyFile(savePath, path);
                File.Delete(savePath);
                HintService.Hint(Lang.Text("Tools.Test.Achievement.Saved"), HintType.Success);
            }
            // 下载成功，返回 True
            else if (response.StatusCode == HttpStatusCode.NotFound)
            {
                // 捕获 404 错误
                ModBase.Log("获取成就图片失败（404）");
                HintService.Hint(Lang.Text("Tools.Test.Achievement.FetchFailed"), HintType.Error);
            }
            else
            {
                // 处理其他非成功状态码
                ModBase.Log("获取成就图片失败（" + (int)response.StatusCode + "）");
            }
        }

        catch (Exception ex)
        {
            // 捕获所有其他异常（如网络连接问题）
            ModBase.Log(ex, "获取成就图片失败");
        }
    }

    private string GetAchievementUrl()
    {
        var block = AchievementBlockTextBox.Text.Trim();
        var title = AchievementTitleTextBox.Text.Replace(" ", "..");
        var str1 = AchievementString1TextBox.Text.Replace(" ", "..");
        var str2 = AchievementString2TextBox.Text.Replace(" ", "..");
        var url = $"https://minecraft-api.com/api/achivements/{block}/{title}/{str1}";
        if (!string.IsNullOrEmpty(str2)) url += $"/{str2}";
        return url;
    }

    private void BtnCrash_Click(object sender, MouseButtonEventArgs e)
    {
        throw new Exception(Lang.Text("Tools.Test.Crash.ManualCrash"));
    }

    private int GetHeadSize() => CmbHeadSize.SelectedIndex switch
    {
        0 => 64,
        1 => 96,
        2 => 128,
        _ => 64
    };

    private void BtnSelectSkin_Click(object sender, RoutedEventArgs e)
    {
        var filePath = SystemDialogs.SelectFile(Lang.Text("Tools.Test.Avatar.FileFilter"),
            Lang.Text("Tools.Test.Avatar.SelectSkinFile"));
        if (!string.IsNullOrEmpty(filePath)) LoadAndGenerateHead(filePath);
    }

    private void LoadAndGenerateHead(string skinPath)
    {
        try
        {
            using (var stream = new FileStream(skinPath, FileMode.Open, FileAccess.Read))
            {
                currentSkinBitmap = new Bitmap(stream);
            }

            this.skinPath = skinPath;

            if (currentSkinBitmap.Width != currentSkinBitmap.Height)
            {
                HintService.Hint(Lang.Text("Tools.Test.Avatar.InvalidSize"), HintType.Error);
                SkinPreviewBorder.Visibility = Visibility.Collapsed;
                return;
            }

            generatedHeadBitmap = GenerateHeadFromSkin(currentSkinBitmap);

            ImgFace.Source = BitmapToBitmapImage(generatedHeadBitmap);
            ImgHair.Source = null;

            SkinPreviewBorder.Visibility = Visibility.Visible;
            HintService.Hint(Lang.Text("Tools.Test.Avatar.Generated"), HintType.Success);
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "生成头像失败");
            HintService.Hint(Lang.Text("Tools.Test.Avatar.GenerateFailed", ex.Message), HintType.Error);
            SkinPreviewBorder.Visibility = Visibility.Collapsed;
        }
    }

    private Bitmap GenerateHeadFromSkin(Bitmap skinBitmap)
    {
        var scale = skinBitmap.Width / 64;
        headSize = GetHeadSize();
        var headBitmap = new Bitmap(headSize, headSize);

        using (var g = Graphics.FromImage(headBitmap))
        {
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            DrawFaceLayer(g, skinBitmap, scale);
            if (skinBitmap.Width >= 64) DrawHairLayer(headBitmap, skinBitmap, scale);
        }

        return headBitmap;
    }

    private void DrawFaceLayer(Graphics g, Bitmap skinBitmap, int scale)
    {
        var faceRect = new Rectangle(8 * scale, 8 * scale, 8 * scale, 8 * scale);
        var faceSize = headSize - headSize / 8;
        var faceScaled = new Bitmap(faceSize, faceSize);

        using (var gFace = Graphics.FromImage(faceScaled))
        {
            gFace.InterpolationMode = InterpolationMode.NearestNeighbor;
            gFace.PixelOffsetMode = PixelOffsetMode.Half;
            gFace.DrawImage(skinBitmap, new Rectangle(0, 0, faceSize, faceSize), faceRect, GraphicsUnit.Pixel);
        }

        var offset = headSize / 16;
        g.DrawImage(faceScaled, offset, offset, faceSize, faceSize);
    }

    private void DrawHairLayer(Bitmap headBitmap, Bitmap skinBitmap, int scale)
    {
        var hairRect = new Rectangle(40 * scale, 8 * scale, 8 * scale, 8 * scale);
        var hairScaled = new Bitmap(headSize, headSize);

        using (var gHair = Graphics.FromImage(hairScaled))
        {
            gHair.InterpolationMode = InterpolationMode.NearestNeighbor;
            gHair.PixelOffsetMode = PixelOffsetMode.Half;
            gHair.DrawImage(skinBitmap, new Rectangle(0, 0, headSize, headSize), hairRect, GraphicsUnit.Pixel);
        }

        for (int x = 0, loopTo = headSize - 1; x <= loopTo; x++)
        for (int y = 0, loopTo1 = headSize - 1; y <= loopTo1; y++)
        {
            var pixel = hairScaled.GetPixel(x, y);
            if (pixel.A > 0) headBitmap.SetPixel(x, y, pixel);
        }
    }

    private void BtnSaveHead_Click(object sender, MouseButtonEventArgs e)
    {
        if (generatedHeadBitmap is null)
        {
            HintService.Hint(Lang.Text("Tools.Test.Avatar.SelectFirst"), HintType.Error);
            return;
        }

        var savePath = SystemDialogs.SelectSaveFile(Lang.Text("Tools.Test.Avatar.Save"), "Head.png");

        if (string.IsNullOrEmpty(savePath))
            return;

        generatedHeadBitmap.Save(savePath, ImageFormat.Png);
        HintService.Hint(Lang.Text("Tools.Test.Avatar.Saved"), HintType.Success);
    }

    private void CmbHeadSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (currentSkinBitmap is not null && skinPath is not null) LoadAndGenerateHead(skinPath);
    }

    private BitmapImage BitmapToBitmapImage(Bitmap bitmap)
    {
        using (var memoryStream = new MemoryStream())
        {
            bitmap.Save(memoryStream, ImageFormat.Png);
            memoryStream.Position = 0L;

            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = memoryStream;
            bitmapImage.EndInit();
            bitmapImage.Freeze();

            return bitmapImage;
        }
    }

    private void TextDownloadFolder_OnValidatedTextChanged(object sender, RoutedEventArgs e)
    {
        SaveCacheDownloadFolder(sender, e);
        TextDownloadName_ValidateChanged(sender, e);
    }
}
