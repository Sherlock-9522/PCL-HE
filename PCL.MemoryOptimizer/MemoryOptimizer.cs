using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PCL.MemoryOptimizer;

/// <summary>优化档位。</summary>
public enum CleanupMode
{
    /// <summary>轻量护航：只整理本进程工作集，不碰任何其他程序。</summary>
    Guarded,

    /// <summary>深度清理（全系统）：信息类 80 / 命令 2，一次性清空所有进程工作集。需提权。</summary>
    DeepSystemWide,

    /// <summary>深度清理（逐进程）：跳过指定进程逐个整理，可在游戏运行中使用。需提权才覆盖完整。</summary>
    DeepPerProcess,

    /// <summary>游戏内护航：绝不触碰游戏进程，只整理其他程序，并把游戏的内存优先级提到最高。无需提权。</summary>
    GameBoost,
}

/// <summary>一次内存快照。</summary>
public sealed record MemorySnapshot(
    long TotalPhysicalBytes,
    long AvailablePhysicalBytes,
    long CommitLimitBytes,
    long CommitAvailableBytes)
{
    public double TotalGb => TotalPhysicalBytes / 1073741824d;
    public double AvailableGb => AvailablePhysicalBytes / 1073741824d;
    public double AvailableMb => AvailablePhysicalBytes / 1048576d;
    public double CommitAvailableGb => CommitAvailableBytes / 1073741824d;
}

/// <summary>单个进程被跳过的原因。</summary>
public enum ProcessSkipReason
{
    None,
    /// <summary>启动器自身。</summary>
    Self,
    /// <summary>由调用方指定的受保护进程（通常是游戏）。</summary>
    Protected,
    /// <summary>系统关键进程，清理它们没有收益且可能影响稳定性。</summary>
    Critical,
    /// <summary>句柄打不开（权限不足）。</summary>
    AccessDenied,
    /// <summary>进程已退出。</summary>
    Exited,
}

/// <summary>单次优化的完整结果，可直接序列化后跨进程传递。</summary>
public sealed record OptimizationReport
{
    public CleanupMode Mode { get; init; }
    public bool Succeeded { get; init; }
    public int NativeStatus { get; init; }
    public long ElapsedMilliseconds { get; init; }
    public MemorySnapshot Before { get; init; } = new(0, 0, 0, 0);
    public MemorySnapshot After { get; init; } = new(0, 0, 0, 0);
    public long WorkingSetBefore { get; init; }
    public long WorkingSetAfter { get; init; }
    public int ProcessCountBefore { get; init; }
    public int ProcessCountAfter { get; init; }
    public int ProcessedCount { get; init; }
    public int SkippedCount { get; init; }
    public int FailedCount { get; init; }

    /// <summary>成功提升内存优先级的进程数（仅游戏内护航档使用）。</summary>
    public int PriorityRaisedCount { get; init; }

    public bool Cancelled { get; init; }
    public string? ErrorMessage { get; init; }

    /// <summary>可用物理内存的实际增量，是给用户看的主指标。</summary>
    public long FreedAvailableBytes => After.AvailablePhysicalBytes - Before.AvailablePhysicalBytes;

    public long FreedAvailableMb => FreedAvailableBytes / 1048576;

    /// <summary>全系统工作集减少量（可能大于可用内存增量，因为部分页进了待机列表）。</summary>
    public long WorkingSetReducedBytes => WorkingSetBefore - WorkingSetAfter;

    public long WorkingSetReducedMb => WorkingSetReducedBytes / 1048576;
}

/// <summary>优化选项。</summary>
public sealed record MemoryOptimizerOptions
{
    /// <summary>受保护、绝不清空的进程 ID（游戏进程放这里）。</summary>
    public IReadOnlySet<uint> ProtectedProcessIds { get; init; } = new HashSet<uint>();

    /// <summary>逐进程遍历时每处理一个进程后的让出时间，避免抢占 CPU。</summary>
    public int ThrottleMilliseconds { get; init; } = 4;

    /// <summary>逐进程遍历时是否跳过系统关键进程。默认开启。</summary>
    public bool SkipCriticalProcesses { get; init; } = true;

    /// <summary>本次最多处理多少个进程；0 表示不限制。仅用于调试与自检。</summary>
    public int MaxProcesses { get; init; }
}

