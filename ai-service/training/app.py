from fastapi import FastAPI, UploadFile, File, HTTPException
from PIL import Image
import io

from model import predict_pil

app = FastAPI(
    title="Chest X-ray Disease Predictor",
    version="1.0"
)

@app.get("/")
def health():
    return {"status": "ok", "message": "Chest X-ray API running"}

@app.post("/predict")
async def predict_xray(
    file: UploadFile = File(...),
    threshold: float = 0.5
):
    if file.content_type not in ["image/png", "image/jpeg"]:
        raise HTTPException(status_code=400, detail="Invalid image type")

    try:
        contents = await file.read()
        image = Image.open(io.BytesIO(contents))
    except Exception:
        raise HTTPException(status_code=400, detail="Invalid image file")

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
