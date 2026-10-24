using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PCL.Core.Utils.OS;

/// <summary>
/// 单个进程的内存占用记录
/// </summary>
/// <param name="Name">进程名（不含扩展名）</param>
/// <param name="Id">进程 ID</param>
/// <param name="WorkingSetBytes">物理内存工作集大小</param>
public readonly record struct ProcessMemoryUsage(string Name, int Id, long WorkingSetBytes);

/// <summary>
/// 启动前内存评估结论
/// </summary>
public enum MemoryVerdict
{
    /// <summary>内存充裕</summary>
    Sufficient,

    /// <summary>内存偏紧，可以启动但可能发生卡顿或频繁 GC</summary>
    Tight,

    /// <summary>内存明显不足，启动很可能失败或导致系统极度卡顿</summary>
    Insufficient
}

/// <summary>
/// 内存诊断结果
/// </summary>
/// <param name="Verdict">评估结论</param>
/// <param name="Snapshot">评估时的系统内存快照</param>
/// <param name="RequestedBytes">游戏请求的最大堆内存</param>
/// <param name="TopProcesses">占用物理内存最多的若干进程</param>
/// <param name="PageFileLooksDisabled">提交上限几乎等于物理内存，通常意味着页面文件被禁用或过小</param>
public sealed record MemoryReport(
    MemoryVerdict Verdict,
    KernelInterop.MemorySnapshot Snapshot,
    ulong RequestedBytes,
    IReadOnlyList<ProcessMemoryUsage> TopProcesses,
    bool PageFileLooksDisabled)
{
    /// <summary>可用物理内存（GiB，保留一位小数）</summary>
    public double AvailablePhysicalGb => Math.Round(Snapshot.AvailablePhysical / 1073741824d, 1);

    /// <summary>提交内存剩余量（GiB，保留一位小数）</summary>
    public double CommitAvailableGb => Math.Round(Snapshot.CommitAvailable / 1073741824d, 1);

    /// <summary>请求的游戏内存（GiB，保留一位小数）</summary>
    public double RequestedGb => Math.Round(RequestedBytes / 1073741824d, 1);
}

/// <summary>
/// 内存诊断。<br/>
/// <para>
/// 本类只读取系统状态，不会修改任何进程的工作集，因此不存在影响系统稳定性的风险。
/// 需要主动释放内存时请使用 PCL.MemoryOptimizer 子项目：它提供「轻量护航」（只整理启动器自身工作集）
/// 与「深度清理」（清空全系统工作集，需管理员授权）两档，机制、权限与代价见该项目的 DESIGN.md。
/// 历史上的「遍历所有进程逐个清空工作集」做法已经删除：它既无法排除用户正在使用的程序
/// （实机实测 265 个进程中可打开 113 个，其工作集合计 7.12 GB，正是用户此刻正在用的程序），
/// 统计口径也不可靠，因此不再提供。
/// </para>
/// </summary>
public static class MemoryDiagnostics
{
    /// <summary>除游戏堆之外，为其保留的安全余量（1.5 GiB），用于 JVM 自身、纹理与系统</summary>
    private const ulong ReserveBytes = 1610612736UL;

    /// <summary>
    /// 评估以 <paramref name="requestedBytes"/> 作为最大堆启动游戏是否安全。
    /// </summary>
    /// <param name="requestedBytes">游戏请求的最大堆内存（即 -Xmx）</param>
    /// <param name="topProcessCount">报告中包含的进程条目数量</param>
    public static MemoryReport EvaluateForLaunch(ulong requestedBytes, int topProcessCount = 8)
    {
        var snapshot = KernelInterop.GetMemorySnapshot();
        var top = GetTopMemoryProcesses(topProcessCount);

        // 页面文件被禁用时，提交上限会退化为接近物理内存总量
        var pageFileLooksDisabled = snapshot.CommitLimit <= snapshot.TotalPhysical + (256UL * 1024 * 1024);

        var verdict = EvaluateVerdict(snapshot, requestedBytes, pageFileLooksDisabled);

        return new MemoryReport(verdict, snapshot, requestedBytes, top, pageFileLooksDisabled);
    }

    /// <summary>
    /// 依据内存快照判断结论。抽成独立方法便于在不触碰真实系统的情况下做单元测试。
    /// </summary>
    public static MemoryVerdict EvaluateVerdict(
        KernelInterop.MemorySnapshot snapshot,
        ulong requestedBytes,
        bool pageFileLooksDisabled)
    {
        // 提交内存是硬约束：JVM 无法 Commit 到足够的地址空间时会直接启动失败
        if (snapshot.CommitAvailable < requestedBytes + ReserveBytes) return MemoryVerdict.Insufficient;

        // 页面文件被禁用时，即便当前提交余量看似够用，运行期也缺少回旋空间
        if (pageFileLooksDisabled) return MemoryVerdict.Tight;

        // 可用物理内存明显低于请求量，意味着启动后必然大量换页
        if (snapshot.AvailablePhysical < requestedBytes + ReserveBytes) return MemoryVerdict.Tight;

        return MemoryVerdict.Sufficient;
    }

    /// <summary>
    /// 获取占用物理内存最多的若干进程。读取失败（权限不足或进程已退出）的条目会被跳过。
    /// </summary>
    public static IReadOnlyList<ProcessMemoryUsage> GetTopMemoryProcesses(int count = 8)
    {
        if (count <= 0) return [];

        var list = new List<ProcessMemoryUsage>();
        Process[] processes;
        try
        {
            processes = Process.GetProcesses();
        }
        catch (Exception ex)
        {
            // 枚举进程本身失败时不应影响启动流程
            Logging.LogWrapper.Warn(ex, "枚举进程失败，跳过内存占用统计");
            return [];
        }

        foreach (var process in processes)
        {
            try
            {
                var workingSet = process.WorkingSet64;
                if (workingSet > 0) list.Add(new ProcessMemoryUsage(process.ProcessName, process.Id, workingSet));
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
            {
                // 系统进程或高权限进程无法读取，忽略
            }
            finally
            {
                process.Dispose();
            }
        }

        return list
            .OrderByDescending(x => x.WorkingSetBytes)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// 将报告格式化为可写入日志的多行文本
    /// </summary>
    public static IEnumerable<string> Describe(MemoryReport report)
    {
        yield return "内存诊断：";
        yield return $"  物理内存：可用 {report.AvailablePhysicalGb} GiB / 共 {Math.Round(report.Snapshot.TotalPhysical / 1073741824d, 1)} GiB";
        yield return $"  提交内存：可用 {report.CommitAvailableGb} GiB / 共 {Math.Round(report.Snapshot.CommitLimit / 1073741824d, 1)} GiB（内存占用 {report.Snapshot.MemoryLoadPercent:F0}%）";
        yield return $"  游戏请求：{report.RequestedGb} GiB";
        if (report.PageFileLooksDisabled)
            yield return "  注意：提交上限接近物理内存总量，页面文件可能已被禁用或设置过小。";
        yield return $"  结论：{report.Verdict}";

        if (report.TopProcesses.Count > 0)
        {
            yield return "  内存占用最高的进程：";
            foreach (var process in report.TopProcesses)
                yield return $"    {process.Name} (#{process.Id})：{Math.Round(process.WorkingSetBytes / 1048576d, 0)} MiB";
        }
    }
}
