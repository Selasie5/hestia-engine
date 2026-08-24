using HostelSystem.Application.Interfaces;
using HostelSystem.Infrastructure.Persistence;
using HostelSystem.Infrastructure.Persistence.Repositories;
using HostelSystem.Infrastructure.Services;
using HostelSystem.Infrastructure.Services.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HostelSystem.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        var provider = configuration["DatabaseProvider"]
                       ?? configuration["Database:Provider"]
                       ?? Environment.GetEnvironmentVariable("DATABASE_PROVIDER")
                       ?? "Sqlite";

        services.AddDbContext<AppDbContext>(options =>
        {
            if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase) ||
                provider.Equals("Sql_Server", StringComparison.OrdinalIgnoreCase) ||
                provider.Equals("mssql", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null);
                });
            }
            else
            {
                options.UseSqlite(connectionString);
            }
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IHostelRepository, HostelRepository>();
        services.AddScoped<IRoomRepository, RoomRepository>();
        services.AddScoped<IStudentRepository, StudentRepository>();
        services.AddScoped<IApplicationRepository, ApplicationRepository>();
        services.AddScoped<IAllocationRepository, AllocationRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();

        services.AddMemoryCache();
        services.AddSingleton<ICacheService, MemoryCacheService>();

        services.AddScoped<DataSeeder>();

        // ── Paystack + QR ──
        services.Configure<PaystackSettings>(configuration.GetSection(PaystackSettings.SectionName));
        services.AddHttpClient<IPaymentGateway, PaystackGateway>(client =>
        {
            var baseUrl = configuration["Paystack:BaseUrl"] ?? "https://api.paystack.co";
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddSingleton<IQrCodeService, QrCodeService>();

        return services;
    }
}
