using Microsoft.AspNetCore.Mvc;
using portals.Data;
using portals.Models;
using portals.DTOs;

namespace portals.Controllers;

[ApiController]
[Route("api/patients")]
public class PatientController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public PatientController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET all patients
    [HttpGet]
    public IActionResult GetPatients()
    {
        return Ok(_context.Patients.ToList());
    }

    // GET patient by id
    [HttpGet("{id}")]
    public IActionResult GetPatient(int id)
    {
        var patient = _context.Patients.Find(id);
        if (patient == null) return NotFound();
        return Ok(patient);
    }

    // CREATE patient
    [HttpPost]
    [HttpPost]
    public IActionResult CreatePatient(CreatePatientDto dto)
    {
        var lastPatient = _context.Patients
            .OrderByDescending(p => p.Id)
            .FirstOrDefault();

        int nextId = lastPatient == null ? 1 : lastPatient.Id + 1;

        var patient = new Patient
        {
            PatientIdentifier = $"PAT{nextId:D6}",
            Name = dto.Name,
            DateOfBirth = dto.DateOfBirth,
            Address = dto.Address,
            ContactNo = dto.ContactNo,
            EmergencyContact = dto.EmergencyContact
        };

        _context.Patients.Add(patient);
        _context.SaveChanges();

        return Ok(patient);
    }

    // UPDATE patient
    [HttpPut("{id}")]
    public IActionResult UpdatePatient(int id, CreatePatientDto dto)
    {
        var patient = _context.Patients.Find(id);
        if (patient == null) return NotFound();

        patient.Name = dto.Name;
        patient.DateOfBirth = DateTime.SpecifyKind(dto.DateOfBirth, DateTimeKind.Utc);
        patient.Address = dto.Address;
        patient.ContactNo = dto.ContactNo;
        patient.EmergencyContact = dto.EmergencyContact;

        _context.SaveChanges();
        return Ok(patient);
    }

    // DELETE patient
    [HttpDelete("{id}")]
    public IActionResult DeletePatient(int id)
    {
        var patient = _context.Patients.Find(id);
        if (patient == null) return NotFound();

        _context.Patients.Remove(patient);
        _context.SaveChanges();
        return Ok();
    }
}