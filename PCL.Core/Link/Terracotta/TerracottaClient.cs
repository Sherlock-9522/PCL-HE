using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Link.Terracotta;

/// <summary>
///     陶瓦联机（Terracotta）本地控制接口的客户端。
/// </summary>
/// <remarks>
///     <para>
///         陶瓦是一个独立的后台程序，它在本机回环地址上暴露一组纯 GET 的 HTTP 接口，
///         所有操作都通过这些接口完成：真正的 HTTP 路由位于其
///         <c>src/server/mod.rs</c> 与 <c>src/server/states.rs</c>，
///         业务编排位于 <c>src/controller/api.rs</c>。
///     </para>
///     <para>
///         本类只通过进程间接口与陶瓦通信，不做任何形式的原生链接 ——
///         这既是陶瓦许可例外条款允许的用法，也避免了把对方代码卷入本项目。
///         调用方因此必须保持陶瓦的二进制原封不动。
///     </para>
/// </remarks>
public sealed class TerracottaClient : IDisposable
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _baseAddress;

    /// <summary>
    ///     创建一个指向指定端口的客户端。
    /// </summary>
    /// <param name="port">陶瓦控制接口监听的端口，由 <see cref="TerracottaProcess"/> 探测得到。</param>
    public TerracottaClient(int port)
    {
        if (port is <= 0 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        _baseAddress = $"http://127.0.0.1:{port}";
    }

    /// <summary>当前连接的端口。</summary>
    public int Port => int.Parse(_baseAddress[( _baseAddress.LastIndexOf(':') + 1)..]);

    /// <summary>读取陶瓦自身的元信息，可用于确认一个已有实例是否就是我们期望的版本。</summary>
    public Task<TerracottaMeta?> GetMetaAsync(CancellationToken ct = default)
        => GetJsonAsync<TerracottaMeta>("/meta", ct);

    /// <summary>读取当前状态。</summary>
    public Task<TerracottaState?> GetStateAsync(CancellationToken ct = default)
        => GetJsonAsync<TerracottaState>("/state", ct);

    /// <summary>
    ///     让陶瓦回到空闲状态，用于取消或重新开始一次联机。
    /// </summary>
    public Task ResetAsync(CancellationToken ct = default)
        => GetBodilessAsync("/state/ide", ct);

    /// <summary>
    ///     作为房主创建房间。
    /// </summary>
    /// <param name="playerName">在房间中显示的玩家名。</param>
    /// <param name="publicNodes">额外的公共节点；留空则只使用陶瓦内置的节点。</param>
    /// <remarks>
    ///     这个调用立即返回，真正的结果需要轮询 <see cref="GetStateAsync"/> 得到：
    ///     状态会依次经过 <c>host-scanning</c>、<c>host-starting</c>，
    ///     最终在 <c>host-ok</c> 时由响应中的 <c>room</c> 字段给出邀请码。
    /// </remarks>
    public Task StartHostingAsync(string? playerName = null, IEnumerable<string>? publicNodes = null,
        CancellationToken ct = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(playerName)) query.Add($"player={Uri.EscapeDataString(playerName)}");
        AppendNodes(query, publicNodes);
        return GetBodilessAsync("/state/scanning" + BuildQuery(query), ct);
    }

    /// <summary>
    ///     加入一个房间。
    /// </summary>
    /// <param name="inviteCode">邀请码，建议先用 <see cref="TerracottaInviteCode.TryParse"/> 校验。</param>
    /// <param name="playerName">在房间中显示的玩家名。</param>
    /// <param name="publicNodes">额外的公共节点；留空则只使用陶瓦内置的节点。</param>
    /// <remarks>
    ///     邀请码格式非法时陶瓦会返回 400，因此调用前应先在本地校验。
    ///     真正的连接结果同样需要轮询 <see cref="GetStateAsync"/>，
    ///     在 <c>guest-ok</c> 时由响应中的 <c>url</c> 字段给出要连接的地址。
    /// </remarks>
    public Task JoinAsync(string inviteCode, string? playerName = null, IEnumerable<string>? publicNodes = null,
        CancellationToken ct = default)
    {
        var query = new List<string> { $"room={Uri.EscapeDataString(inviteCode)}" };
        if (!string.IsNullOrWhiteSpace(playerName)) query.Add($"player={Uri.EscapeDataString(playerName)}");
        AppendNodes(query, publicNodes);
        return GetBodilessAsync("/state/guesting" + BuildQuery(query), ct);
    }

    /// <summary>
    ///     请求陶瓦优雅退出。
    /// </summary>
    public async Task ShutdownAsync(CancellationToken ct = default)
    {
        try
        {
            await GetBodilessAsync("/panic?peaceful=true", ct).ConfigureAwait(false);
        }
        catch
        {
            // 进程退出会让连接被立即切断，这里把"连接中断"视为退出成功。
        }
    }

    /// <summary>
    ///     取回陶瓦的运行日志。
    /// </summary>
    public async Task<string?> GetLogAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await Http.GetAsync($"{_baseAddress}/log?fetch=true", ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private static void AppendNodes(ICollection<string> query, IEnumerable<string>? publicNodes)
    {
        if (publicNodes is null) return;
        foreach (var node in publicNodes)
        {
            if (string.IsNullOrWhiteSpace(node)) continue;
            query.Add($"public_nodes={Uri.EscapeDataString(node)}");
        }
    }

    private static string BuildQuery(IReadOnlyCollection<string> parts)
        => parts.Count == 0 ? string.Empty : "?" + string.Join('&', parts);

    private async Task<T?> GetJsonAsync<T>(string path, CancellationToken ct) where T : class
    {
        using var response = await Http.GetAsync(_baseAddress + path, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(body)) return null;
        return JsonSerializer.Deserialize<T>(body, JsonOptions);
    }

    private async Task GetBodilessAsync(string path, CancellationToken ct)
    {
        using var response = await Http.GetAsync(_baseAddress + path, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"陶瓦接口 {path} 返回 {(int)response.StatusCode}");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // HttpClient 是共享的，这里没有需要释放的托管资源；
        // 实现 IDisposable 是为了让调用方能用 using 表达生命周期意图。
    }
}
