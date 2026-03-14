from fastapi import FastAPI, UploadFile, File, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from PIL import Image
import io
import numpy as np
import pydicom

from model import predict_pil

app = FastAPI(
    title="Chest X-ray Disease Predictor",
    version="1.0"
)

# Enable CORS for your frontend
app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:5500"],  # your frontend origin
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

@app.get("/")
def health():
    return {"status": "ok", "message": "Chest X-ray API running"}

@app.post("/predict")
async def predict_xray(
    file: UploadFile = File(...),
    threshold: float = 0.5
):
    # Accept DICOM files too
    if file.content_type not in ["image/png", "image/jpeg", "application/dicom", "application/octet-stream"]:
        raise HTTPException(status_code=400, detail="Invalid image type")

    try:
        contents = await file.read()

        if file.filename.lower().endswith(".dcm") or "dicom" in file.content_type:
            # Read DICOM
            dicom_file = pydicom.dcmread(io.BytesIO(contents))
            pixel_array = dicom_file.pixel_array  # numpy array
            # Convert grayscale to PIL Image
            # Normalize to 0-255
            pixels = ((pixel_array - np.min(pixel_array)) / (np.max(pixel_array) - np.min(pixel_array)) * 255).astype(np.uint8)
            image = Image.fromarray(pixels)
        else:
            # Normal PNG/JPG
            image = Image.open(io.BytesIO(contents))

    except Exception as e:
        raise HTTPException(status_code=400, detail=f"Invalid image file: {e}")

    # Run prediction
    predictions = predict_pil(image, threshold)

    positives = {
        k: v for k, v in predictions.items() if v["positive"]
    }

    return {
        "filename": file.filename,
        "threshold": threshold,
        "positive_findings": positives,
        "all_predictions": predictions
    }