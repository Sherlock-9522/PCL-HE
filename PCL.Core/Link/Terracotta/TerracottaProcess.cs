using System;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App;
using PCL.Core.Logging;

namespace PCL.Core.Link.Terracotta;

/// <summary>
///     负责把陶瓦（Terracotta）这个独立后台程序取到本地、跑起来，并告诉调用方它的控制端口。
/// </summary>
/// <remarks>
///     <para>
///         陶瓦以「未修改的官方二进制 + 进程间 HTTP 接口」的方式被使用，
///         这正是它许可例外条款允许的两种用法。因此本类<strong>绝不</strong>重新编译、
///         补丁或改写它的二进制，也不对它做任何形式的原生链接。
///     </para>
///     <para>
///         陶瓦自身的单实例语义值得注意：它用 <c>%TEMP%\terracotta\terracotta.lock</c>
///         做互斥，若同一时刻有另一个「编译时间更新」的实例被拉起来，
///         新实例会让旧实例退出并接管。因此这里始终优先复用已经在跑的实例，
///         并固定使用同一个版本目录，避免出现互相杀进程的局面。
///     </para>
///     <para>
///         它在 Windows 上监听动态端口（rocket 以 <c>port: 0</c> 启动），
///         官方为此提供了 <c>--hmcl &lt;文件&gt;</c> 参数：
///         进程就绪后把 <c>{"port":N}</c> 写进该文件。这是最稳的端口发现方式，
///         本类以它为主，以读取锁文件为辅。
///     </para>
/// </remarks>
public static class TerracottaProcess
{
    /// <summary>所使用的陶瓦版本。固定单一版本，避免出现实例互相接管。</summary>
    public const string Version = "0.4.2";

    private const string Tag = "Terracotta";

