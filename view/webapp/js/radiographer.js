/**
 * SmartPACS - Radiographer Portal Logic
 * Handles: Auto-ID generation, Patient Lookup, Image Acquisition, 
 * DICOM Conversion, and Report Database Integration.
 */

// -------------------------------
//  State Management
// -------------------------------
const dummyImages = [
    '../resources/dummy_image/test1.png',
    '../resources/dummy_image/test2.png',
    '../resources/dummy_image/test3.png',
    '../resources/dummy_image/test4.png'
];

let currentImageFile = null;
// We will now use the hidden input #studyId instead of a loose global variable 
// to ensure it stays in sync with the form.

// -------------------------------
//  Initialization
// -------------------------------
document.addEventListener('DOMContentLoaded', () => {
    generateNewStudyIds();
    setupListeners();
});

function setupListeners() {
    document.getElementById('patientId').addEventListener('blur', lookupPatient);
    document.getElementById('fileInput').addEventListener('change', handleManualUpload);
    document.getElementById('acquireBtn').addEventListener('click', simulateAcquisition);
    document.getElementById('uploadDicomBtn').addEventListener('click', uploadToPACS);
    document.getElementById('fillDummyBtn').addEventListener('click', fillDummyData);
    document.getElementById('clearFieldsBtn').addEventListener('click', clearEverything);
}

// -------------------------------
//  ID Generation & Patient Lookup
// -------------------------------

async function generateNewStudyIds() {
    try {
        const response = await fetch('http://localhost:5266/api/Radiographer/generate-ids', {
            headers: getAuthHeaders() // Pass JWT
        });

        if (await handleAuthError(response)) return;

        if (response.ok) {
            const data = await response.json();
            // REQUIRED: Update both visible Accession and hidden StudyID
            document.getElementById('accessionNo').value = data.accessionNo;
            document.getElementById('studyId').value = data.studyId; 
            
            console.log(`IDs Synced: Accession: ${data.accessionNo}, StudyID: ${data.studyId}`);
        }
    } catch (err) {
        console.error("Failed to generate IDs:", err);
    }
}

async function lookupPatient(event) {
    const patientId = event.target.value.trim();
    if (!patientId) return;

    try {
        const response = await fetch(`http://localhost:5266/api/Radiographer/patient-lookup/${patientId}`, {
            headers: getAuthHeaders() // Pass JWT
        });

        if (await handleAuthError(response)) return;

        if (response.ok) {
            const patient = await response.json();
            
            document.getElementById('patientName').value = patient.name;
            
            // REQUIRED FIX: Ensure the dropdown selects correctly
            // Maps "Male" to "M", "Female" to "F", etc.
            const sexValue = (patient.sex || "O").charAt(0).toUpperCase();
            document.getElementById('patientSex').value = sexValue;
            
            if (patient.dateOfBirth) {
                document.getElementById('patientDOB').value = patient.dateOfBirth.split('T')[0];
            }
            
            console.log("Patient record found and loaded.");
        } else {
            console.warn("Patient not found in database. Proceeding as new/emergency.");
        }
    } catch (err) {
        console.error("Error during patient lookup:", err);
    }
}

// -------------------------------
//  Image Handling
// -------------------------------

function updatePreview(source) {
    const img = document.getElementById('preview');
    if (!img) return;

    if (!source) {
        img.src = "https://via.placeholder.com/300x300?text=No+Image+Loaded";
        return;
    }
    img.src = source;
}

function handleManualUpload(event) {
    const file = event.target.files[0];
    if (!file) return;

    currentImageFile = file;
    const reader = new FileReader();
    reader.onload = (e) => updatePreview(e.target.result);
    reader.readAsDataURL(file);
}

async function simulateAcquisition() {
    const randomIndex = Math.floor(Math.random() * dummyImages.length);
    const path = dummyImages[randomIndex];

    try {
        const response = await fetch(path);
        const blob = await response.blob();
        
        currentImageFile = new File([blob], path.split("/").pop(), { type: blob.type });
        
        updatePreview(URL.createObjectURL(blob));
        alert("Simulated X-Ray acquisition complete!");
    } catch (err) {
        console.error("Acquisition simulation failed:", err);
    }
}

// -------------------------------
//  PACS Upload & Database Integration
// -------------------------------

async function uploadToPACS() {
    // Check both manual upload and simulated acquisition
    const fileToUpload = currentImageFile || document.getElementById('fileInput').files[0];

    if (!fileToUpload) {
        alert("Please acquire or upload an image first.");
        return;
    }

    const patientId = document.getElementById('patientId').value;
    if (!patientId) {
        alert("Patient ID is required.");
        return;
    }

    const formData = new FormData();
    formData.append("PatientID", patientId);
    formData.append("PatientName", document.getElementById('patientName').value);
    formData.append("PatientDOB", document.getElementById('patientDOB').value);
    formData.append("PatientSex", document.getElementById('patientSex').value);
    formData.append("StudyType", document.getElementById('studyType').value);
    formData.append("AccessionNo", document.getElementById('accessionNo').value);
    // REQUIRED: Get StudyID from the hidden field
    formData.append("StudyID", document.getElementById('studyId').value);
    formData.append("ImageFile", fileToUpload);

    try {
        const response = await fetch("http://localhost:5266/api/Radiographer/upload-to-pacs", {
            method: "POST",
            headers: getAuthHeaders(), // Pass JWT
            body: formData
        });

        if (await handleAuthError(response)) return;

        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(errorText);
        }

        const result = await response.json();
        alert(`Success!\nReport: ${result.reportId}\nInstance: ${result.instanceId}`);
        
        // REQUIRED: Reset and generate FRESH IDs for the next patient
        clearEverything();
        generateNewStudyIds();

    } catch (err) {
        console.error("Upload Error:", err);
        alert("Failed to upload DICOM to PACS: " + err.message);
    }
}

// -------------------------------
//  Helpers & UI
// -------------------------------

function fillDummyData() {
    document.getElementById('patientId').value = `PAT-${Math.floor(Math.random() * 90000 + 10000)}`;
    document.getElementById('patientName').value = 'Test Patient';
    document.getElementById('patientDOB').value = '1985-05-20';
    document.getElementById('patientSex').value = 'M';
    document.getElementById('studyType').value = 'CR';
}

function clearEverything() {
    document.getElementById('patientForm').reset();
    document.getElementById('fileInput').value = "";
    currentImageFile = null;
    updatePreview(null);
    // Refresh IDs so the next patient doesn't get the cleared one
    generateNewStudyIds();
}

function logout() {
    localStorage.removeItem('jwtToken');
    window.location.href = "./login.html";
}

// Immediate redirect if no token
if (!localStorage.getItem('jwtToken')) logout();

const getAuthHeaders = () => ({
    "Authorization": `Bearer ${localStorage.getItem('jwtToken')}`
});

/**
 * Checks if the response is 401/403 and shows the security modal
 */
async function handleAuthError(res) {
    if (res.status === 401 || res.status === 403) {
        document.getElementById('authErrorModal').style.display = 'flex';
        document.getElementById('mainContent').style.filter = 'blur(8px)';
        return true;
    }
    return false;
}