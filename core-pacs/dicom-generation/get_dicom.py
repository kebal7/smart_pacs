import os
import numpy as np
from PIL import Image
import pydicom
from pydicom.dataset import Dataset, FileMetaDataset
from pydicom.uid import generate_uid, ExplicitVRLittleEndian

def create_dicom_from_image(
    img_path,
    patient_name,
    patient_id,
    birthdate,
    sex,
    output_dir,
    body_part="CHEST",
    view_position="PA",
    modality="CR"
):
    """
    Converts an image (JPG/PNG) to a fully Orthanc-compatible CR DICOM.

    Parameters:
        img_path (str): Path to input image.
        patient_name (str): Patient's name.
        patient_id (str): Patient ID.
        birthdate (str): Patient birthdate in YYYYMMDD.
        sex (str): 'M', 'F', or 'O'.
        output_dir (str): Directory to save generated DICOM.
        body_part (str): Body part examined. Default 'CHEST'.
        view_position (str): Image view position. Default 'PA'.
        modality (str): Modality code. Default 'CR'.
    """
    os.makedirs(output_dir, exist_ok=True)

    # Load image and convert to 16-bit grayscale
    img = Image.open(img_path).convert("L")
    arr = np.array(img, dtype=np.uint16)
    arr_16 = arr * 257  # scale 0-255 -> 0-65535

    rows, cols = arr_16.shape

    # File meta information
    file_meta = FileMetaDataset()
    file_meta.MediaStorageSOPClassUID = "1.2.840.10008.5.1.4.1.1.1"  # CR Image Storage
    file_meta.MediaStorageSOPInstanceUID = generate_uid()
    file_meta.TransferSyntaxUID = ExplicitVRLittleEndian
    file_meta.ImplementationClassUID = generate_uid()

    # Main dataset
    ds = Dataset()
    ds.file_meta = file_meta
    ds.is_little_endian = True
    ds.is_implicit_VR = False

    # Patient information
    ds.PatientName = patient_name
    ds.PatientID = patient_id
    ds.PatientBirthDate = birthdate
    ds.PatientSex = sex

    # Study and Series information
    ds.StudyInstanceUID = generate_uid()
    ds.SeriesInstanceUID = generate_uid()
    ds.SOPInstanceUID = file_meta.MediaStorageSOPInstanceUID
    ds.SOPClassUID = file_meta.MediaStorageSOPClassUID
    ds.Modality = modality
    ds.StudyDate = ds.SeriesDate = ds.ContentDate = "20251128"
    ds.StudyTime = ds.SeriesTime = ds.ContentTime = "120000"
    ds.AccessionNumber = generate_uid()[:16]  # unique accession number

    # Image pixel data
    ds.Rows = rows
    ds.Columns = cols
    ds.SamplesPerPixel = 1
    ds.PhotometricInterpretation = "MONOCHROME2"
    ds.BitsAllocated = 16
    ds.BitsStored = 16
    ds.HighBit = 15
    ds.PixelRepresentation = 0
    ds.PixelData = arr_16.tobytes()
    ds.PixelSpacing = ["0.168", "0.168"]
    ds.ImagerPixelSpacing = ["0.168", "0.168"]

    # Mandatory CR/DX attributes for Orthanc
    ds.BodyPartExamined = body_part
    ds.ViewPosition = view_position
    ds.KVP = "120"
    ds.ExposureTime = "10"
    ds.Exposure = "5"

    # Optional: imaging device info
    ds.Manufacturer = "PythonDICOM"
    ds.ManufacturerModelName = "Synthetic"
    ds.StationName = "PYTHON_CR"

    # Save DICOM
    filename = f"{patient_id}_{generate_uid()[:8]}.dcm"
    save_path = os.path.join(output_dir, filename)
    ds.save_as(save_path, write_like_original=False)

    print("Saved DICOM:", save_path)
    print(f"Rows: {ds.Rows}, Columns: {ds.Columns}, BitsAllocated: {ds.BitsAllocated}")
    print(f"SOPClassUID: {ds.SOPClassUID}, Modality: {ds.Modality}")

# Example usage
create_dicom_from_image(
    "chest-x-ray-image.jpg",
    "Prashant Rijal",
    "PRC986",
    "19320805",
    "M",
    "output-dicoms"
)
