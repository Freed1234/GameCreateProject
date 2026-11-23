"""Graphic variants over shared motion data. Pillow; run from any directory."""
import sys
sys.dont_write_bytecode = True
from pathlib import Path
import importlib.util
import math
import json
import hashlib
from PIL import Image, ImageDraw, ImageOps

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[3]
spec = importlib.util.spec_from_file_location('motion', OUT.parent/'generate.py')
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)
NAMES = {
 'Z': ['깃바람 화살','나선 송곳 화살','제비날개 화살'],
 'X': ['겹바람 소용돌이','나뭇잎 회오리','나선 바람 원반'],
 'C': ['거대 깃창 화살','삼중 풍압 화살','날개형 폭풍 화살'],
 'Shift': ['이중 쐐기','깃날개 쐐기','풍압 방패'],
}
SUB = {'Z':'Z2 · 순차 포착 연사', 'X':'X1 · 고정 중심 흡인',
       'C':'C1 · 단일 대형 관통 화살', 'Shift':'Shift1 · 경로 타격 돌진'}
MINT, WHITE, DARK = '#75e5c2','#e5fff2','#173e38'

def sprite(path, height):
    im=Image.open(ROOT/path).convert('RGBA')
    im=im.crop(im.getbbox())
    return im.resize((round(im.width*height/im.height),height),Image.Resampling.NEAREST)

ACTOR={a:[sprite(Path(f'Assets/Textures/CharacterMotion/Frames/right-{a}-{i}.png'),40)
           for i in range(n)] for a,n in [('idle',2),('attack',3),('walk',4)]}
ENEMY=[sprite(Path(f'Assets/Art/Enemies/RandomAppearance/Slime/yellow/walk_{i:02}.png'),24)
       for i in range(1,17)]

def paste(im,src,p,flash=False):
    if flash:
        src=src.copy(); white=Image.new('RGBA',src.size,(225,255,245,255))
        white.putalpha(src.getchannel('A')); src=Image.blend(src,white,.65)
    im.paste(src,(round(p[0]-src.width/2),round(p[1]-src.height+10)),src)

def poly(d,points,fill): d.polygon(points,fill=fill)
def local(p,di,coords):
    return [(p[0]+di[0]*x-di[1]*y,p[1]+di[1]*x+di[0]*y) for x,y in coords]
def shape(d,p,di,coords,fill): poly(d,local(p,di,coords),fill)
def stroke(d,p,di,coords,fill,width=1): d.line(local(p,di,coords),fill=fill,width=width)

def arrow(d,p,di,v,t,large=False):
    length=62 if large else 25
    width=13 if large else 4
    # All tip designs have the same maximum width; ornament stays inside that envelope.
    stroke(d,p,di,[(-length-17,0),(-length,0)],'#4a9c83',3 if large else 1)
    stroke(d,p,di,[(-length,0),(-5,0)],DARK,7 if large else 3)
    stroke(d,p,di,[(-length,0),(-5,0)],MINT,3 if large else 1)
    if v==0:
        shape(d,p,di,[(0,0),(-18 if large else -9,-width),(-12 if large else -6,0),(-18 if large else -9,width)],DARK)
        shape(d,p,di,[(-1,0),(-16 if large else -7,-width+1),(-10 if large else -4,0),(-16 if large else -7,width-1)],WHITE)
        for sign in [-1,1]:
            shape(d,p,di,[(-length+12,0),(-length-2,sign*width*.8),(-length+1,0)],MINT)
            stroke(d,p,di,[(-length+9,sign*2),(-length,sign*width*.65)],WHITE)
    elif v==1:
        if large:
            for offset in [27,14,0]:
                stroke(d,p,di,[(-offset-14,-width),(-offset,0),(-offset-14,width)],DARK,5)
                stroke(d,p,di,[(-offset-14,-width+1),(-offset,0),(-offset-14,width-1)],WHITE if offset==0 else MINT,3)
        else:
            shape(d,p,di,[(0,0),(-11,-3),(-8,0),(-11,3)],WHITE)
            pts=[(-j,math.sin(j*.65-t*15)*3) for j in range(7,30)]
            stroke(d,p,di,pts,MINT,1)
    else:
        shape(d,p,di,[(0,0),(-length*.67,-width),(-length*.42,-2),(-length,0),(-length*.42,2),(-length*.67,width)],DARK)
        shape(d,p,di,[(-1,0),(-length*.6,-width+1),(-length*.37,-1),(-length*.8,0),(-length*.37,1),(-length*.6,width-1)],MINT)
        stroke(d,p,di,[(-length*.5,0),(-2,0)],WHITE,3 if large else 1)
        for sign in [-1,1]:
            stroke(d,p,di,[(-length-15,sign*width*.6),(-length*.7,sign*width*.6)],MINT,2 if large else 1)

