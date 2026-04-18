using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace portals.Models;

[Table("staff_profiles")]
public class StaffProfile
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("user_id")] // Foreign Key to AspNetUsers.Id
    public string UserId { get; set; }

    [Required]
    [Column("full_name")]
    public string FullName { get; set; }

    [Column("contact_no")]
    public string ContactNo { get; set; }

    [Column("address")]
    public string Address { get; set; }

    [Column("professional_email")]
    public string ProfessionalEmail { get; set; }

    [Column("license_number")] // Covers NMC, NHPC, etc.
    public string LicenseNumber { get; set; }

    [Column("department_or_modality")] // e.g. "Radiology", "CT Scan", "Cardiology"
    public string DepartmentOrModality { get; set; }

    [Column("current_position")] // e.g. "Senior Consultant", "Lead Technician"
    public string CurrentPosition { get; set; }

    [Column("staff_type")] // "Radiologist", "Radiographer", or "Clinician"
    public string StaffType { get; set; }

    [Column("career_start_date")]
    public DateTime CareerStartDate { get; set; }

    [Column("hospital_join_date")]
    public DateTime HospitalJoinDate { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Helper to calculate total experience for the UI
    [NotMapped]
    public int TotalYearsExperience => DateTime.Now.Year - CareerStartDate.Year;
}