using System.Text;
using HostelSystem.Api.Extensions;
using HostelSystem.Api.Middleware;
using HostelSystem.Application;
using HostelSystem.Identity;
using HostelSystem.Infrastructure;
using HostelSystem.Infrastructure.Persistence;
using HostelSystem.Identity.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ─── Serilog (structured logging with correlation IDs) ───
builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration)
          .Enrich.WithProperty("Application", "HostelSystem.Api")
          .Enrich.FromLogContext());

// ─── Add Clean Architecture layers ───
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddIdentityServices(builder.Configuration);

// ─── Controllers + API versioning + Swagger ───
builder.Services.AddControllers();
builder.Services.AddApiVersioningConfig();
builder.Services.AddSwaggerDocumentation();

// ─── JWT Authentication ───
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()!;

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtSettings.Audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdminRole", policy => policy.RequireRole("Admin"));
    options.AddPolicy("RequireStudentRole", policy => policy.RequireRole("Student"));
});

// ─── Rate Limiting ───
// Rate limiting requires System.Threading.RateLimiting NuGet package in .NET 10
// Install: dotnet add package System.Threading.RateLimiting
// builder.Services.AddRateLimiter(options =>
// {
//     options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
//     options.AddFixedWindowLimiter("Fixed", opt =>
//     {
//         opt.PermitLimit = 100;
//         opt.Window = TimeSpan.FromMinutes(1);
//     });
// });

// ─── Health Checks ───
builder.Services.AddHealthChecks();

// ─── CORS ───
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient", policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["https://localhost:5001"])
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// ─── Middleware pipeline (order matters!) ───
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerDocumentation();
}
else
{
    app.UseHsts();
}

app.UseSerilogRequestLogging(); // Must come early — logs the HTTP request

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseHttpsRedirection();

app.UseCors("AllowBlazorClient");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

// ─── Seed / Reset (dev + CLI) ───
bool shouldReset = args.Contains("--seed-reset", StringComparer.OrdinalIgnoreCase);
bool shouldSeed = shouldReset || app.Environment.IsDevelopment();

if (shouldSeed)
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
    if (shouldReset)
        await seeder.ResetAsync();
    else
        await seeder.SeedAsync();

    var identitySeeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
    await identitySeeder.SeedAsync();

    // If invoked only for reset, exit after seeding (useful for CI / docker entrypoint)
    if (args.Contains("--seed-reset-only", StringComparer.OrdinalIgnoreCase))
    {
        Console.WriteLine("Seed reset complete — exiting as --seed-reset-only was specified.");
        return;
    }
}

app.Run();

// Required for WebApplicationFactory<Program> in integration tests
public partial class Program { }
