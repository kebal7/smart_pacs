using System.ComponentModel.DataAnnotations;

namespace portals.Models;

public class Report
{
    [Key]
    public int Id { get; set; }
    
    // --- 1. IDENTIFIERS (Required at Upload) ---
    public string ReportIdentifier { get; set; }  // REP-000001
    public string InstanceNumber { get; set; }   // Orthanc ID
    public string StudyIdentifier { get; set; }   // DICOM UID
    public string AccessionNumber { get; set; }  // Reference Number

    // --- 2. PATIENT SNAPSHOT (Required at Upload) ---
    public string PatientIdentifier { get; set; }
    public string PatientName { get; set; }
    public string PatientSex { get; set; }
    public string PatientDOB { get; set; }

    // --- 3. STUDY METADATA ---
    public string Modality { get; set; }         // CR, CT, MR
    public string? StudyDescription { get; set; } 
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // Image arrival time

    // --- 4. CLINICAL CONTENT (Nullable) ---
    public string? ClinicalHistory { get; set; } // Provided by Radiographer
    public string? Findings { get; set; }        // The detailed observations
    public string? Impression { get; set; }      // The summary/conclusion
    public string? AiSuggestion { get; set; }    // Record of AI output
    public string? OtherNote { get; set; }   // Internal/Private notes (NOT FOR PRINT)
    
    // --- 5. THE AUDIT TRAIL (Workflow) ---
    public bool IsFinalized { get; set; } = false;

    // Draft Creation Info
    public DateTime? GeneratedAt { get; set; }
    public string? GeneratedBy { get; set; }     // e.g., "Dr. Junior"

    // Legal Sign-off Info
    public DateTime? FinalizedAt { get; set; }
    public string? FinalizedBy { get; set; }     // e.g., "Dr. Senior (Consultant)"
}