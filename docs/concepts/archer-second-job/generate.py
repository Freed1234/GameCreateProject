"""Deterministic explanatory animation; not Unity footage. Requires Pillow."""
from pathlib import Path
import math
import random
import json
from PIL import Image, ImageDraw, ImageFont

OUT = Path(__file__).resolve().parent
W, H, FPS, FRAMES = 400, 280, 20, 130
MINT, WHITE, GOLD = '#75e5c2', '#e5fff2', '#f2cc75'
FONT = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 13)
BOLD = ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf', 17)
SMALL = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 11)
NAMES = {
 'Z': ['집중 추적 연사', '순차 포착 연사', '삼연점사'],
 'X': ['응축 회오리', '전진 회오리', '쌍둥이 회오리'],
 'C': ['대형 풍압 화살', '나선 관통 화살', '후속 돌풍 화살'],
 'Shift': ['쐐기 바람 돌진', '나선 바람 돌진', '후류 폭발 돌진'],
}
DESC = {
 'Z': ['한 적에 집중 → 처치 후 표적 전환', '가까운 세 적을 차례로 조준', '3발씩 집중 사격 → 다음 묶음 재조준'],
 'X': ['고정된 중심으로 흡인 · 지속 타격', '전진하는 중심에 적을 모으며 이동', '두 중심으로 흡인 → 합체 타격'],
 'C': ['거대 화살 관통 · 즉시 좌우 밀치기', '나선 화살 관통 · 명중 지점 풍압', '선행 화살 → 뒤따르는 넓은 돌풍'],
 'Shift': ['돌진 경로 타격 · 즉시 좌우 밀치기', '회전 기류 돌진 · 경로당 1회 타격', '돌진 완료 → 지나온 경로 폭발'],
}

def clamp(v, a=0., b=1.): return max(a, min(b, v))
def lerp(a, b, f): return a + (b-a)*f
def vec(a, b):
    dx, dy = b[0]-a[0], b[1]-a[1]
    length = math.hypot(dx, dy)
    return (dx/max(length, .001), dy/max(length, .001))
def plus(p, d, n): return (p[0]+d[0]*n, p[1]+d[1]*n)

def arrow(d, p, direction, size=16, thick=3):
    ux, uy = direction
    back = plus(p, direction, -size)
    d.line([back, p], fill=MINT, width=thick)
    d.line([plus(back, direction, -size*.6), back], fill='#3e8d78', width=max(1, thick))
    d.polygon([p, (p[0]-ux*7-uy*thick*1.5, p[1]-uy*7+ux*thick*1.5),
               (p[0]-ux*5, p[1]-uy*5),
               (p[0]-ux*7+uy*thick*1.5, p[1]-uy*7-ux*thick*1.5)], fill=WHITE)

def ring(d, p, r, color=MINT, width=1):
    d.ellipse((p[0]-r, p[1]-r*.45, p[0]+r, p[1]+r*.45), outline=color, width=width)

def burst(d, p, age, size=20):
    if not 0 <= age < .35: return
    r = 3 + size*age/.35
    for i in range(8):
        a = i*math.tau/8
        d.line([(p[0]+math.cos(a)*r*.6, p[1]+math.sin(a)*r*.6),
                (p[0]+math.cos(a)*r, p[1]+math.sin(a)*r)], fill=WHITE, width=2)

def archer(d, p, t, aiming=None):
    x, y = map(int, p)
    ring(d, (x,y+8), 11, '#233c32', 3)
    # Pixel silhouette: green cloak, blonde head, leather boots and visible bow.
    d.polygon([(x-5,y-9),(x+3,y-8),(x+6,y+6),(x-9,y+6)], fill='#246e60')
    d.rectangle((x-4,y-14,x+3,y-7), fill='#e6bb8b')
    d.rectangle((x-5,y-16,x+3,y-13), fill='#ecd279')
    d.rectangle((x-5,y+5,x-2,y+10), fill='#473c31')
    d.rectangle((x+2,y+5,x+5,y+10), fill='#473c31')
    aim = vec(p, aiming) if aiming else (1,0)
    b = plus(p, aim, 12)
    side = (-aim[1],aim[0])
    a, c = plus(b,side,-10), plus(b,side,10)
    d.line([a, plus(b,aim,5), c], fill=GOLD, width=2)
    d.line([a,c], fill='#d9e2be')
    d.line([p,b], fill='#e6bb8b', width=3)

