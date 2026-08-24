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

        var response = await base.SendAsync(request, cancellationToken);

        // 401 -> try refresh once
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var authService = _services.GetService<AuthService>();
            if (authService is not null && await authService.TryRefreshAsync())
            {
                try
                {
                    var fresh = await _storage.GetAsync<string>(AccessTokenKey);
                    if (fresh.Success && !string.IsNullOrWhiteSpace(fresh.Value))
                    {
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", fresh.Value);
                        // Clone request? For simplicity retry with new request if content was read; here assume idempotent GET or retry with same request (may fail if content consumed)
                        // For POST with JSON content, HttpContent can be reused if not disposed; we retry only if we can
                        response.Dispose();
                        response = await base.SendAsync(request, cancellationToken);
                    }
                }
                catch { }
            }
        }

        return response;
    }
}
