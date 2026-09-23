using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HostelSystem.Identity;

public class IdentitySeeder
{
    private readonly UserManager<Models.ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<IdentitySeeder> _logger;

    public IdentitySeeder(
        UserManager<Models.ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration,
        ILogger<IdentitySeeder> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        foreach (var role in new[] { "Admin", "Student" })
        {
            if (!await _roleManager.RoleExistsAsync(role))
                await _roleManager.CreateAsync(new IdentityRole(role));
        }

        var adminEmail = _configuration["Admin:Email"] ?? "admin@hostelsystem.local";
        var adminPassword = _configuration["Admin:Password"] ?? "Admin@12345";
        var resetPasswordOnSeed = string.Equals(
            _configuration["SeedOnStartup"], "true", StringComparison.OrdinalIgnoreCase);

        var existing = await _userManager.FindByEmailAsync(adminEmail);
        if (existing == null)
        {
            var admin = new Models.ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = "System",
                LastName = "Admin",
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(admin, adminPassword);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(admin, "Admin");
                _logger.LogInformation("Seeded default admin user {Email}", adminEmail);
            }
            else
            {
                _logger.LogWarning("Admin seed failed for {Email}: {Errors}",
                    adminEmail, string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }
        else
        {
            // Seeder is idempotent, but a stale password locks you out of prod.
            // With SeedOnStartup=true explicitly set, bring the admin back to a known state.
            if (!await _userManager.IsInRoleAsync(existing, "Admin"))
            {
                await _userManager.AddToRoleAsync(existing, "Admin");
                _logger.LogInformation("Added existing user {Email} to Admin role", adminEmail);
            }

            if (resetPasswordOnSeed && !string.IsNullOrWhiteSpace(adminPassword))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(existing);
                var reset = await _userManager.ResetPasswordAsync(existing, token, adminPassword);
                if (reset.Succeeded)
                    _logger.LogInformation("Reset admin password for {Email} via SeedOnStartup", adminEmail);
                else
                    _logger.LogWarning("Admin password reset failed for {Email}: {Errors}",
                        adminEmail, string.Join("; ", reset.Errors.Select(e => e.Description)));
            }
        }
    }
}