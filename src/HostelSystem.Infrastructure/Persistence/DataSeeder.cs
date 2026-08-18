using HostelSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HostelSystem.Infrastructure.Persistence;

public class DataSeeder
{
    private readonly AppDbContext _context;
    private readonly ILogger<DataSeeder> _logger;

    public DataSeeder(AppDbContext context, ILogger<DataSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await _context.Database.MigrateAsync(ct);

        if (await _context.Hostels.AnyAsync(ct))
        {
            _logger.LogInformation("Database already seeded. Skipping.");
            return;
        }

        _logger.LogInformation("Seeding hostels and rooms...");

        var mensahSarbah = new Hostel("Mensah Sarbah Hall", "University of Ghana, Legon", "All-male hall of residence");
        var legonHall = new Hostel("Legon Hall", "University of Ghana, Legon", "The premier hall of residence");
        var akuafoHall = new Hostel("Akuafo Hall", "University of Ghana, Legon", "Mixed hall of residence");

        _context.Hostels.AddRange(mensahSarbah, legonHall, akuafoHall);
        await _context.SaveChangesAsync(ct);

        var rooms = new List<Room>
        {
            new("A101", 2, 1800m, mensahSarbah.Id),
            new("A102", 2, 1800m, mensahSarbah.Id),
            new("A103", 3, 1600m, mensahSarbah.Id),
            new("B201", 4, 1400m, mensahSarbah.Id),
            new("B202", 4, 1400m, mensahSarbah.Id),

            new("L101", 2, 2000m, legonHall.Id),
            new("L102", 2, 2000m, legonHall.Id),
            new("L103", 3, 1800m, legonHall.Id),

            new("AK101", 3, 1600m, akuafoHall.Id),
            new("AK102", 3, 1600m, akuafoHall.Id),
            new("AK201", 4, 1300m, akuafoHall.Id),
        };

        _context.Rooms.AddRange(rooms);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Seeded {HostelCount} hostels and {RoomCount} rooms.",
            await _context.Hostels.CountAsync(ct),
            await _context.Rooms.CountAsync(ct));
    }
}
