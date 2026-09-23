from PIL import Image, ImageDraw, ImageFilter
import math, os

ROOT = r"C:\Users\Chris\STOW"
OUT_PNG = os.path.join(ROOT, "assets", "STOW.png")
OUT_ICO = os.path.join(ROOT, "assets", "STOW.ico")
S = 1024
SS = 2
W = S * SS

def sc(v): return int(round(v*SS))
def gradient(size, c0, c1, diagonal=False):
    w,h=size
    im=Image.new("RGBA", size)
    px=im.load()
    for y in range(h):
        for x in range(w):
            t=(x+y)/(w+h-2) if diagonal else y/max(1,h-1)
            col=tuple(round(c0[i]*(1-t)+c1[i]*t) for i in range(4))
            px[x,y]=col
    return im

canvas=Image.new("RGBA",(W,W),(0,0,0,0))

# soft exterior glow
glow=Image.new("RGBA",(W,W),(0,0,0,0))
gd=ImageDraw.Draw(glow)
gd.rounded_rectangle([sc(70),sc(70),sc(954),sc(954)],radius=sc(205),fill=(29,126,255,82))
glow=glow.filter(ImageFilter.GaussianBlur(sc(30)))
canvas.alpha_composite(glow)

# dark tile
base=gradient((sc(840),sc(840)),(11,40,69,255),(5,20,39,255),True)
mask=Image.new("L",base.size,0)
md=ImageDraw.Draw(mask)
md.rounded_rectangle([0,0,base.width-1,base.height-1],radius=sc(175),fill=255)
base.putalpha(mask)
canvas.alpha_composite(base,(sc(92),sc(92)))

# inner rim
rim=Image.new("RGBA",(W,W),(0,0,0,0))
rd=ImageDraw.Draw(rim)
rd.rounded_rectangle([sc(94),sc(94),sc(930),sc(930)],radius=sc(172),outline=(67,155,255,175),width=sc(5))
rd.rounded_rectangle([sc(113),sc(113),sc(911),sc(911)],radius=sc(156),outline=(111,220,238,48),width=sc(3))
canvas.alpha_composite(rim)

def card(cx,cy,w,h,angle,c0,c1,alpha=242):
    layer=Image.new("RGBA",(sc(w),sc(h)),(0,0,0,0))
    g=gradient(layer.size,(*c0,alpha),(*c1,alpha),True)
    m=Image.new("L",layer.size,0)
    d=ImageDraw.Draw(m)
    d.rounded_rectangle([0,0,layer.width-1,layer.height-1],radius=sc(44),fill=255)
    g.putalpha(Image.eval(m, lambda p: p*alpha//255))
    # highlight
    hi=Image.new("RGBA",layer.size,(0,0,0,0))
    hd=ImageDraw.Draw(hi)
    hd.rounded_rectangle([sc(12),sc(12),layer.width-sc(12),layer.height-sc(12)],radius=sc(38),outline=(255,255,255,115),width=sc(3))
    g.alpha_composite(hi)
    rot=g.rotate(angle, resample=Image.Resampling.BICUBIC, expand=True)
    x=sc(cx)-rot.width//2; y=sc(cy)-rot.height//2
    shadow=Image.new("RGBA",(W,W),(0,0,0,0))
    sd=ImageDraw.Draw(shadow)
    sd.rounded_rectangle([x+sc(18),y+sc(25),x+rot.width-sc(8),y+rot.height-sc(2)],radius=sc(55),fill=(0,0,0,90))
    shadow=shadow.filter(ImageFilter.GaussianBlur(sc(22)))
    canvas.alpha_composite(shadow)
    canvas.alpha_composite(rot,(x,y))

card(420,405,330,390,-13,(98,222,220),(23,139,171),225)
card(610,350,330,405,13,(220,246,255),(35,112,255),245)

# tray shadow
shadow=Image.new("RGBA",(W,W),(0,0,0,0))
sd=ImageDraw.Draw(shadow)
sd.rounded_rectangle([sc(190),sc(620),sc(835),sc(840)],radius=sc(94),fill=(0,0,0,125))
shadow=shadow.filter(ImageFilter.GaussianBlur(sc(28)))
canvas.alpha_composite(shadow)

# tray outer gradient
tray=gradient((sc(650),sc(235)),(230,248,255,255),(54,145,241,255),False)
tm=Image.new("L",tray.size,0)
td=ImageDraw.Draw(tm)
td.rounded_rectangle([0,0,tray.width-1,tray.height-1],radius=sc(88),fill=255)
tray.putalpha(tm)
canvas.alpha_composite(tray,(sc(187),sc(585)))

# tray interior
inner=Image.new("RGBA",(W,W),(0,0,0,0))
idraw=ImageDraw.Draw(inner)
idraw.rounded_rectangle([sc(276),sc(615),sc(748),sc(715)],radius=sc(47),fill=(6,28,52,255))
idraw.rounded_rectangle([sc(295),sc(629),sc(729),sc(692)],radius=sc(31),fill=(9,43,76,255))
idraw.line([sc(270),sc(742),sc(752),sc(742)],fill=(255,255,255,155),width=sc(4))
canvas.alpha_composite(inner)

# downsample
canvas=canvas.resize((S,S),Image.Resampling.LANCZOS)
os.makedirs(os.path.dirname(OUT_PNG),exist_ok=True)
canvas.save(OUT_PNG,"PNG")
canvas.save(OUT_ICO,"ICO",sizes=[(16,16),(20,20),(24,24),(32,32),(40,40),(48,48),(64,64),(96,96),(128,128),(256,256)])
print(OUT_PNG)
print(OUT_ICO)
