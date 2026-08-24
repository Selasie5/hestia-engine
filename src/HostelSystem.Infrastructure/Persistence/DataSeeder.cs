using HostelSystem.Domain.Entities;
using HostelSystem.Domain.Enums;
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

    /// <summary>
    /// Idempotent seed — runs migrations and inserts realistic dataset if empty.
    /// Safe to call on every startup in Development.
    /// </summary>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        await _context.Database.MigrateAsync(ct);

        if (await _context.Hostels.AnyAsync(ct))
        {
            _logger.LogInformation("Database already seeded. Skipping. Use ResetAsync() to re-seed.");
            return;
        }

        await SeedRealisticDatasetAsync(ct);
    }

    /// <summary>
    /// Destructive reset — deletes all domain data and re-seeds.
    /// Intended for local dev and integration test setup. Never call in production.
    /// Usage: inject DataSeeder and call await seeder.ResetAsync();
    /// Or run the API with argument: dotnet run -- --seed-reset
    /// </summary>
    public async Task ResetAsync(CancellationToken ct = default)
    {
        _logger.LogWarning("Resetting database — deleting all domain data...");

        // Order matters due to FK restrict
        _context.Payments.RemoveRange(_context.Payments);
        _context.RoomAllocations.RemoveRange(_context.RoomAllocations);
        _context.RoomApplications.RemoveRange(_context.RoomApplications);
        _context.Rooms.RemoveRange(_context.Rooms);
        _context.Students.RemoveRange(_context.Students);
        _context.Hostels.RemoveRange(_context.Hostels);
        await _context.SaveChangesAsync(ct);

        await SeedRealisticDatasetAsync(ct);
        _logger.LogWarning("Database reset complete.");
    }

    private async Task SeedRealisticDatasetAsync(CancellationToken ct)
    {
        _logger.LogInformation("Seeding realistic dataset...");

        // ── Hostels (5) ──
        var hostels = new[]
        {
            new Hostel("Mensah Sarbah Hall", "University of Ghana, Legon", "All-male hall of residence — capacity ~ 60 beds in this seed"),
            new Hostel("Legon Hall", "University of Ghana, Legon", "Premier hall — mixed, central campus"),
            new Hostel("Akuafo Hall", "University of Ghana, Legon", "Mixed hall — agricultural heritage"),
            new Hostel("Commonwealth Hall", "University of Ghana, Legon", "All-male, 'Vandal' spirit"),
            new Hostel("Volta Hall", "University of Ghana, Legon", "All-female hall"),
        };
        _context.Hostels.AddRange(hostels);
        await _context.SaveChangesAsync(ct);

        var hostelMap = hostels.ToDictionary(h => h.Name, h => h.Id);

        // Helper to create rooms with deterministic pricing/capacity
        List<Room> rooms = new();
        void AddRooms(string hostelName, params (string number, int cap, decimal price)[] defs)
        {
            var hid = hostelMap[hostelName];
            foreach (var (number, cap, price) in defs)
                rooms.Add(new Room(number, cap, price, hid));
        }

        AddRooms("Mensah Sarbah Hall",
            ("A101", 2, 1800m), ("A102", 2, 1800m), ("A103", 3, 1600m), ("A104", 2, 1800m),
            ("B201", 4, 1400m), ("B202", 4, 1400m), ("B203", 3, 1600m), ("B204", 2, 1800m),
            ("C301", 4, 1400m), ("C302", 4, 1400m), ("C303", 2, 1800m), ("C304", 3, 1600m));

        AddRooms("Legon Hall",
            ("L101", 2, 2000m), ("L102", 2, 2000m), ("L103", 3, 1800m), ("L104", 3, 1800m),
            ("L201", 4, 1500m), ("L202", 4, 1500m), ("L203", 2, 2000m), ("L204", 3, 1800m),
            ("L301", 4, 1500m), ("L302", 4, 1500m), ("L303", 2, 2000m), ("L304", 3, 1800m));

        AddRooms("Akuafo Hall",
            ("AK101", 3, 1600m), ("AK102", 3, 1600m), ("AK103", 2, 1800m), ("AK104", 4, 1400m),
            ("AK201", 4, 1300m), ("AK202", 3, 1600m), ("AK203", 2, 1800m), ("AK204", 4, 1300m),
            ("AK301", 3, 1600m), ("AK302", 3, 1600m), ("AK303", 4, 1300m), ("AK304", 2, 1800m));

        AddRooms("Commonwealth Hall",
            ("CW101", 3, 1550m), ("CW102", 3, 1550m), ("CW103", 4, 1350m), ("CW104", 4, 1350m),
            ("CW201", 2, 1850m), ("CW202", 2, 1850m), ("CW203", 3, 1550m), ("CW204", 4, 1350m),
            ("CW301", 4, 1350m), ("CW302", 3, 1550m));

        AddRooms("Volta Hall",
            ("V101", 2, 1900m), ("V102", 2, 1900m), ("V103", 3, 1700m), ("V104", 3, 1700m),
            ("V201", 4, 1450m), ("V202", 4, 1450m), ("V203", 2, 1900m), ("V204", 3, 1700m),
            ("V301", 4, 1450m), ("V302", 4, 1450m));

        _context.Rooms.AddRange(rooms);
        await _context.SaveChangesAsync(ct);

        // Simulate some occupancy for realism (every 5th room partially filled)
        for (int i = 0; i < rooms.Count; i += 5)
        {
            var r = rooms[i];
            // Bump occupancy by 1 (direct property via reflection? Use domain method via dummy student)
            // Instead simulate by creating a student + allocation flow.
            // For simplicity, leave occupancy at 0 here and let allocation seeding drive it.
        }

        // ── Students (20 realistic) ──
        var students = new List<Student>();
        var firstNames = new[] { "Kwame", "Ama", "Kofi", "Akosua", "Yaw", "Adwoa", "Kwesi", "Abena", "Kojo", "Efua",
                                  "Selasie", "Loica", "Nana", "Esi", "Fiifi", "Maame", "Kweku", "Araba", "Ekow", "Sena" };
        var lastNames  = new[] { "Mensah", "Owusu", "Boateng", "Asare", "Appiah", "Osei", "Adjei", "Agyeman", "Ofori", "Darko",
                                  "Addo", "Quayson", "Annor", "Bediako", "Fosu", "Gyasi", "Kwarteng", "Opoku", "Sarpong", "Tetteh" };
        var genders = new[] { Gender.Male, Gender.Female };

        for (int i = 0; i < 20; i++)
        {
            var userId = Guid.NewGuid().ToString();
            var studentNumber = $"UG{20240001 + i}";
            var gender = genders[i % 2];
            var s = new Student(userId, studentNumber, firstNames[i % firstNames.Length], lastNames[i % lastNames.Length], gender);
            // Add phone numbers for half
            if (i % 2 == 0) s.UpdateContactInfo($"+23324{i:0000000}");
            students.Add(s);
        }
        _context.Students.AddRange(students);
        await _context.SaveChangesAsync(ct);

        // ── RoomApplications (15) + Allocations (8) + Payments (10) ──
        var rnd = new Random(42); // deterministic
        var pendingApps = new List<RoomApplication>();
        var roomIds = rooms.Select(r => r.Id).ToList();

        for (int i = 0; i < 15; i++)
        {
            var student = students[rnd.Next(students.Count)];
            var roomId = roomIds[rnd.Next(roomIds.Count)];
            var app = new RoomApplication(student.Id, roomId, i % 3 == 0 ? "Prefers ground floor" : null);
            pendingApps.Add(app);
        }
        _context.RoomApplications.AddRange(pendingApps);
        await _context.SaveChangesAsync(ct);

        // Approve first 8 applications and create allocations
        var allocations = new List<RoomAllocation>();
        for (int i = 0; i < 8; i++)
        {
            var app = pendingApps[i];
            app.Approve("admin@hostel.local");
            // Ensure room occupancy is updated via domain (simulate)
            var room = rooms.First(r => r.Id == app.RoomId);
            var student = students.First(s => s.Id == app.StudentId);
            try { room.AllocateStudent(student); } catch { /* already allocated */ }

            var allocation = new RoomAllocation(app.StudentId, app.RoomId, app.Id);
            allocations.Add(allocation);
        }
        // Reject next 3
        for (int i = 8; i < 11; i++)
            pendingApps[i].Reject("admin@hostel.local", "Room capacity exceeded for preferred hostel");

        // Leave rest as Pending

        _context.RoomAllocations.AddRange(allocations);
        await _context.SaveChangesAsync(ct);

        // ── Payments ──
        var payments = new List<Payment>();
        for (int i = 0; i < allocations.Count; i++)
        {
            var alloc = allocations[i];
            var amount = rooms.First(r => r.Id == alloc.RoomId).PricePerSemester;
            var payment = new Payment(alloc.StudentId, alloc.Id, amount, DateTime.UtcNow.AddDays(14));
            if (i < 5) // 5 completed
                payment.MarkCompleted($"TXN-SEED-{20240001 + i}", i % 2 == 0 ? "MobileMoney" : "BankTransfer");
            else if (i == 6) // 1 failed
                payment.MarkFailed();
            // else remain Pending

            payments.Add(payment);
        }
        // Add 2 overdue pending payments (due date in past)
        for (int i = 0; i < 2; i++)
        {
            var alloc = allocations[i % allocations.Count];
            var amount = rooms.First(r => r.Id == alloc.RoomId).PricePerSemester;
            var overdue = new Payment(alloc.StudentId, alloc.Id, amount, DateTime.UtcNow.AddDays(-5));
            payments.Add(overdue);
        }

        _context.Payments.AddRange(payments);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Seeded {HostelCount} hostels, {RoomCount} rooms, {StudentCount} students, {AppCount} applications, {AllocCount} allocations, {PaymentCount} payments.",
            await _context.Hostels.CountAsync(ct),
            await _context.Rooms.CountAsync(ct),
            await _context.Students.CountAsync(ct),
            await _context.RoomApplications.CountAsync(ct),
            await _context.RoomAllocations.CountAsync(ct),
            await _context.Payments.CountAsync(ct));
    }
}
