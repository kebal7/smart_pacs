import torch
import torch.nn as nn
import torch.nn.functional as F
from torchvision import models, transforms
from PIL import Image
import numpy as np
import cv2

device = torch.device("cuda" if torch.cuda.is_available() else "cpu")

all_labels = [
    'Atelectasis','Cardiomegaly','Effusion','Infiltration','Mass',
    'Nodule','Pneumonia','Pneumothorax','Consolidation','Edema',
    'Emphysema','Fibrosis','Pleural_Thickening','Hernia'
]

# Load model
model = models.densenet121(weights=None)
model.classifier = nn.Linear(model.classifier.in_features, 14)
model.load_state_dict(torch.load("densenet121_chestxray14.pth", map_location=device), strict=True)
model.to(device)
model.eval()

transform = transforms.Compose([
    transforms.Resize((224, 224)),
    transforms.Grayscale(3),
    transforms.ToTensor(),
    transforms.Normalize([0.485,0.456,0.406],[0.229,0.224,0.225])
])

# ─────────────────────────────────────────────
class NotXrayError(ValueError):
    pass


def _check_is_xray(image):
    w, h = image.size
    ratio = w / h
    if not (0.5 <= ratio <= 2.0):
        raise NotXrayError("Invalid aspect ratio")

    hsv = cv2.cvtColor(np.array(image.convert("RGB")), cv2.COLOR_RGB2HSV)
    if hsv[:, :, 1].mean() > 35:
        raise NotXrayError("Too colorful to be X-ray")


def _check_confidence(probs):
    if probs.max() < 0.15:
        raise NotXrayError("Low confidence → not X-ray")


# ─────────────────────────────────────────────
@torch.no_grad()
def predict_pil(image, threshold=0.5):
    _check_is_xray(image)

    tensor = transform(image.convert("L")).unsqueeze(0).to(device)
    logits = model(tensor)
    probs = torch.sigmoid(logits).cpu().squeeze()

    _check_confidence(probs)

    return {
        label: {
            "probability": float(prob),
            "positive": bool(prob >= threshold)
        }
        for label, prob in zip(all_labels, probs)
    }


# ─────────────────────────────────────────────
def generate_gradcam(image, class_index=None):
    _check_is_xray(image)

    tensor = transform(image.convert("L")).unsqueeze(0).to(device)
    tensor.requires_grad_(True)

    activations = []
    gradients = []

    def f_hook(m, i, o): activations.append(o)
    def b_hook(m, gi, go): gradients.append(go[0])

    layer = model.features.denseblock4
    fh = layer.register_forward_hook(f_hook)
    bh = layer.register_full_backward_hook(b_hook)

    try:
        logits = model(tensor)
        probs = torch.sigmoid(logits).cpu().squeeze()

        _check_confidence(probs)

        if class_index is None:
            class_index = int(probs.argmax())

        model.zero_grad()
        logits[0, class_index].backward()

        grads = gradients[0]
        acts = activations[0]

        weights = grads.mean(dim=(2,3), keepdim=True)
        # cam = (weights * acts).sum(dim=1).squeeze().cpu().numpy()
        cam = (weights * acts).sum(dim=1).squeeze().detach().cpu().numpy()

        cam = np.maximum(cam, 0)
        cam = cam / cam.max()

        cam = cv2.resize(cam, (224,224))
        heatmap = cv2.applyColorMap(np.uint8(255*cam), cv2.COLORMAP_JET)

        orig = np.array(image.convert("RGB").resize((224,224)))
        overlay = (0.6*heatmap + 0.4*orig).astype(np.uint8)

        return overlay, class_index, float(probs[class_index])

    finally:
        fh.remove()
        bh.remove()