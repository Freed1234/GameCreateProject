"""Five C / Shift art variants, preserving the selected motion snapshots."""
import sys
sys.dont_write_bytecode = True
from pathlib import Path
import importlib.util
import math
import json
import hashlib
from PIL import Image, ImageDraw

OUT=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('graphics',OUT.parent/'generate.py')
g=importlib.util.module_from_spec(spec); spec.loader.exec_module(g)
m=g.m
WHITE,MINT,DARK=g.WHITE,g.MINT,g.DARK
LEAF='#b3d879'
NAMES={'C':['백깃 장궁 화살','잎날 창 화살','나선 송곳 화살','매 형상 화살','압축 바람 화살'],
       'Shift':['백깃 돌진','잎바람 돌진','유선형 풍압막','매의 급강하','지면 가르기']}

# Capture selected references with the original renderer before replacing effect hooks.
references=[g.render('Z',0,42),g.render('X',1,42)]
REF=Image.new('RGB',(400,280),'#101f25'); rd=ImageDraw.Draw(REF)
rd.text((12,10),'선택 완료 · 스타일 기준',font=m.BOLD,fill=WHITE)
rd.text((12,40),'Z-A 깃바람 화살',font=m.FONT,fill=MINT)
rd.text((212,40),'X-B 나뭇잎 회오리',font=m.FONT,fill=LEAF)
for j,im in enumerate(references):
    REF.paste(im.crop((35,80,380,235)).resize((190,120),Image.Resampling.NEAREST),(5+j*200,72))
rd=ImageDraw.Draw(REF)
rd.text((12,209),'C1 · 관통 / Shift1 · 경로 돌진 유지',font=m.FONT,fill='#bbd1c5')
rd.text((12,236),'새 그래픽 R1~R5 · Unity 적용 전 시안',font=m.SMALL,fill='#90afa4')

def feather(d,p,di,length=12,width=4,color=WHITE):
    g.shape(d,p,di,[(0,0),(-length*.4,-width),(-length,-width*.6),(-length*.7,0),(-length,width*.6),(-length*.4,width)],color)
    g.stroke(d,p,di,[(-length,0),(0,0)],'#629d89',1)

def leaf(d,p,di,length=12,width=4):
    g.shape(d,p,di,[(0,0),(-length*.4,-width),(-length,0),(-length*.4,width)],DARK)
    g.shape(d,p,di,[(-1,0),(-length*.4,-width+1),(-length+1,0),(-length*.4,width-1)],LEAF)
    g.stroke(d,p,di,[(-length+1,0),(-1,0)],'#668c55')

