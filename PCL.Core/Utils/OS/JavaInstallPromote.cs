using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading;
using PCL.Core.App.Essentials;

namespace PCL.Core.Utils.OS;

/// <summary>
///     JRE 静默安装的提权侧入口。
///     <para>
///         MSI 安装需要写入 Program Files 与 HKLM，必然要管理员权限；这里把整个安装动作交给
///         提权子进程执行，启动器本体始终保持普通权限。安装结果以单行 JSON 返回。
///     </para>
/// </summary>
public static class JavaInstallPromote
{
    /// <summary>MSI 返回码：安装成功但需要重启。</summary>
    private const int ErrorSuccessRebootRequired = 3010;

    /// <summary>
    ///     提权操作 <c>java-install-msi</c>：以 <c>/qn /norestart</c> 静默安装给定的 MSI。
    /// </summary>
    /// <param name="arg">形如 <c>安装包完整路径|安装目录</c>，安装目录可省略</param>
    /// <returns>形如 <c>{"ok":true,"code":0}</c> 的单行 JSON</returns>
    [PromoteOperation("java-install-msi")]
    public static string? InstallMsi(string? arg)
    {
        var result = new JsonObject
        {
            ["ok"] = false,
            ["code"] = -1
        };

        var parts = (arg ?? "").Split('|');
        var msiPath = parts[0];
        var installDir = parts.Length > 1 ? parts[1] : null;
        if (string.IsNullOrWhiteSpace(msiPath) || !File.Exists(msiPath))
        {
            result["message"] = "安装包不存在";
            return result.ToJsonString();
        }

        try
        {
            var msiexec = Path.Combine(Environment.SystemDirectory, "msiexec.exe");
            // INSTALLDIR 是 Adoptium 安装包里的 public property，命令行可以直接覆盖默认安装目录
            var arguments = $"/i \"{msiPath}\" /qn /norestart";
            if (!string.IsNullOrWhiteSpace(installDir)) arguments += $" INSTALLDIR=\"{installDir}\"";
            var startInfo = new ProcessStartInfo
            {
                FileName = msiexec,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                result["message"] = "无法启动 msiexec";
                return result.ToJsonString();
            }

            // 静默安装通常几十秒；给足 10 分钟，超时说明安装器卡住，直接放行让调用方看到失败
            if (!process.WaitForExit(TimeSpan.FromMinutes(10)))
            {
                result["message"] = "安装超时";
                return result.ToJsonString();
            }

            var code = process.ExitCode;
            result["code"] = code;
            result["ok"] = code is 0 or ErrorSuccessRebootRequired;
            if (code == ErrorSuccessRebootRequired) result["rebootRequired"] = true;
            if (!(code is 0 or ErrorSuccessRebootRequired)) result["message"] = $"安装器返回 {code}";
            return result.ToJsonString();
        }
        catch (Exception ex)
        {
            result["message"] = ex.Message;
            return result.ToJsonString();
        }
    }

    /// <summary>
    ///     提权操作 <c>java-uninstall-msi</c>：以 <c>/qn /norestart</c> 静默卸载给定的产品。
    /// </summary>
    /// <param name="arg">形如 <c>{产品代码}</c>，即卸载登记表的键名</param>
    /// <returns>形如 <c>{"ok":true,"code":0}</c> 的单行 JSON</returns>
    [PromoteOperation("java-uninstall-msi")]
    public static string? UninstallMsi(string? arg)
    {
        var result = new JsonObject
        {
            ["ok"] = false,
            ["code"] = -1
        };

        var productCode = (arg ?? "").Trim();
        if (!productCode.StartsWith('{') || !productCode.EndsWith('}'))
        {
            result["message"] = "产品代码无效";
            return result.ToJsonString();
        }

        try
        {
            var msiexec = Path.Combine(Environment.SystemDirectory, "msiexec.exe");
            var startInfo = new ProcessStartInfo
            {
                FileName = msiexec,
                Arguments = $"/x {productCode} /qn /norestart",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                result["message"] = "无法启动 msiexec";
                return result.ToJsonString();
            }

            if (!process.WaitForExit(TimeSpan.FromMinutes(10)))
            {
                result["message"] = "卸载超时";
                return result.ToJsonString();
            }

            var code = process.ExitCode;
            result["code"] = code;
            result["ok"] = code is 0 or ErrorSuccessRebootRequired;
            if (code == ErrorSuccessRebootRequired) result["rebootRequired"] = true;
            if (!(code is 0 or ErrorSuccessRebootRequired)) result["message"] = $"卸载器返回 {code}";
            return result.ToJsonString();
        }
        catch (Exception ex)
        {
            result["message"] = ex.Message;
            return result.ToJsonString();
        }
    }

