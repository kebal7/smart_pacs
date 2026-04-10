using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using portals.Data;
using portals.Models;

namespace portals.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClinicianController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ClinicianController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: api/Clinician/Patients
    // Returns a unique list of patients based on their Identifier
    [HttpGet("Patients")]
    public async Task<IActionResult> GetPatients()
    {
        var patients = await _context.Reports
            .Select(r => new { r.PatientIdentifier, r.PatientName })
            .Distinct()
            .OrderBy(p => p.PatientName)
            .ToListAsync();

        return Ok(patients);
    }
    //
    // GET: api/Clinician/PatientHistory/{patientId}
    // Aggregates all reports/events for a single patient into a timeline
    // [HttpGet("PatientHistory/{patientId}")]
    // public async Task<IActionResult> GetPatientHistory(string patientId)
    // {
    //     var history = await _context.Reports
    //         .Where(r => r.PatientIdentifier == patientId)
    //         .OrderByDescending(r => r.CreatedAt)
    //         .Select(r => new
    //         {
    //             EventDate = r.CreatedAt,
    //             EventType = r.Modality ?? "IMG",
    //             Description = r.StudyDescription ?? "General Imaging Study",
    //             // Meta info for the UI
    //             InstanceId = r.InstanceNumber,
    //             IsFinalized = r.IsFinalized,
    //             Summary = r.Impression ?? "No impression recorded yet."
    //         })
    //         .ToListAsync();
    //
    //     if (history == null || !history.Any())
    //         return NotFound("No history found for this patient.");
    //
    //     return Ok(history);
    // }
    
    [HttpGet("PatientHistory/{patientId}")]
    public async Task<IActionResult> GetPatientHistory(string patientId)
    {
        var patient = await _context.Patients
            .FirstOrDefaultAsync(p => p.PatientIdentifier == patientId);

        if (patient == null) return NotFound(new { message = "Patient not found" });

        var history = await (from t in _context.TimelineEvents
            // Join 1: Radiology Reports
            join r in _context.Reports on t.SourceId equals r.Id into reportJoin
            from r in reportJoin.DefaultIfEmpty()

            // Join 2: Lab Results (Assuming you'll create this table soon)
            // join l in _context.LabResults on t.SourceId equals l.Id into labJoin
            // from l in labJoin.DefaultIfEmpty()

            where t.PatientId == patient.Id
            orderby t.EventDate descending
            select new
            {
                t.Id,
                t.EventDate,
                t.EventType, // "RADIOLOGY", "LAB", "CLINICAL_NOTE"
                t.SourceTable,
                sourceId = t.SourceId,
                t.Description,
                
            
                // Radiology Specifics
                InstanceId = r != null ? r.InstanceNumber : null,
                Modality = r != null ? r.Modality : null,
            
                // Lab Specifics (Example of how you'd add it)
                // LabValue = l != null ? l.Value : null,
                // LabUnit = l != null ? l.Unit : null,

                // A smart summary that picks the best text to show
                Summary = r != null ? r.Impression : t.Description
            }).ToListAsync();

        return Ok(history);
    }

    [HttpGet("PatientDemographics/{patientId}")]
    public async Task<IActionResult> GetDemographics(string patientId)
    {
        // Querying the actual patients table now
        var patient = await _context.Patients
            .Where(p => p.PatientIdentifier == patientId)
            .Select(p => new
            {
                p.Name,
                p.PatientIdentifier,
                p.DateOfBirth,
                p.Address,
                p.ContactNo,
                p.EmergencyContact,
                // Calculate age on the fly if needed
                Age = DateTime.Today.Year - p.DateOfBirth.Year
            })
            .FirstOrDefaultAsync();

        if (patient == null) return NotFound("Patient record not found in master table.");

        return Ok(patient);
    }
}