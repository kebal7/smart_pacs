using System.ComponentModel.DataAnnotations.Schema;

namespace portals.Models
{
    public class ClinicalCase 
    {
        public int Id { get; set; }
        public string PatientIdentifier { get; set; } = string.Empty; 
        public string CaseTitle { get; set; } = string.Empty;
        public string Status { get; set; } = "Active"; 
        public string? TreatmentGoal { get; set; }
        public string? LongTermAdvice { get; set; }
    
        // Clean property, no hardcoded name here
        public string LeadClinician { get; set; } = string.Empty; 
    
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Collections initialized to prevent null errors
        public ICollection<PatientVital> Vitals { get; set; } = new List<PatientVital>();
        public ICollection<Medication> Medications { get; set; } = new List<Medication>();
        public ICollection<CaseItemLink> LinkedRecords { get; set; } = new List<CaseItemLink>();
        public ICollection<ClinicalNote> ClinicalNotes { get; set; } = new List<ClinicalNote>();
    }

    public class PatientVital
    {
        public int Id { get; set; }

        // This is what your JS is sending
        public int CaseId { get; set; } 

        // Explicitly tell EF that CaseId is the link to ClinicalCase
        [ForeignKey("CaseId")]
        public ClinicalCase? ClinicalCase { get; set; }

        public float Temperature { get; set; }
        public string BloodPressure { get; set; }
        public float SpO2 { get; set; }
        public string RecordedBy { get; set; }
        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    }

    public class Medication 
    {
        public int Id { get; set; }
        public int CaseId { get; set; }
    
        [ForeignKey("CaseId")]
        public ClinicalCase? ClinicalCase { get; set; }

        public string DrugName { get; set; }
        public string Dosage { get; set; }
        public string Frequency { get; set; }
    
        // Legal & Audit Fields
        public bool IsActive { get; set; } = true;
        public DateTime PrescribedDate { get; set; } = DateTime.UtcNow;
        public DateTime? DiscontinuedDate { get; set; }
        public string? DiscontinuedReason { get; set; } // e.g., "Allergic reaction", "Completed course"
    }
    
    public class DiscontinueMedRequest
    {
        public int MedId { get; set; }
        public string Reason { get; set; }
    }
    
    public class AddNoteRequest {
        public int CaseId { get; set; }
        public string NoteText { get; set; }
    }

    public class CaseItemLink
    {
        public int Id { get; set; }
        public int CaseId { get; set; }
        
        [ForeignKey("CaseId")]
        public ClinicalCase? ClinicalCase { get; set; }
        
        public string SourceTable { get; set; } // "reports" or "lab_reports"
        public int SourceId { get; set; } 
        public string Category { get; set; } // "Imaging", "Lab"
    }
    
    public class ClinicalNote
    {
        public int Id { get; set; }
        public int CaseId { get; set; }
    
        [ForeignKey("CaseId")]
        public ClinicalCase? ClinicalCase { get; set; }
    
        public string NoteText { get; set; }
        public string AuthoredBy { get; set; } // e.g., "Dr. Kebal"
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
    
}