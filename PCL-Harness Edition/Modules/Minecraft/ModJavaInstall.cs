using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Microsoft.Win32;
using PCL.Core.App;
using PCL.Network;
using PCL.Network.Loaders;

namespace PCL;

/// <summary>
///     Adoptium（Eclipse Temurin）JRE 的自助下载与安装。
///     <para>
///         与 <see cref="ModJava" /> 里那套 Mojang 运行库下载不同：这里只下载一个官方 MSI 安装包，
///         装好之后由 Windows 自己维护注册表与目录，启动器只负责把用户领到正确的安装包前。
///         走 MSI 的好处是卸载、升级、多版本共存都交给系统，不会在用户机器上留下无法追踪的目录。
///     </para>
/// </summary>
public static class ModJavaInstall
{
    /// <summary>启动器提供下载的 JRE 主版本。</summary>
    public static readonly int[] SupportedFeatures = [8, 11, 17, 21, 25];

    private const string ApiTemplate =
        "https://api.adoptium.net/v3/assets/latest/{0}/hotspot?architecture=x64&image_type=jre&os=windows&vendor=eclipse";

    /// <summary>
    ///     国内镜像，按 Adoptium 官方目录结构同步。
    ///     <para>
    ///         实测（2026-10-30）：清华镜像的 8 / 11 / 17 / 21 / 25 五个版本全部在线，字节数与官方 API 完全一致；
    ///         而 GitHub Releases 直链在大量国内网络下根本连不上，要等满 30 秒超时才失败。
    ///         因此镜像排在前面、官方直链只在最后兜底：镜像缺版本时是毫秒级 404 立刻换下一个源，
    ///         这个代价远小于让直链先卡满 30 秒。
    ///     </para>
    /// </summary>
    private static readonly string[] MirrorRoots =
    [
        "https://mirrors.tuna.tsinghua.edu.cn/Adoptium",
        // 南大镜像 2026-10-30 实测对本仓库的五个版本全部返回 302，暂留作兜底，等它恢复
        "https://mirror.nju.edu.cn/Adoptium"
    ];

    /// <summary>一个可安装的 JRE 版本。</summary>
    public sealed class JreRelease
    {
        /// <summary>主版本，例如 21。</summary>
        public int Feature { get; init; }

        /// <summary>完整版本号，例如 21.0.13+9。</summary>
        public string Version { get; init; } = "";

        public string FileName { get; init; } = "";

        /// <summary>安装包的 SHA-256，空字符串表示接口没有给出。</summary>
        public string Sha256 { get; init; } = "";

        public long Size { get; init; }

        /// <summary>下载地址，按优先级排列。</summary>
        public IReadOnlyList<string> Urls { get; init; } = [];
    }

    /// <summary>安装包缓存目录（系统临时目录下，装完不留痕）。</summary>
    public static string GetDownloadFolder() => Path.Combine(Paths.Temp, "JavaInstaller");

    /// <summary>
    ///     查询指定主版本的最新 JRE 安装包。需要在工作线程调用。
    /// </summary>
    /// <exception cref="Exception">接口不可达、返回结构异常，或该版本没有 Windows x64 的 MSI。</exception>
    public static JreRelease FetchLatest(int feature, bool officialOnly = false)
    {
        ModBase.Log($"[Java] 查询 Adoptium JRE {feature} 的发布信息");
        var raw = ModNet.NetGetCodeByLoader(string.Format(ApiTemplate, feature), 30000);
        var root = ModBase.GetJson(raw) as JsonArray
                   ?? throw new Exception($"Adoptium 返回的数据不是数组（{feature}）");
        if (root.Count == 0)
            throw new Exception($"Adoptium 没有返回 JRE {feature} 的可用版本");

        var entry = root[0]!;
        var installer = entry["binary"]?["installer"]
                        ?? throw new Exception($"JRE {feature} 没有 Windows 安装包（只有压缩包）");

        var fileName = installer["name"]?.GetValue<string>();
        var link = installer["link"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(link))
            throw new Exception($"JRE {feature} 的安装包信息不完整");

        // 默认镜像优先、官方直链兜底：镜像缺版本是毫秒级 404，官方直链在部分网络下要卡满 30 秒超时才会换源。
        // 玩家也可以显式选「官方源」，那就只走 Adoptium 直链，不经任何第三方镜像。
        var urls = new List<string>();
        if (!officialOnly)
            foreach (var mirror in MirrorRoots)
                urls.Add($"{mirror}/{feature}/jre/x64/windows/{fileName}");
        urls.Add(link);

        var version = NormalizeVersion(entry["release_name"]?.GetValue<string>()
                                       ?? entry["version"]?["semver"]?.GetValue<string>()
                                       ?? feature.ToString());

        var release = new JreRelease
        {
            Feature = feature,
            Version = version,
            FileName = fileName,
            Sha256 = installer["checksum"]?.GetValue<string>() ?? "",
            Size = installer["size"]?.GetValue<long>() ?? -1,
            Urls = urls
        };
        ModBase.Log($"[Java] 最新 JRE {feature}：{version}，{SizeText(release.Size)}，{(officialOnly ? "官方源" : "镜像源优先")}，{urls.Count} 个下载源");
        return release;
    }

