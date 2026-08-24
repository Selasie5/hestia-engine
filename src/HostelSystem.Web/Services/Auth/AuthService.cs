using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace HostelSystem.Web.Services.Auth;

public record AuthResponseDto(string AccessToken, string RefreshToken, DateTime AccessTokenExpiresAt);
public record LoginRequestDto(string Email, string Password);
public record RegisterRequestDto(string Email, string Password, string FirstName, string LastName, string StudentNumber, string Gender);

public class AuthService
{
    private const string AccessTokenKey = "auth.access_token";
    private const string RefreshTokenKey = "auth.refresh_token";
    private const string ExpiresAtKey = "auth.expires_at";

    private readonly ProtectedLocalStorage _storage;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        ProtectedLocalStorage storage,
        IHttpClientFactory httpFactory,
        IConfiguration config,
        AuthenticationStateProvider authStateProvider,
        ILogger<AuthService> logger)
    {
        _storage = storage;
        _httpFactory = httpFactory;
        _config = config;
        _authStateProvider = authStateProvider;
        _logger = logger;
    }

    private HttpClient CreateApiClient()
    {
        // Use bypass client to avoid AuthHeaderHandler loop (which would try to refresh via this service)
        return _httpFactory.CreateClient("AuthBypass");
    }

    public async Task<(bool Ok, string? Error)> LoginAsync(string email, string password)
    {
        try
        {
            var client = CreateApiClient();
            var res = await client.PostAsJsonAsync("/api/v1.0/Auth/login", new LoginRequestDto(email, password));
            var body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
            {
                var err = TryExtractError(body) ?? $"Login failed: {res.StatusCode}";
                if ((int)res.StatusCode == 429) err = "Too many attempts — try again in a minute (rate limited).";
                return (false, err);
            }
            var dto = JsonSerializer.Deserialize<AuthResponseDto>(body, JsonOpts);
            if (dto is null) return (false, "Invalid server response.");
            await StoreTokensAsync(dto);
            await NotifyAuthStateChangedAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login error");
            return (false, ex.Message);
        }
    }

    public async Task<(bool Ok, string? Error)> RegisterAsync(RegisterRequestDto req)
    {
        try
        {
            var client = CreateApiClient();
            var res = await client.PostAsJsonAsync("/api/v1.0/Auth/register", req);
            var body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
            {
                var err = TryExtractError(body) ?? $"Registration failed: {res.StatusCode}";
                if (body.Contains("already exists", StringComparison.OrdinalIgnoreCase)) err = "Email or Student number already exists.";
                if ((int)res.StatusCode == 429) err = "Too many attempts — try again in a minute.";
                return (false, err);
            }
            var dto = JsonSerializer.Deserialize<AuthResponseDto>(body, JsonOpts);
            if (dto is null) return (false, "Invalid server response.");
            await StoreTokensAsync(dto);
            await NotifyAuthStateChangedAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Register error");
            return (false, ex.Message);
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            var refresh = await GetRefreshTokenAsync();
            if (!string.IsNullOrWhiteSpace(refresh))
            {
                var client = CreateApiClient();
                var tokens = await GetAccessTokenAsync();
                if (!string.IsNullOrWhiteSpace(tokens))
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens);
                await client.PostAsJsonAsync("/api/v1.0/Auth/logout", new { refreshToken = refresh });
            }
        }
        catch { }
        await _storage.DeleteAsync(AccessTokenKey);
        await _storage.DeleteAsync(RefreshTokenKey);
        await _storage.DeleteAsync(ExpiresAtKey);
        await NotifyAuthStateChangedAsync();
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        try
        {
            var res = await _storage.GetAsync<string>(AccessTokenKey);
            return res.Success ? res.Value : null;
        }
        catch { return null; }
    }

    public async Task<string?> GetRefreshTokenAsync()
    {
        try { var r = await _storage.GetAsync<string>(RefreshTokenKey); return r.Success ? r.Value : null; }
        catch { return null; }
    }

    public async Task<bool> TryRefreshAsync()
    {
        var refresh = await GetRefreshTokenAsync();
        if (string.IsNullOrWhiteSpace(refresh)) return false;
        try
        {
            var client = CreateApiClient();
            var res = await client.PostAsJsonAsync("/api/v1.0/Auth/refresh", new { refreshToken = refresh });
            var body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode) return false;
            var dto = JsonSerializer.Deserialize<AuthResponseDto>(body, JsonOpts);
            if (dto is null) return false;
            await StoreTokensAsync(dto);
            await NotifyAuthStateChangedAsync();
            return true;
        }
        catch { return false; }
    }

    private async Task StoreTokensAsync(AuthResponseDto dto)
    {
        await _storage.SetAsync(AccessTokenKey, dto.AccessToken);
        await _storage.SetAsync(RefreshTokenKey, dto.RefreshToken);
        await _storage.SetAsync(ExpiresAtKey, dto.AccessTokenExpiresAt.ToString("O"));
    }

    private async Task NotifyAuthStateChangedAsync()
    {
        if (_authStateProvider is JwtAuthenticationStateProvider jwt)
            await jwt.NotifyAuthenticationStateChangedAsync();
        else
            (_authStateProvider as AuthenticationStateProvider)?.GetAuthenticationStateAsync();
    }

    private static string? TryExtractError(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String) return e.GetString();
            if (doc.RootElement.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String) return t.GetString();
            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                return doc.RootElement[0].GetString();
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(json) && json.Length < 500) return json;
        }
        return null;
    }

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
}
