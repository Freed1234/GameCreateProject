"""Export the selected code-drawn pixel effects, without changing concept GIFs."""
import sys
sys.dont_write_bytecode=True
from pathlib import Path
import importlib.util
from PIL import Image, ImageDraw
ROOT=Path(__file__).resolve().parents[3]
def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path); result=importlib.util.module_from_spec(spec);spec.loader.exec_module(result);return result
g=module('selected_graphics',Path(__file__).parent/'graphics/generate.py')
r=module('selected_revision',Path(__file__).parent/'graphics/revision-2/generate.py')
out=ROOT/'Assets/Resources/SecondArcher'
for key in ['Z','X','C','Shift','Hit']:
    folder=out/key;folder.mkdir(parents=True,exist_ok=True)
    for i in range(16):
        im=Image.new('RGBA',(192,128));d=ImageDraw.Draw(im);t=i/16
        if key=='Z':g.arrow(d,(144,64),(1,0),0,t)
        if key=='X':g.vortex(d,(96,78),1,t,1)
        if key=='C':r.arrow(d,(144,64),(1,0),4,t,True)
        if key=='Shift':r.dash_fx(d,(96,64),0,t*.2)
        if key=='Hit':r.impact(d,(96,64),t*.24,0,True)
        im.save(folder/f'{i:02}.png')
    icon=Image.new('RGBA',(64,64));bounds=im.getbbox()
    cut=im.crop(bounds);cut.thumbnail((56,56),Image.Resampling.NEAREST)
    icon.paste(cut,((64-cut.width)//2,(64-cut.height)//2),cut)
    icon.save(out/f'{key}Icon.png')
im=Image.new('RGBA',(40,40));d=ImageDraw.Draw(im)
for sx in [-1,1]:
    for sy in [-1,1]:d.line([(20+sx*16,20+sy*10),(20+sx*16,20+sy*16),(20+sx*10,20+sy*16)],fill='#f2cc75',width=2)
im.save(out/'Target.png')
print('Exported selected Z-A / X-B / C-R5 / Shift-R1 frames')
