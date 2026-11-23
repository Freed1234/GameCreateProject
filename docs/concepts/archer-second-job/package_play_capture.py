"""Package camera frames captured by SecondArcherCheck.Run (Pillow required)."""
from pathlib import Path
import sys
from PIL import Image, ImageDraw

source = Path(sys.argv[1])
output = Path(__file__).parent / 'applied'
output.mkdir(exist_ok=True)
clips = {}
for key in ('Z', 'X', 'C', 'Shift'):
    frames = [Image.open(p).convert('RGB') for p in sorted((source / key).glob('*.png'))[::2]]
    frames[0].save(output / f'{key}.gif', save_all=True, append_images=frames[1:], duration=67, loop=0)
    clips[key] = frames
overview = []
for i in range(max(map(len, clips.values()))):
    canvas = Image.new('RGB', (960, 600), '#151c22')
    draw = ImageDraw.Draw(canvas)
    for n, (key, frames) in enumerate(clips.items()):
        x, y = (n % 2) * 480, (n // 2) * 300
        draw.text((x + 12, y + 8), {'Z':'Z-A / aimed feather barrage', 'X':'X-B / leaf vortex', 'C':'C-R5 / compressed wind', 'Shift':'Shift-R1 / feather dash'}[key], fill='#b4ffe0')
        canvas.paste(frames[min(i, len(frames)-1)].resize((480, 270), Image.Resampling.NEAREST), (x, y + 30))
    overview.append(canvas)
overview[0].save(output / 'unity-play-overview.gif', save_all=True, append_images=overview[1:], duration=67, loop=0)
print(output)
