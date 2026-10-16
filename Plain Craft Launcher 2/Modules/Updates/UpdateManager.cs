using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Utils;
using PCL.Core.Utils.OS;

namespace PCL;

public static class UpdateManager
{
    public static bool isUpdateWaitingRestart;

    /// <summary>
    ///     PCL-HE 是独立发行的版本，不从任何上游启动器更新服务拉取版本、公告或本体。
    ///     这里刻意留空：所有取版本 / 公告 / 下载的调用都会走到「所有源都不可用」分支，
    ///     由调用方的 try/catch 兜住，不会产生任何对外网络请求。
    /// </summary>
    public static UpdatesWrapperModel remoteServer = new(new List<IUpdateSource>());

    public static bool IsCurrentVersionBeta
    {
        get
        {
            if (ModBase.versionBaseName.Contains("beta"))
                return true;
            return (int)Config.Update.UpdateChannel == 1;
        }
    }

    /// <summary>
    ///     PCL-HE 不与上游更新服务通信，因此永远报告为最新版本。
    /// </summary>
    public static UpdateEnums.VersionStatus GetVersionStatus()
    {
        return UpdateEnums.VersionStatus.Latest;
    }

    public static ModLoader.LoaderCombo<JsonObject> updateLoader;

    /// <summary>
    ///     PCL-HE 是独立发行的版本，不提供经由上游服务器的自动更新。
    ///     保留此入口仅为兼容既有调用点，调用后不会产生任何下载行为。
    /// </summary>
    public static void UpdateStart(UpdateEnums.UpdateType type, string receivedKey = null, bool forceValidated = false)
    {
        ModBase.Log("[Update] PCL-HE 不提供自动更新，已忽略更新请求");
    }

    public static void UpdateRestart(bool triggerRestartAndByEnd, bool triggerRestart = true)
    {
        try
        {
            var fileName = ModBase.exePath + @"PCL\Plain Craft Launcher Community Edition.exe";
            if (!File.Exists(fileName))
            {
                ModBase.Log("[System] 更新失败：未找到更新文件");
                return;
            }

            // id old new restart
            var text =
                $"update {Process.GetCurrentProcess().Id} \"{Basics.ExecutablePath}\" \"{fileName}\" {(triggerRestart ? "true" : "false")}";
            ModBase.Log("[System] 更新程序启动，参数：" + text);
            Process.Start(new ProcessStartInfo(fileName)
                { WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true, Arguments = text });
            if (triggerRestartAndByEnd)
            {
                ModMain.frmMain.EndProgram(false, true);
                ModBase.Log("[System] 已由于更新强制结束程序");
            }
        }
        catch (Win32Exception ex)
        {
            ModBase.Log(ex, "自动更新时触发 Win32 错误，疑似被拦截");
            ModMain.MyMsgBox(
                Lang.Text("Update.Error.UpdateBlockedMessage", ModBase.exePath),
                Lang.Text("Update.Error.UpdateBlocked"),
                Lang.Text("Common.Action.Confirm"),
                "",
                "",
                true);
        }
    }

    /// <summary>
    ///     确保 PathTemp 下的 Latest.exe 可用，它会被用于整合包打包。
    ///     PCL-HE 不从网络下载启动器本体，直接复用当前正在运行的可执行文件。
    /// </summary>
    internal static void DownloadLatestPCL(ModLoader.LoaderBase loaderToSyncProgress = null)
    {
        var latestPCLPath = Path.Combine(ModBase.pathTemp, "CE-Latest.exe");
        ModBase.CopyFile(Basics.ExecutablePath, latestPCLPath);
    }

    public static ModLoader.LoaderTask<int, int> serverLoader =
        new(Lang.Text("Update.Service.PclCe"),
            _ => LoadOnlineInfo(),
            priority: ThreadPriority.BelowNormal);

    private static void LoadOnlineInfo()
    {
        // PCL-HE 不拉取上游公告，也不按上游策略检查更新。
        ModBase.Log("[Update] PCL-HE 不检查更新、不拉取公告");
    }

    /// <summary>
    ///     展示社区版提示
    /// </summary>
    /// <param name="IsUpdate">是否为更新时启动</param>
    public static void ShowCEAnnounce()
    {
        ModMain.MyMsgBox(Lang.Text("Update.CommunityNotice.Body"),
            Lang.Text("Update.CommunityNotice.Title"),
            Lang.Text("Update.CommunityNotice.Confirm"));
    }
}
