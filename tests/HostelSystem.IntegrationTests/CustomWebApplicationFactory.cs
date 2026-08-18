using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using HostelSystem.Identity.Models;
using HostelSystem.Infrastructure.Persistence;

namespace HostelSystem.IntegrationTests;

/// <summary>
/// Spins up the real ASP.NET Core pipeline against isolated SQLite databases.
/// Each test class inherits this; a fresh db file is created per test run.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    // Unique file names prevent cross-test contamination when tests run in parallel
    private readonly string _appDbPath = Path.Combine(Path.GetTempPath(), $"test-app-{Guid.NewGuid()}.db");
    private readonly string _identityDbPath = Path.Combine(Path.GetTempPath(), $"test-identity-{Guid.NewGuid()}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // ── Replace application DbContext with test SQLite ─────────────
            services.RemoveAll<DbContextOptions<HostelSystemDbContext>>();
            services.AddDbContext<HostelSystemDbContext>(options =>
                options.UseSqlite($"Data Source={_appDbPath}"));

            // ── Replace identity DbContext with test SQLite ────────────────
            services.RemoveAll<DbContextOptions<AppIdentityDbContext>>();
            services.AddDbContext<AppIdentityDbContext>(options =>
                options.UseSqlite($"Data Source={_identityDbPath}"));

            // ── Run migrations and seed roles ─────────────────────────────
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();

            var appDb = scope.ServiceProvider.GetRequiredService<HostelSystemDbContext>();
            appDb.Database.Migrate();

            var identityDb = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
            identityDb.Database.Migrate();
        });

        // Override JWT settings so integration tests can produce valid tokens
        builder.UseSetting("Jwt:Secret", "IntegrationTestSecretKeyThatIsLongEnoughForHS256Algorithm!");
        builder.UseSetting("Jwt:Issuer", "HostelSystem.Api");
        builder.UseSetting("Jwt:Audience", "HostelSystem.Web");
        builder.UseSetting("Jwt:AccessTokenExpiryMinutes", "60");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        // Clean up temp db files
        TryDelete(_appDbPath);
        TryDelete(_identityDbPath);
        TryDelete(_appDbPath + "-shm");
        TryDelete(_appDbPath + "-wal");
        TryDelete(_identityDbPath + "-shm");
        TryDelete(_identityDbPath + "-wal");
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* best effort */ }
    }
}