def arrow(d,p,di,v,t,large=False):
    # Constant 26-pixel tip envelope, one projectile for all five designs.
    if v==0:
        g.stroke(d,p,di,[(-88,0),(-10,0)],DARK,6)
        g.stroke(d,p,di,[(-88,0),(-10,0)],MINT,2)
        g.shape(d,p,di,[(0,0),(-29,-13),(-21,0),(-29,13)],DARK)
        g.shape(d,p,di,[(-2,0),(-27,-11),(-19,0),(-27,11)],WHITE)
        for sign in [-1,1]:
            for j in range(3):
                q=g.local(p,di,[(-65-j*7,sign*(3+j*2))])[0]
                feather(d,q,(di[0],di[1]+sign*.45),18,4)
        g.stroke(d,p,di,[(-112,0),(-89,0)],'#6baf95',2)
    elif v==1:
        g.stroke(d,p,di,[(-78,0),(-30,0)],'#749c60',3)
        g.shape(d,p,di,[(0,0),(-18,-11),(-37,-13),(-53,-6),(-60,0),(-53,6),(-37,13),(-18,11)],DARK)
        g.shape(d,p,di,[(-2,0),(-19,-9),(-36,-11),(-51,-5),(-57,0),(-51,5),(-36,11),(-19,9)],LEAF)
        g.stroke(d,p,di,[(-56,0),(-3,0)],WHITE,2)
        for x in [-18,-29,-40]:
            for sign in [-1,1]: g.stroke(d,p,di,[(x,0),(x-10,sign*8)],'#668c55')
        for j in range(5):
            q=g.local(p,di,[(-70-j*8,math.sin(t*8+j)*9)])[0]
            leaf(d,q,di,8,3)
    elif v==2:
        g.stroke(d,p,di,[(-85,0),(-6,0)],WHITE,2)
        g.shape(d,p,di,[(0,0),(-23,-4),(-18,0),(-23,4)],WHITE)
        for phase in [0,math.pi]:
            coords=[(-j,math.sin(j*.18-t*15+phase)*12) for j in range(11,86)]
            g.stroke(d,p,di,coords,DARK,4)
            g.stroke(d,p,di,coords,MINT if phase==0 else '#c7f7cb',2)
        g.stroke(d,p,di,[(-106,0),(-86,0)],'#53977e',2)
    elif v==3:
        # Folded wings and a beak, no flapping and no secondary projectiles.
        g.shape(d,p,di,[(0,0),(-13,-5),(-25,-5),(-55,-13),(-76,-12),(-59,-3),(-90,-4),(-82,0),(-90,4),(-59,3),(-76,12),(-55,13),(-25,5),(-13,5)],DARK)
        g.shape(d,p,di,[(-2,0),(-14,-3),(-26,-3),(-55,-11),(-70,-10),(-52,-1),(-79,0),(-52,1),(-70,10),(-55,11),(-26,3),(-14,3)],WHITE)
        for sign in [-1,1]:
            for j in range(3): g.stroke(d,p,di,[(-35-j*9,sign*4),(-56-j*6,sign*10)],MINT)
        g.stroke(d,p,di,[(-107,0),(-84,0)],MINT,2)
    else:
        for offset,col in [(0,WHITE),(-12,MINT),(-23,'#619f8a')]:
            g.stroke(d,p,di,[(offset-42,-13),(offset-18,-9),(offset,0),(offset-18,9),(offset-42,13)],DARK,4)
            g.stroke(d,p,di,[(offset-42,-12),(offset-18,-8),(offset,0),(offset-18,8),(offset-42,12)],col,2)
        # Open center with discontinuous wind strokes rather than a solid shaft.
        for j in range(6):
            x=-25-j*11+(t*25%8)
            g.stroke(d,p,di,[(x-7,0),(x,0)],WHITE if j<3 else MINT,1)

def dash_fx(d,p,v,age):
    x,y=p; a=m.clamp(age/.6)
    if v==0:
        for sign in [-1,1]:
            for j in range(4):
                feather(d,(x+17-j*7,y-6+sign*(3+j*4)),(1,-sign*.3),19,4)
            d.line([(x-29,y-6+sign*15),(x-13,y-6+sign*10)],fill=MINT,width=2)
    elif v==1:
        d.arc((x-4,y-29,x+32,y+18),-85,85,fill=MINT,width=2)
        for j in range(14):
            phase=j*.73-age*7
            xx=x+18-(j%5)*10
            yy=y-6+math.sin(phase)*21
            leaf(d,(xx,yy),(math.cos(phase*.4),math.sin(phase*.4)),9,3)
    elif v==2:
        # Curved open teardrop shell, keeping the character center unobstructed.
        points=[(x+27,y-5),(x+13,y-20),(x-8,y-26),(x-31,y-20),(x-44,y-5),
                (x-31,y+10),(x-8,y+16),(x+13,y+10),(x+27,y-5)]
        d.line(points,fill=DARK,width=5); d.line(points,fill=MINT,width=2)
        d.line(points[:3],fill=WHITE,width=3); d.line(points[-3:],fill=WHITE,width=3)
        d.line([(x-37,y-9),(x-19,y-14)],fill='#86bea4')
    elif v==3:
        for sign in [-1,1]:
            points=[(x+23,y-6),(x+4,y-11),(x-28,y-6+sign*24),(x-50,y-6+sign*20),(x-24,y-6+sign*8),(x-44,y-6+sign*6)]
            d.line(points,fill=DARK,width=5); d.line(points,fill=WHITE,width=2)
            d.line(points[2:5],fill=MINT,width=3)
        d.line([(x-60,y-6),(x-31,y-6)],fill=MINT,width=2)
    else:
        # Ground-only forward wedge and two diverging wake lines.
        for sign in [-1,1]:
            d.line([(max(65,x-65),y+8+sign*15),(x-10,y+8+sign*9),(x+21,y+8)],fill=DARK,width=5)
            d.line([(max(65,x-65),y+8+sign*15),(x-10,y+8+sign*9),(x+21,y+8)],fill=MINT,width=2)
            for j in range(6):
                xx=x-12-j*9
                if xx>65:
                    yy=y+8+sign*(11+(j%3)*3)
                    d.rectangle((xx,yy,xx+3,yy+2),fill='#b4bd8d')

