// radiographer-portal.js

// -------------------------------
//  Dummy Images List
// -------------------------------
const dummyImages = [
    '/resources/dummy_image/test1.png',
    '/resources/dummy_image/test2.png',
    '/resources/dummy_image/test3.png',
    '/resources/dummy_image/test4.png'
];

let currentDicom = null;
let currentImageName = null;
let currentImagePath = null;


// -------------------------------
//  Fill Dummy Patient Data
// -------------------------------
function fillDummyData() {
    document.getElementById('patientId').value = `PAT-${Math.floor(Math.random() * 90000 + 10000)}`;
    document.getElementById('accessionNo').value = `ACC-${Math.floor(Math.random() * 90000 + 10000)}`;
    document.getElementById('patientName').value = 'John Doe';
    document.getElementById('patientDOB').value = '1990-01-01';
    document.getElementById('patientSex').value = 'M';
    document.getElementById('studyType').value = 'CR';
}


// -------------------------------
//  Image Preview Helper
// -------------------------------
function updatePreview(path) {
    const img = document.getElementById('preview');
    if (!img) return console.error("Preview element missing!");

    if (!path) {
        img.style.display = 'none';
        img.src = "";
        return;
    }

    img.src = path;
    img.style.display = 'block';
}


// -------------------------------
//  Handle Manual Image Upload
// -------------------------------
const fileInput = document.getElementById("fileInput");

fileInput.addEventListener("change", (event) => {
    const file = event.target.files[0];
    if (!file) return;

    currentImageName = file.name;

    const reader = new FileReader();
    reader.onload = (e) => updatePreview(e.target.result);
    reader.readAsDataURL(file);

    alert(`Image "${file.name}" loaded!`);
});


// -------------------------------
//  Simulate X-ray Acquisition
// -------------------------------
document.getElementById('acquireBtn').addEventListener('click', () => {
    const randomIndex = Math.floor(Math.random() * dummyImages.length);
    currentImagePath = dummyImages[randomIndex];
    currentImageName = currentImagePath;

    updatePreview(currentImagePath);
    alert("Simulated X-Ray acquired!");
});


// -------------------------------
//  Generate Simulated DICOM
// -------------------------------
document.getElementById('uploadDicomBtn').addEventListener('click', () => {
    if (!currentImageName) {
        alert("Please acquire or upload an image first!");
        return;
    }

    const patientData = {
        PatientID: document.getElementById('patientId').value,
        AccessionNumber: document.getElementById('accessionNo').value || `TEMP-${Date.now()}`,
        PatientName: document.getElementById('patientName').value,
        PatientDOB: document.getElementById('patientDOB').value,
        PatientSex: document.getElementById('patientSex').value,
        StudyType: document.getElementById('studyType').value,
        Image: currentImageName,
        Timestamp: new Date().toISOString()
    };

    currentDicom = JSON.stringify(patientData, null, 2);
    console.log("Generated simulated DICOM:", currentDicom);

    alert("DICOM generated (simulated)!");
});


// -------------------------------
//  Download Simulated DICOM
// -------------------------------
document.getElementById('downloadDicomBtn').addEventListener('click', () => {
    if (!currentDicom) {
        alert("No DICOM available for download!");
        return;
    }

    const blob = new Blob([currentDicom], { type: 'application/json' });
    const url = URL.createObjectURL(blob);

    const a = document.createElement('a');
    a.href = url;
    a.download = 'simulated_dicom.json';
    a.click();

    URL.revokeObjectURL(url);
});


// -------------------------------
//  Clear Everything
// -------------------------------
document.getElementById('clearFieldsBtn').addEventListener('click', () => {
    document.getElementById('patientForm').reset();
    document.getElementById('fileInput').value = "";
    currentDicom = null;
    currentImageName = null;
    currentImagePath = null;

    updatePreview(null);

    alert("Form and image preview cleared!");
});


// -------------------------------
//  Fill Dummy Button
// -------------------------------
document.getElementById('fillDummyBtn').addEventListener('click', () => {
    fillDummyData();
    alert("Dummy patient info filled!");
});