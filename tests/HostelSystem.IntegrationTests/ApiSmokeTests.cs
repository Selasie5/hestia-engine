using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace HostelSystem.IntegrationTests;

public sealed class ApiSmokeTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ApiSmokeTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new()
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Health_ReturnsSuccess()
    {
        using var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RootHeadProbe_ReturnsSuccess()
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, "/");
        using var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PublicHostels_ReturnsPagedContract()
    {
        using var response = await _client.GetAsync("/api/v1.0/Hostels?onlyActive=false&page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        payload.RootElement.TryGetProperty("items", out var items).Should().BeTrue();
        items.ValueKind.Should().Be(JsonValueKind.Array);
        payload.RootElement.TryGetProperty("totalCount", out _).Should().BeTrue();
    }

    [Fact]
    public async Task PaymentQr_RequiresAuthentication()
    {
        using var response = await _client.GetAsync("/api/v1.0/Payments/example/qr?format=png");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Webhook_WithoutConfiguredSecret_IsUnavailableOutsideDevelopment()
    {
        using var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync("/api/v1.0/Payments/webhook", content);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }
}
