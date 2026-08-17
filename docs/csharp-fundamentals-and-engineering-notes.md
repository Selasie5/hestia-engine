# C# Fundamentals & Engineering Notes

> Study companion for the Hostel Room Allocation System. Each section maps to
> concepts you'll encounter in the codebase AND on your C# programming exams.

---

## Part A: C# Language Fundamentals

### A1. Types — Value vs Reference

| | Value Type | Reference Type |
|---|---|---|
| **Stored** | On the stack (directly) | On the heap (pointer on stack) |
| **Examples** | `int`, `bool`, `DateTime`, `struct`, `enum` | `string`, `class`, `array`, `delegate`, `interface` |
| **Default** | `0`, `false`, etc. | `null` |
| **Assignment** | Copies the value | Copies the reference |
| **Nullable** | Must use `int?` / `Nullable<int>` | Already nullable |

```csharp
int a = 5;
int b = a;   // COPY — b is independent
b = 10;      // a is still 5

var list1 = new List<int> { 1, 2, 3 };
var list2 = list1;  // REFERENCE — both point to same object
list2.Add(4);       // list1 now also has [1,2,3,4]
```

**Exam tip:** `string` is a reference type but behaves like a value type (immutable — every
modification creates a new string).

### A2. Classes vs Structs vs Records

| Feature | `class` | `struct` | `record` | `record struct` |
|---|---|---|---|---|
| Type kind | Reference | Value | Reference | Value |
| Equality | Reference (by default) | Value-based | Value-based | Value-based |
| Immutability | Manual | Manual | `init`-only / `with` | `init`-only / `with` |
| Inheritance | Yes | No | Yes | No |
| Use case | Entities, services | Small data (<16 bytes) | DTOs, commands, queries | Small immutable data |

```csharp
// Class — reference type, equality by reference
public class Student { public string Name { get; set; } }

// Record — reference type, equality by VALUE (compiler auto-generates Equals/GetHashCode)
public record RoomDto(int Id, string Name, int Capacity);

// Struct — value type, for tiny things
public struct Money { public decimal Amount; public string Currency; }

// Record struct — value type, value equality
public readonly record struct Coordinate(double X, double Y);
```

**Exam tip:** `record` auto-generates `Equals()`, `GetHashCode()`, `ToString()`, and a copy
constructor (`with` expression). This is why we use records for CQRS commands/queries.

### A3. Access Modifiers

| Modifier | Meaning |
|---|---|
| `public` | Accessible from anywhere |
| `private` | Only within the same class/struct |
| `protected` | Within the class AND derived classes |
| `internal` | Within the same assembly (project) |
| `protected internal` | Within the assembly OR derived classes |
| `private protected` | Within the assembly AND derived classes |

**Production convention in this project:** Entities use `private set` on properties so only
domain methods can change state — this enforces invariants.

```csharp
public class RoomApplication
{
    public int Id { get; private set; }          // Anyone can read
    public ApplicationStatus Status { get; private set; } // Only methods can change

    public void Approve(string reviewedBy)       // The ONLY way to change Status
    {
        Status = ApplicationStatus.Approved;
    }
}
```

### A4. Properties — Auto vs Full

```csharp
// Auto property (compiler generates backing field)
public string Name { get; set; }

// Read-only auto property
public int Id { get; }

// Private set (our domain pattern)
public DateTime CreatedAt { get; private set; }

// Full property (you control the backing field)
private string _name;
public string Name
{
    get => _name;
    set
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Name cannot be empty");
        _name = value;
    }
}

// Expression-bodied
public string FullName => $"{FirstName} {LastName}"; // Computed, no backing field
```

### A5. `readonly` — Fields, Structs, Methods

```csharp
// readonly field — can only be set in constructor or field initializer
private readonly IRepository _repo;

// readonly struct — ALL fields must be readonly (prevents defensive copies)
public readonly struct Money { ... }

// readonly method — guarantees it won't mutate the struct
public readonly override string ToString() => ...;
```

