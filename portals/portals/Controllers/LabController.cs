using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using portals.Data;
using portals.Models;


namespace portals.Controllers;

[Authorize(Roles = "Clinician", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[ApiController]
[Route("api/[controller]")]

public class LabController: ControllerBase
{
    private readonly ApplicationDbContext _context;
    
    public LabController(ApplicationDbContext context)
    {
        _context = context;
    }
    
    [HttpPost("AddLabReport")]
    public async Task<IActionResult> AddLabReport([FromBody] LabReport model)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 1. Save the Lab Report first
            _context.LabReport.Add(model);
            await _context.SaveChangesAsync(); 

            // 2. Create the Timeline Event
            var timelineEvent = new TimelineEvent
            {
                PatientId = model.PatientId,
                EventType = "LAB",
                EventDate = DateTime.UtcNow,
                SourceTable = "lab_reports",
                SourceId = model.Id, // Link to the ID we just generated
                Description = $"{model.TestName}: {model.ResultValue} {model.Unit} ({model.Interpretation})"
            };

            _context.TimelineEvents.Add(timelineEvent);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
            return Ok(new { message = "Success", labId = model.Id });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, $"Internal error: {ex.Message}");
        }
    }
    
    [HttpGet("GetLabReport/{id}")]
    public async Task<IActionResult> GetLabReport(int id)
    {
        var report = await _context.LabReport.FindAsync(id);
        if (report == null) return NotFound();
        return Ok(report);
    }
}