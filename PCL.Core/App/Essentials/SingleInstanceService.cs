using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using PCL.Core.App.IoC;
using PCL.Core.IO;
using PCL.Core.Utils;

namespace PCL.Core.App.Essentials;

[LifecycleService(LifecycleState.BeforeLoading, Priority = -2134567890)]
[LifecycleScope("single-instance", "单例", false)]
public sealed partial class SingleInstanceService
{
    private static FileStream? _lockStream;
    private static readonly string _LockFilePath = Path.Combine(Paths.SharedLocalData, "instance.lock");

    private static void _TryRpc(string processId, string content)
    {
        var pipeName = $"{RpcService.PipePrefix}@{processId}";
        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
        pipe.Connect(1000);
        using var sw = new StreamWriter(pipe, PipeComm.PipeEncoding);
        sw.WriteLine(content);
        sw.Write(PipeComm.PipeEndingChar);
        sw.Flush();
    }

    [LifecycleStart]
    private static void _Start()
    {
        try
        {
            var stream = File.Open(_LockFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
            Context.Debug("未发现重复实例，正在向单例锁写入信息");
            using var sw = new StreamWriter(stream, Encoding.ASCII, 8, true);
            sw.Write(Basics.CurrentProcessId);
            sw.Flush();
            _lockStream = stream;
        }
        catch (Exception exCreate)
        {
            // 只有「锁文件存在且被另一个进程占用」才说明确实已有实例在运行。
            // 目录不存在、无写权限、磁盘已满这类环境问题同样会走到这里，但那时
            // 直接退出会让启动器毫无反应、用户也无从判断原因。单例锁只负责防止
            // 重复启动，缺了它程序照常可用，所以这种情况选择继续启动。
            if (!_TryNotifyExistingInstance())
            {
                Context.Warn("无法创建单例锁，已跳过单例检查继续启动", exCreate);
                return;
            }
            Context.RequestExit(1);
        }
    }

    /// <summary>
    ///     尝试读取锁文件里的进程号并唤醒那个实例。
    /// </summary>
    /// <returns>确实找到并通知了另一个实例时为 <c>true</c>；锁文件不可读或内容无效时为 <c>false</c>。</returns>
    private static bool _TryNotifyExistingInstance()
    {
        try
        {
            using var stream = File.Open(_LockFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            var pid = reader.ReadToEnd().Trim();
            if (string.IsNullOrWhiteSpace(pid))
            {
                // 上一个实例写到一半就退出了，锁文件是空的，不算重复实例
                Context.Debug("单例锁内容为空，忽略");
                return false;
            }
            Context.Info($"发现重复实例 {pid}，尝试传递参数并拉起主窗口");
            try
            {
                _TryRpc(pid, "REQ cli\n" + JsonSerializer.Serialize(StartupService.UnhandledCommands, JsonCompat.SerializerOptions));
                _TryRpc(pid, "REQ activate");
            }
            catch (Exception ex) { Context.Warn("RPC 通信失败", ex); }
            return true;
        }
        catch (Exception ex)
        {
            Context.Debug("读取单例锁失败，判定为环境问题", ex);
            return false;
        }
    }

    [LifecycleStop]
    private static void _Stop()
    {
        if (_lockStream is null) return;
        Context.Debug("正在删除单例锁");
        try
        {
            _lockStream.Dispose();
            File.Delete(_LockFilePath);
        }
        catch (Exception ex)
        {
            // 锁文件删不掉不影响退出，下次启动会覆盖它
            Context.Debug("删除单例锁失败，忽略", ex);
        }
    }
}