### A6. `const` vs `readonly`

| | `const` | `readonly` |
|---|---|---|
| Set at | Compile time | Constructor/field init |
| Type | Built-in types + string | Any type |
| Static? | Implicitly static | Instance or static |
| Performance | Burned into calling code | Read at runtime |

```csharp
public const int MaxRoomsPerHostel = 50;       // Compile-time constant
public readonly DateTime ServerStartTime;       // Set at runtime, never changes
```

### A7. `static` — What It Actually Means

- **Static member**: belongs to the TYPE, not any instance. One copy shared by all.
- **Static class**: cannot be instantiated, all members must be static.
- **Static constructor**: runs once before any static member is accessed.

```csharp
public static class CacheKeys
{
    public static string RoomAvailability(int hostelId) => $"rooms:available:{hostelId}";
    // ^ Note: this method is static. Use: CacheKeys.RoomAvailability(5)
}
```

---

## Part B: OOP Concepts (Exam Heavy)

### B1. Inheritance

```csharp
public class DomainException : Exception  // DomainException IS-A exception
{
    public DomainException(string message) : base(message) { }
}

public class ConcurrencyException : DomainException  // ConcurrencyException IS-A DomainException
{
    public ConcurrencyException() : base("Concurrent modification detected.") { }
}
```

**Key rule:** C# supports single inheritance only. Use interfaces for multiple behaviors.

### B2. Polymorphism (virtual/override)

```csharp
public class PaymentGateway
{
    public virtual async Task<PaymentResult> ProcessAsync(Payment payment)
    {
        // Default behavior
    }
}

public class PaystackGateway : PaymentGateway
{
    public override async Task<PaymentResult> ProcessAsync(Payment payment)
    {
        // Paystack-specific behavior
    }
}
```

### B3. Abstract Classes vs Interfaces

| | Abstract Class | Interface |
|---|---|---|
| Can have implementation | Yes | No (until C# 8 default methods) |
| Can have state (fields) | Yes | No |
| Multiple | No (single inheritance) | Yes (implement many) |
| Constructor | Yes | No |
| Use when | "IS-A" with shared code | "CAN-DO" capability contract |

```csharp
// Abstract class — shared base with default behavior
public abstract class Entity
{
    public int Id { get; protected set; }
    public DateTime CreatedAt { get; protected set; }
}

// Interface — capability contract
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
```

### B4. Generics

```csharp
// Generic method
public T? GetById<T>(int id) where T : class
{
    return _db.Set<T>().Find(id);
}

// Generic interface
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
}

// Constraints: where T : class | struct | new() | BaseClass | IInterface
```

### B5. Boxing and Unboxing

Boxing = wrapping a value type in an object (heap allocation). **Performance cost.**

```csharp
int x = 42;
object boxed = x;      // BOXING — allocates on heap
int y = (int)boxed;    // UNBOXING — runtime type check + copy back
```

**Avoid boxing** in hot paths. Generics prevent it — `List<int>` never boxes, but
`ArrayList` does (it stores `object`).

---

## Part C: Advanced C# — What You'll Actually Write

### C1. Async/Await (Critical for this project)

```csharp
// async = this method uses await
// Task = represents an asynchronous operation
// Task<T> = an async operation that returns T
public async Task<Room?> GetRoomAsync(int id, CancellationToken ct = default)
{
    return await _context.Rooms.FindAsync(new object[] { id }, ct);
}

// Rules:
// 1. async void is ONLY for event handlers — ALWAYS use async Task
// 2. Always pass CancellationToken through
// 3. .ConfigureAwait(false) in libraries (not needed in ASP.NET Core controllers)
// 4. NEVER .Result or .Wait() — deadlock risk
```

**Common exam question:** What does `await` actually do?
→ It yields control back to the caller until the Task completes. The thread is freed
for other work. When the awaited operation finishes, the method resumes (on the captured
SynchronizationContext or a thread pool thread).

### C2. LINQ (Language Integrated Query)

```csharp
// Method syntax (what we use in this project)
var pending = _context.Applications
    .Where(a => a.Status == ApplicationStatus.Pending)
    .OrderBy(a => a.CreatedAt)
    .Select(a => new ApplicationDto(a.Id, a.StudentId))
    .ToListAsync(ct);

// Query syntax (less common in production, good for exams)
var pending = from a in _context.Applications
              where a.Status == ApplicationStatus.Pending
              orderby a.CreatedAt
              select new ApplicationDto(a.Id, a.StudentId);
```

**Deferred execution:** Queries don't run until you enumerate (`.ToList()`, `.First()`,
`foreach`). This is why `.ToListAsync()` is the trigger point.

