using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using portals.Data;
using portals.Models;

namespace portals.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CaseController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    public CaseController(ApplicationDbContext context) { _context = context; }

    [HttpPost("CreateCase")]
    public async Task<IActionResult> CreateCase([FromBody] ClinicalCase model)
    {
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
        var result = await _context.ClinicalCases
            .Include(c => c.Vitals)
            .Include(c => c.Medications)
            .Include(c => c.ClinicalNotes)
            .Include(c => c.LinkedRecords)
            .FirstOrDefaultAsync(c => c.Id == caseId);

        return Ok(result);
    }
    
    [HttpPost("LinkRecordToCase")]
    public async Task<IActionResult> LinkRecordToCase([FromBody] CaseItemLink link)
    {
        if (link.CaseId <= 0 || link.SourceId <= 0) 
            return BadRequest("Invalid Case or Record ID.");

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

        // 4. Log to Timeline (Now using the correct 'int' PatientId)
        var timeline = new TimelineEvent {
            PatientId = patient.Id, // <--- This is now an 'int', matching your model
            EventType = "CASE_LINK",
            EventDate = DateTime.UtcNow,
            Description = $"Linked {link.Category} record #{link.SourceId} to Case: {clinicalCase.CaseTitle}",
            SourceTable = "CaseItemLinks",
            SourceId = link.Id
        };
    
        _context.TimelineEvents.Add(timeline);
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
            AuthoredBy = "Dr. Kebal" // In a real app, get this from user session
        };
    
        _context.ClinicalNote.Add(note);
        await _context.SaveChangesAsync();
        return Ok(note);
    }
}