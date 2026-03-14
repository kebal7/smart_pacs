using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using portals.Models;

namespace portals.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Patient> Patients => Set<Patient>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Patient>(entity =>
        {
            entity.ToTable("patients");

            entity.HasKey(p => p.Id);

            entity.Property(p => p.Id)
                .HasColumnName("id");

            entity.Property(p => p.PatientIdentifier)
                .HasColumnName("patient_identifier")
                .IsRequired();

            entity.Property(p => p.Name)
                .HasColumnName("name")
                .IsRequired();

            entity.Property(p => p.DateOfBirth)
                .HasColumnName("date_of_birth")
                .HasConversion(
                    v => v.ToUniversalTime(),
                    v => DateTime.SpecifyKind(v, DateTimeKind.Utc)
                );

            entity.Property(p => p.Address)
                .HasColumnName("address");

            entity.Property(p => p.ContactNo)
                .HasColumnName("contact_no");

            entity.Property(p => p.EmergencyContact)
                .HasColumnName("emergency_contact");

            entity.Property(p => p.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("NOW()");
        });
    }
}