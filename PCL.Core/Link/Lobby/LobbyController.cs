using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Link.EasyTier;
using PCL.Core.Link.Scaffolding;
using PCL.Core.Link.Scaffolding.Client.Models;
using PCL.Core.Link.Scaffolding.Client.Requests;
using PCL.Core.Logging;
using PCL.Core.Utils.Exts;
using PCL.Core.Utils.OS;
using PCL.Core.Utils.Secret;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.IO.Net;
using static PCL.Core.Link.Lobby.LobbyInfoProvider;
using static PCL.Core.Link.Natayark.NatayarkProfileManager;
using LobbyType = PCL.Core.Link.Scaffolding.Client.Models.LobbyType;
using PCL.Core.Link.McPing;
using PCL.Core.IO.Net.Http;
using PCL.Core.IO.Net.SocketForward;

namespace PCL.Core.Link.Lobby;

/// <summary>
/// The controller of lobby that used for creating Scaffolding entity.
/// </summary>
public sealed class LobbyController
{
    /// <summary>
    /// Demonstrate the current lobby is host or joiner.
    /// </summary>
    public bool IsHost = false;

    /// <summary>
    /// Scaffolding client entity.
    /// </summary>
    public ScaffoldingClientEntity? ScfClientEntity;

    /// <summary>
    /// Scaffolding server entity.
    /// </summary>
    public ScaffoldingServerEntity? ScfServerEntity;

