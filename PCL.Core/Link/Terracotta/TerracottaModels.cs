using System;
using System.Text.Json.Serialization;

namespace PCL.Core.Link.Terracotta;

/// <summary>
///     陶瓦联机的连接状态。
/// </summary>
/// <remarks>
///     取值与陶瓦官方 v0.4.2 的 <c>src/controller/api.rs</c> 中状态机的字符串一一对应，
///     因此这里用字符串常量而非 C# 枚举，以免因大小写或未知新状态导致反序列化失败。
/// </remarks>
public static class TerracottaStates
{
    /// <summary>空闲，等待操作。</summary>
    public const string Waiting = "waiting";

    /// <summary>房主侧正在扫描局域网内的 Minecraft 世界。</summary>
    public const string HostScanning = "host-scanning";

    /// <summary>房主侧正在启动组网。</summary>
    public const string HostStarting = "host-starting";

    /// <summary>房主侧就绪，此时响应中带有邀请码。</summary>
    public const string HostOk = "host-ok";

    /// <summary>访客侧正在连接房主。</summary>
    public const string GuestConnecting = "guest-connecting";

    /// <summary>访客侧正在启动组网。</summary>
    public const string GuestStarting = "guest-starting";

    /// <summary>访客侧就绪，此时响应中带有可直接连接的地址。</summary>
    public const string GuestOk = "guest-ok";

    /// <summary>发生错误，此时响应中带有错误类型。</summary>
    public const string Exception = "exception";
}

/// <summary>
///     陶瓦在 <see cref="TerracottaStates.Exception"/> 状态下返回的错误类型。
/// </summary>
/// <remarks>数值取自陶瓦官方 <c>src/controller/api.rs</c>，不可随意改动。</remarks>
public enum TerracottaErrorType
{
    /// <summary>未能连通房主。</summary>
    PingHostFail = 0,

    /// <summary>与房主的连接被重置。</summary>
    PingHostRst = 1,

    /// <summary>访客侧 EasyTier 崩溃。</summary>
    GuestEasytierCrash = 2,

    /// <summary>房主侧 EasyTier 崩溃。</summary>
    HostEasytierCrash = 3,

    /// <summary>与中继服务器的连接被重置。</summary>
    PingServerRst = 4,

    /// <summary>Scaffolding 认证服务返回了无效响应。</summary>
    ScaffoldingInvalidResponse = 5
}

/// <summary>
///     陶瓦 <c>GET /state</c> 的响应。
/// </summary>
public sealed class TerracottaState
{
    /// <summary>当前状态，取值见 <see cref="TerracottaStates"/>。</summary>
    [JsonPropertyName("state")]
    public string? State { get; set; }

    /// <summary>
    ///     单调递增的序号。同一个序号表示状态未变化，
    ///     调用方可以据此跳过重复处理。
    /// </summary>
    [JsonPropertyName("index")]
    public ulong Index { get; set; }

    /// <summary>房主就绪时的邀请码，形如 <c>U/XXXX-XXXX-XXXX-XXXX</c>。</summary>
    [JsonPropertyName("room")]
    public string? Room { get; set; }

    /// <summary>访客就绪时可直接连接的地址。</summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>当前使用的档案序号。</summary>
    [JsonPropertyName("profile_index")]
    public int ProfileIndex { get; set; }

    /// <summary>出错时的错误类型。</summary>
    [JsonPropertyName("type")]
    public int? ErrorTypeRaw { get; set; }

    /// <summary>状态是否处于错误。</summary>
    [JsonIgnore]
    public bool IsException => string.Equals(State, TerracottaStates.Exception, StringComparison.OrdinalIgnoreCase);

    /// <summary>状态是否已空闲。</summary>
    [JsonIgnore]
    public bool IsWaiting => string.Equals(State, TerracottaStates.Waiting, StringComparison.OrdinalIgnoreCase);

    /// <summary>房主侧是否已就绪。</summary>
    [JsonIgnore]
    public bool IsHostReady => string.Equals(State, TerracottaStates.HostOk, StringComparison.OrdinalIgnoreCase);

    /// <summary>访客侧是否已就绪。</summary>
    [JsonIgnore]
    public bool IsGuestReady => string.Equals(State, TerracottaStates.GuestOk, StringComparison.OrdinalIgnoreCase);

    /// <summary>把 <see cref="ErrorTypeRaw"/> 转成强类型枚举。</summary>
    public TerracottaErrorType? GetErrorType()
    {
        if (ErrorTypeRaw is not { } raw) return null;
        return Enum.IsDefined(typeof(TerracottaErrorType), raw) ? (TerracottaErrorType)raw : null;
    }
}

/// <summary>
///     陶瓦 <c>GET /meta</c> 的响应，用于识别一个已经在运行的实例。
/// </summary>
public sealed class TerracottaMeta
{
    /// <summary>陶瓦自身版本号。</summary>
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    /// <summary>内嵌 EasyTier 的版本号。</summary>
    [JsonPropertyName("easytier_version")]
    public string? EasytierVersion { get; set; }

    /// <summary>Scaffolding 认证服务监听的端口。</summary>
    [JsonPropertyName("yggdrasil_port")]
    public int YggdrasilPort { get; set; }

    /// <summary>目标平台三元组。</summary>
    [JsonPropertyName("target_tuple")]
    public string? TargetTuple { get; set; }

    /// <summary>构建时间戳，陶瓦用它判断哪个实例更新。</summary>
    [JsonPropertyName("compile_timestamp")]
    public string? CompileTimestamp { get; set; }
}
