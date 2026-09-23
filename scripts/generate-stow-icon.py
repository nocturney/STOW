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

# restrained exterior shadow: the mark should not read as a neon tile.
glow=Image.new("RGBA",(W,W),(0,0,0,0))
gd=ImageDraw.Draw(glow)
gd.rounded_rectangle([sc(84),sc(88),sc(940),sc(944)],radius=sc(202),fill=(0,14,28,72))
glow=glow.filter(ImageFilter.GaussianBlur(sc(18)))
canvas.alpha_composite(glow)

# dark tile
base=gradient((sc(844),sc(844)),(15,49,82,255),(4,18,35,255),True)
mask=Image.new("L",base.size,0)
md=ImageDraw.Draw(mask)
md.rounded_rectangle([0,0,base.width-1,base.height-1],radius=sc(188),fill=255)
base.putalpha(mask)
canvas.alpha_composite(base,(sc(90),sc(90)))

# inner rim
rim=Image.new("RGBA",(W,W),(0,0,0,0))
rd=ImageDraw.Draw(rim)
rd.rounded_rectangle([sc(91),sc(91),sc(933),sc(933)],radius=sc(187),outline=(92,169,255,60),width=sc(2))
canvas.alpha_composite(rim)

# Quiet internal light behind the layered windows.
inner_glow=Image.new("RGBA",(W,W),(0,0,0,0))
igd=ImageDraw.Draw(inner_glow)
igd.ellipse([sc(246),sc(182),sc(836),sc(720)],fill=(48,135,255,54))
inner_glow=inner_glow.filter(ImageFilter.GaussianBlur(sc(54)))
canvas.alpha_composite(inner_glow)

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

card(606,382,360,404,10,(249,254,255),(60,132,255),244)
card(420,468,316,338,-11,(154,245,239),(31,159,190),218)

# tray shadow
shadow=Image.new("RGBA",(W,W),(0,0,0,0))
sd=ImageDraw.Draw(shadow)
sd.rounded_rectangle([sc(240),sc(620),sc(784),sc(802)],radius=sc(78),fill=(0,0,0,90))
shadow=shadow.filter(ImageFilter.GaussianBlur(sc(18)))
canvas.alpha_composite(shadow)

# tray outer gradient
tray=gradient((sc(544),sc(182)),(253,255,255,255),(74,157,245,255),False)
tm=Image.new("L",tray.size,0)
td=ImageDraw.Draw(tm)
td.rounded_rectangle([0,0,tray.width-1,tray.height-1],radius=sc(78),fill=255)
tray.putalpha(tm)
canvas.alpha_composite(tray,(sc(240),sc(620)))

# tray interior
inner=Image.new("RGBA",(W,W),(0,0,0,0))
idraw=ImageDraw.Draw(inner)
idraw.rounded_rectangle([sc(302),sc(615),sc(722),sc(704)],radius=sc(40),fill=(6,24,46,255))
idraw.rounded_rectangle([sc(327),sc(632),sc(697),sc(687)],radius=sc(24),fill=(10,51,87,255))
canvas.alpha_composite(inner)

# downsample
canvas=canvas.resize((S,S),Image.Resampling.LANCZOS)
os.makedirs(os.path.dirname(OUT_PNG),exist_ok=True)
canvas.save(OUT_PNG,"PNG")
canvas.save(OUT_ICO,"ICO",sizes=[(16,16),(20,20),(24,24),(32,32),(40,40),(48,48),(64,64),(96,96),(128,128),(256,256)])
print(OUT_PNG)
print(OUT_ICO)
