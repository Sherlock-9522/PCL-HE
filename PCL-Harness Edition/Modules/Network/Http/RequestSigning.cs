using System.Net;
using System.Net.Http;
using PCL.Core.App;

namespace PCL.Network;

public static class RequestSigning
{
    internal static string SecretCdnSign(string urlWithMark)
    {
        if (!urlWithMark.EndsWithF("{CDN}"))
            return urlWithMark;
        return urlWithMark.Replace("{CDN}", "").Replace(" ", "%20");
    }
    
    /// <summary>
    ///     设置 Headers 的 UA、Referer。
    /// </summary>
    internal static void SecretHeadersSign(string url, ref HttpRequestMessage client, bool useBrowserUserAgent = false,
        string customUserAgent = "")
    {
        client.Version = HttpVersion.Version20;
        client.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
        if (Secrets.HasCurseForgeApiKey
            && Uri.TryCreate(url, UriKind.Absolute, out var parsedUri)
            && (parsedUri.Host == "api.curseforge.com"
            || parsedUri.Host == "edge.forgecdn.net"
            || parsedUri.Host == "mediafilez.forgecdn.net"))
        {
            client.Headers.Add("x-api-key", Secrets.CurseForgeAPIKey);
        }
        // 不要在 UA 里挂上游 PCL2 / PCL-CE 的名字：请求量会被算进上游的 UA 声誉里，
        // 用户一多就可能把上游的 UA 一起拖进限流甚至封禁。这里只用自己的标识加仓库地址。
        var userAgent = !string.IsNullOrEmpty(customUserAgent)
            ? customUserAgent
            : useBrowserUserAgent
                ? $"PCL-HE/{ModBase.versionStandardCode} (+https://github.com/Sherlock-9522/PCL-HE) Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko) Chrome/136.0.0.0 Safari/537.36 Edg/136.0.0.0"
                : $"PCL-HE/{ModBase.versionStandardCode} (+https://github.com/Sherlock-9522/PCL-HE)";
        client.Headers.Add("User-Agent", userAgent);
    }
}