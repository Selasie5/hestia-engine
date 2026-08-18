using HostelSystem.Application.Interfaces;
using HostelSystem.Infrastructure.Persistence;
using HostelSystem.Infrastructure.Persistence.Repositories;
using HostelSystem.Infrastructure.Services;
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

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(connectionString));

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

        return services;
    }
}
