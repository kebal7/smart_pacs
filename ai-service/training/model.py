import torch
import torch.nn as nn
from torchvision import models, transforms
from PIL import Image

device = torch.device("cuda" if torch.cuda.is_available() else "cpu")

all_labels = ['Atelectasis','Cardiomegaly','Effusion','Infiltration','Mass',
              'Nodule','Pneumonia','Pneumothorax','Consolidation','Edema',
              'Emphysema','Fibrosis','Pleural_Thickening','Hernia']

# Load model
model = models.densenet121(weights=None)
model.classifier = nn.Linear(model.classifier.in_features, 14)
model.load_state_dict(torch.load("densenet121_chestxray14.pth", map_location=device))
model.to(device)
model.eval()

# Transform (same as training)
transform = transforms.Compose([
    transforms.Resize((224, 224)),
    transforms.Grayscale(num_output_channels=3),
    transforms.ToTensor(),
    transforms.Normalize(
        mean=[0.485,0.456,0.406],
        std=[0.229,0.224,0.225]
    )
])

@torch.no_grad()
def predict_pil(image: Image.Image, threshold=0.5):
    image = image.convert("L")
    image = transform(image).unsqueeze(0).to(device)

    logits = model(image)
    probs = torch.sigmoid(logits).cpu().squeeze(0)

    results = {}
    for label, prob in zip(all_labels, probs):
        results[label] = {
            "probability": round(float(prob), 4),
            "positive": bool(prob >= threshold)
        }

    return results
