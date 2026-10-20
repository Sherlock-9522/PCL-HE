using PCL.Core.IO.Storage.Cache;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.IO.Net.Http.Cache;

/// <summary>
/// HTTP 缓存处理器
/// </summary>
public class HttpCacheHandler : DelegatingHandler
{
    private ICacheService _cacheService;
    public HttpCacheHandler(HttpMessageHandler invoker, ICacheService cacheService)
    {
        InnerHandler = invoker;
        _cacheService = cacheService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var uri = request.RequestUri?.ToString();
        if (string.IsNullOrEmpty(uri))
        {
            return await base.SendAsync(request, ct).ConfigureAwait(false);
        }

        // seek cache
        var cacheKey = CacheKeys.ApiResponse("http", uri);
        var metaKey = CacheKeys.ApiResponseMeta("http", uri);
        var cached = await _cacheService.GetAsync<byte[]>(cacheKey, ct).ConfigureAwait(false);
        var metaCached = await _cacheService.GetAsync<HttpCacheMetadata>(metaKey, ct).ConfigureAwait(false);

        if (cached.Found)
        {
            var cachedResponse = _DeserializeResponse(cached.Value!, metaCached.Found ? metaCached.Value : null);
            cachedResponse.Headers.Add("X-Cache-Hit", "HIT");
            cachedResponse.RequestMessage = request;
            return cachedResponse;
        }

        // 正文缓存不在但元数据还在，说明这条缓存已经残缺（正文过期被清，或上次写入失败）。
        // 这种情况下再发带 ETag / If-Modified-Since 的条件请求，服务器会回 304，
        // 而 304 本身没有正文，最终会往缓存里写一条 0 字节的“成功”响应，之后所有
        // 请求都命中它。所以先把元数据清掉，让这次请求拿到完整响应。
        if (metaCached.Found)
        {
            await _cacheService.DeleteAsync(metaKey).ConfigureAwait(false);
            metaCached = default;
        }

        // seek metadata
        if (metaCached.Found)
        {
            if (metaCached.Value!.ETag is not null)
            {
                request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(metaCached.Value!.ETag));
            }

            if (metaCached.Value!.LastModified is not null)
            {
                request.Headers.IfModifiedSince = DateTimeOffset.Parse(metaCached.Value!.LastModified);
            }
        }

        // send request
        var response = await base.SendAsync(request, ct).ConfigureAwait(false);
        if (response.Headers.CacheControl?.NoStore ?? false)
        {
            return response;
        }

        // not modified, return cached response if exists
        if (response.StatusCode is HttpStatusCode.NotModified)
        {
            var reCached = await _cacheService.GetAsync<byte[]>(cacheKey, ct).ConfigureAwait(false);
            if (reCached.Found)
            {
                var cachedResponse = _DeserializeResponse(reCached.Value!, metaCached.Found ? metaCached.Value : null);
                cachedResponse.Headers.Add("X-Cache-Hit", "HIT");
                cachedResponse.RequestMessage = request;
                response.Dispose();
                return cachedResponse;
            }

            // 没有正文可用：把这个 304 原样交出去，但绝不能把它写进缓存。
            response.Headers.Add("X-Cache-Hit", "MISS");
            return response;
        }

        // 只缓存成功响应。404 / 500 一旦被缓存，在有效期（默认 5 分钟）内会以
        // 缓存的形态反复返回，调用方的 EnsureSuccessStatusCode() 还会因为状态码
        // 被还原成 200 而检查通过，变成完全无声的失败。
        if (!response.IsSuccessStatusCode)
        {
            response.Headers.Add("X-Cache-Hit", "MISS");
            return response;
        }

        // cache response
        var body = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        var ttl = _ComputeTtl(response);

        await _cacheService.SetAsync(cacheKey, body, new CachePolicy
        {
            AbsoluteExpiration = ttl,
            Group = "http",
            Tags = "http-response"
        }, ct).ConfigureAwait(false);

        await _cacheService.SetAsync(metaKey, new HttpCacheMetadata
        {
            ETag = response.Headers.ETag?.Tag,
            LastModified = response.Content.Headers.LastModified?.ToString("O"),
            StatusCode = (int)response.StatusCode
        }, CachePolicy.NeverExpire, ct).ConfigureAwait(false);

        // not matched, return original response
        response.Headers.Add("X-Cache-Hit", "MISS");
        return response;
    }

    private static TimeSpan? _ComputeTtl(HttpResponseMessage response)
    {
        var cc = response.Headers.CacheControl;
        if (cc?.MaxAge is not null)
        {
            return cc.MaxAge;
        }

        if (cc?.SharedMaxAge is not null)
        {
            return cc.SharedMaxAge;
        }

        return TimeSpan.FromMinutes(5);
    }

    private static HttpResponseMessage _DeserializeResponse(byte[] data, HttpCacheMetadata? meta)
    {
        // 元数据里一直存着原始状态码，但过去从不读回，于是被缓存的 404 / 500 会以
        // 200 返回。这里按存下来的状态码还原，未知时退回 200。
        var status = meta is { StatusCode: > 0 }
            ? (HttpStatusCode)meta.StatusCode
            : HttpStatusCode.OK;

        return new HttpResponseMessage(status)
        {
            Content = new ByteArrayContent(data)
        };
    }
}
