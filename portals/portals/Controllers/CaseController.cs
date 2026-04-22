using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using portals.Data;
using portals.Models;
using System.Security.Claims;

namespace portals.Controllers;

[Authorize(Roles = "Clinician", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[ApiController]
[Route("api/[controller]")]
public class CaseController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    public CaseController(ApplicationDbContext context) { _context = context; }

    [HttpPost("CreateCase")]
    public async Task<IActionResult> CreateCase([FromBody] ClinicalCase model)
    {
        if (string.IsNullOrEmpty(model.PatientIdentifier))
            return BadRequest("Patient Identifier is required.");
        
        model.LeadClinician = await GetCurrentProfessionalName();
    
        // Ensure audit fields are set
        model.CreatedAt = DateTime.UtcNow;
        model.Status = "Active";

        _context.ClinicalCases.Add(model);
        await _context.SaveChangesAsync();
    
        return Ok(model);
    }

    [HttpGet("PatientCases/{patientIdentifier}")]
    public async Task<IActionResult> GetPatientCases(string patientIdentifier)
    {
        var cases = await _context.ClinicalCases
            .Where(c => c.PatientIdentifier == patientIdentifier)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
        return Ok(cases);
    }
    [HttpGet("GetCaseDetails/{caseId}")]
    public async Task<IActionResult> GetCaseDetails(int caseId)
    {
        // 1. Fetch the main Case data
        var clinicalCase = await _context.ClinicalCases
            .Include(c => c.Vitals)
            .Include(c => c.Medications)
            .Include(c => c.ClinicalNotes)
            .FirstOrDefaultAsync(c => c.Id == caseId);

        if (clinicalCase == null) return NotFound();

        // 2. Fetch Linked Records using the "Timeline Style" Join approach
        // This is much safer and handles InstanceId correctly
        var linkedRecords = await (from link in _context.CaseItemLinks
            // Join with Radiology Reports
            join r in _context.Reports on link.SourceId equals r.Id into reportJoin
            from r in reportJoin.DefaultIfEmpty()

            // Join with Lab Reports
            join l in _context.LabReport on link.SourceId equals l.Id into labJoin
            from l in labJoin.DefaultIfEmpty()

            where link.CaseId == caseId
            select new
            {
                link.Id,
                link.SourceId,
                link.Category,
                link.SourceTable,

                // If it's a report, get InstanceNumber. If lab, it's null.
                InstanceId = (link.SourceTable.ToLower() == "reports" && r != null) ? r.InstanceNumber : null,

                // Build the summary dynamically
                Summary = link.SourceTable.ToLower() == "reports" 
                    ? (r != null ? r.Impression : "Imaging Record")
                    : (l != null ? $"{l.TestName}: {l.ResultValue} {l.Unit}" : "Lab Record")
            })
            .ToListAsync();

        // 3. Remove duplicates from the list (Defense against dirty database data)
        var distinctRecords = linkedRecords
            .GroupBy(x => new { x.SourceTable, x.SourceId })
            .Select(g => g.First())
            .ToList();

        // 4. Return everything together
        var result = new
        {
            clinicalCase.Id,
            clinicalCase.PatientIdentifier,
            clinicalCase.CaseTitle,
            clinicalCase.Status,
            clinicalCase.TreatmentGoal,
            clinicalCase.LongTermAdvice,
            Vitals = clinicalCase.Vitals.OrderByDescending(v => v.RecordedAt),
            Medications = clinicalCase.Medications,
            ClinicalNotes = clinicalCase.ClinicalNotes.OrderByDescending(n => n.CreatedAt),
            LinkedRecords = distinctRecords
        };

        // Log for debugging
        foreach (var rec in distinctRecords)
        {
            Console.WriteLine($"Case Detail -> ID: {rec.Id}, Table: {rec.SourceTable}, Instance: {rec.InstanceId}");
        }

        return Ok(result);
    }
    
    [HttpPost("LinkRecordToCase")]
    public async Task<IActionResult> LinkRecordToCase([FromBody] CaseItemLink link)
    {
        if (link.CaseId <= 0 || link.SourceId <= 0) 
            return BadRequest("Invalid Case or Record ID.");

        // --- NEW CHECK: Stop duplicates from being saved ---
        var alreadyExists = await _context.CaseItemLinks
            .AnyAsync(l => l.CaseId == link.CaseId 
                           && l.SourceId == link.SourceId 
                           && l.SourceTable == link.SourceTable);

        if (alreadyExists) 
            return BadRequest("This record is already linked to this case.");
        
        // 1. Get the Case so we know who the patient is
        var clinicalCase = await _context.ClinicalCases.FindAsync(link.CaseId);
        if (clinicalCase == null) return NotFound("Case not found");

        // 2. Get the actual Patient internal ID using the Identifier string
        var patient = await _context.Patients
            .FirstOrDefaultAsync(p => p.PatientIdentifier == clinicalCase.PatientIdentifier);
    
        if (patient == null) return NotFound("Associated patient record not found");

        // 3. Save the link
        _context.CaseItemLinks.Add(link);
        await _context.SaveChangesAsync();
        
        return Ok(new { message = "Record successfully linked to case" });
    }
    
    [HttpPost("AddVital")]
    public async Task<IActionResult> AddVital([FromBody] PatientVital vital)
    {
        _context.Vitals.Add(vital);
        await _context.SaveChangesAsync();
        return Ok(vital);
    }

    [HttpPost("AddMedication")]
    public async Task<IActionResult> AddMedication([FromBody] Medication med)
    {
        if (med.CaseId <= 0) return BadRequest("Invalid Case ID");

        med.PrescribedDate = DateTime.UtcNow; // Ensure this is set
        med.IsActive = true; 

        _context.Medications.Add(med);
        await _context.SaveChangesAsync();
        return Ok(med);
    }
    
    [HttpPost("UpdateAdvice")]
    public async Task<IActionResult> UpdateAdvice([FromBody] dynamic data)
    {
        int caseId = (int)data.caseId;
        string advice = (string)data.advice;

        var clinicalCase = await _context.ClinicalCases.FindAsync(caseId);
        if (clinicalCase == null) return NotFound();

        clinicalCase.LongTermAdvice = advice;
        await _context.SaveChangesAsync();
        return Ok(new { message = "Notes updated" });
    }
    
    [HttpPost("DiscontinueMedication")]
    public async Task<IActionResult> DiscontinueMedication([FromBody] DiscontinueMedRequest request)
    {
        var med = await _context.Medications.FindAsync(request.MedId);
        if (med == null) return NotFound();

        med.IsActive = false;
        med.DiscontinuedDate = DateTime.UtcNow;
        med.DiscontinuedReason = request.Reason;

        await _context.SaveChangesAsync();
        return Ok(new { message = "Medication discontinued for audit history." });
    }
    
    [HttpPost("AddNote")]
    public async Task<IActionResult> AddNote([FromBody] AddNoteRequest request)
    {
        var note = new ClinicalNote {
            CaseId = request.CaseId,
            NoteText = request.NoteText,
            AuthoredBy = await GetCurrentProfessionalName(),
        };
    
        _context.ClinicalNote.Add(note);
        await _context.SaveChangesAsync();
        return Ok(note);
    }
    
    [HttpGet("GetLinkableRecords/{patientIdentifier}")]
    public async Task<IActionResult> GetLinkableRecords(string patientIdentifier)
    {
        // Fetch radiology reports
        var radiology = await _context.Reports
            .Where(r => r.PatientIdentifier == patientIdentifier)
            .Select(r => new { r.Id, Title = r.Findings, Date = r.CreatedAt, Type = "Imaging" })
            .ToListAsync();

        // Fetch lab reports 
        var labs = await _context.LabReport
            .Where(l => l.PatientIdentifier == patientIdentifier)
            .Select(l => new { l.Id, Title = l.TestName, Date = l.CreatedAt, Type = "Lab" })
            .ToListAsync();

        return Ok(new { imaging = radiology, lab = labs });
    }
    
    private async Task<string> GetCurrentProfessionalName()
    {
        // Extract the GUID from the JWT Token
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    
        // Look up the Staff Profile
        var profile = await _context.StaffProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId);

        // Return FullName, or fallback to Email, or "Unknown"
        return profile?.FullName ?? User.Identity?.Name ?? "Unknown Clinician";
    }
}