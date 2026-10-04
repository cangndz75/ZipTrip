"""Assemble ART-GATE-02B review sheets from Blender and real Unity captures."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Builds/art-gate-02b/review'
ISO = OUT / 'isolated'
GAME = ROOT / 'Builds/art-gate-02b/gameplay'
OUT.mkdir(parents=True, exist_ok=True)

FONT_PATH = 'C:/Windows/Fonts/arial.ttf'
BOLD_PATH = 'C:/Windows/Fonts/arialbd.ttf'
FONT = ImageFont.truetype(FONT_PATH, 25)
BOLD = ImageFont.truetype(BOLD_PATH, 34)
SMALL = ImageFont.truetype(FONT_PATH, 19)
BG = (242,236,222)
PANEL = (222,214,197)
INK = (37,54,57)

# Native pixel crop coordinates from the 1080x2340 real PuzzleGameplay capture.
ITEMS = [
    ('Passport',(590,440,765,725),'suitcase'),
    ('Towel',(196,822,344,1380),'suitcase'),
    ('Shampoo',(236,1605,358,1955),'Source Tray'),
    ('Sunglasses',(380,1608,610,1775),'Source Tray'),
    ('TravelPouch',(625,1597,837,1960),'Source Tray'),
]


def text(draw, xy, value, font=BOLD, fill=INK):
    draw.text(xy,value,font=font,fill=fill)


def tile(canvas, box, image, label, native=False):
    draw = ImageDraw.Draw(canvas)
    x0,y0,x1,y1 = box
    draw.rounded_rectangle(box, radius=20, fill=PANEL)
    text(draw,(x0+19,y0+15),label,FONT)
    avail_w, avail_h = x1-x0-32, y1-y0-73
    image = image.convert('RGBA')
    if native and (image.width > avail_w or image.height > avail_h):
        left = max(0,(image.width-avail_w)//2)
        top = max(0,(image.height-avail_h)//2)
        image = image.crop((left,top,left+min(image.width,avail_w),top+min(image.height,avail_h)))
    if not native:
        ratio = min(avail_w/image.width,avail_h/image.height)
        image = image.resize((round(image.width*ratio),round(image.height*ratio)),Image.Resampling.LANCZOS)
    cx = x0+(x1-x0-image.width)//2
    cy = y0+59+(avail_h-image.height)//2
    canvas.paste(image,(cx,cy),image)


def base(title, subtitle, width=1740, height=1240):
    canvas = Image.new('RGB',(width,height),BG)
    draw = ImageDraw.Draw(canvas)
    text(draw,(42,28),title)
    text(draw,(42,76),subtitle,SMALL)
    return canvas


initial = Image.open(GAME/'01-initial.png').convert('RGB')
for name, rect, context in ITEMS:
    canvas = base(name.upper()+'  /  ART-GATE-02B',
                  'Blender isolated views; context and native phone pixels from Unity Mobile 1080x2340')
    crop = initial.crop(rect)
    top = Image.open(ISO/f'{name}-top.png')
    three = Image.open(ISO/f'{name}-three-quarter.png')
    rotated = Image.open(ISO/f'{name}-rotation-90.png')
    boxes = [(40+570*c,122+555*r,590+570*c,650+555*r) for r in range(2) for c in range(3)]
    tile(canvas,boxes[0],top,'Top-down / rotation 0')
    tile(canvas,boxes[1],three,'Three-quarter close')
    tile(canvas,boxes[2],rotated,'Top-down / rotation 90')
    tile(canvas,boxes[3],crop,'Real '+context+' context')
    tile(canvas,boxes[4],crop,'Native 1:1 phone crop',native=True)
    tile(canvas,boxes[5],initial,'Real Golden Lv1 full screen')
    canvas.save(OUT/f'{name}-review.png',optimize=True)


def comparison(left,right,title,left_label,right_label,dest):
    width,height=1200,1510
    canvas=base(title,'Unity Mobile 1080x2340; both screens shown at equal scale',width,height)
    for image,label,x in ((left,left_label,35),(right,right_label,610)):
        tile(canvas,(x,120,x+555,1490),image,label)
    canvas.save(OUT/dest,optimize=True)


comparison(Image.open(ROOT/'Builds/ui-slice-01-1/D-clean-1080x2340.png'),initial,
           'A  /  PROXIES BEFORE  vs  FINAL ITEMS','Approved UI with proxies','ART-GATE-02B real items',
           'A-proxies-before-vs-final.png')
comparison(Image.open(ROOT/'Builds/golden-lv1/style/style-frame-clean.png'),initial,
           'B  /  STYLE FRAME  vs  REAL GOLDEN LV1','STYLE-FRAME-01','Real Golden Lv1',
           'B-style-frame-vs-real.png')

family=base('C  /  SWEATER + FIVE FINAL ITEMS',
            'Neutral family sheet; sweater is the approved Unity crop, others are isolated Blender renders',
            1800,1190)
entries=[('Sweater',initial.crop((185,485,610,825)))] + \
        [(name,Image.open(ISO/f'{name}-top.png')) for name,_,_ in ITEMS]
for i,(name,image) in enumerate(entries):
    col,row=i%3,i//3
    tile(family,(40+585*col,125+525*row,600+585*col,630+525*row),image,name)
family.save(OUT/'C-family.png',optimize=True)

phone=base('D  /  NATIVE PHONE-SCALE ITEM CROPS',
           'Unscaled 1:1 pixels from the real Unity Mobile 1080x2340 initial frame',
           1600,1000)
for i,(name,rect,_) in enumerate(ITEMS):
    x=40+(i%3)*515; y=125+(i//3)*435
    tile(phone,(x,y,x+485,y+415),initial.crop(rect),name,native=True)
phone.save(OUT/'D-phone-scale-crops.png',optimize=True)

shots=sorted(GAME.glob('[0-1][0-9]-*.png'))
contact=base('E  /  REAL GOLDEN LV1 GAMEPLAY PROOF',
             '17 Unity Editor frames, Mobile quality; 1080x2340 except the marked 9:16 frame',
             1800,2280)
for i,shot in enumerate(shots):
    col,row=i%4,i//4
    x,y=40+440*col,125+425*row
    tile(contact,(x,y,x+420,y+400),Image.open(shot),shot.stem.replace('-',' '))
contact.save(OUT/'E-gameplay-contact-sheet.png',optimize=True)

print('Review sheets:',len(list(OUT.glob('*review.png')))+5)