def enemy(d, p, hp=1, flash=False):
    x,y = map(int,p)
    ring(d,(x,y+7),9,'#233c32',3)
    col = WHITE if flash else '#bd685d'
    d.polygon([(x-9,y+5),(x-9,y-1),(x-6,y-7),(x+5,y-8),(x+9,y-3),(x+10,y+5)],fill=col)
    d.line([(x-6,y+6),(x+7,y+6)],fill='#763c42',width=2)
    d.rectangle((x-4,y-2,x-3,y),fill='#302c35')
    d.rectangle((x+3,y-2,x+4,y),fill='#302c35')
    d.line([(x-8,y-13),(x+8,y-13)],fill='#283c34',width=2)
    d.line([(x-8,y-13),(x-8+16*max(.02,hp),y-13)],fill='#e8a486',width=2)

def vortex(d, p, radius, t, strength=1):
    ring(d,p,radius,'#74bea0',2)
    for band in range(4):
        r = radius*(1-band*.19)
        cy = p[1]-band*7
        for j in range(2):
            phase = t*340 + band*55 + j*180
            d.arc((p[0]-r,cy-r*.35,p[0]+r,cy+r*.35),phase,phase+135,
                  fill=WHITE if band==3 else MINT,width=2)
    for j in range(8):
        a = t*3+j*math.tau/8
        r = radius*(1.05 + ((j*.19-t*.5)%1)*.4)
        x,y=p[0]+math.cos(a)*r,p[1]+math.sin(a)*r*.6
        d.line([(x,y),(x-math.sin(a)*6,y+math.cos(a)*3)],fill='#8bbd9d',width=1)

def background():
    im=Image.new('RGB',(W,H),'#101f25'); d=ImageDraw.Draw(im)
    d.rectangle((0,61,W,247),fill='#385646')
    rng=random.Random(37)
    for _ in range(260):
        x,y=rng.randrange(W),rng.randrange(64,246)
        col=rng.choice(['#42614c','#304d3e','#4a6650'])
        d.line([(x,y),(x+2,y-2)],fill=col)
    return im

BG=background()
BASE=[(191,130),(232,180),(266,114),(297,160),(330,205),(350,105),(212,215)]
ORIGIN=(65,165)

def z_sim(variant, scenario='normal'):
    """Fixed timestep target acquisition + straight projectile collision simulation."""
    pts=[[float(x),float(y)] for x,y in BASE]
    hp=[5]*len(pts); bullets=[]; snapshots=[]; events=[]
    next_fire=.8; shot=0; locked=None; aimed=None
    for f in range(FRAMES):
        t=f/FPS
        for i,p in enumerate(pts):
            p[1]=BASE[i][1]+math.sin(t*1.5+i)*8
        available=[i for i in range(len(pts)) if hp[i]>0 and math.dist(ORIGIN,pts[i])<300]
        if scenario=='empty' or (scenario=='gap' and 1.2<t<1.8): available=[]
        available.sort(key=lambda i:math.dist(ORIGIN,pts[i]))
        if .8<=t<2.8 and t+1e-8>=next_fire:
            if available:
                if variant==0:
                    if locked not in available: locked=available[0]
                elif variant==1: locked=available[shot%min(3,len(available))]
                elif shot%3==0 or locked not in available: locked=available[0]
                aimed=locked
                direction=vec(ORIGIN,pts[locked])
                bullets.append({'p':plus(ORIGIN,direction,17),'v':direction})
                events.append({'time':t,'target':locked,'direction':direction,'target_position':pts[locked][:]})
                shot+=1
            else: aimed=None
            next_fire=t+(.30 if variant==2 and shot%3==0 else .10)
        flashes=set()
        for b in bullets[:]:
            a=b['p']; b['p']=plus(a,b['v'],310/FPS)
            hit=None
            for i,p in enumerate(pts):
                if hp[i]<=0: continue
                ab=(b['p'][0]-a[0],b['p'][1]-a[1]); den=ab[0]**2+ab[1]**2
                q=clamp(((p[0]-a[0])*ab[0]+(p[1]-a[1])*ab[1])/den)
                if math.dist(p,(a[0]+ab[0]*q,a[1]+ab[1]*q))<10: hit=i; break
            if hit is not None:
                hp[hit]-=1; flashes.add(hit); bullets.remove(b)
            elif not 0<b['p'][0]<W or not 61<b['p'][1]<247: bullets.remove(b)
        snapshots.append(([p[:] for p in pts],hp[:],[(b['p'],b['v']) for b in bullets],aimed,flashes))
    return snapshots,events

ZDATA=[z_sim(v)[0] for v in range(3)]