/// <summary>
/// PCL-HE 内存优化器。
/// 只把暂时用不到的物理内存页移出，不结束任何进程，不修改任何业务数据。
/// </summary>
public static class MemoryOptimizer
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static readonly JsonSerializerOptions CompactJsonOptions = new() { WriteIndented = false };

    /// <summary>系统关键进程名单：清理它们收益极低，却可能影响系统稳定性。</summary>
    private static readonly HashSet<string> CriticalNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Idle", "Registry", "Memory Compression", "MemCompression",
        "smss", "csrss", "wininit", "winlogon", "services", "lsass", "lsaiso",
        "svchost", "fontdrvhost", "dwm", "audiodg", "WUDFHost", "SecurityHealthService",
        "MsMpEng", "NisSrv", "SgrmBroker", "MpDefenderCoreService",
    };

    /// <summary>读取一次系统内存快照。不读取进程工作集，因此极快。</summary>
    public static MemorySnapshot TakeSnapshot()
    {
        var status = new NativeMethods.MEMORYSTATUSEX();
        status.dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MEMORYSTATUSEX>();
        if (!NativeMethods.GlobalMemoryStatusEx(ref status))
        {
            return new MemorySnapshot(0, 0, 0, 0);
        }
        return new MemorySnapshot(
            (long)status.ullTotalPhys,
            (long)status.ullAvailPhys,
            (long)status.ullTotalPageFile,
            (long)status.ullAvailPageFile);
    }

    /// <summary>
    /// 尝试为当前进程启用深度清理所需的两个特权。
    /// 这两个特权在管理员令牌里也是「已禁用」状态，必须显式启用。
    /// 返回值同时充当「当前进程是否具备深度清理能力」的判据。
    /// </summary>
    public static bool TryEnablePrivileges(out string? error)
    {
        error = null;
        if (!NativeMethods.OpenProcessToken(NativeMethods.GetCurrentProcess(),
                NativeMethods.TOKEN_ADJUST_PRIVILEGES | NativeMethods.TOKEN_QUERY, out var token))
        {
            error = "无法打开当前进程令牌。";
            return false;
        }
        try
        {
            foreach (var name in new[] { NativeMethods.SeProfileSingleProcess, NativeMethods.SeIncreaseQuota })
            {
                if (!NativeMethods.LookupPrivilegeValue(null, name, out var luid))
                {
                    error = $"找不到特权 {name}。";
                    return false;
                }
                var state = new NativeMethods.TOKEN_PRIVILEGES
                {
                    PrivilegeCount = 1,
                    Luid = luid,
                    Attributes = NativeMethods.SE_PRIVILEGE_ENABLED,
                };
                if (!NativeMethods.AdjustTokenPrivileges(token, false, ref state, 0, 0, 0))
                {
                    error = $"启用特权 {name} 失败。";
                    return false;
                }
                // AdjustTokenPrivileges 即使部分特权未分配也返回 TRUE，必须查 LastError。
                var last = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                if (last != 0)
                {
                    error = $"特权 {name} 未被授予（错误码 {last}），请以管理员身份运行。";
                    return false;
                }
            }
            return true;
        }
        finally
        {
            NativeMethods.CloseHandle(token);
        }
    }

    /// <summary>轻量护航：只整理本进程工作集，不触碰任何其他程序。</summary>
    public static OptimizationReport RunGuarded(CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        var before = TakeSnapshot();
        var selfId = (uint)Environment.ProcessId;
        var processed = 0;
        var failed = 0;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var handle = NativeMethods.OpenProcess(
                NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION | NativeMethods.PROCESS_SET_QUOTA,
                false, selfId);
            if (handle == 0)
            {
                failed++;
            }
            else
            {
                try
                {
                    if (NativeMethods.EmptyWorkingSet(handle))
                    {
                        processed++;
                    }
                    else
                    {
                        failed++;
                    }
                }
                finally
                {
                    NativeMethods.CloseHandle(handle);
                }
            }
            watch.Stop();
            return new OptimizationReport
            {
                Mode = CleanupMode.Guarded,
                Succeeded = processed > 0,
                ElapsedMilliseconds = watch.ElapsedMilliseconds,
                Before = before,
                After = TakeSnapshot(),
                ProcessedCount = processed,
                FailedCount = failed,
            };
        }
        catch (Exception ex)
        {
            watch.Stop();
            return new OptimizationReport
            {
                Mode = CleanupMode.Guarded,
                Succeeded = false,
                ElapsedMilliseconds = watch.ElapsedMilliseconds,
                Before = before,
                After = TakeSnapshot(),
                FailedCount = failed,
                ErrorMessage = ex.Message,
            };
        }
    }

    /// <summary>
    /// 深度清理（全系统）：调用信息类 80 / 命令 2，一次性清空所有进程的工作集。
    /// 该调用没有任何排除参数，因此绝不能在游戏运行中使用。
    /// </summary>
    public static OptimizationReport RunDeepSystemWide(CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        var before = TakeSnapshot();
        var metricsBefore = CollectProcessMetrics();
        var command = NativeMethods.MemoryEmptyWorkingSets;

        if (!TryEnablePrivileges(out var privilegeError))
        {
            watch.Stop();
            return new OptimizationReport
            {
                Mode = CleanupMode.DeepSystemWide,
                Succeeded = false,
                ElapsedMilliseconds = watch.ElapsedMilliseconds,
                Before = before,
                After = TakeSnapshot(),
                ErrorMessage = privilegeError ?? "当前进程不具备执行深度清理的特权。",
            };
        }

        cancellationToken.ThrowIfCancellationRequested();
        var status = NativeMethods.NtSetSystemInformation(
            NativeMethods.SystemMemoryListInformation, ref command, sizeof(int));
        watch.Stop();

        var metricsAfter = CollectProcessMetrics();
        return new OptimizationReport
        {
            Mode = CleanupMode.DeepSystemWide,
            Succeeded = status == 0,
            NativeStatus = status,
            ElapsedMilliseconds = watch.ElapsedMilliseconds,
            Before = before,
            After = TakeSnapshot(),
            WorkingSetBefore = metricsBefore.WorkingSet,
            WorkingSetAfter = metricsAfter.WorkingSet,
            ProcessCountBefore = metricsBefore.Count,
            ProcessCountAfter = metricsAfter.Count,
            ErrorMessage = status == 0 ? null : $"内核返回 0x{status:X8}。",
        };
    }

    /// <summary>
    /// 深度清理（逐进程）：逐个整理工作集，跳过受保护进程与系统关键进程。
    /// 因为可以排除进程，这是唯一能在游戏运行中使用而不触碰游戏的方式。
    /// </summary>
    public static OptimizationReport RunDeepPerProcess(
        MemoryOptimizerOptions options, CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        var before = TakeSnapshot();
        var metricsBefore = CollectProcessMetrics();
        var processed = 0;
        var skipped = 0;
        var failed = 0;
        var error = default(string);

        TryEnablePrivileges(out _);
        var selfId = (uint)Environment.ProcessId;
        Process[] processes;
        try
        {
            processes = Process.GetProcesses();
        }
        catch (Exception ex)
        {
            watch.Stop();
            return new OptimizationReport
            {
                Mode = CleanupMode.DeepPerProcess,
                Succeeded = false,
                ElapsedMilliseconds = watch.ElapsedMilliseconds,
                Before = before,
                After = TakeSnapshot(),
                ErrorMessage = "无法枚举进程：" + ex.Message,
            };
        }

        try
        {
            for (var i = 0; i < processes.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var process = processes[i];
                try
                {
                    var pid = (uint)process.Id;
                    if (pid <= 4)
                    {
                        skipped++;
                        continue;
                    }
                    if (pid == selfId || options.ProtectedProcessIds.Contains(pid))
                    {
                        skipped++;
                        continue;
                    }
                    if (options.SkipCriticalProcesses)
                    {
                        var name = process.ProcessName;
                        if (CriticalNames.Contains(name))
                        {
                            skipped++;
                            continue;
                        }
                    }

                    var handle = NativeMethods.OpenProcess(
                        NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION | NativeMethods.PROCESS_SET_QUOTA,
                        false, pid);
                    if (handle == 0)
                    {
                        // 打不开就跳过：系统进程与受保护进程属预期，不当作失败。
                        skipped++;
                        continue;
                    }
                    try
                    {
                        if (NativeMethods.EmptyWorkingSet(handle))
                        {
                            processed++;
                        }
                        else
                        {
                            failed++;
                        }
                    }
                    finally
                    {
                        NativeMethods.CloseHandle(handle);
                    }

                    if (options.MaxProcesses > 0 && processed + failed >= options.MaxProcesses)
                    {
                        skipped++;
                        break;
                    }

                    if (options.ThrottleMilliseconds > 0 && (i & 7) == 0)
                    {
                        Thread.Sleep(options.ThrottleMilliseconds);
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    error ??= ex.Message;
                }
                finally
                {
                    process.Dispose();
                }
            }
            watch.Stop();
            var metricsAfter = CollectProcessMetrics();
            return new OptimizationReport
            {
                Mode = CleanupMode.DeepPerProcess,
                Succeeded = processed > 0,
                ElapsedMilliseconds = watch.ElapsedMilliseconds,
                Before = before,
                After = TakeSnapshot(),
                WorkingSetBefore = metricsBefore.WorkingSet,
                WorkingSetAfter = metricsAfter.WorkingSet,
                ProcessCountBefore = metricsBefore.Count,
                ProcessCountAfter = metricsAfter.Count,
                ProcessedCount = processed,
                SkippedCount = skipped,
                FailedCount = failed,
                ErrorMessage = error,
            };
        }
        catch (OperationCanceledException)
        {
            watch.Stop();
            return new OptimizationReport
            {
                Mode = CleanupMode.DeepPerProcess,
                Succeeded = processed > 0,
                Cancelled = true,
                ElapsedMilliseconds = watch.ElapsedMilliseconds,
                Before = before,
                After = TakeSnapshot(),
                ProcessedCount = processed,
                SkippedCount = skipped,
                FailedCount = failed,
            };
        }
    }

    /// <summary>
    /// 游戏内护航：先把游戏进程的内存优先级提到最高，再逐个整理其他进程的工作集。
    /// 与深度清理不同，这里绝不会触碰游戏进程，因此可以在游戏运行时随时使用而不会造成卡顿。
    /// 不需要管理员权限：能整理的是当前用户有权限打开的进程，实测已覆盖本机大部分工作集。
    /// </summary>
    /// <param name="gameProcessIds">游戏进程 ID 集合，会被完整排除并提升内存优先级。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="options">可选的遍历选项（限流、最大处理数）；其中受保护进程集合始终以 gameProcessIds 为准。</param>
    public static OptimizationReport RunGameBoost(
        IReadOnlySet<uint> gameProcessIds, CancellationToken cancellationToken = default,
        MemoryOptimizerOptions? options = null)
    {
        var raised = 0;
        foreach (var pid in gameProcessIds)
        {
            if (TryRaiseMemoryPriority((int)pid))
            {
                raised++;
            }
        }

        var report = RunDeepPerProcess(options is null
            ? new MemoryOptimizerOptions { ProtectedProcessIds = gameProcessIds }
            : options with { ProtectedProcessIds = gameProcessIds }, cancellationToken);
        return report with { Mode = CleanupMode.GameBoost, PriorityRaisedCount = raised };
    }

    /// <summary>
    /// 把指定进程的内存优先级提到 NORMAL，让系统在内存紧张时尽量最后换出它的页面。
    /// 游戏进程受益最大：被换出的页面要重新从页面文件读回，直接表现为进游戏后卡顿。
    /// 失败（权限不足或进程已退出）只返回 false，不影响调用方流程。
    /// </summary>
    public static bool TryRaiseMemoryPriority(int processId)
    {
        if (processId <= 4)
        {
            return false;
        }

        var handle = NativeMethods.OpenProcess(
            NativeMethods.PROCESS_SET_INFORMATION | NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION,
            false, (uint)processId);
        if (handle == 0)
        {
            return false;
        }

        try
        {
            return NativeMethods.SetMemoryPriority(handle, NativeMethods.MEMORY_PRIORITY_NORMAL);
        }
        catch
        {
            return false;
        }
        finally
        {
            NativeMethods.CloseHandle(handle);
        }
    }

    /// <summary>把报告序列化为 JSON 文本，供提权子进程与父进程之间传递。</summary>
    public static string Serialize(OptimizationReport report) => JsonSerializer.Serialize(report, JsonOptions);

    /// <summary>
    /// 序列化为单行 JSON，用于经提权管道回传（管道协议要求单行文本）。
    /// </summary>
    public static string SerializeCompact(OptimizationReport report) =>
        JsonSerializer.Serialize(report, CompactJsonOptions);

    /// <summary>解析提权子进程写下的 JSON 结果；失败时返回 null。</summary>
    public static OptimizationReport? Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<OptimizationReport>(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>遍历全部进程，统计进程数与工作集总和。读取失败的进程跳过。</summary>
    internal static (int Count, long WorkingSet) CollectProcessMetrics()
    {
        var count = 0;
        long workingSet = 0;
        Process[] processes;
        try
        {
            processes = Process.GetProcesses();
        }
        catch
        {
            return (0, 0);
        }
        foreach (var process in processes)
        {
            try
            {
                workingSet += process.WorkingSet64;
                count++;
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception
                or InvalidOperationException or NotSupportedException)
            {
                // 权限不足或进程已退出：不计入统计，也不视为错误。
            }
            finally
            {
                process.Dispose();
            }
        }
        return (count, workingSet);
    }
}