### C3. Extension Methods

```csharp
public static class ServiceCollectionExtensions
{
    // "this IServiceCollection services" → this is an extension on IServiceCollection
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(...);
        return services; // Fluent pattern — enables chaining
    }
}

// Usage — looks like a regular method:
services.AddInfrastructure(configuration);
```

### C4. Nullable Reference Types (NRT)

Enabled in this project via `<Nullable>enable</Nullable>`:
```csharp
public string Name { get; set; }      // MUST never be null
public string? Nickname { get; set; }  // CAN be null

// Null-forgiving operator (use sparingly — only when YOU know better than the compiler)
_context.Rooms!.Find(id); // "Trust me, _context.Rooms is not null"
```

### C5. Pattern Matching

```csharp
// Switch expression (C# 8+)
string statusMessage = application.Status switch
{
    ApplicationStatus.Pending => "Awaiting review",
    ApplicationStatus.Approved => "Approved — room allocated",
    ApplicationStatus.Rejected => "Application rejected",
    _ => "Unknown status"
};

// Property pattern
if (room is { CurrentOccupancy: >= 0, Capacity: > 0 })
{
    // room has valid occupancy/capacity values
}

// Type pattern
if (result is not null)
{
    // result is not null — compiler knows the type
}
```

### C6. Delegates, Func, Action, EventHandler

```csharp
// Func<T1, TResult> — takes T1, returns TResult
Func<int, int, int> add = (a, b) => a + b;

// Action<T> — takes T, returns void
Action<string> log = message => Console.WriteLine(message);

// EventHandler — the standard .NET event pattern
public event EventHandler<ApplicationApprovedEvent>? Approved;

// Lambda expression
var available = rooms.Where(r => r.CurrentOccupancy < r.Capacity);
```

### C7. `using` Statement vs `using` Directive vs `await using`

```csharp
// using DIRECTIVE — imports a namespace (top of file)
using HostelSystem.Domain.Entities;

// using STATEMENT — ensures Dispose() is called (IDisposable)
using (var scope = _serviceProvider.CreateScope())
{
    // scope is disposed when block exits
}

// using DECLARATION (C# 8+) — disposes at end of enclosing block
using var scope = _serviceProvider.CreateScope();

// await using — for IAsyncDisposable (EF Core DbContext, etc.)
await using var context = _contextFactory.CreateDbContext();
```

---

## Part D: Dependency Injection (DI) — The Backbone of ASP.NET Core

### D1. Service Lifetimes

| Lifetime | Created | Use Case |
|---|---|---|
| `Transient` | Every time requested | Lightweight, stateless services |
| `Scoped` | Once per HTTP request | DbContext, Unit of Work |
| `Singleton` | Once per app lifetime | Cache, configuration, thread-safe services |

```csharp
// In Program.cs or extension method:
services.AddScoped<IApplicationRepository, ApplicationRepository>();
services.AddScoped<IUnitOfWork, UnitOfWork>();
services.AddSingleton<ICacheService, RedisCacheService>();
services.AddTransient<IEmailService, SmtpEmailService>();
```