def x_centers(v,s):
    a=clamp(s/2.6)
    if v==0: return [(258,163)]
    if v==1: return [(lerp(165,304,a),163)]
    return [(lerp(216,263,a),lerp(120,163,a)),(lerp(310,263,a),lerp(202,163,a))]

def x_sim(v):
    positions=[list(p) for p in BASE]; hp=[1.]*len(BASE); frames=[]
    for f in range(FRAMES):
        s=f/FPS-.8; centers=x_centers(v,s); flashes=set()
        if 0<s<2.7:
            for i,p in enumerate(positions):
                c=min(centers,key=lambda c:math.dist(p,c)); dist=math.dist(p,c)
                if dist<105:
                    di=vec(p,c); step=min(max(0,dist-10),34/FPS)
                    p[:]=plus(p,di,step)
                    if f%6==0: hp[i]-=.055; flashes.add(i)
        if v==2 and f==69:
            for i,p in enumerate(positions):
                if math.dist(p,(263,163))<80: hp[i]-=.18; flashes.add(i)
        frames.append(([p[:] for p in positions],hp[:],flashes))
    return frames

XDATA=[x_sim(v) for v in range(3)]

def render(key,v,f):
    t=f/FPS; s=t-.8
    im=BG.copy(); d=ImageDraw.Draw(im)
    d.text((12,8),f'{key}{v+1}  {NAMES[key][v]}',font=BOLD,fill=WHITE)
    d.text((12,35),DESC[key][v],font=FONT,fill='#a8c6bb')
    player=ORIGIN; positions=[list(p) for p in BASE]; hp=[1.]*len(BASE); flashes=set(); aim=None
    if key=='Z':
        positions,health,bullets,target,flashes=ZDATA[v][f]; hp=[h/5 for h in health]
        if target is not None and .8<=t<2.8 and hp[target]>0:
            aim=positions[target]; x,y=aim
            for sx,sy in [(-1,-1),(1,-1),(-1,1),(1,1)]:
                d.line([(x+sx*14,y+sy*7),(x+sx*14,y+sy*12),(x+sx*9,y+sy*12)],fill=GOLD,width=1)
        for p,di in bullets: arrow(d,p,di,19,2)
        if v==2 and .65<t<2.8: ring(d,(65,170),15+(t*6%1)*7,MINT)
    elif key=='X':
        centers=x_centers(v,s)
        positions,hp,flashes=XDATA[v][f]
        if 0<s<2.7:
            for c in centers: vortex(d,c,43 if v==2 else 54,t)
        if v==2:
            if 2.6<s<3.1: vortex(d,(263,163),62,t)
            burst(d,(263,163),s-2.65,55)
    else:
        # Straight horizontal corridor makes both lateral displacement and timing legible.
        positions=[[155,151],[194,178],[233,159],[276,176],[322,152],[225,105],[330,214]]
        starts=[p[:] for p in positions]
        if key=='C':
            launch=1.15; tip=65+(t-launch)*260
            if .8<t<launch:
                ring(d,(70,168),18+(launch-t)*20,MINT,2)
                arrow(d,(86,165),(1,0),25,4)
            if launch<=t<2.4:
                if v==0: arrow(d,(tip,165),(1,0),66,9)
                elif v==1:
                    arrow(d,(tip,165),(1,0),42,3)
                    for sign in [-1,1]:
                        pts=[(tip-j,165+sign*math.sin(j*.19-t*14)*10) for j in range(0,65,2)]
                        d.line(pts,fill=MINT,width=2)
                else:
                    arrow(d,(tip,165),(1,0),35,4)
                    wave=tip-70
                    d.arc((wave-30,133,wave+12,197),-65,65,fill=WHITE,width=4)
                    d.arc((wave-46,136,wave-4,194),-65,65,fill=MINT,width=3)
            for i,p in enumerate(positions):
                if abs(p[1]-165)>25: continue
                impact=launch+(p[0]-65)/260
                push_at=impact+(.27 if v==2 else 0)
                if t>=impact: hp[i]=.48
                move=clamp((t-push_at)/.25)*32
                p[1]+=(-1 if p[1]<165 else 1)*move
                if 0<t-impact<.1: flashes.add(i)
                burst(d,p,t-push_at,18 if v==1 else 12)
        else:
            start=.95; end=1.55
            progress=clamp((t-start)/(end-start)); player=(lerp(65,340,progress),165)
            if start<t<end+.2:
                for j in range(1,6):
                    px=player[0]-j*13
                    if px>65: d.line([(px,159-j%2*4),(px+8,159-j%2*4)],fill=MINT,width=2)
                if v==0:
                    x=player[0]
                    d.line([(x-5,145),(x+19,165),(x-5,185)],fill=WHITE,width=3)
                    d.line([(x-12,145),(x+10,165),(x-12,185)],fill=MINT,width=2)
                elif v==1:
                    vortex(d,(player[0],173),23,t*2)
            if v==2 and start<t<end+.35:
                d.line([(65,165),(player[0],165)],fill=MINT,width=2)
                if t>end:
                    spread=(t-end)*70
                    for dy in [-spread,spread]:
                        d.line([(70,165+dy),(335,165+dy)],fill=WHITE,width=3)
                    for x in range(90,340,45): burst(d,(x,165),t-end,24)
            for i,p in enumerate(positions):
                if abs(p[1]-165)>25: continue
                impact=end+.04 if v==2 else start+(p[0]-65)/275*(end-start)
                if t>=impact:
                    hp[i]=.55
                    p[1]+=(-1 if p[1]<165 else 1)*clamp((t-impact)/.23)*34
                if 0<t-impact<.1: flashes.add(i)
    for i,p in sorted(enumerate(positions),key=lambda pair:pair[1][1]):
        if hp[i]>0: enemy(d,p,hp[i],i in flashes)
    archer(d,player,t,aim)
    stage='준비' if t<.8 else '시전' if t<(2.8 if key=='Z' else 3.9 if key=='X' else 2.5) else '결과 확인'
    d.text((12,254),f'동작 시안 · {stage}',font=SMALL,fill='#b7cbc3')
    d.text((302,254),f'{t:04.1f} / 6.5s',font=SMALL,fill='#90afa4')
    d.rectangle((0,277,int(W*f/(FRAMES-1)),279),fill=MINT)
    return im