def impact(d,p,age,v,large=False):
    if not 0<=age<.3: return
    r=4+age*(70 if large else 35)
    if v==0:
        for j in range(4):
            a=j*math.tau/4+.3; x,y=p[0]+math.cos(a)*r,p[1]+math.sin(a)*r
            poly(d,[(x-2,y),(x+2,y-2),(x+1,y+3)],WHITE)
    elif v==1:
        d.arc((p[0]-r,p[1]-r,p[0]+r,p[1]+r),age*900,age*900+245,fill=MINT,width=2)
    else:
        for sign in [-1,1]:
            x,y=p[0]+sign*r,p[1]-r*.4
            d.line([(x-sign*4,y-3),(x,y),(x-sign*4,y+3)],fill=WHITE,width=2)

def vortex(d,p,v,t,strength):
    r=57*strength
    if r<1: return
    m.ring(d,p,60,'#649c80',1)
    if v==0:
        for band in range(4):
            rr=r*(1-band*.17); cy=p[1]-band*7
            for half in range(2):
                phase=t*260+band*35+half*180
                box=(p[0]-rr,cy-rr*.28,p[0]+rr,cy+rr*.28)
                d.arc(box,phase,phase+130,fill=DARK,width=5)
                d.arc(box,phase,phase+130,fill=WHITE if band==3 else MINT,width=2)
    elif v==1:
        for band in range(3):
            rr=r*(1-band*.2); cy=p[1]-band*9
            d.arc((p[0]-rr,cy-rr*.3,p[0]+rr,cy+rr*.3),t*240+band*80,t*240+band*80+240,fill=MINT)
        for j in range(19):
            a=t*4+j*2.4; frac=(j*.17-t*.5)%1; rr=r*(.3+.8*frac)
            x,y=p[0]+math.cos(a)*rr,p[1]+math.sin(a)*rr*.4-18*(1-frac)
            shape(d,(x,y),(-math.sin(a),math.cos(a)),[(4,0),(0,-2),(-4,0),(0,2)],'#b7de7b')
            stroke(d,(x,y),(-math.sin(a),math.cos(a)),[(-3,0),(3,0)],'#4f845a')
    else:
        for arm in range(3):
            pts=[]
            for j in range(45):
                frac=j/44; a=frac*4.2+t*3+arm*math.tau/3
                rr=7+frac*(r-7)
                pts.append((p[0]+math.cos(a)*rr,p[1]+math.sin(a)*rr*.5))
            d.line(pts,fill=DARK,width=5); d.line(pts,fill=MINT,width=2)
        m.ring(d,(p[0],p[1]-3),9,WHITE,2)

def dash_fx(d,p,v,age):
    x,y=p
    for j in range(1,5):
        xx=x-j*14
        if xx>65: d.line([(xx-7,y+5),(xx,y+5)],fill='#65af96',width=2)
    if v==0:
        for off,col in [(5,MINT),(14,WHITE)]:
            d.line([(x+off-16,y-23),(x+off+3,y-5),(x+off-16,y+13)],fill=DARK,width=5)
            d.line([(x+off-16,y-23),(x+off+3,y-5),(x+off-16,y+13)],fill=col,width=2)
    elif v==1:
        for sign in [-1,1]:
            poly(d,[(x+19,y-5),(x-15,y-5+sign*20),(x-8,y-5+sign*8),(x-24,y-5+sign*11)],MINT)
            d.line([(x-15,y-5+sign*18),(x+19,y-5)],fill=WHITE,width=2)
    else:
        coords=[(x-7,y-26),(x+24,y-5),(x-7,y+16),(x+4,y-5),(x-7,y-26)]
        d.line(coords,fill=DARK,width=6); d.line(coords,fill=MINT,width=4)
        d.line(coords[:3],fill=WHITE,width=2)

def state(key,f):
    t=f/20; player=m.ORIGIN; aim=None; flashes=set(); impacts=[]; bullets=[]
    if key=='Z':
        pos,h,bullets,target,flashes=m.ZDATA[1][f]; hp=[n/5 for n in h]
        if target is not None and .8<=t<2.8 and hp[target]>0: aim=pos[target]
        for prev in range(max(0,f-5),f+1):
            pp,_,_,_,ff=m.ZDATA[1][prev]
            impacts.extend((pp[i],(f-prev)/20) for i in ff)
    elif key=='X': pos,hp,flashes=m.XDATA[0][f]
    else:
        pos=[[155,151],[194,178],[233,159],[276,176],[322,152],[225,105],[330,214]]
        hp=[1.]*7
        if key=='Shift': player=(m.lerp(65,340,m.clamp((t-.95)/.6)),165)
        for i,p in enumerate(pos):
            if abs(p[1]-165)>25: continue
            hit=1.15+(p[0]-65)/260 if key=='C' else .95+(p[0]-65)/275*.6
            if t>=hit:
                hp[i]=.48; p[1]+=(-1 if p[1]<165 else 1)*m.clamp((t-hit)/.25)*32
                impacts.append((p[:],t-hit))
            if 0<=t-hit<.1: flashes.add(i)
    return pos,hp,flashes,player,aim,bullets,impacts