**Golden rule:** Never inject a Scoped service into a Singleton. You'll get a captive
dependency — it works at first but reuses the same scoped instance across requests.

### D2. Constructor Injection

```csharp
public class ApplicationsController : ControllerBase
{
    private readonly IMediator _mediator;

    // DI injects IMediator — you never call "new Mediator()"
    public ApplicationsController(IMediator mediator)
    {
        _mediator = mediator;
    }
}
```

### D3. `IOptions<T>` Pattern

```csharp
// Configuration class
public class JwtSettings
{
    public const string SectionName = "Jwt";
    public string Secret { get; set; } = string.Empty;
    public int AccessTokenExpiryMinutes { get; set; } = 15;
}

// Binding in Program.cs
services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));

// Usage
public class TokenService
{
    public TokenService(IOptions<JwtSettings> jwtOptions) { ... }
}
```

---

## Part E: EF Core Deep Dive

### E1. DbContext — The Unit of Work

```csharp
public class AppDbContext : DbContext
{
    public DbSet<Hostel> Hostels => Set<Hostel>();
    public DbSet<Room> Rooms => Set<Room>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        // This automatically applies all IEntityTypeConfiguration<T> classes
    }
}
```

### E2. Entity Configuration (Fluent API)

```csharp
public class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("Rooms");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.RoomNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(r => r.Capacity)
            .IsRequired();

        // Optimistic concurrency token
        builder.Property(r => r.RowVersion)
            .IsRowVersion();

        // Relationship
        builder.HasOne(r => r.Hostel)
            .WithMany(h => h.Rooms)
            .HasForeignKey(r => r.HostelId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

### E3. Query Types to Know

```csharp
// Eager loading (Include/ThenInclude)
var hostel = await _context.Hostels
    .Include(h => h.Rooms)
    .ThenInclude(r => r.Allocations)
    .FirstOrDefaultAsync(h => h.Id == id, ct);

// Projection (Select — most efficient, only fetches needed columns)
var dto = await _context.Rooms
    .Where(r => r.HostelId == hostelId)
    .Select(r => new RoomDto(r.Id, r.RoomNumber, r.Capacity, r.CurrentOccupancy))
    .ToListAsync(ct);

// No-tracking queries (READ-ONLY — faster for GET endpoints)
var rooms = await _context.Rooms
    .AsNoTracking()
    .ToListAsync(ct);
```

### E4. SaveChanges and Transactions

```csharp
// Single save
await _context.SaveChangesAsync(ct);

// Explicit transaction
using var transaction = await _context.Database.BeginTransactionAsync(ct);
try
{
    room.Release();
    await _context.SaveChangesAsync(ct);
    await transaction.CommitAsync(ct);
}
catch
{
    await transaction.RollbackAsync(ct);
    throw;
}
```

### E5. Migrations Commands

```bash
# Create a migration (run from Api project, but migration file goes to Infrastructure)
dotnet ef migrations add InitialCreate --project src/HostelSystem.Infrastructure --startup-project src/HostelSystem.Api

# Apply migrations to database
dotnet ef database update --project src/HostelSystem.Infrastructure --startup-project src/HostelSystem.Api

# Remove last migration (before applying to DB)
dotnet ef migrations remove --project src/HostelSystem.Infrastructure --startup-project src/HostelSystem.Api
```

---

## Part F: CQRS and MediatR — The Heart of This Architecture

### F1. What Happens When You Call `IMediator.Send(command)`

```
Controller
    → IMediator.Send(ApproveApplicationCommand)
        → MediatR finds ApproveApplicationHandler (by IRequestHandler<ApproveApplicationCommand, ...>)
            → Runs pipeline behaviors (ValidationBehavior → LoggingBehavior → ...)
                → Calls ApproveApplicationHandler.Handle(command, ct)
                    → Returns Result<AllocationDto>
