using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using portals.Data;
using portals.Models;
using portals.services;

[Authorize(Roles = "Radiographer", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[ApiController]
[Route("api/[controller]")]
public class RadiographerController : ControllerBase
{
    private readonly IDicomService _dicomService;
    private readonly ApplicationDbContext _context;
    private readonly string orthancUrl = "http://localhost:8042";

    public RadiographerController(IDicomService dicomService, ApplicationDbContext context)
    {
        _dicomService = dicomService;
        _context = context;
    }

    [HttpGet("patient-lookup/{identifier}")]
    public IActionResult LookupPatient(string identifier)
    {
        var patient = _context.Patients.FirstOrDefault(p => p.PatientIdentifier == identifier);
        if (patient == null) return NotFound();
        return Ok(patient);
    }

    [HttpGet("generate-ids")]
    public async Task<IActionResult> GenerateIds()
    {
        var now = DateTime.UtcNow;
        var datePrefix = now.ToString("yyyyMMdd"); // 20260319

        // 1. Get daily count for uniqueness
        var todayCount = await _context.Reports
            .CountAsync(r => r.CreatedAt >= now.Date);
        int nextSeq = todayCount + 1;

        // 2. Accession Number (Legal/Billing identifier)
        // Format: 20260319-0001
        string accessionNo = $"{datePrefix}-{nextSeq:D4}";

        // 3. Study ID (Short Human-Readable Label)
        // Format: S-XXXX (e.g., S-0001) 
        string studyId = $"S-{nextSeq:D4}";

        return Ok(new {
            accessionNo = accessionNo,
            studyId = studyId
        });
    }

    [HttpPost("upload-to-pacs")]
    public async Task<IActionResult> UploadToPacs([FromForm] RadiographerUploadModel model)
    {
        if (model.ImageFile == null) return BadRequest("No image provided.");

        // 1. Prepare DICOM using Service
        byte[] imageBytes;
        using (var ms = new MemoryStream())
        {
            await model.ImageFile.CopyToAsync(ms);
            imageBytes = ms.ToArray();
        }

        var dicomDataset = await _dicomService.CreateDicomAsync(imageBytes, new PatientData
        {
            PatientID = model.PatientID,
            PatientName = model.PatientName,
            BirthDate = model.PatientDOB.Replace("-", ""),
            Sex = model.PatientSex,
            StudyType = model.StudyType,
            AccessionNumber = model.AccessionNo,
            StudyID = model.StudyID            
        });

        // 2. Convert to Stream for Orthanc
        var dicomFile = new FellowOakDicom.DicomFile(dicomDataset);
        using var msDicom = new MemoryStream();
        await dicomFile.SaveAsync(msDicom);
        msDicom.Position = 0;

        // 3. Upload to Orthanc
        using var client = new HttpClient();
        var authHeader = Convert.ToBase64String(Encoding.ASCII.GetBytes("orthanc:orthanc"));
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authHeader);
        
        var content = new StreamContent(msDicom);
        var response = await client.PostAsync($"{orthancUrl}/instances", content);
        
        if (!response.IsSuccessStatusCode) return StatusCode(500, "PACS Upload Failed");

        // 4. Parse Orthanc Response to get the Instance ID
        var orthancJson = await response.Content.ReadFromJsonAsync<JsonElement>();
        string instanceId = orthancJson.GetProperty("ID").GetString();

        // 5. Create Entry in Report Database
        var lastReport = _context.Reports.OrderByDescending(r => r.Id).FirstOrDefault();
        int nextId = (lastReport?.Id ?? 0) + 1;

        var newReport = new Report
        {
            // Identifiers
            ReportIdentifier = $"REP{nextId:D6}",
            PatientIdentifier = model.PatientID,
            StudyIdentifier = model.StudyID,
            AccessionNumber = model.AccessionNo, // Essential for matching hospital records
            InstanceNumber = instanceId,         // Orthanc ID
        
            // --- THE SNAPSHOT (Essential for Printing) ---
            PatientName = model.PatientName,
            PatientSex = model.PatientSex,
            PatientDOB = model.PatientDOB,
            Modality = model.StudyType,
        
            // Timestamps
            CreatedAt = DateTime.UtcNow,

            // --- NULLABLE DEFAULTS (Essential to prevent DB errors) ---
            IsFinalized = false,
            Findings = null,
            Impression = null,
            OtherNote = null,      // We'll keep this clean for now
            AiSuggestion = null,
            GeneratedBy = null,
            FinalizedBy = null
        };

        _context.Reports.Add(newReport);
        await _context.SaveChangesAsync();

        return Ok(new { 
            message = "Success", 
            instanceId = instanceId, 
            reportId = newReport.ReportIdentifier 
        });
    }
}

public class RadiographerUploadModel
{
    public string PatientID { get; set; }
    public string PatientName { get; set; }
    public string PatientDOB { get; set; }
    public string PatientSex { get; set; }
    public string StudyType { get; set; }
    public string AccessionNo { get; set; }
    public string StudyID { get; set; }
    public IFormFile ImageFile { get; set; }
}