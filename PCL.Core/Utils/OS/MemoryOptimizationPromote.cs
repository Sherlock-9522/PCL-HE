using System;
using System.Threading;
using PCL.Core.App.Essentials;
using MemOpt = PCL.MemoryOptimizer.MemoryOptimizer;

namespace PCL.Core.Utils.OS;

/// <summary>
/// 内存优化的提权侧入口。
/// <para>
/// 深度清理需要 <c>SeProfileSingleProcessPrivilege</c> 与 <c>SeIncreaseQuotaPrivilege</c>，
/// 因此执行体必须运行在提权进程中；这里的操作由 <see cref="PCL.Core.App.Essentials.PromoteService"/>
/// 在提权子进程内注册并调用。
/// </para>
/// </summary>
public static class MemoryOptimizationPromote
{
    /// <summary>
    /// 提权操作 <c>memory-deep-clean</c>：清空全系统工作集（信息类 80 / 命令 2），
    /// 返回压缩为单行的 JSON 报告。
    /// </summary>
    [PromoteOperation("memory-deep-clean")]
    public static string? DeepClean(string? arg)
    {
        var report = MemOpt.RunDeepSystemWide(CancellationToken.None);
        return MemOpt.SerializeCompact(report);
    }

    /// <summary>
    /// 经提权子进程执行一次深度清理，并把结果反序列化后返回。
    /// <para>
    /// 若提权通道已就绪（本次启动器运行期间用户已授权过一次），直接复用、不会再次弹 UAC；
    /// 尚未就绪时会拉起提权进程并触发一次授权请求，用户取消则返回 <c>null</c>。
    /// </para>
    /// </summary>
    /// <param name="timeout">等待结果的最长时间，缺省 2 分钟</param>
    /// <returns>优化报告；返回 <c>null</c> 表示未执行或执行失败，调用方应自行提示用户</returns>
    public static PCL.MemoryOptimizer.OptimizationReport? RunDeepViaPromote(TimeSpan? timeout = null)
    {
        PCL.MemoryOptimizer.OptimizationReport? report = null;
        using var done = new ManualResetEventSlim(false);
        PromoteService.Append("memory-deep-clean", result =>
        {
            try
            {
                if (!string.IsNullOrEmpty(result)) report = MemOpt.Deserialize(result);
            }
            catch (Exception)
            {
                // 解析失败时保持 report 为 null，由调用方统一提示用户
            }
            finally
            {
                done.Set();
            }
        });
        if (!PromoteService.Activate()) return null;
        done.Wait(timeout ?? TimeSpan.FromMinutes(2));
        return report;
    }
}
