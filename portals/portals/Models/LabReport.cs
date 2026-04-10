using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace portals.Models;

[Table("lab_reports")]
public class LabReport
{
    [Key]
    public int Id { get; set; }
    
    // --- 1. RELATIONAL LINK (For Fast Joins) ---
    [Required]
    public int PatientId { get; set; } // The actual FK to the Patients table

    // --- 2. THE SNAPSHOT (Matches Report.cs) ---
    public string PatientIdentifier { get; set; } // "PAT-101"
    public string PatientName { get; set; }
    public string PatientSex { get; set; }
    public string PatientDOB { get; set; }

    // --- 3. THE DATA ---
    public string LabReportIdentifier { get; set; }
    public string TestName { get; set; }
    public string ResultValue { get; set; }
    public string Unit { get; set; }
    public string ReferenceRange { get; set; } // e.g., "13.5-17.5"
    public string Interpretation { get; set; } // Normal, High, Low
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? SignedBy { get; set; }
}