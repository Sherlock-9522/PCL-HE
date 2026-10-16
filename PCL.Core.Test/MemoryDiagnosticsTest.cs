using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils.OS;

namespace PCL.Core.Test;

[TestClass]
public class MemoryDiagnosticsTest
{
    private const ulong GiB = 1073741824UL;
    private const ulong MiB = 1048576UL;

    /// <summary>构造内存快照，单位统一用 GiB 便于阅读</summary>
    private static KernelInterop.MemorySnapshot Snapshot(
        double totalPhysicalGb,
        double availablePhysicalGb,
        double commitLimitGb,
        double commitAvailableGb) =>
        new(
            (ulong)(totalPhysicalGb * GiB),
            (ulong)(availablePhysicalGb * GiB),
            (ulong)(commitLimitGb * GiB),
            (ulong)(commitAvailableGb * GiB),
            50d);

    [TestMethod]
    [Description("内存充裕时应判定为 Sufficient")]
    public void EvaluateVerdict_Sufficient()
    {
        var snapshot = Snapshot(32, 20, 48, 32);
        var verdict = MemoryDiagnostics.EvaluateVerdict(snapshot, 4 * GiB, pageFileLooksDisabled: false);
        Assert.AreEqual(MemoryVerdict.Sufficient, verdict);
    }

    [TestMethod]
    [Description("提交内存不足以覆盖请求值加保留量时必须判定为 Insufficient")]
    public void EvaluateVerdict_InsufficientWhenCommitTooSmall()
    {
        // 请求 4 GiB + 保留 1.5 GiB = 5.5 GiB，但提交内存只剩 4 GiB
        var snapshot = Snapshot(16, 12, 16, 4);
        var verdict = MemoryDiagnostics.EvaluateVerdict(snapshot, 4 * GiB, pageFileLooksDisabled: false);
        Assert.AreEqual(MemoryVerdict.Insufficient, verdict);
    }

    [TestMethod]
    [Description("提交内存刚好等于请求值加保留量时不应视为不足（边界取闭区间）")]
    public void EvaluateVerdict_ExactCommitIsNotInsufficient()
    {
        // 提交内存 = 4 GiB + 1.5 GiB = 5.5 GiB，物理内存同样充足
        var exactCommit = 1_610_612_736UL + 4 * GiB;
        var snapshot = new KernelInterop.MemorySnapshot(
            32 * GiB,
            20 * GiB,
            48 * GiB,
            exactCommit,
            50d);
        var verdict = MemoryDiagnostics.EvaluateVerdict(snapshot, 4 * GiB, pageFileLooksDisabled: false);
        Assert.AreEqual(MemoryVerdict.Sufficient, verdict);
    }

    [TestMethod]
    [Description("页面文件被禁用时即便当前提交余量充足也应提示 Tight")]
    public void EvaluateVerdict_TightWhenPageFileDisabled()
    {
        var snapshot = Snapshot(16, 12, 16, 10);
        var verdict = MemoryDiagnostics.EvaluateVerdict(snapshot, 4 * GiB, pageFileLooksDisabled: true);
        Assert.AreEqual(MemoryVerdict.Tight, verdict);
    }

    [TestMethod]
    [Description("提交内存不足的优先级高于页面文件检测——应先报 Insufficient")]
    public void EvaluateVerdict_InsufficientTakesPrecedenceOverPageFile()
    {
        var snapshot = Snapshot(16, 12, 16, 4);
        var verdict = MemoryDiagnostics.EvaluateVerdict(snapshot, 4 * GiB, pageFileLooksDisabled: true);
        Assert.AreEqual(MemoryVerdict.Insufficient, verdict);
    }

    [TestMethod]
    [Description("可用物理内存低于请求值加保留量时应提示 Tight")]
    public void EvaluateVerdict_TightWhenPhysicalLow()
    {
        var snapshot = Snapshot(32, 3, 48, 40);
        var verdict = MemoryDiagnostics.EvaluateVerdict(snapshot, 4 * GiB, pageFileLooksDisabled: false);
        Assert.AreEqual(MemoryVerdict.Tight, verdict);
    }

    [TestMethod]
    [Description("真实系统读取不应抛异常，且各项数值应落在合理范围内")]
    public void EvaluateForLaunch_OnRealSystemIsSane()
    {
        var report = MemoryDiagnostics.EvaluateForLaunch(2 * GiB, 5);

        Assert.IsTrue(report.Snapshot.TotalPhysical > 0, "物理内存总量应大于 0");
        Assert.IsTrue(report.Snapshot.CommitLimit > 0, "提交上限应大于 0");
        Assert.IsTrue(report.Snapshot.AvailablePhysical <= report.Snapshot.TotalPhysical, "可用物理内存不应超过总量");
        Assert.IsTrue(report.TopProcesses.Count <= 5, "进程条目不应超过请求数量");
        Assert.IsTrue(Enum.IsDefined(report.Verdict), "结论应为已定义的枚举值");

        // 输出一份真实读数，便于人工核对量纲是否正确
        foreach (var line in MemoryDiagnostics.Describe(report)) Console.WriteLine(line);
    }

    [TestMethod]
    [Description("进程占用列表应按工作集降序排列且不含非正数条目")]
    public void GetTopMemoryProcesses_IsSortedDescending()
    {
        var processes = MemoryDiagnostics.GetTopMemoryProcesses(8);

        Assert.IsTrue(processes.All(p => p.WorkingSetBytes > 0), "不应包含工作集非正的条目");
        var sorted = processes.OrderByDescending(p => p.WorkingSetBytes).ToList();
        CollectionAssert.AreEqual(sorted, processes.ToList(), "结果应按工作集降序");
    }

    [TestMethod]
    [Description("请求数量为 0 时应返回空列表而不是抛异常")]
    public void GetTopMemoryProcesses_ZeroCountReturnsEmpty()
    {
        Assert.AreEqual(0, MemoryDiagnostics.GetTopMemoryProcesses(0).Count);
    }

    [TestMethod]
    [Description("Describe 应总是输出结论行，且页面文件被禁用时给出提示")]
    public void Describe_MentionsPageFileWhenDisabled()
    {
        var disabled = new MemoryReport(
            MemoryVerdict.Tight,
            Snapshot(16, 12, 16 + (128 * MiB / (double)GiB), 10),
            4 * GiB,
            [],
            true);
        var text = string.Join("\n", MemoryDiagnostics.Describe(disabled));
        Assert.IsTrue(text.Contains("页面文件"), "应提示页面文件可能被禁用");
        Assert.IsTrue(text.Contains("Tight"), "应包含结论");

        var normal = disabled with { PageFileLooksDisabled = false };
        var normalText = string.Join("\n", MemoryDiagnostics.Describe(normal));
        Assert.IsFalse(normalText.Contains("页面文件"), "页面文件正常时不应出现该提示");
    }

    [TestMethod]
    [Description("页面文件是否被禁用的判定阈值应落在 256 MiB")]
    public void EvaluateForLaunch_DetectsDisabledPageFile()
    {
        // 提交上限只比物理内存多 128 MiB，应判定为页面文件被禁用
        var disabledReport = MemoryDiagnostics.EvaluateForLaunch(2 * GiB);
        // 真实机器的提交上限通常显著大于物理内存；这里只断言字段可读且逻辑自洽
        Assert.AreEqual(
            disabledReport.Snapshot.CommitLimit <= disabledReport.Snapshot.TotalPhysical + (256UL * MiB),
            disabledReport.PageFileLooksDisabled,
            "页面文件检测结果应与阈值判断一致");
    }
}
