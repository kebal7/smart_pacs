// --- REPORTING STATE ---
const reportParams = new URLSearchParams(window.location.search);
const instanceId = reportParams.get("instanceId");
let currentAiSummary = "";

    function getAuthHeaders() {
        return {
            "Authorization": `Bearer ${localStorage.getItem('jwtToken')}`,
            "Content-Type": "application/json"
        };
    }

    async function handleAuthError(res) {
        if (res.status === 401 || res.status === 403) {
            document.getElementById('authErrorModal').style.display = 'flex';
            document.getElementById('mainContent').style.filter = 'blur(8px)';
            return true;
        }
        return false;
    }

// --- 1. AI LOGIC (FastAPI Port 8001) ---
async function runAIAnalysis() {
    const resDiv = document.getElementById('aiResults');
    const aiField = document.getElementById('aiSuggestion'); 
    resDiv.innerHTML = "AI Analyzing Study...";

    try {
        const aiRes = await fetch(`http://localhost:5266/api/Radiologist/AnalyzeWithAi/${instanceId}`, { 
            method: "POST",
            headers: getAuthHeaders()
        });

        if (await handleAuthError(aiRes)) return;

        if (!aiRes.ok) {
            const errorText = await aiRes.text();
            throw new Error(errorText || "Backend AI Proxy error");
        }

        const data = await aiRes.json();

        // 1. Sort ALL predictions by probability descending
        const sortedPredictions = Object.entries(data.all_predictions)
            .sort(([, a], [, b]) => b.probability - a.probability);

        // 2. Build Sidebar UI (Visual Bars for all)
        resDiv.innerHTML = sortedPredictions.map(([key, val]) => {
            const color = val.positive ? '#ef4444' : '#10b981';
            const pct = (val.probability * 100).toFixed(1);
            return `<div style="padding:4px; border-left:3px solid ${color}; margin-bottom:4px; font-size:12px; background:#09090b;">
                <div style="display:flex; justify-content:space-between;">
                    <span>${key}</span>
                    <span>${pct}%</span>
                </div>
                <div style="height:2px; background:#27272a; margin-top:2px;">
                    <div style="width:${pct}%; background:${color}; height:100%;"></div>
                </div>
            </div>`;
        }).join('');

        // 3. Extract Top 5 for Database/PDF
        // We take the top 5 most probable findings to provide a comprehensive view
        const top5 = sortedPredictions.slice(0, 5).map(([key, val]) => {
            const pct = (val.probability * 100).toFixed(1);
            return `${key} (${pct}%)`;
        });

        // Set the global variable for use in submitReport
        currentAiResult = top5.join(", ");
        
        if(aiField) aiField.value = currentAiResult;
        
        log("AI Analysis Complete - Top 5 Recorded");
    } catch (e) {
        resDiv.innerHTML = "AI Error: Service Offline";
        console.error("AI Analysis failed:", e);
    }
}
// --- 2. SAVE TO DATABASE.NET BACKEND (Port 5266) ---
async function submitReport(isFinal) {
    // Map UI IDs to your C# Model Properties
    const payload = {
            InstanceId: instanceId,            
            StudyDescription: document.getElementById('studyDescription').value,
            ClinicalHistory: document.getElementById('clinicalHistory').value,
            Findings: document.getElementById('findings').value,
            Impression: document.getElementById('impression').value,
            OtherNote: document.getElementById('otherNote').value,
            AiSuggestion: typeof currentAiResult !== 'undefined' ? currentAiResult : "", 
            ShouldFinalize: isFinal            // Changed from isFinalized
    };

    if (isFinal && !confirm("Finalize Report? This action is permanent.")) return;

    try {
        const res = await fetch("http://localhost:5266/api/Radiologist/UpdateReport", {
            method: "POST",
            headers: getAuthHeaders(),
            body: JSON.stringify(payload)
        });
        
        if (await handleAuthError(res)) return;

        if (res.ok) {
            alert(isFinal ? "Report Finalized!" : "Draft Saved.");
            window.location.reload(); 
        }
    } catch (e) {
        alert("Error: Could not save to database.");
    }
}