```

### F2. Command vs Query vs Notification

```csharp
// COMMAND — a write operation. Returns Result<T> or just Result.
public record CreateApplicationCommand(int StudentId, int RoomId) : IRequest<Result<int>>;

// QUERY — a read operation. Returns T directly.
public record GetAvailableRoomsQuery(int HostelId) : IRequest<List<RoomDto>>;

// NOTIFICATION — an event. Can have multiple handlers. Returns nothing.
public record ApplicationApprovedNotification(int ApplicationId) : INotification;
```

### F3. Pipeline Behaviors (Cross-Cutting Concerns)

```csharp
// These wrap EVERY handler automatically — no duplicated code
// Execution order: Validation → Logging → [your handler]
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        // 1. Run all validators for this request type
        // 2. If validation fails, return Result.Fail (never calls next())
        // 3. If valid, call next() to continue the pipeline
    }
}
```

---

## Part G: Design Patterns You're Using

### G1. Repository Pattern
Abstraction over data access. `IApplicationRepository` hides EF Core details.

### G2. Unit of Work
Coordinates multiple repository operations into a single transaction. `SaveChangesAsync()`.

### G3. Result Pattern
Instead of exceptions for expected failures:
```csharp
public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }

    public static Result<T> Ok(T value) => new(true, value, null);
    public static Result<T> Fail(string error) => new(false, default, error);
}
```

### G4. Strategy Pattern
`IPaymentGateway` with implementations for Paystack, Flutterwave, etc.

### G5. Factory Pattern
`IDbContextFactory<T>` — creates DbContext instances for parallel/background work.

---

## Part H: Common Exam Pitfalls

1. **`==` vs `.Equals()`**: For reference types, `==` checks reference identity (unless
   overridden). For strings, `==` is overloaded to check value. Records auto-generate
   value-based equality.

2. **`throw;` vs `throw ex;`**: `throw;` preserves original stack trace. `throw ex;`
   resets it. Always use `throw;` in catch blocks.

3. **`IEnumerable` is lazy**: `.Where()` doesn't execute until enumerated. Calling `.ToList()`
   materializes the query.

4. **`async void`**: Never use except for event handlers. Exceptions can't be caught — they
   crash the process.

5. **Capturing loop variables in closures**: The `foreach` variable is captured by
   reference in older C# versions. Fixed in C# 5+. Still a classic exam question.

6. **Dispose pattern**: If a class owns an `IDisposable` field, it should itself implement
   `IDisposable` and dispose that field.

---

## Part I: Glossary — Terms You Need to Know Cold

| Term | Definition |
|---|---|
| **SOLID** | 5 design principles. S=Single Responsibility, O=Open/Closed, L=Liskov Substitution, I=Interface Segregation, D=Dependency Inversion |
| **DRY** | Don't Repeat Yourself |
| **KISS** | Keep It Simple, Stupid |
| **YAGNI** | You Ain't Gonna Need It |
| **Middleware** | A pipeline component that can handle, modify, or short-circuit an HTTP request |
| **ORM** | Object-Relational Mapper — EF Core maps C# classes ↔ SQL tables |
| **DTO** | Data Transfer Object — flat object for moving data between layers |
| **POCO** | Plain Old CLR Object — a class with no framework dependencies |
| **Garbage Collector (GC)** | Automatically reclaims heap memory no longer referenced |
| **Managed vs Unmanaged** | Managed = GC handles it. Unmanaged = you must free it (file handles, DB connections — use `IDisposable`) |
| **Immutable** | Cannot be changed after creation (strings, records with `init`) |
| **Deferred Execution** | LINQ query doesn't run until you enumerate it |
| **Expression-bodied member** | `=>` shorthand for methods/properties with a single expression |
| **Nullable Reference Types (NRT)** | Compiler feature that warns when you might dereference null |
| **Concurrency Token** | A column (like `RowVersion`) EF Core uses to detect concurrent modifications |