    /// <summary>
    /// Launch a Scaffolding Client.
    /// </summary>
    /// <param name="username">Join user name.</param>
    /// <param name="code">Lobby share code.</param>
    /// <returns>Created <see cref="ScaffoldingClientEntity"/>.</returns>
    public async Task<ScaffoldingClientEntity?> LaunchClientAsync(string username, string code, CancellationToken ct = default)
    {
        if (!await _SendTelemetryAsync(false).ConfigureAwait(false))
        {
            return null;
        }

        try
        {
            var scfEntity = await ScaffoldingFactory
                .CreateClientAsync(username, code, LobbyType.Scaffolding, ct).ConfigureAwait(false);

            ScfClientEntity = scfEntity;

            await scfEntity.Client.ConnectAsync().ConfigureAwait(false);

            var port = await scfEntity.Client.SendRequestAsync(new GetServerPortRequest()).ConfigureAwait(false);

            var hostname = string.Empty;

            // 服务端不返回玩家列表时（半开连接、服务端异常），这里会一直等下去，
            // 界面就永久停在「连接中」且没有取消入口。给一个总时限，超时按失败处理。
            const int playerListTimeoutMs = 15_000;
            var playerListWaited = 0;
            while (scfEntity.Client.PlayerList is null)
            {
                if (playerListWaited >= playerListTimeoutMs)
                    throw new TimeoutException("等待大厅玩家列表超时，可能是大厅服务端无响应");
                ct.ThrowIfCancellationRequested();
                await Task.Delay(800, ct).ConfigureAwait(false);
                playerListWaited += 800;
            }

            foreach (var profile in scfEntity.Client.PlayerList)
            {
                if (profile.Kind == PlayerKind.HOST)
                {
                    hostname = profile.Name;
                    LogWrapper.Debug($"大厅创建者的用户名: {hostname}");
                }
            }

            var localPort = await scfEntity.EasyTier
                .AddPortForwardAsync(scfEntity.HostInfo.Ip, port)
                .ConfigureAwait(false);
            var desc = hostname.IsNullOrWhiteSpace()
                ? string.Empty
                : Lang.Text("Link.Lobby.MotdDesc", hostname);

            var tcpPortForForward = NetworkHelper.NewTcpPort();
            // 重复加入时旧转发实例会被直接覆盖，它的监听 socket 与连接信号量就无人释放了。
            if (McForward is not null || McBroadcast is not null)
            {
                McForward?.Stop();
                McBroadcast?.Stop();
                McForward = null;
                McBroadcast = null;
            }
            McForward = new TcpForwardBuilder()
                .BindLocal((ushort)tcpPortForForward)
                .SetRemote(IPAddress.Loopback, (ushort)localPort)
                .Build();
            McForward.Start();
            McBroadcast = new BroadcastLocal(Lang.Text("Link.Lobby.MotdFormat", desc), tcpPortForForward);
            McBroadcast.Start();

            return scfEntity;
        }
        catch (ArgumentNullException e)
        {
            LogWrapper.Error(e, "大厅创建者的用户名为空");
        }
        catch (ArgumentException e)
        {
            if (e.Message.Contains("lobby code"))
            {
                LogWrapper.Error(e, "大厅编号无效");
            }
            else if (e.Message.Contains("hostname"))
            {
                LogWrapper.Error(e, "大厅创建者的用户名无效");
            }
            else
            {
                LogWrapper.Error(e, "在加入大厅时出现意外的无效参数");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            LogWrapper.Error(e, "在加入大厅时发生意外错误");
        }

        return null;
    }

    /// <summary>
    /// Launch a Scaffolding Server.
    /// </summary>
    /// <param name="username">Host user name.</param>
    /// <param name="port">Minecraft port.</param>
    /// <returns>Created <see cref="ScaffoldingServerEntity"/>.</returns>
    /// <remarks>
    /// Because of event handling of Scaffolding Server. You SHOULD start the server on your own.
    /// </remarks>
    public async Task<ScaffoldingServerEntity?> LaunchServerAsync(string username, int port)
    {
        if (!await _SendTelemetryAsync(true).ConfigureAwait(false))
        {
            return null;
        }

        try
        {
            var scfEntity = ScaffoldingFactory.CreateServer(port, username);
            ScfServerEntity = scfEntity;
            IsHost = true;

            LogWrapper.Info("LobbyController", "Successfully to launch Scaffolding Server.");

            return scfEntity;
        }
        catch (Exception e)
        {
            LogWrapper.Error(e, "Occurred error when launching Scafolding Server.");
        }

        return null;
    }

    /// <summary>
    /// 检查主机的 MC 实例是否可用。
    /// </summary>
    public static async Task<bool> IsHostInstanceAvailableAsync(int port)
    {
        using var ping = McPingServiceFactory.CreateService("127.0.0.1", port);
        var info = await ping.PingAsync().ConfigureAwait(false);

        if (info is not null) return true;

        LogWrapper.Warn("Link", $"本地 MC 局域网实例 ({port}) 疑似已关闭");

        return false;
    }

    /// <summary>
    /// 退出大厅。这将同时关闭 EasyTier 和 MC 端口转发，需要自行清理 UI。
    /// </summary>
    public async Task<int> CloseAsync()
    {
        McForward?.Stop();
        McForward = null;
        McBroadcast?.Stop();
        McBroadcast = null;
        try
        {
            if (ScfClientEntity is not null)
            {
                await ScfClientEntity.EasyTier.StopAsync().ConfigureAwait(false);
                await ScfClientEntity.Client.DisposeAsync().ConfigureAwait(false);
            }
            else if (ScfServerEntity is not null)
            {
                await ScfServerEntity.EasyTier.StopAsync().ConfigureAwait(false);
                await ScfServerEntity.Server.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            ScfClientEntity = null;
            ScfServerEntity = null;
        }
        return 0;
    }

    private static async Task<bool> _SendTelemetryAsync(bool isHost)
    {
        // PCL-HE：本方法不向任何服务器上报数据，恒返回 false，
        // 调用方会按「跳过上报」继续联机流程。
        //
        // 上游 PCL-CE 在此收集 LauncherId、Natayark 账户 ID / 邮箱 / 最近登录 IP
        // 以及中继服务器列表，并 POST 到其联机统计服务。那套实现已整体移除：它与本项目
        // 「不经过任何自有服务器、不收集个人信息」的联机说明直接冲突，保留在源码里也
        // 有被误启用的风险。
        _ = isHost;
        LogWrapper.Info("Link", "PCL-HE 不上报联机遥测数据，已跳过。");
        return await Task.FromResult(false).ConfigureAwait(false);
    }
}
