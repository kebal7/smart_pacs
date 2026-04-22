using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using portals.Data;
using portals.Models;
using System.Security.Claims;

namespace portals.Controllers;

    [Authorize(Roles = "Radiologist,Clinician", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ApiController]
    [Route("api/[controller]")]
    
    public class RadiologistController : Controller
    {
        private readonly string orthancUrl = "http://localhost:8042";
        private readonly string orthancUser = "orthanc";
        private readonly string orthancPassword = "orthanc";
        private readonly IConfiguration _config;

        private readonly ApplicationDbContext _context;
        
        public RadiologistController(ApplicationDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }
        
        private HttpClient CreateClient()
        {
            var client = new HttpClient();
            client.BaseAddress = new Uri(orthancUrl);
            var byteArray = Encoding.ASCII.GetBytes($"{orthancUser}:{orthancPassword}");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            return client;
        }

        // GET: /Radiologist
        [HttpGet]

        public IActionResult Index()
        {
            return View();
        }

        private async Task<IActionResult> CallOrthanc(Func<HttpClient, Task<HttpResponseMessage>> action)
        {
            using var client = CreateClient();
            try
            {
                var response = await action(client);
        
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return StatusCode((int)response.StatusCode, $"Orthanc error: {error}");
                }

                // Return the raw content or stream depending on what you need
                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }
            catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
            {
                // This catches "Connection Refused" specifically
                return StatusCode(503, new { message = "PACS Server (Orthanc) is currently unreachable. Please check if the service is running." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Internal error communicating with Orthanc: {ex.Message}" });
            }
        }
        
        // GET: /Radiologist/ListDicoms
        [Authorize(Roles = "Radiologist")]
        [HttpGet("ListDicoms")] 
        public async Task<IActionResult> ListDicoms()
        {
            return await CallOrthanc(c => c.GetAsync("instances"));
        }
        
        

        // GET: /Radiologist/GetDicomMetadata?instanceId=xxx
        [HttpGet("GetDicomMetadata/{instanceId}")]
        public async Task<IActionResult> GetDicomMetadata(string instanceId)
        {
            return await CallOrthanc(c => c.GetAsync($"instances/{instanceId}/tags"));
        }

        // GET: /Radiologist/DownloadDicom?instanceId=xxx
        [HttpGet("DownloadDicom")]
        public async Task<IActionResult> DownloadDicom(string instanceId)
        {
            using var client = CreateClient();

            // 1. Get metadata to build a proper filename
            var metaRes = await client.GetAsync($"instances/{instanceId}/tags");
            if (!metaRes.IsSuccessStatusCode)
                return StatusCode(500, "Failed to fetch DICOM metadata");

            var meta = await metaRes.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();

            string patientName = meta?["0010,0010"].GetProperty("Value").GetString() ?? "UnknownPatient";
            string patientId = meta?["0010,0020"].GetProperty("Value").GetString() ?? instanceId;
            string studyDate = meta?["0008,0020"].GetProperty("Value").GetString() ?? DateTime.Now.ToString("yyyyMMdd");

            // Clean filename
            string safePatientName = patientName.Replace(" ", "_");
            string fileName = $"{patientId}-{safePatientName}-{studyDate}.dcm";

            // 2. Get the actual DICOM bytes
            var dicomRes = await client.GetAsync($"instances/{instanceId}/file");
            if (!dicomRes.IsSuccessStatusCode)
                return StatusCode(500, "Failed to fetch DICOM file");

            var bytes = await dicomRes.Content.ReadAsByteArrayAsync();

            // 3. Return file to browser
            return File(bytes, "application/dicom", fileName);
        }
    
        [Authorize(Roles = "Radiologist")]
    [HttpGet("GetWorklist")]
    public async Task<IActionResult> GetWorklist(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 10, 
        [FromQuery] string search = "", 
        [FromQuery] string status = "All")
    {
        var query = _context.Reports.AsQueryable();

        // 1. Search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            string s = search.ToLower();
            query = query.Where(r => r.PatientName.ToLower().Contains(s) || 
                                     r.PatientIdentifier.ToLower().Contains(s) || 
                                     r.InstanceNumber.Contains(s));
        }

        // 2. Status filter
        if (status != "All")
        {
            if (status == "New") query = query.Where(r => !r.IsFinalized && string.IsNullOrEmpty(r.Findings));
            else if (status == "Draft") query = query.Where(r => !r.IsFinalized && !string.IsNullOrEmpty(r.Findings));
            else if (status == "Finalized") query = query.Where(r => r.IsFinalized);
        }

        // 3. Get the data
        var rawData = await query.ToListAsync();

        // 4. HELPER: Extract percentage from string "Label (XX.X%)"
        double GetPercentage(string aiStr) {
            if (string.IsNullOrEmpty(aiStr)) return -1;
            var match = System.Text.RegularExpressions.Regex.Match(aiStr, @"(\d+(\.\d+)?)");
            return match.Success ? double.Parse(match.Value) : -1;
        }

        // 5. MASTER SORTING LOGIC:
        // Priority 1: Unfinalized (New/Draft) first
        // Priority 2: Highest AI Percentage first
        // Priority 3: Newest Date first
        var sortedData = rawData
            .OrderBy(r => r.IsFinalized) 
            .ThenByDescending(r => GetPercentage(r.AiSuggestion)) 
            .ThenByDescending(r => r.CreatedAt)
            .ToList();

        int totalCount = sortedData.Count;
        int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var pagedData = sortedData
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new {
                r.InstanceNumber,
                r.PatientIdentifier,
                r.PatientName,
                r.Modality,
                r.CreatedAt,
                r.AiSuggestion,
                r.IsFinalized,
                r.Findings
            });

        return Ok(new { totalCount, totalPages, currentPage = page, data = pagedData });
    }
        
        [Authorize(Roles = "Radiologist")]
    [HttpPost("UpdateReport")]
    public async Task<IActionResult> UpdateReport([FromBody] UpdateReportRequest model)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        var profile = await _context.StaffProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId);
        
        // Create a professional signature string: "Dr. Name (NMC: 12345)"
        string professionalSignature = profile != null 
            ? $"{profile.FullName} (License: {profile.LicenseNumber})" 
            : (User.Identity?.Name ?? "Unknown Radiologist");
        
        // 1. Fetch the report
        var report = await _context.Reports.FirstOrDefaultAsync(r => r.InstanceNumber == model.InstanceId);
        if (report == null) return NotFound("Report not found.");
        if (report.IsFinalized) return BadRequest("Report is finalized and cannot be updated.");

        // 2. Update the Report fields
        report.StudyDescription = model.StudyDescription;
        report.ClinicalHistory = model.ClinicalHistory;
        report.Findings = model.Findings;
        report.Impression = model.Impression;
        report.OtherNote = model.OtherNote;

        if (!string.IsNullOrEmpty(model.AiSuggestion)) 
            report.AiSuggestion = model.AiSuggestion;

        if (model.ShouldFinalize)
        {
            report.IsFinalized = true;
            report.FinalizedAt = DateTime.UtcNow;
            report.FinalizedBy = professionalSignature;
        }
        else 
        {
            report.IsFinalized = false; 
            report.GeneratedBy = professionalSignature;
            report.GeneratedAt = DateTime.UtcNow;
        }

        // 3. --- TIMELINE SYNC LOGIC ---
        // Since Report only has PatientIdentifier (string), we find the internal Int ID
        var internalPatient = await _context.Patients
            .FirstOrDefaultAsync(p => p.PatientIdentifier == report.PatientIdentifier);

        if (internalPatient != null)
        {
            // Check if a timeline event already exists for this report
            var existingEvent = await _context.TimelineEvents
                .FirstOrDefaultAsync(t => t.SourceTable == "reports" && t.SourceId == report.Id);

            string displayDesc = $"{report.Modality}: {(!string.IsNullOrEmpty(report.Impression) ? report.Impression : "Drafting in progress...")}";

            if (existingEvent == null)
            {
                // First time saving - Create new timeline entry
                _context.TimelineEvents.Add(new TimelineEvent
                {
                    PatientId = internalPatient.Id, // The essential integer ID
                    EventType = "RADIOLOGY",
                    EventDate = DateTime.UtcNow,
                    SourceTable = "reports",
                    SourceId = report.Id,
                    Description = displayDesc
                });
            }
            else
            {
                // Update existing timeline entry
                existingEvent.Description = displayDesc;
                existingEvent.EventDate = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = report.IsFinalized ? "Finalized!" : "Draft Saved!" });
    }

        public class UpdateReportRequest
        {
            public string InstanceId { get; set; } // Matches JS InstanceId
            public string StudyDescription { get; set; }
            public string ClinicalHistory { get; set; }
            public string Findings { get; set; }
            public string Impression { get; set; }
            public string OtherNote { get; set; }
            public string AiSuggestion { get; set; }
            public bool ShouldFinalize { get; set; } // Matches JS ShouldFinalize
        }
        
        // Add this to RadiologistController.cs

        [HttpGet("GetReport")]
        public async Task<IActionResult> GetReport(string instanceId)
        {
            // Search by the Orthanc Instance UID
            var report = await _context.Reports
                .FirstOrDefaultAsync(r => r.InstanceNumber == instanceId);

            if (report == null) return NotFound();

            // Return EVERYTHING so the UI can repopulate
            return Ok(new
            {
                studyDescription = report.StudyDescription,
                clinicalHistory = report.ClinicalHistory,
                findings = report.Findings,
                impression = report.Impression,
                otherNote = report.OtherNote,
                aiSuggestion = report.AiSuggestion,
                isFinalized = report.IsFinalized
            });
        }
        
        [HttpGet("GetPrintingReport")]
        public async Task<IActionResult> GetPrintingReport(string instanceId)
        {
            var report = await _context.Reports
                .FirstOrDefaultAsync(r => r.InstanceNumber == instanceId);

            if (report == null) return NotFound("Report record missing.");

            // Return every single field for the print template
            return Ok(report); 
        }
        
        [Authorize(Roles = "Radiologist")]
        [HttpPost("AnalyzeWithAi/{instanceId}")]
        public async Task<IActionResult> AnalyzeWithAi(string instanceId)
        {
            try
            {
                using var orthancClient = CreateClient();
                var dicomRes = await orthancClient.GetAsync($"instances/{instanceId}/file");
                if (!dicomRes.IsSuccessStatusCode) return StatusCode(500, "Orthanc Error");
                var fileBytes = await dicomRes.Content.ReadAsByteArrayAsync();

                string aiUrl = _config["AiService:BaseUrl"];
                string aiKey = _config["AiService:InternalKey"];
                
                using var aiClient = new HttpClient();
        
                aiClient.DefaultRequestHeaders.Add("X-Internal-Key", aiKey);
                
                using var content = new MultipartFormDataContent();
                var fileContent = new ByteArrayContent(fileBytes);
                content.Add(fileContent, "file", "image.dcm");

                var aiResponse = await aiClient.PostAsync($"{aiUrl}/predict", content);
        
                if (!aiResponse.IsSuccessStatusCode) 
                    return StatusCode((int)aiResponse.StatusCode, "AI Service rejected request");

                return Content(await aiResponse.Content.ReadAsStringAsync(), "application/json");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
