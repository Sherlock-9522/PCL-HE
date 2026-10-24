using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.IO.Net;
using PCL.Core.IO.Net.Http;
using PCL.Core.Logging;
using PCL.Core.Minecraft.IdentityModel;
using PCL.Core.Minecraft.IdentityModel.Extensions.JsonWebToken;
using PCL.Core.Minecraft.IdentityModel.Extensions.YggdrasilConnect;
using PCL.Core.Minecraft.IdentityModel.OAuth;
using PCL.Core.Minecraft.IdentityModel.Yggdrasil;
using PCL.Core.Minecraft.Profile.Models;
using YggdrasilProfile = PCL.Core.Minecraft.IdentityModel.Yggdrasil.Profile;

namespace PCL.Core.Minecraft.Profile.Authentication;

public sealed class YggdrasilConnectProvider : IAuthenticateProvider
{
    /// <summary>
    ///     内置的第三方账号服务客户端 ID。这里的值必须是本项目自行注册的应用，
    ///     而不是上游项目注册的应用 —— 否则白名单与配额都算在上游头上。
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> _BuiltInClientIds =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["littleskin.cn"] = "1637"
        };

    /// <summary>
    ///     覆盖内置客户端 ID 的环境变量前缀，完整形式为前缀加规范化后的主机名。
    /// </summary>
    private const string ClientIdOverridePrefix = "PCL_OAUTH_CLIENT_ID_";

    // 权限说明：PlayerProfiles.Read 用于登录后在启动器内列出账号下的全部角色，
    // 由用户自己挑选；MinecraftToken.Create 用于把该角色换成真正的 Minecraft 令牌。
    // 注意 PlayerProfiles.Read 与 PlayerProfiles.Select 互斥，同时申请会被拒绝（invalid_scope）。
    private static readonly string[] _Scopes =
    [
        "openid",
        "offline_access",
        "Yggdrasil.PlayerProfiles.Read",
        "Yggdrasil.MinecraftToken.Create",
        "Yggdrasil.Server.Join"
    ];

    private readonly YggdrasilOptions _options;
    private readonly string? _yggdrasilServer;
    private YggdrasilClient? _client;

    public YggdrasilConnectProvider(string discoveryAddress, string? clientId = null, string? yggdrasilServer = null)
    {
        var resolvedClientId = clientId;
        if (string.IsNullOrWhiteSpace(resolvedClientId) &&
            Uri.TryCreate(yggdrasilServer, UriKind.Absolute, out var serverUri) &&
            TryGetBuiltInClientId(serverUri.Host, out var builtInClientId))
            resolvedClientId = builtInClientId;

        _options = new YggdrasilOptions
        {
            OpenIdDiscoveryAddress = discoveryAddress,
            ClientId = resolvedClientId?.Trim() ?? string.Empty,
            OnlyDeviceAuthorize = true,
            GetClient = () => NetworkService.GetClient()
        };
        _yggdrasilServer = yggdrasilServer;
    }

    /// <summary>
    ///     解析某个认证服务器对应的 OAuth 客户端 ID。
    ///     <para>
    ///     先看 <c>PCL_OAUTH_CLIENT_ID_&lt;主机名&gt;</c> 环境变量（点与连字符换成下划线、转大写），
    ///     没有才回落到内置表。内置的 LittleSkin ID 是上游 PCL 注册的应用，授权页会显示上游名称；
    ///     使用者可以在 LittleSkin 注册自己的应用，再把新 ID 通过环境变量注入，无需改代码。
    ///     例如 littleskin.cn 对应 <c>PCL_OAUTH_CLIENT_ID_LITTLESKIN_CN</c>。
    ///     </para>
    /// </summary>
    public static bool TryGetBuiltInClientId(string host, out string clientId)
    {
        var normalizedHost = host.Trim().TrimEnd('.');
        var overrideVar = ClientIdOverridePrefix + normalizedHost.ToUpperInvariant().Replace('.', '_').Replace('-', '_');
        var overrideValue = Environment.GetEnvironmentVariable(overrideVar);
        if (!string.IsNullOrWhiteSpace(overrideValue))
        {
            clientId = overrideValue.Trim();
            return true;
        }

        return _BuiltInClientIds.TryGetValue(normalizedHost, out clientId!);
    }

    public Task InitializeAsync(CancellationToken token)
    {
        _client = new YggdrasilClient(_options);
        return _client.InitializeAsync(token);
    }

    public Task<DeviceCodeData?> GetCodePairAsync(CancellationToken token)
    {
        _EnsureInitialized();
        return _client!.GetCodePairAsync(_Scopes, token);
    }

    public Task<AuthorizeResult?> PollDeviceCodeAsync(DeviceCodeData data, CancellationToken token)
    {
        _EnsureInitialized();
        return _client!.AuthorizeWithDeviceAsync(data, token);
    }

    public async Task<AuthenticationResult> AuthenticateAsync(AuthenticationRequest request, CancellationToken token)
    {
        _EnsureInitialized();
        var oauth = request.OAuthResult;
        var isSilentRefresh = false;
        if (oauth is null && !string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            isSilentRefresh = true;
            oauth = await _client!.AuthorizeWithSilentAsync(new AuthorizeResult { RefreshToken = request.RefreshToken }, token)
                .ConfigureAwait(false);
        }
        if (oauth is null && request.DeviceCodeHandler is not null)
        {
            var code = await GetCodePairAsync(token).ConfigureAwait(false)
                       ?? throw new IdentityModelAuthenticationException("device_authorization_failed", "The server returned no device code.");
            oauth = await request.DeviceCodeHandler(
                new DeviceCodeAuthenticationContext(code, pollToken => PollDeviceCodeAsync(code, pollToken)), token)
                .ConfigureAwait(false);
        }
        if (oauth is null)
            throw new IdentityModelConfigurationException("Yggdrasil Connect login requires an OAuth result or device-code handler.");
        return await _CompleteAsync(oauth, request, isSilentRefresh, token).ConfigureAwait(false);
    }

    public Task<AuthenticationResult> RefreshAsync(McProfile profile, CancellationToken token)
        => AuthenticateAsync(new AuthenticationRequest
        {
            RefreshToken = profile.RefreshToken,
            IdToken = profile.IdToken,
            ExistingProfile = profile
        }, token);

    public async Task<bool> ValidateAsync(McProfile profile, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(profile.AccessToken)) return false;
        _EnsureInitialized();
        // 这里存的是 Minecraft 令牌而不是 OAuth 访问令牌，因此改为向角色列表接口确认它仍然有效
        // （正常返回角色数组 = 有效；401/403 = 已失效，交由上层走静默刷新）
        try
        {
            using var response = await HttpRequest.Create(_GetServerRoot() + "/sessionserver/session/minecraft/profile")
                .WithBearerToken(profile.AccessToken)
                .SendAsync(NetworkService.GetClient(), cancellationToken: token)
                .ConfigureAwait(false);
            return response.IsSuccess;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, "Profile", "校验第三方账号令牌失败，将尝试静默刷新");
            return false;
        }
    }

    private async Task<AuthenticationResult> _CompleteAsync(AuthorizeResult oauth, AuthenticationRequest request,
        bool isSilentRefresh, CancellationToken token)
    {
        if (oauth.IsError) throw new IdentityModelAuthenticationException(oauth.Error, oauth.ErrorDescription);
        oauth.Validate(requireIdToken: true, requireRefreshToken: !isSilentRefresh);

        var claims = await _ReadIdTokenAsync(oauth.IdToken ?? request.IdToken, token).ConfigureAwait(false);
        var profile = claims.SelectedProfile;
        var available = claims.AvailableProfiles ?? [];
        if (profile is null || available.Length == 0)
        {
            var userInfo = await _GetUserInfoAsync(oauth.AccessToken!, token).ConfigureAwait(false);
            profile ??= userInfo?.SelectedProfile;
            if (available.Length == 0) available = userInfo?.AvailableProfiles ?? [];
        }
        // ID 令牌与用户信息都没带角色时，用 PlayerProfiles.Read 主动拉一次角色列表，
        // 这样即便服务端只在用户信息里返回当前角色，启动器也能让用户改选其他角色
        if (available.Length == 0)
            available = await _GetAvailableProfilesAsync(oauth.AccessToken!, token).ConfigureAwait(false);
        if (profile is null && !request.ForceReselectProfile &&
            request.ExistingProfile is { Uuid: { } existingUuid } && !string.IsNullOrWhiteSpace(existingUuid))
        {
            profile = available.FirstOrDefault(p =>
                string.Equals(p.Id, existingUuid, StringComparison.Ordinal));
        }
        if (profile is null && available.Length == 0 && request.ExistingProfile is { } existing &&
            !string.IsNullOrWhiteSpace(existing.Uuid) && !string.IsNullOrWhiteSpace(existing.UserName))
        {
            profile = new YggdrasilProfile { Id = existing.Uuid, Name = existing.UserName };
        }
        if ((request.ForceReselectProfile || profile is null) && available.Length > 1)
        {
            if (request.ProfileSelector is null)
                throw new IdentityModelAuthenticationException("profile_selection_required", "Yggdrasil Connect returned multiple player profiles.");
            var candidates = available.Select(p => new AuthenticationCandidate(p.Id, p.Name)).ToArray();
            var selected = await request.ProfileSelector(candidates, token).ConfigureAwait(false);
            if (selected is null)
                throw new IdentityModelAuthenticationException("access_denied", "Yggdrasil Connect profile selection was cancelled.");
            profile = available.FirstOrDefault(p => p.Id == selected.Id);
        }
        profile ??= available.FirstOrDefault();
        if (profile is null)
            throw new IdentityModelAuthenticationException("invalid_profile", "Yggdrasil Connect returned no player profile.");

        // OAuth 访问令牌只能访问账号服务，进游戏必须换成所选角色对应的 Minecraft 令牌
        var minecraftToken = await _CreateMinecraftTokenAsync(oauth.AccessToken!, profile.Id, token).ConfigureAwait(false);

        return new AuthenticationResult
        {
            ProfileType = ProfileType.YggdrasilConnect,
            UserName = profile.Name,
            Uuid = profile.Id,
            AccessToken = minecraftToken.AccessToken!,
            RefreshToken = oauth.RefreshToken ?? request.RefreshToken!,
            ClientToken = minecraftToken.ClientToken ?? profile.Id,
            TokenType = oauth.TokenType!,
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(oauth.ExpiresIn!.Value),
            Provider = "yggdrasil-connect",
            Server = _GetServerAddress(),
            ServerName = await _GetServerNameAsync(token).ConfigureAwait(false),
            DiscoveryAddress = _options.OpenIdDiscoveryAddress,
            IdToken = oauth.IdToken ?? request.IdToken
        };
    }

    private async Task<YggdrasilConnectClaims> _ReadIdTokenAsync(string? idToken, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(idToken))
            throw new SecurityException("Yggdrasil Connect did not return an ID token.");
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(idToken);
        if (string.IsNullOrWhiteSpace(jwt.Header.Kid))
            throw new SecurityException("Yggdrasil Connect ID token has no key id.");
        var key = await _options.GetSignatureKeyAsync(jwt.Header.Kid, token).ConfigureAwait(false);
        var verifiedToken = new JsonWebToken(idToken, _options.Meta!);
        var verified = verifiedToken.VerifySignature(key, _options.ClientId);
        if (verified is null) throw new SecurityException("Yggdrasil Connect ID token validation failed.");
        return verifiedToken.ReadTokenPayload<YggdrasilConnectClaims>()
               ?? throw new SecurityException("Yggdrasil Connect ID token payload was empty.");
    }

    private async Task<YggdrasilConnectClaims?> _GetUserInfoAsync(string accessToken, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(_options.Meta?.UserInfoEndpoint)) return null;
        using var response = await HttpRequest.Create(_options.Meta!.UserInfoEndpoint)
            .WithBearerToken(accessToken)
            .SendAsync(NetworkService.GetClient(), cancellationToken: token)
            .ConfigureAwait(false);
        if (!response.IsSuccess) return null;
        try
        {
            return await response.AsJsonAsync<YggdrasilConnectClaims>(cancellationToken: token).ConfigureAwait(false);
        }
        catch (InvalidDataException)
        {
            // 用户信息端点返回的声明不完整时按“未提供用户信息”处理，交由后续回退逻辑
            return null;
        }
    }

    /// <summary>
    ///     取账号服务的根地址（去掉 Yggdrasil 的 /authserver 段），用于访问角色列表等接口。
    /// </summary>
    private string _GetServerRoot()
    {
        var server = _GetServerAddress().TrimEnd('/');
        return server.EndsWith("/authserver", StringComparison.OrdinalIgnoreCase)
            ? server[..^"/authserver".Length]
            : server;
    }

    /// <summary>
    ///     列出当前账号下的全部角色。需要 PlayerProfiles.Read 权限；
    ///     任何失败都退化为空数组，由调用方回退到令牌自带的角色信息。
    /// </summary>
    private async Task<YggdrasilProfile[]> _GetAvailableProfilesAsync(string accessToken, CancellationToken token)
    {
        try
        {
            using var response = await HttpRequest.Create(_GetServerRoot() + "/sessionserver/session/minecraft/profile")
                .WithBearerToken(accessToken)
                .SendAsync(NetworkService.GetClient(), cancellationToken: token)
                .ConfigureAwait(false);
            if (!response.IsSuccess)
            {
                LogWrapper.Warn("Profile",
                    $"获取第三方账号的角色列表失败（HTTP {(int)response.StatusCode}），将使用令牌自带的角色信息");
                return [];
            }
            var profiles = await response.AsJsonAsync<YggdrasilProfile[]>(cancellationToken: token).ConfigureAwait(false);
            return profiles ?? [];
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Profile", "获取第三方账号的角色列表时出错，将使用令牌自带的角色信息");
            return [];
        }
    }

    /// <summary>
    ///     用 OAuth 访问令牌与选定的角色 UUID 换取 Minecraft 令牌。需要 MinecraftToken.Create 权限。
    /// </summary>
    private async Task<MinecraftTokenResult> _CreateMinecraftTokenAsync(string accessToken, string uuid,
        CancellationToken token)
    {
        using var response = await HttpRequest.CreatePost(_GetServerRoot() + "/authserver/oauth")
            .WithBearerToken(accessToken)
            .WithJsonContent(new MinecraftTokenRequest { Uuid = uuid })
            .SendAsync(NetworkService.GetClient(), cancellationToken: token)
            .ConfigureAwait(false);
        if (!response.IsSuccess)
        {
            var detail = await response.AsStringAsync().ConfigureAwait(false);
            throw new IdentityModelAuthenticationException("minecraft_token_failed",
                $"无法用所选角色换取 Minecraft 令牌（HTTP {(int)response.StatusCode}）：{detail}");
        }
        var result = await response.AsJsonAsync<MinecraftTokenResult>(cancellationToken: token).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(result?.AccessToken))
            throw new IdentityModelAuthenticationException("minecraft_token_failed",
                "第三方账号服务未返回 Minecraft 访问令牌。");
        return result!;
    }

    private async Task<string?> _GetServerNameAsync(CancellationToken token)
    {
        try
        {
            var root = _GetServerRoot();
            using var response = await HttpRequest.Create(root)
                .SendAsync(NetworkService.GetClient(), cancellationToken: token)
                .ConfigureAwait(false);
            if (!response.IsSuccess) return null;
            var metadata = await response.AsJsonAsync<JsonObject>(cancellationToken: token).ConfigureAwait(false);
            return metadata?["meta"]?["serverName"]?.ToString();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private void _EnsureInitialized()
    {
        if (_client is null || _options.Meta is null)
            throw new IdentityModelConfigurationException("Please initialize Yggdrasil Connect before use.");
    }

    private string _GetServerAddress()
    {
        if (!string.IsNullOrWhiteSpace(_yggdrasilServer))
        {
            var server = _yggdrasilServer.TrimEnd('/');
            return server.EndsWith("/authserver", StringComparison.OrdinalIgnoreCase) ? server : server + "/authserver";
        }
        var value = _options.OpenIdDiscoveryAddress;
        var marker = value.IndexOf("/.well-known/", StringComparison.OrdinalIgnoreCase);
        return marker > 0 ? value[..marker].TrimEnd('/') : value.TrimEnd('/');
    }

    // 协议要求：ID 令牌必须包含 iss / sub / aud / iat / exp 声明（iss/iat/exp 由签名验证流程校验）；
    // 用户信息端点响应为 ID 令牌声明的超集，同样必须包含 sub / aud
    private sealed record YggdrasilConnectClaims
    {
        [JsonPropertyName("sub")] public required string? Subject { get; init; }
        [JsonPropertyName("aud")] public required string? Audience { get; init; }
        [JsonPropertyName("selectedProfile")] public YggdrasilProfile? SelectedProfile { get; init; }
        [JsonPropertyName("availableProfiles")] public YggdrasilProfile[]? AvailableProfiles { get; init; }
    }

    private sealed record MinecraftTokenRequest
    {
        [JsonPropertyName("uuid")] public required string Uuid { get; init; }
    }

    private sealed record MinecraftTokenResult
    {
        [JsonPropertyName("accessToken")] public string? AccessToken { get; init; }
        [JsonPropertyName("clientToken")] public string? ClientToken { get; init; }
        [JsonPropertyName("selectedProfile")] public YggdrasilProfile? SelectedProfile { get; init; }
    }
}
