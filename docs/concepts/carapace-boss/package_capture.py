"""Encode GIF previews from actual Unity camera frames; requires Pillow.

Usage: python package_capture.py <validation-project>/CarapaceCapture
The MP4 is encoded by UnityEditor.Media.MediaEncoder, not by this script.
"""
import csv
from pathlib import Path
import shutil
import sys
from PIL import Image

source = Path(sys.argv[1])
output = Path(sys.argv[2]) if len(sys.argv) > 2 else Path(__file__).parent / "applied"
output.mkdir(parents=True, exist_ok=True)
rows = list(csv.DictReader((source / "frames.csv").open(encoding="utf-8")))
for skill in ("Charge", "Slam", "Volley"):
    selected = [row for row in rows if row["skill"] == skill][::2]
    frames = []
    for row in selected:
        with Image.open(source / "frames" / f'{int(row["frame"]):04d}.png') as image:
            frames.append(image.convert("RGB").resize((960, 540), Image.Resampling.NEAREST))
    frames[0].save(output / f"{skill}.gif", save_all=True, append_images=frames[1:], duration=67, loop=0)
    print(skill, len(frames), "frames")
shutil.copy2(source / "CarapaceDemo.mp4", output / "CarapaceDemo.mp4")
shutil.copy2(source / "frames.csv", output / "frames.csv")
print(output)