    /// <summary>解析提权进程返回的单行 JSON，解析不了时返回 <c>null</c>。</summary>
    private static InstallResult? ParseResult(string? raw)
    {
        try
        {
            if (string.IsNullOrEmpty(raw) || JsonNode.Parse(raw) is not JsonObject json) return null;
            return new InstallResult(
                json["ok"]?.GetValue<bool>() ?? false,
                json["code"]?.GetValue<int>() ?? -1,
                json["rebootRequired"]?.GetValue<bool>() ?? false,
                json["message"]?.GetValue<string>());
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>一次静默安装的结果。</summary>
    public sealed record InstallResult(bool Ok, int Code, bool RebootRequired, string? Message);

    /// <summary>
    ///     经提权子进程静默安装一个 MSI。
    ///     <para>
    ///         若提权通道已在启动器运行期间接通（用户此前授权过别的提权操作），不会再次弹 UAC。
    ///         返回 <c>null</c> 表示用户取消了授权或等待超时。
    ///     </para>
    /// </summary>
    /// <param name="msiPath">安装包完整路径</param>
    /// <param name="installDirectory">目标安装目录，为 <c>null</c> 时用安装包自带的默认目录</param>
    /// <param name="timeout">等待安装完成的最长时间，缺省 5 分钟</param>
    public static InstallResult? RunInstallViaPromote(string msiPath, string? installDirectory = null,
        TimeSpan? timeout = null)
    {
        InstallResult? result = null;
        using var done = new ManualResetEventSlim(false);
        // 命令格式为「操作名 参数」，提权进程按第一个空格拆分，参数里的空格会原样保留
        var command = string.IsNullOrWhiteSpace(installDirectory)
            ? $"java-install-msi {msiPath}"
            : $"java-install-msi {msiPath}|{installDirectory}";
        PromoteService.Append(command, raw =>
        {
            // 结果无法解析时保持 null，由调用方按失败处理
            result = ParseResult(raw);
            done.Set();
        });
        if (!PromoteService.Activate()) return null;
        done.Wait(timeout ?? TimeSpan.FromMinutes(5));
        return result;
    }

    /// <summary>
    ///     经提权子进程静默卸载一个产品。
    ///     <para>
    ///         与 <see cref="RunInstallViaPromote"/> 共用提权通道，等待语义一致：
    ///         返回 <c>null</c> 表示用户取消了授权或等待超时。
    ///     </para>
    /// </summary>
    /// <param name="productCode">卸载登记表的键名，形如 <c>{4925953F-...}</c></param>
    /// <param name="timeout">等待卸载完成的最长时间，缺省 5 分钟</param>
    public static InstallResult? RunUninstallViaPromote(string productCode, TimeSpan? timeout = null)
    {
        InstallResult? result = null;
        using var done = new ManualResetEventSlim(false);
        PromoteService.Append($"java-uninstall-msi {productCode}", raw =>
        {
            result = ParseResult(raw);
            done.Set();
        });
        if (!PromoteService.Activate()) return null;
        done.Wait(timeout ?? TimeSpan.FromMinutes(5));
        return result;
    }
}
