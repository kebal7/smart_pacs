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
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<TimelineEvent> TimelineEvents => Set<TimelineEvent>();
    
    public DbSet<LabReport> LabReport => Set<LabReport>();
    public DbSet<ClinicalCase> ClinicalCases { get; set; }
    public DbSet<PatientVital> Vitals { get; set; }
    public DbSet<Medication> Medications { get; set; }
    public DbSet<CaseItemLink> CaseItemLinks { get; set; }
    public DbSet<ClinicalNote> ClinicalNote { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}