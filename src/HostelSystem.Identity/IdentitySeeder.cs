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
        if (await _userManager.FindByEmailAsync(adminEmail) == null)
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
        }
    }
}