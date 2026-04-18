from fastapi import FastAPI, UploadFile, File, HTTPException, Query, Header, Depends
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import StreamingResponse
from PIL import Image
import io, os, json, traceback
import numpy as np
import pydicom
import httpx
from dotenv import load_dotenv

from model import predict_pil, generate_gradcam, all_labels, NotXrayError

load_dotenv()

INTERNAL_AUTH_KEY = os.getenv("INTERNAL_AUTH_KEY", "change-this-in-production")

# 1. RESTRICT CORS
# Only allow NET backend and your local dev environment
app = FastAPI(title="Chest X-ray AI", version="2.0")

app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:5266"],
    allow_credentials=True,
    allow_methods=["POST"],
    allow_headers=["*"],
)

# 2. THE SECURITY CHECK FUNCTION
def verify_internal_key(x_internal_key: str = Header(None)):
    """
    FastAPI will look for a Header called 'X-Internal-Key'.
    If it doesn't match our secret, it returns 403 Forbidden.
    """
    if x_internal_key != INTERNAL_AUTH_KEY:
        raise HTTPException(status_code=403, detail="Forbidden: Internal Access Only")
    return True

GROQ_API_KEY = os.getenv("GROQ_API_KEY")
GROQ_MODEL = os.getenv("GROQ_MODEL", "llama-3.1-8b-instant")


# ─────────────────────────────────────────────
# Utils
# ─────────────────────────────────────────────
def load_image(file_bytes: bytes, filename: str) -> Image.Image:
    if filename.lower().endswith(".dcm"):
        dicom_file = pydicom.dcmread(io.BytesIO(file_bytes))
        pixel_array = dicom_file.pixel_array

        pixels = ((pixel_array - np.min(pixel_array)) /
                  (np.max(pixel_array) - np.min(pixel_array)) * 255).astype(np.uint8)

        return Image.fromarray(pixels)

    return Image.open(io.BytesIO(file_bytes))


def load_image_rgb(file_bytes: bytes, filename: str) -> Image.Image:
    if filename.lower().endswith(".dcm"):
        dicom_file = pydicom.dcmread(io.BytesIO(file_bytes))
        pixel_array = dicom_file.pixel_array

        # Normalize to 0-255
        pixels = ((pixel_array - np.min(pixel_array)) /
                  (np.max(pixel_array) - np.min(pixel_array)) * 255).astype(np.uint8)

        img = Image.fromarray(pixels)

        # ✅ Ensure RGB
        if img.mode != "RGB":
            img = img.convert("RGB")

        return img

    img = Image.open(io.BytesIO(file_bytes))

    if img.mode != "RGB":
        img = img.convert("RGB")
    return img

# ─────────────────────────────────────────────
@app.get("/")
def health():
    return {"status": "ok"}


# ─────────────────────────────────────────────
@app.post("/predict",  dependencies=[Depends(verify_internal_key)])
async def predict_xray(
    file: UploadFile = File(...),
    threshold: float = 0.2
):
    try:
        contents = await file.read()
        image = load_image(contents, file.filename)

        predictions = predict_pil(image, threshold)

    except NotXrayError as e:
        raise HTTPException(status_code=422, detail=str(e))
    except Exception as e:
        raise HTTPException(status_code=400, detail=f"Invalid file: {e}")

    positives = {k: v for k, v in predictions.items() if v["positive"]}

    return {
        "filename": file.filename,
        "positive_findings": positives,
        "all_predictions": predictions
    }


# ─────────────────────────────────────────────
@app.post("/gradcam",  dependencies=[Depends(verify_internal_key)])
async def gradcam_endpoint(
    file: UploadFile = File(...),
    class_index: int = Query(None)
):
    try:
        contents = await file.read()
        image = load_image_rgb(contents, file.filename)
        print("Image mode:", image.mode, "size:", image.size)
        overlay, used_idx, prob = generate_gradcam(image, class_index)

    except NotXrayError as e:
        raise HTTPException(status_code=422, detail=str(e))
    except Exception as e:
        print(traceback.format_exc())
        raise HTTPException(status_code=400, detail=str(e))


    img = Image.fromarray(overlay)
    buf = io.BytesIO()
    img.save(buf, format="PNG")
    buf.seek(0)

    return StreamingResponse(
        buf,
        media_type="image/png",
        headers={
            "X-Class": all_labels[used_idx],
            "X-Probability": str(prob)
        }
    )


# ─────────────────────────────────────────────
@app.post("/icd10",  dependencies=[Depends(verify_internal_key)])
async def icd10_mapping(body: dict):
    diseases = body.get("diseases", [])

    if not diseases:
        raise HTTPException(status_code=400, detail="No diseases provided")

    if not GROQ_API_KEY:
        raise HTTPException(status_code=500, detail="Missing GROQ_API_KEY")

    prompt = f"""
You are a medical coding expert.

Map the following findings to ICD-10-CM:

{", ".join(diseases)}

Return ONLY JSON:
[
  {{
    "disease": "",
    "icd10_code": "",
    "icd10_description": "",
    "clinical_note": ""
  }}
]
"""

    try:
        async with httpx.AsyncClient(timeout=30) as client:
            resp = await client.post(
                "https://api.groq.com/openai/v1/chat/completions",
                headers={
                    "Authorization": f"Bearer {GROQ_API_KEY}",
                    "Content-Type": "application/json"
                },
                json={
                    "model": GROQ_MODEL,
                    "messages": [{"role": "user", "content": prompt}],
                    "temperature": 0.1
                }
            )

        data = resp.json()
        content = data["choices"][0]["message"]["content"].strip()

        # Clean markdown if exists
        if content.startswith("```"):
            content = content.split("\n", 1)[1].rsplit("```", 1)[0]

        mappings = json.loads(content)

        return {"icd10_mappings": mappings}

    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))


# ─────────────────────────────────────────────
if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8001)