STATES={key:[state(key,f) for f in range(m.FRAMES)] for key in NAMES}

def render(key,v,f):
    t=f/20; im=m.BG.copy(); d=ImageDraw.Draw(im)
    pos,hp,flashes,player,aim,bullets,impacts=STATES[key][f]
    d.text((12,8),f'{key}-{chr(65+v)}  {NAMES[key][v]}',font=m.BOLD,fill=WHITE)
    d.text((12,35),SUB[key]+' · 동작 동일',font=m.FONT,fill='#a8c6bb')
    if key=='X' and .65<t<3.8:
        strength=min(m.clamp((t-.65)/.3),m.clamp((3.8-t)/.4))
        vortex(d,(258,163),v,t,strength)
        if .8<t<3.5 and f%6==0: m.ring(d,(258,163),10,WHITE,2)
    for i,p in sorted(enumerate(pos),key=lambda pair:pair[1][1]):
        if hp[i]<=0: continue
        m.ring(d,(p[0],p[1]+8),10,'#243c32',3)
        paste(im,ENEMY[(int(t*8)+i)%16],p,i in flashes)
        d=ImageDraw.Draw(im)
        d.line([(p[0]-9,p[1]-21),(p[0]+9,p[1]-21)],fill=DARK,width=2)
        d.line([(p[0]-9,p[1]-21),(p[0]-9+18*hp[i],p[1]-21)],fill='#dfb77f',width=2)
    action='walk' if key=='Shift' and .95<t<1.55 else 'attack' if .8<t<(2.8 if key=='Z' else 1.4) else 'idle'
    actor=ACTOR[action][int(t*(10 if action!='idle' else 2))%len(ACTOR[action])]
    m.ring(d,(player[0],player[1]+8),12,'#243c32',3)
    paste(im,actor,player); d=ImageDraw.Draw(im)
    if key=='Z':
        if aim:
            x,y=aim
            for sx,sy in [(-1,-1),(1,-1),(-1,1),(1,1)]:
                d.line([(x+sx*15,y+sy*11),(x+sx*15,y+sy*15),(x+sx*10,y+sy*15)],fill=m.GOLD)
        for p,di in bullets: arrow(d,p,di,v,t)
    elif key=='C':
        if .8<t<1.15:
            for j in range(5):
                a=j*math.tau/5; rr=(1.15-t)*70+6
                p=(77+math.cos(a)*rr,160+math.sin(a)*rr)
                d.line([p,(m.lerp(p[0],77,.3),m.lerp(p[1],160,.3))],fill=MINT,width=2)
        if 1.15<=t<2.6: arrow(d,(65+(t-1.15)*260,165),(1,0),v,t,True)
    elif key=='Shift' and .95<t<1.65: dash_fx(d,player,v,t-.95)
    for p,age in impacts: impact(d,p,age,v,key in ['C','Shift'])
    stage='준비' if t<.8 else '발동' if t<(3.8 if key=='X' else 2.8) else '결과 확인'
    d.text((12,254),f'그래픽 시안 · {stage}',font=m.SMALL,fill='#b7cbc3')
    d.text((303,254),f'{t:04.1f} / 6.5s',font=m.SMALL,fill='#90afa4')
    d.rectangle((0,277,int(400*f/129),279),fill=MINT)
    return im

def main():
    board=Image.new('RGB',(1200,1120))
    results={'note':'Shared motion snapshots for every graphic variant; not Unity footage.', 'motion':{}}
    for row,key in enumerate(NAMES):
        before=repr(STATES[key])
        variants=[]
        for v in range(3):
            frames=[render(key,v,f) for f in range(130)]
            m.save_gif([im.resize((800,560),Image.Resampling.NEAREST) for im in frames],OUT/f'{key}-{chr(65+v)}.gif')
            variants.append(frames)
            board.paste(frames[28 if key=='Shift' else 33 if key=='C' else 42],(400*v,280*row))
        assert before==repr(STATES[key]), 'Renderer mutated shared motion'
        results['motion'][key]=hashlib.sha256(before.encode()).hexdigest()
        comparison=[]
        for f in range(130):
            im=Image.new('RGB',(1200,280))
            for v in range(3): im.paste(variants[v][f],(400*v,0))
            comparison.append(im)
        m.save_gif(comparison,OUT/f'{key}-compare.gif')
        print(key+' graphics complete',flush=True)
    board.save(OUT/'overview.png')
    for path in OUT.glob('*.gif'):
        with Image.open(path) as im:
            assert im.n_frames==130 and im.info['loop']==0
            total=0
            for f in range(im.n_frames): im.seek(f); total+=im.info['duration']
            assert total==6500
            results[path.name]={'frames':130,'duration_ms':total,'size':im.size}
    (OUT/'verification.json').write_text(json.dumps(results,indent=2),encoding='utf-8')

if __name__=='__main__': main()