def save_gif(frames,path):
    # A common palette prevents frame-to-frame color flicker.
    palette=Image.new('RGB',(W*3,H))
    for i,f in enumerate([frames[0],frames[len(frames)//4],frames[len(frames)//2]]):
        palette.paste(f.resize((W,H)),(i*W,0))
    palette=palette.quantize(colors=128)
    indexed=[f.quantize(palette=palette,dither=Image.Dither.NONE) for f in frames]
    indexed[0].save(path,save_all=True,append_images=indexed[1:],duration=50,loop=0,optimize=False,disposal=2)

def main():
    checks={}
    for v in range(3):
        _,normal=z_sim(v); _,empty=z_sim(v,'empty'); _,gap=z_sim(v,'gap')
        assert not empty
        assert all(not 1.2<e['time']<1.8 for e in gap)
        assert any(e['time']>=1.8 for e in gap)
        for e in normal:
            expected=vec(ORIGIN,e['target_position'])
            assert math.dist(expected,e['direction'])<1e-9
        assert len(set(e['target'] for e in normal))>=2
        checks[f'Z{v+1}']={'shots':len(normal),'targets':sorted(set(e['target'] for e in normal)),
                         'empty_no_shots':True,'gap_resume':True,'exact_aim':True}
    contact=Image.new('RGB',(W*3,H*4),'#101f25')
    for row,key in enumerate(NAMES):
        variants=[]
        for v in range(3):
            frames=[render(key,v,f) for f in range(FRAMES)]
            save_gif([im.resize((W*2,H*2),Image.Resampling.NEAREST) for im in frames],OUT/f'{key}{v+1}.gif')
            variants.append(frames)
            contact.paste(frames[32 if key!='X' else 50],(v*W,row*H))
        comparison=[]
        for f in range(FRAMES):
            im=Image.new('RGB',(W*3,H))
            for v in range(3): im.paste(variants[v][f],(v*W,0))
            comparison.append(im)
        save_gif(comparison,OUT/f'{key}-compare.gif')
        print(f'{key}: three individual GIFs + comparison',flush=True)
    contact.save(OUT/'overview.png')
    for p in OUT.glob('*.gif'):
        with Image.open(p) as im:
            assert im.n_frames==FRAMES,(p.name,im.n_frames)
            assert im.info['loop']==0
            duration=0
            for f in range(im.n_frames): im.seek(f); duration+=im.info['duration']
            assert duration==6500
            checks[p.name]={'frames':im.n_frames,'duration_ms':duration,'size':im.size}
    (OUT/'verification.json').write_text(json.dumps(checks,ensure_ascii=False,indent=2),encoding='utf-8')

if __name__=='__main__': main()
