from PIL import Image, ImageDraw, ImageFilter
import os

ROOT = r"C:\Users\Chris\STOW"
OUT = os.path.join(ROOT, "assets", "STOWHero.png")
SCALE = 2
W,H = 1200*SCALE, 760*SCALE

def sc(v): return int(round(v*SCALE))

def gradient(size, c0, c1, diagonal=False):
    w,h=size
    im=Image.new("RGBA", size)
    px=im.load()
    for y in range(h):
        for x in range(w):
            t=(x+y)/(w+h-2) if diagonal else y/max(1,h-1)
            px[x,y]=tuple(round(c0[i]*(1-t)+c1[i]*t) for i in range(4))
    return im

canvas=Image.new("RGBA",(W,H),(0,0,0,0))

def card(cx,cy,w,h,angle,c0,c1,alpha=242):
    layer=gradient((sc(w),sc(h)),(*c0,alpha),(*c1,alpha),True)
    mask=Image.new("L",layer.size,0)
    d=ImageDraw.Draw(mask)
    d.rounded_rectangle([0,0,layer.width-1,layer.height-1],radius=sc(48),fill=255)
    layer.putalpha(Image.eval(mask,lambda p:p*alpha//255))
    hi=Image.new("RGBA",layer.size,(0,0,0,0))
    hd=ImageDraw.Draw(hi)
    hd.rounded_rectangle([sc(12),sc(12),layer.width-sc(12),layer.height-sc(12)],
                         radius=sc(42),outline=(255,255,255,120),width=sc(4))
    layer.alpha_composite(hi)
    rot=layer.rotate(angle,resample=Image.Resampling.BICUBIC,expand=True)
    x=sc(cx)-rot.width//2; y=sc(cy)-rot.height//2
    shadow=Image.new("RGBA",(W,H),(0,0,0,0))
    sd=ImageDraw.Draw(shadow)
    sd.rounded_rectangle([x+sc(16),y+sc(25),x+rot.width-sc(8),y+rot.height],
                         radius=sc(55),fill=(0,0,0,85))
    shadow=shadow.filter(ImageFilter.GaussianBlur(sc(24)))
    canvas.alpha_composite(shadow)
    canvas.alpha_composite(rot,(x,y))

# atmospheric glow
glow=Image.new("RGBA",(W,H),(0,0,0,0))
gd=ImageDraw.Draw(glow)
gd.ellipse([sc(270),sc(105),sc(980),sc(675)],fill=(52,145,255,44))
glow=glow.filter(ImageFilter.GaussianBlur(sc(52)))
canvas.alpha_composite(glow)

card(696,292,366,405,10,(250,254,255),(60,132,255),245)
card(468,374,322,340,-11,(154,245,239),(31,159,190),218)

# tray shadow
shadow=Image.new("RGBA",(W,H),(0,0,0,0))
sd=ImageDraw.Draw(shadow)
sd.rounded_rectangle([sc(286),sc(548),sc(914),sc(714)],radius=sc(76),fill=(0,0,0,82))
shadow=shadow.filter(ImageFilter.GaussianBlur(sc(20)))
canvas.alpha_composite(shadow)

# recessed cavity behind an open tray
inner=Image.new("RGBA",(W,H),(0,0,0,0))
idraw=ImageDraw.Draw(inner)
idraw.rounded_rectangle([sc(356),sc(522),sc(844),sc(622)],radius=sc(44),fill=(6,24,46,255))
idraw.rounded_rectangle([sc(388),sc(542),sc(812),sc(601)],radius=sc(27),fill=(10,51,87,255))
canvas.alpha_composite(inner)

# front wall + side rails, matching the official app icon silhouette
tray=gradient((sc(628),sc(194)),(253,255,255,255),(74,157,245,255),False)
mask=Image.new("L",tray.size,0)
md=ImageDraw.Draw(mask)
md.rounded_rectangle([0,sc(78),tray.width-1,tray.height-1],radius=sc(66),fill=255)
md.rounded_rectangle([0,sc(10),sc(118),sc(164)],radius=sc(50),fill=255)
md.rounded_rectangle([tray.width-sc(118),sc(10),tray.width-1,sc(164)],radius=sc(50),fill=255)
tray.putalpha(mask)
canvas.alpha_composite(tray,(sc(286),sc(520)))

lip=Image.new("RGBA",(W,H),(0,0,0,0))
ld=ImageDraw.Draw(lip)
ld.line([sc(360),sc(660),sc(840),sc(660)],fill=(255,255,255,120),width=sc(4))
canvas.alpha_composite(lip)

canvas=canvas.resize((1200,760),Image.Resampling.LANCZOS)
canvas.save(OUT,"PNG")
print(OUT)
