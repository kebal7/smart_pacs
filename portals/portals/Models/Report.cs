using System.ComponentModel.DataAnnotations;

namespace portals.Models;

public class Report
{
    [Key]
    public int Id { get; set; }   // Primary key

    public string ReportIdentifier { get; set; } = "";  // REP000001

    public string PatientIdentifier { get; set; } = ""; // PAT000001

    public string StudyIdentifier { get; set; } = "";   // Study UID or custom

    public string InstanceNumber { get; set; } = "";    // Orthanc instance ID

    public string? AiSuggestion { get; set; }

    public string? Findings { get; set; }

    public string? OtherNote { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}