    /// <summary>为安装包创建下载任务，落盘到 <see cref="GetDownloadFolder" />。</summary>
    public static DownloadFile CreateDownloadTask(JreRelease release)
    {
        var folder = GetDownloadFolder();
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);
        return new DownloadFile(
            release.Urls,
            Path.Combine(folder, release.FileName),
            new ModBase.FileChecker(
                actualSize: release.Size,
                hash: string.IsNullOrWhiteSpace(release.Sha256) ? null : release.Sha256));
    }

    /// <summary>为安装包创建下载加载器（调用方负责 <c>Start()</c>）。</summary>
    public static LoaderDownload CreateLoader(JreRelease release, string taskName) =>
        new(taskName, [CreateDownloadTask(release)]);

    /// <summary>
    ///     「选择安装位置」弹窗的入参与回执。
    ///     <para>
    ///         弹窗会把用户最终选定的目录写回 <see cref="SelectedDirectory" />；
    ///         为 <c>null</c> 表示用户取消。
    ///     </para>
    /// </summary>
    public sealed class InstallPathContext
    {
        public int Feature { get; init; }
        public string Version { get; init; } = "";
        public string SystemDirectory { get; init; } = "";
        public string LauncherDirectory { get; init; } = "";

        /// <summary>是否把系统默认位置作为推荐项（只有一块硬盘时推荐它，否则推荐启动器文件夹）。</summary>
        public bool RecommendSystem { get; init; }

        public string? SelectedDirectory { get; set; }
    }

    /// <summary>按当前机器情况生成安装位置弹窗的入参。</summary>
    public static InstallPathContext CreatePathContext(JreRelease release) => new()
    {
        Feature = release.Feature,
        Version = release.Version,
        SystemDirectory = GetSystemInstallDir(release),
        LauncherDirectory = GetLauncherInstallDir(release.Feature),
        RecommendSystem = !HasOtherDrive()
    };

    /// <summary>系统里已经装好的 Adoptium JRE（来自控制面板的卸载登记表）。</summary>
    public sealed record InstalledJre(int Feature, string Version, string DisplayName, string Location,
        string? ProductCode)
    {
        /// <summary>登记表里的安装目录现在还在不在。用户手动挪过文件夹时这里会是 <c>false</c>。</summary>
        public bool LocationExists =>
            !string.IsNullOrWhiteSpace(Location) && System.IO.Directory.Exists(Location);

        /// <summary>能不能交给 <c>msiexec /x</c> 卸载。登记表键名不是产品代码时为 <c>false</c>。</summary>
        public bool CanUninstall =>
            !string.IsNullOrWhiteSpace(ProductCode) && ProductCode.StartsWith('{') && ProductCode.EndsWith('}');
    }

    private const string ArpPrefix = "Eclipse Temurin JRE";

    /// <summary>
    ///     枚举系统中已安装的 Adoptium JRE。
    ///     <para>
    ///         Adoptium 的 MSI 不会写 <c>JavaSoft</c> 注册表键，只写控制面板的卸载登记表，
    ///         所以只能从这里读，光看 <c>java.exe</c> 是找不到它的。
    ///     </para>
    /// </summary>
    public static IReadOnlyList<InstalledJre> GetInstalledJres()
    {
        var result = new List<InstalledJre>();
        string[] roots =
        [
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        ];
        foreach (var root in roots)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(root);
                if (key is null) continue;
                foreach (var name in key.GetSubKeyNames())
                {
                    using var sub = key.OpenSubKey(name);
                    if (sub?.GetValue("DisplayName") is not string display ||
                        !display.StartsWith(ArpPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                    var version = sub.GetValue("DisplayVersion") as string ?? "";
                    var location = sub.GetValue("InstallLocation") as string ?? "";
                    var feature = ParseFeature(display, version);
                    if (feature <= 0) continue;
                    location = ResolveLocation(feature, location);
                    result.Add(new InstalledJre(feature, version, display, location,
                        name.StartsWith('{') ? name : null));
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, $"[Java] 读取卸载登记表失败（{root}）", ModBase.LogLevel.Debug);
            }
        }
        return result.OrderBy(x => x.Feature).ToList();
    }

    /// <summary>
    ///     纠正卸载登记表里的安装目录。
    ///     <para>
    ///         Adoptium 的 MSI 只在安装那一刻把 <c>InstallLocation</c> 写进注册表，用户之后手动挪走文件夹它不会跟着变，
    ///         界面上就会一直显示一个已经不存在的旧路径。这里先认登记表本身，失效了再拿启动器实际扫到的同名 JRE 顶上
    ///         ——用户挪过目录之后，扫描结果才是真的。
    ///     </para>
    /// </summary>
    private static string ResolveLocation(int feature, string arpLocation)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(arpLocation) &&
                System.IO.File.Exists(Path.Combine(arpLocation, "bin", "java.exe")))
                return arpLocation;
            foreach (var entry in ModJava.Javas.GetSortedJavaList())
            {
                if (entry.Installation.MajorVersion != feature) continue;
                var folder = Path.GetDirectoryName(entry.Installation.JavaFolder);
                if (string.IsNullOrWhiteSpace(folder)) continue;
                if (!System.IO.File.Exists(Path.Combine(folder, "bin", "java.exe"))) continue;
                ModBase.Log($"[Java] 登记表里的安装目录已失效，改用实际扫描到的路径：{arpLocation} → {folder}",
                    ModBase.LogLevel.Debug);
                return folder;
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Java] 纠正已安装 JRE 的路径失败", ModBase.LogLevel.Debug);
        }

        return arpLocation;
    }

    /// <summary>查找指定主版本已安装的 JRE，没有则返回 <c>null</c>。</summary>
    public static InstalledJre? FindInstalled(int feature) =>
        GetInstalledJres().FirstOrDefault(x => x.Feature == feature);

    /// <summary>Adoptium 默认安装目录，例如 <c>C:\Program Files\Eclipse Adoptium\jre-21.0.13.9-hotspot</c>。</summary>
    public static string GetSystemInstallDir(JreRelease release)
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        return Path.Combine(programFiles, "Eclipse Adoptium", DirectoryName(release));
    }

    /// <summary>启动器目录内的安装位置，例如 <c>…\PCL\Adoptium JRE\jre-21</c>。</summary>
    public static string GetLauncherInstallDir(int feature) =>
        Path.Combine(ModBase.exePath, "PCL", "Adoptium JRE", $"jre-{feature}");

    /// <summary>去掉 release_name 里的 JDK 前缀，例如 <c>jdk8u504-b01</c>→<c>8u504-b01</c>、<c>jdk-21.0.13+9</c>→<c>21.0.13+9</c>。</summary>
    private static string NormalizeVersion(string raw)
    {
        var text = (raw ?? "").Trim();
        if (text.StartsWith("jdk-", StringComparison.OrdinalIgnoreCase)) return text[4..];
        if (text.StartsWith("jdk", StringComparison.OrdinalIgnoreCase)) return text[3..];
        return text;
    }

    /// <summary>Adoptium 使用的目录名，例如 <c>jre-21.0.13.9-hotspot</c>。</summary>
    private static string DirectoryName(JreRelease release) =>
        release.Version.Contains('+')
            ? $"jre-{release.Version.Replace('+', '.')}-hotspot"
            : $"jre-{release.Version}-hotspot";

    /// <summary>除系统盘以外是否还有别的固定磁盘（用来决定默认安装位置的推荐项）。</summary>
    public static bool HasOtherDrive()
    {
        try
        {
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var systemRoot = Path.GetPathRoot(windows) ?? "C:\\";
            return DriveInfo.GetDrives().Any(drive =>
                drive.DriveType == DriveType.Fixed && drive.IsReady &&
                !string.Equals(drive.RootDirectory.FullName, systemRoot, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Java] 枚举磁盘失败", ModBase.LogLevel.Debug);
            return false;
        }
    }

    private static int ParseFeature(string displayName, string version)
    {
        var first = version.Split('.')[0];
        if (int.TryParse(first, out var major) && major > 0) return major;
        var index = displayName.IndexOf("Hotspot ", StringComparison.OrdinalIgnoreCase);
        if (index < 0) return 0;
        var tail = displayName[(index + 8)..].Split('.')[0];
        return int.TryParse(tail, out var parsed) ? parsed : 0;
    }

    /// <summary>
    ///     把刚装好的 JRE 登记进启动器的 Java 列表并启用。
    ///     <para>
    ///         Adoptium 的 MSI 不写 <c>JavaSoft</c> 注册表键，装到自定义目录时现有扫描器不保证能发现它，
    ///         所以装完要自己登记一次；另外 <see cref="JavaManager" /> 对 8 以上的 JRE 默认禁用，
    ///         这里显式启用，免得用户装完了却看到一条删除线。
    ///     </para>
    /// </summary>
    /// <returns>登记到的 <c>java.exe</c> 路径；目录里没有 java.exe 时返回 <c>null</c>。</returns>
    public static string? RegisterInstalledDirectory(string installDirectory)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(installDirectory)) return null;
            var javaExe = Path.Combine(installDirectory, "bin", "java.exe");
            if (!File.Exists(javaExe))
            {
                ModBase.Log($"[Java] 安装目录里没有 java.exe，暂不登记：{javaExe}", ModBase.LogLevel.Debug);
                return null;
            }

            var entry = ModJava.Javas.AddOrGet(javaExe);
            if (entry is null) return null;
            entry.IsEnabled = true;
            ModJava.Javas.SaveConfig();
            ModBase.Log($"[Java] 已登记并启用新安装的 JRE：{javaExe}", ModBase.LogLevel.Feedback);
            return javaExe;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Java] 登记新安装的 JRE 失败", ModBase.LogLevel.Debug);
            return null;
        }
    }

    /// <summary>把字节数写成人看的文本。</summary>
    public static string SizeText(long bytes) => bytes switch
    {
        < 0 => "大小未知",
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024d:F0} KB",
        _ => $"{bytes / 1024d / 1024d:F1} MB"
    };
}
