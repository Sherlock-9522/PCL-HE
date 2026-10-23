using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using PCL.Core.App;
using PCL.Core.Logging;
using PCL.Core.Utils.Exts;

namespace PCL.Core.Utils.OS;

public static partial class EnvironmentInterop
{
    private const string LogModule = "Environment";

    /// <summary>
    /// 本地密钥文件的位置：启动器数据目录下的 <c>secrets.json</c>。
    /// <para>
    /// 用途是让使用者自带凭据（目前主要是 CurseForge API Key），而凭据本身永远不进仓库：
    /// 该文件名已写进仓库根的 .gitignore，既不会被提交，也不会被 GitHub 的爬虫扫到。
    /// </para>
    /// <para>
    /// 文件内容是扁平 JSON，例如：<c>{ "CURSEFORGE_API_KEY": "你的 Key" }</c>。
    /// 键名就是 <see cref="GetSecret"/> 的 <c>key</c> 参数（不带 <c>PCL_</c> 前缀）。
    /// </para>
    /// </summary>
    private static string LocalSecretsFilePath => Path.Combine(Paths.SharedLocalData ?? "", "secrets.json");

    private static readonly Dictionary<string, string?> LocalSecretsCache = new();

    /// <summary>
    /// 读取环境变量并使用 <see cref="StringExtension.Convert{T}"/> 将其转换为指定类型并写入目标引用。
    /// </summary>
    /// <param name="key">环境变量名</param>
    /// <param name="target">需要写入的目标引用 (不存在该环境变量或转换失败时不会写入)</param>
    /// <param name="detailLog">是否在日志中输出变量值</param>
    /// <typeparam name="TValue">目标引用的类型</typeparam>
    /// <returns>是否成功写入目标引用</returns>
    public static bool ReadVariable<TValue>(string key, ref TValue target, bool detailLog = true)
    {
        var envValue = Environment.GetEnvironmentVariable(key);
        if (envValue is null) return false;
        var valueLog = detailLog ? $" = {envValue}" : string.Empty;
        LogWrapper.Debug(LogModule, $"读取到环境变量 {key}{valueLog}");
        var value = envValue.Convert<TValue>();
        if (value is null)
        {
            LogWrapper.Warn(LogModule, $"环境变量 {key} 类型转换失败");
            return false;
        }
        target = value;
        return true;
    }

    public static string? GetSecret(string key, bool readEnv = true, bool readEnvDebugOnly = false)
    {
        if (!SecretDictionary.TryGetValue(key, out var value) &&
            readEnv &&
#if !DEBUG
            !readEnvDebugOnly &&
#endif
            ReadVariable($"PCL_{key}", ref value, false)
        ) SecretDictionary[key] = value;
        // 内置值与环境变量都拿不到时，回落到本地 secrets.json：
        // 这样 Release 版用户也能自带 CurseForge Key，而 Key 本身永远不会进仓库。
        return value.ReplaceNullOrEmpty() is { Length: > 0 } ? value : ReadLocalSecret(key);
    }

    /// <summary>
    /// 从本地 <c>secrets.json</c> 读取一个密钥；文件不存在、键缺失或解析失败都返回 <c>null</c>，
    /// 由调用方自行降级（例如没有 CurseForge Key 时就不显示 CurseForge 下载源）。
    /// 结果按关键字缓存，避免每次请求都读盘。
    /// </summary>
    private static string? ReadLocalSecret(string key)
    {
        if (LocalSecretsCache.TryGetValue(key, out var cached)) return cached;

        string? result = null;
        try
        {
            var file = LocalSecretsFilePath;
            if (File.Exists(file))
            {
                var secrets = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file));
                if (secrets is not null && secrets.TryGetValue(key, out var fromFile) &&
                    !string.IsNullOrWhiteSpace(fromFile))
                {
                    result = fromFile;
                    LogWrapper.Debug(LogModule, $"已从 secrets.json 读取到 {key}");
                }
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(LogModule, $"读取 secrets.json 失败，已忽略：{ex.Message}");
        }

        LocalSecretsCache[key] = result;
        return result;
    }
    
    /// <summary>
    /// 获取当前操作系统名称。
    /// </summary>
    /// <returns>返回小写的操作系统名称，如 "windows", "linux", "osx"。</returns>
    public static string GetCurrentOsName() {
        if (OperatingSystem.IsWindows())
            return "windows";
        if (OperatingSystem.IsLinux())
            return "linux";
        return OperatingSystem.IsMacOS() 
            ? "osx"
            : "unknown";
    }
}