    /// <summary>等待陶瓦写出端口号的上限。官方自身在 <c>--hmcl</c> 模式下轮询 8 秒。</summary>
    private static readonly TimeSpan PortWaitTimeout = TimeSpan.FromSeconds(12);

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };

    private static readonly SemaphoreSlim Gate = new(1, 1);

    private static TerracottaClient? _client;

    private static string InstallDirectory => Path.Combine(Paths.SharedLocalData, "Terracotta", Version);

    private static string BinaryName => $"terracotta-{Version}-windows-{ArchTag}.exe";

    /// <summary>陶瓦在 Windows 上不是单文件，这个运行时库必须与主程序放在同一目录。</summary>
    private const string RuntimeDllName = "VCRUNTIME140.DLL";

    private static string ArchitectureTag
    {
        get
        {
            var tag = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture switch
            {
                System.Runtime.InteropServices.Architecture.X64 => "x86_64",
                System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
                _ => "x86_64"
            };
            return tag;
        }
    }

    private static string ArchTag => ArchitectureTag;

    /// <summary>
    ///     取到一个已经就绪的陶瓦客户端，必要时先把陶瓦拉起来。
    /// </summary>
    /// <param name="ct">取消标记。</param>
    /// <returns>指向本机陶瓦控制接口的客户端。</returns>
    public static async Task<TerracottaClient> EnsureRunningAsync(CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_client is not null) return _client;

            // 先尝试复用已经在跑的实例：可能是上一次会话留下的，也可能是用户自己启动的。
            // 这一步很重要，因为贸然再拉一个进程可能触发陶瓦的实例接管逻辑。
            var existing = await TryAttachExistingAsync(ct).ConfigureAwait(false);
            if (existing is not null)
            {
                LogWrapper.Info(Tag, $"复用已在运行的陶瓦实例，端口 {existing.Port}");
                _client = existing;
                return existing;
            }

            var executable = await EnsureBinaryAsync(ct).ConfigureAwait(false);
            _client = await LaunchAsync(executable, ct).ConfigureAwait(false);
            return _client;
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>
    ///     丢弃当前持有的客户端引用，使下一次 <see cref="EnsureRunningAsync"/> 重新探测。
    /// </summary>
    public static void ResetClient()
    {
        // 与 EnsureRunningAsync 共用同一把 Gate，否则并发的探测会丢掉刚启动的 client。
        Gate.Wait();
        try
        {
            _client = null;
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>
    ///     请求陶瓦优雅退出。这不会影响其他用户进程，只针对本机这个实例。
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         陶瓦是一个独立后台进程，启动器不显式关闭它，它就会一直驻留：
    ///         既占着 EasyTier 的虚拟网卡与端口，也会让下次启动器把它当作
    ///         “已经在运行的实例”直接复用，用户会以为“关掉启动器还在联机”。
    ///         因此主窗体退出流程必须调用这里。
    ///     </para>
    ///     <para>
    ///         本机同一时间只允许一个陶瓦实例（<c>%TEMP%/terracotta/terracotta.lock</c> 互斥），
    ///         所以关闭当前连着的实例就等于结束本次联机；若该实例是本机其它启动器拉起的，
    ///         在锁语义下双方共享的本来就是同一个实例，随启动器退出而结束联机同样符合预期。
    ///     </para>
    /// </remarks>
    public static async Task ShutdownAsync(CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var client = _client;
            _client = null;
            if (client is null) return;
            await client.ShutdownAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>
    ///     尝试连接一个已经在运行的陶瓦实例。
    /// </summary>
    /// <remarks>
    ///     端口有两个来源：官方的锁文件 <c>terracotta.lock</c>（前两个字节是大端序端口号），
    ///     以及逐个尝试附近的端口。只有 <c>/meta</c> 真的应答了才算成功。
    /// </remarks>
    private static async Task<TerracottaClient?> TryAttachExistingAsync(CancellationToken ct)
    {
        var port = TryReadLockedPort();
        if (port is { } p && await ProbeAsync(p, ct).ConfigureAwait(false))
            return new TerracottaClient(p);
        return null;
    }

    /// <summary>读取陶瓦锁文件里记录的端口号。</summary>
    private static int? TryReadLockedPort()
    {
        try
        {
            var path = Path.Combine(Path.GetTempPath(), "terracotta", "terracotta.lock");
            if (!File.Exists(path)) return null;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            Span<byte> head = stackalloc byte[2];
            if (stream.Read(head) < 2) return null;
            var port = (head[0] << 8) | head[1];
            return port is > 0 and <= 65535 ? port : null;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(Tag, "读取陶瓦锁文件失败: " + ex.Message);
            return null;
        }
    }

    /// <summary>确认某个端口上确实是陶瓦在应答。</summary>
    private static async Task<bool> ProbeAsync(int port, CancellationToken ct)
    {
        try
        {
            using var client = new TerracottaClient(port);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            var meta = await client.GetMetaAsync(timeout.Token).ConfigureAwait(false);
            if (meta is null) return false;
            LogWrapper.Info(Tag, $"探测到陶瓦 {meta.Version}（EasyTier {meta.EasytierVersion}）");
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     启动一个新的陶瓦进程，并等待它报出控制端口。
    /// </summary>
    private static async Task<TerracottaClient> LaunchAsync(string executable, CancellationToken ct)
    {
        var portFile = Path.Combine(Path.GetTempPath(), $"pcl-he-terracotta-{Guid.NewGuid():N}.json");
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = Path.GetDirectoryName(executable)!,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("--hmcl");
            startInfo.ArgumentList.Add(portFile);

            LogWrapper.Info(Tag, "正在启动陶瓦: " + executable);
            using var process = Process.Start(startInfo);
            if (process is null) throw new InvalidOperationException("无法启动陶瓦进程");

            var port = await WaitForPortAsync(portFile, process, ct).ConfigureAwait(false);
            var client = new TerracottaClient(port);

            if (!await ProbeAsync(port, ct).ConfigureAwait(false))
                throw new InvalidOperationException("陶瓦已报出端口但控制接口无应答");

            LogWrapper.Info(Tag, $"陶瓦已就绪，控制端口 {port}");
            return client;
        }
        finally
        {
            try
            {
                if (File.Exists(portFile)) File.Delete(portFile);
            }
            catch
            {
                // 临时文件清理失败不影响功能。
            }
        }
    }

    /// <summary>
    ///     等待陶瓦把端口号写进约定的文件。
    /// </summary>
    /// <remarks>
    ///     <c>--hmcl</c> 模式下真正长期驻留的是它以 DETACHED 方式拉起的
    ///     <c>--hmcl2</c> 子进程，父进程写完端口号就会退出，
    ///     因此这里不能把「父进程退出」当成失败。
    /// </remarks>
    private static async Task<int> WaitForPortAsync(string portFile, Process process, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + PortWaitTimeout;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            if (File.Exists(portFile))
            {
                try
                {
                    var text = await File.ReadAllTextAsync(portFile, ct).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        using var document = JsonDocument.Parse(text);
                        if (document.RootElement.TryGetProperty("port", out var portElement) &&
                            portElement.TryGetInt32(out var port) && port is > 0 and <= 65535)
                            return port;
                    }
                }
                catch (JsonException)
                {
                    // 文件可能正写到一半，下一轮再读。
                }
                catch (IOException)
                {
                    // 同上。
                }
            }

            await Task.Delay(100, ct).ConfigureAwait(false);
        }

        throw new TimeoutException(process.HasExited
            ? $"陶瓦进程已退出（退出码 {process.ExitCode}），未能取得控制端口"
            : "等待陶瓦报告控制端口超时");
    }

    /// <summary>
    ///     确保官方二进制已经就位，必要时下载并解压。
    /// </summary>
    /// <returns>主程序的可执行文件路径。</returns>
    private static async Task<string> EnsureBinaryAsync(CancellationToken ct)
    {
        var directory = InstallDirectory;
        var executable = Path.Combine(directory, BinaryName);
        var runtime = Path.Combine(directory, RuntimeDllName);

        if (File.Exists(executable) && File.Exists(runtime))
        {
            LogWrapper.Info(Tag, "陶瓦已就位: " + executable);
            return executable;
        }

        Directory.CreateDirectory(directory);

        // 官方发布的是 tar.gz 包，解开后含主程序与 VCRUNTIME140.DLL 两个文件。
        var archive = Path.Combine(directory, $"terracotta-{Version}-windows-{ArchTag}-pkg.tar.gz");
        var relative = $"burningtnt/Terracotta/releases/download/v{Version}/" +
                       $"terracotta-{Version}-windows-{ArchTag}-pkg.tar.gz";

        // GitHub 在国内多数网络下无法直连，因此按「直连 → 公共反代」顺序回退。
        var urls = new[]
        {
            "https://github.com/" + relative,
            "https://gh-proxy.com/https://github.com/" + relative,
            "https://ghfast.top/https://github.com/" + relative
        };

        Exception? lastError = null;
        var downloaded = false;
        foreach (var url in urls)
        {
            ct.ThrowIfCancellationRequested();
            LogWrapper.Info(Tag, "正在下载陶瓦: " + url);
            try
            {
                await DownloadAsync(url, archive, ct).ConfigureAwait(false);
                ExtractArchive(archive, directory);
                downloaded = true;
                break;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastError = ex;
                LogWrapper.Warn(Tag, $"下载源不可用（{url}）：{ex.Message}");
            }
            finally
            {
                try
                {
                    if (File.Exists(archive)) File.Delete(archive);
                }
                catch
                {
                    // 忽略清理失败。
                }
            }
        }

        if (!downloaded)
            throw new IOException("所有陶瓦下载源均不可用，请检查网络连接。", lastError);

        // 压缩包内的主程序名与版本号绑定，这里统一改名为本类约定的名字，
        // 这样升级版本时只需改动 Version 常量。
        var extracted = Path.Combine(directory, BinaryName);
        if (!File.Exists(extracted))
        {
            var candidate = Directory.GetFiles(directory, "terracotta-*.exe");
            if (candidate.Length == 0)
                throw new FileNotFoundException("陶瓦压缩包中未找到可执行文件", directory);
            File.Move(candidate[0], extracted, true);
        }

        if (!File.Exists(runtime))
            throw new FileNotFoundException($"陶瓦缺少 {RuntimeDllName}，无法启动", runtime);

        LogWrapper.Info(Tag, "陶瓦安装完成: " + executable);
        return executable;
    }

    private static async Task DownloadAsync(string url, string destination, CancellationToken ct)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var target = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
        await source.CopyToAsync(target, ct).ConfigureAwait(false);
    }

    /// <summary>把 tar.gz 归档解压到目标目录。</summary>
    private static void ExtractArchive(string archivePath, string destination)
    {
        using var fileStream = File.OpenRead(archivePath);
        using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
        using var reader = new TarReader(gzipStream);

        while (reader.GetNextEntry() is { } entry)
        {
            var name = Path.GetFileName(entry.Name);
            if (string.IsNullOrEmpty(name)) continue;
            if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                !name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) continue;

            var target = Path.Combine(destination, name);
            entry.ExtractToFile(target, true);
        }
    }
}