def impact(d,p,age,v,large=False):
    if not 0<=age<.3: return
    r=5+age*65
    for sign in [-1,1]:
        q=(p[0]+math.sin(age*10)*3,p[1]+sign*r)
        if v==0: feather(d,q,(.4,sign),11,3)
        elif v==1: leaf(d,q,(.4,sign),12,4)
        elif v==2:
            d.arc((q[0]-9,q[1]-5,q[0]+9,q[1]+5),age*800,age*800+210,fill=MINT,width=2)
        elif v==3:
            for j in range(2): feather(d,(q[0]-j*5,q[1]),(.5,sign),13,3)
        else: d.line([(q[0]-8,q[1]-sign*3),(q[0],q[1]),(q[0]+8,q[1]-sign*3)],fill=WHITE,width=1)

g.arrow=arrow; g.dash_fx=dash_fx; g.impact=impact
g.NAMES.update(NAMES)

def render(key,v,f):
    im=g.render(key,v,f); d=ImageDraw.Draw(im)
    # Replace letter identifiers with revision-specific identifiers.
    d.rectangle((0,0,400,31),fill='#101f25')
    d.text((12,8),f'{key}-R{v+1}  {NAMES[key][v]}',font=m.BOLD,fill=WHITE)
    # Purely visual dissipation after the dash. No additional hit or displacement.
    t=f/20
    if key=='Shift' and 1.65<=t<2.05:
        age=t-1.65; x,y=g.STATES[key][f][3]
        if v in [0,1]:
            for j in range(5):
                q=(x-15-j*5-age*28,y-4+(j%2)*8+age*20)
                if v==0: feather(d,q,(.8,.4),max(2,9-age*16),2)
                else: leaf(d,q,(.7,.5),max(2,10-age*16),3)
        elif v==2:
            d.arc((x-20,y-24,x+23,y+16),-65+age*180,65,fill='#60937f',width=1)
        elif v==3:
            for j in range(4):
                xx=x-20-j*10-age*25
                d.line([(xx-7,y-7+j*2),(xx,y-7+j*2)],fill='#60937f')
    return im

def main():
    report={'selected':{'Z':'Z-A','X':'X-B'},'motion_sha256':{},'gifs':{}}
    overview=Image.new('RGB',(1200,1120))
    for row,key in enumerate(NAMES):
        before=repr(g.STATES[key]); variants=[]
        board=Image.new('RGB',(1200,560))
        for v in range(5):
            frames=[render(key,v,f) for f in range(130)]
            m.save_gif([im.resize((800,560),Image.Resampling.NEAREST) for im in frames],OUT/f'{key}-R{v+1}.gif')
            variants.append(frames)
            board.paste(frames[33 if key=='C' else 28],((v%3)*400,(v//3)*280))
        assert before==repr(g.STATES[key])
        report['motion_sha256'][key]=hashlib.sha256(before.encode()).hexdigest()
        board.paste(REF,(800,280)); board.save(OUT/f'{key}-sheet.png')
        overview.paste(board,(0,row*560))
        comparison=[]
        for f in range(130):
            im=Image.new('RGB',(1200,560))
            for v in range(5): im.paste(variants[v][f],((v%3)*400,(v//3)*280))
            im.paste(REF,(800,280)); comparison.append(im)
        m.save_gif(comparison,OUT/f'{key}-compare.gif')
        print(key+': five variants and comparison complete',flush=True)
    overview.save(OUT/'overview.png')
    prior=json.loads((OUT.parent/'verification.json').read_text(encoding='utf-8'))
    for key,digest in report['motion_sha256'].items():
        assert digest==prior['motion'][key],f'{key}: motion changed from approved preview'
    for path in OUT.glob('*.gif'):
        with Image.open(path) as im:
            assert im.n_frames==130 and im.info['loop']==0
            total=0
            for f in range(im.n_frames): im.seek(f); total+=im.info['duration']
            assert total==6500
            report['gifs'][path.name]={'frames':130,'duration_ms':total,'size':im.size}
    assert len(report['gifs'])==12
    (OUT/'verification.json').write_text(json.dumps(report,indent=2),encoding='utf-8')

if __name__=='__main__': main()
