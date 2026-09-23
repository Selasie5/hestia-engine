using System.Net;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace HostelSystem.Web.Services.Auth;

public class AuthHeaderHandler : DelegatingHandler
{
    private readonly ProtectedLocalStorage _storage;
    private readonly IServiceProvider _services;
    private const string AccessTokenKey = "auth.access_token";

    public AuthHeaderHandler(ProtectedLocalStorage storage, IServiceProvider services)
    {
        _storage = storage;
        _services = services;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Attach token if not already present
        if (request.Headers.Authorization is null)
        {
            try
            {
                var res = await _storage.GetAsync<string>(AccessTokenKey);
                if (res.Success && !string.IsNullOrWhiteSpace(res.Value))
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", res.Value);
            }
            catch { }
        }

        // HttpRequestMessage instances can only be sent once. Buffer a clone before
        // the first attempt so an expired-token POST can be retried safely.
        var retryRequest = await CloneRequestAsync(request, cancellationToken);
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            retryRequest.Dispose();
            return response;
        }

        // 401 -> try refresh once, then send the buffered clone.
        var authService = _services.GetService<AuthService>();
        if (authService is null || !await authService.TryRefreshAsync())
        {
            retryRequest.Dispose();
            return response;
        }

        string? freshToken = null;
        try
        {
            var fresh = await _storage.GetAsync<string>(AccessTokenKey);
            if (fresh.Success && !string.IsNullOrWhiteSpace(fresh.Value))
                freshToken = fresh.Value;
        }
        catch
        {
            // Browser storage can be temporarily unavailable during prerendering.
        }

        if (string.IsNullOrWhiteSpace(freshToken))
        {
            retryRequest.Dispose();
            return response;
        }

        retryRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", freshToken);
        response.Dispose();
        return await base.SendAsync(retryRequest, cancellationToken);
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };

        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        foreach (var option in request.Options)
            clone.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);

        if (request.Content is not null)
        {
            var content = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            clone.Content = new ByteArrayContent(content);
            foreach (var header in request.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