let fullReportData = null; 

async function fetchExistingReport() {
    try {
        const res = await fetch(`http://localhost:5266/api/Radiologist/GetPrintingReport?instanceId=${instanceId}`, {
            headers: getAuthHeaders() // <--- Uses the token
        });

        if (await handleAuthError(res)) return; 

        if (!res.ok) return;

        fullReportData = await res.json(); // Store all 22 fields

        // 1. Fill the Sidebar UI
        document.getElementById('studyDescription').value = fullReportData.studyDescription || "";
        document.getElementById('clinicalHistory').value = fullReportData.clinicalHistory || "";
        document.getElementById('findings').value = fullReportData.findings || "";
        document.getElementById('impression').value = fullReportData.impression || "";
        document.getElementById('otherNote').value = fullReportData.otherNote || "";

        // --- THE FIX: FILL THE AI INSIGHTS BLOCK ---
        const aiDisplay = document.getElementById('aiResults');
        const aiHidden = document.getElementById('aiSuggestion');
        
        if (fullReportData.aiSuggestion) {
            // Fill the visual div with the saved string
            aiDisplay.innerHTML = `<div style="color:var(--accent); font-size:12px; line-height:1.4;">
                ${fullReportData.aiSuggestion}
            </div>`;
            // Keep the hidden field in sync for re-saving
            if(aiHidden) aiHidden.value = fullReportData.aiSuggestion;
            // Set the global variable so submitReport doesn't wipe it out
            currentAiResult = fullReportData.aiSuggestion; 
        }
        
        // 2. Lock UI if finalized
        if (fullReportData.isFinalized) {
            // Disable all textareas and inputs
            document.querySelectorAll('.sidebar textarea, .sidebar input').forEach(el => {
                el.disabled = true;
                el.style.opacity = "0.7";
                el.style.cursor = "not-allowed";
            });

            // Update the Status Badge (Ensures user knows why it's locked)
            const badge = document.getElementById('statusBadge');
            if (badge) {
                badge.innerText = "FINALIZED";
                badge.style.color = "#10b981"; // Success Green
            }

            // --- THE FIX: HIDE THE ACTION BUTTONS ---
            // Target the div containing Save Draft and Finalize
            const actionContainer = document.querySelector('.sidebar div[style*="margin-top: auto"]');
            if (actionContainer) {
                actionContainer.style.display = 'none'; 
            }

            // --- ADD THE PRINT BUTTON ---
            // Check to prevent multiple print buttons on refresh
            if (!document.getElementById('btnPrintReport')) {
                const printBtn = document.createElement('button');
                printBtn.id = "btnPrintReport";
                printBtn.className = "btn";
                printBtn.style.width = "100%";
                printBtn.style.background = "var(--accent)";
                printBtn.style.marginTop = "10px";
                printBtn.innerHTML = '<i data-lucide="printer"></i> Print / Export PDF';
                
                // Append it to the bottom of the sidebar
                document.querySelector('.sidebar').appendChild(printBtn);
                
                printBtn.onclick = preparePrint;
                
                // Refresh Lucide icons so the printer icon shows up
                if (typeof lucide !== 'undefined') lucide.createIcons();
            }
        }
    } catch (e) { console.error("Data fetch failed", e); }
}

function preparePrint() {
    if (!fullReportData) return;

    // Map all fields to the Print Template
    const fields = [
        "PatientName", "PatientIdentifier", "PatientDOB", "PatientSex", 
        "AccessionNumber", "Modality", "StudyDescription", "ClinicalHistory", 
        "Findings", "Impression", "AiSuggestion", "OtherNote", 
        "ReportIdentifier", "GeneratedBy", "GeneratedAt", "FinalizedBy", "FinalizedAt", "CreatedAt"
    ];

    fields.forEach(field => {
        const el = document.getElementById(`p-${field}`);
        const val = fullReportData[field.charAt(0).toLowerCase() + field.slice(1)]; // Handle camelCase
        if (el) el.innerText = val || "N/A";
    });

    window.print();
}

fetchExistingReport();