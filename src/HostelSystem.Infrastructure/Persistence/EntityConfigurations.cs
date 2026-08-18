using HostelSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HostelSystem.Infrastructure.Persistence;

public class HostelConfiguration : IEntityTypeConfiguration<Hostel>
{
    public void Configure(EntityTypeBuilder<Hostel> builder)
    {
        builder.ToTable("Hostels");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(h => h.Description)
            .HasMaxLength(1000);

        builder.Property(h => h.Address)
            .IsRequired()
            .HasMaxLength(500);

        builder.HasMany(h => h.Rooms)
            .WithOne(r => r.Hostel)
            .HasForeignKey(r => r.HostelId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("Rooms");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.RoomNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(r => r.PricePerSemester);

        // Optimistic concurrency (SQLite uses a GUID token; SQL Server would use rowversion)
        builder.Property(r => r.Version)
            .IsConcurrencyToken();

        // CHECK constraint as defense-in-depth
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_Room_Occupancy",
            "[CurrentOccupancy] >= 0 AND [CurrentOccupancy] <= [Capacity]"));

        builder.HasMany(r => r.Allocations)
            .WithOne(a => a.Room)
            .HasForeignKey(a => a.RoomId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("Students");

        builder.HasKey(s => s.Id);

        builder.HasIndex(s => s.UserId).IsUnique();
        builder.HasIndex(s => s.StudentNumber).IsUnique();

        builder.Property(s => s.UserId).IsRequired().HasMaxLength(450);
        builder.Property(s => s.StudentNumber).IsRequired().HasMaxLength(50);
        builder.Property(s => s.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(s => s.LastName).IsRequired().HasMaxLength(100);
        builder.Property(s => s.PhoneNumber).HasMaxLength(20);

        builder.Property(s => s.Gender)
            .HasConversion<string>()
            .HasMaxLength(10);
    }
}

public class RoomApplicationConfiguration : IEntityTypeConfiguration<RoomApplication>
{
    public void Configure(EntityTypeBuilder<RoomApplication> builder)
    {
        builder.ToTable("RoomApplications");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.ReviewedBy).HasMaxLength(200);
        builder.Property(a => a.RejectionReason).HasMaxLength(500);
        builder.Property(a => a.AdditionalNotes).HasMaxLength(1000);

        builder.HasOne(a => a.Student)
            .WithMany(s => s.Applications)
            .HasForeignKey(a => a.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Room)
            .WithMany()
            .HasForeignKey(a => a.RoomId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class RoomAllocationConfiguration : IEntityTypeConfiguration<RoomAllocation>
{
    public void Configure(EntityTypeBuilder<RoomAllocation> builder)
    {
        builder.ToTable("RoomAllocations");

        builder.HasKey(a => a.Id);

        builder.HasOne(a => a.Student)
            .WithMany()
            .HasForeignKey(a => a.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Application)
            .WithMany()
            .HasForeignKey(a => a.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");

        builder.HasKey(p => p.Id);

        builder.HasIndex(p => p.TransactionReference).IsUnique();

        builder.Property(p => p.Amount)
            .IsRequired();

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.TransactionReference).HasMaxLength(200);
        builder.Property(p => p.PaymentMethod).HasMaxLength(50);

        builder.HasOne(p => p.Student)
            .WithMany()
            .HasForeignKey(p => p.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Allocation)
            .WithMany()
            .HasForeignKey(p => p.AllocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
