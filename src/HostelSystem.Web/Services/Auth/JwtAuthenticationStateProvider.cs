using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace HostelSystem.Web.Services.Auth;

public class JwtAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly ProtectedLocalStorage _storage;
    private readonly ILogger<JwtAuthenticationStateProvider> _logger;
    private const string AccessTokenKey = "auth.access_token";

    public JwtAuthenticationStateProvider(ProtectedLocalStorage storage, ILogger<JwtAuthenticationStateProvider> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var res = await _storage.GetAsync<string>(AccessTokenKey);
            if (!res.Success || string.IsNullOrWhiteSpace(res.Value))
                return Anonymous();

            var token = res.Value!;
            if (IsExpired(token))
            {
                _logger.LogDebug("JWT expired");
                return Anonymous();
            }

            var claims = ParseClaims(token);
            var identity = new ClaimsIdentity(claims, "jwt");
            var user = new ClaimsPrincipal(identity);
            return new AuthenticationState(user);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get auth state");
            return Anonymous();
        }
    }

    public async Task NotifyAuthenticationStateChangedAsync()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        await Task.CompletedTask;
    }

    private static AuthenticationState Anonymous()
    {
        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
    }

    private static bool IsExpired(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);
            return jwt.ValidTo < DateTime.UtcNow;
        }
        catch { return true; }
    }

    private static IEnumerable<Claim> ParseClaims(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        try
        {
            var jwt = handler.ReadJwtToken(token);
            return jwt.Claims;
        }
        catch
        {
            // Fallback: manual base64 payload parse
            try
            {
                var payload = token.Split('.')[1];
                var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(PadBase64(payload)));
                var doc = JsonDocument.Parse(json);
                var claims = new List<Claim>();
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var v in prop.Value.EnumerateArray())
                            claims.Add(new Claim(prop.Name, v.GetString() ?? v.ToString()));
                    }
                    else if (prop.Value.ValueKind == JsonValueKind.String)
                        claims.Add(new Claim(prop.Name, prop.Value.GetString()!));
                    else
                        claims.Add(new Claim(prop.Name, prop.Value.ToString()));
                }
                return claims;
            }
            catch { return []; }
        }
    }

    private static string PadBase64(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4) { case 2: s += "=="; break; case 3: s += "="; break; }
        return s;
    }
}
