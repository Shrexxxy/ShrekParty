"""
Готовит картинку из ComfyUI для приложения: срезает белую/кремовую рамку вокруг хромакея,
вырезает фон (зелёный или пурпурный — определяет сам), обрезает пустые поля и уменьшает.

usage: python prep.py in.png out.png [size=256] [square=1]
  size   — длинная сторона результата;
  square — 1: дополнить до квадрата прозрачностью (иконки), 0: оставить пропорции (персонажи),
           frame: не обрезать по фигуре — все позы босса остаются в одной рамке и одного масштаба.
Нужны ffmpeg/ffprobe в PATH и key.py рядом.
"""
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
inp, out = sys.argv[1], sys.argv[2]
size = int(sys.argv[3]) if len(sys.argv) > 3 else 256
shape = sys.argv[4] if len(sys.argv) > 4 else '1'
square = shape == '1'
tmp = out + '.tmp.png'


def probe(path):
    w, h = subprocess.run(['ffprobe', '-v', 'error', '-select_streams', 'v:0', '-show_entries', 'stream=width,height', '-of', 'csv=p=0', path],
                          capture_output=True, text=True).stdout.strip().split(',')[:2]
    return int(w), int(h)


def rgb(path, w, h):
    return subprocess.run(['ffmpeg', '-v', 'error', '-i', path, '-vf', f'scale={w}:{h}', '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-'], capture_output=True).stdout


# Ищем поле хромакея на уменьшенной копии: какой цвет (зелёный/пурпурный) занимает больше и где его сплошные строки и столбцы.
W0, H0 = probe(inp)
w, h = 256, round(256 * H0 / W0)
raw = rgb(inp, w, h)
px = [(raw[3 * i], raw[3 * i + 1], raw[3 * i + 2]) for i in range(w * h)]
masks = {
    'green': [g - max(r, b) > 50 for r, g, b in px],
    'magenta': [min(r, b) - g > 50 for r, g, b in px],
}
mode = max(masks, key=lambda m: sum(masks[m]))
m = masks[mode]
rows = [y for y in range(h) if sum(m[y * w:(y + 1) * w]) > 0.5 * w]
cols = [x for x in range(w) if sum(m[y * w + x] for y in range(h)) > 0.5 * h]
x0, x1, y0, y1 = min(cols), max(cols), min(rows), max(rows)
inset = 3  # отступ внутрь поля, чтобы не захватить край рамки
sx, sy = W0 / w, H0 / h
cx0, cy0 = int((x0 + inset) * sx), int((y0 + inset) * sy)
cw, ch = int((x1 - x0 - 2 * inset) * sx), int((y1 - y0 - 2 * inset) * sy)
work = 512
scale = f'scale={work}:-2' if cw >= ch else f'scale=-2:{work}'
subprocess.run(['ffmpeg', '-v', 'error', '-y', '-i', inp, '-vf', f'crop={cw}:{ch}:{cx0}:{cy0},{scale}:flags=lanczos', tmp], check=True)
subprocess.run([sys.executable, os.path.join(HERE, 'key.py'), tmp, tmp, mode], check=True, capture_output=True)

if shape == 'frame':
    subprocess.run(['ffmpeg', '-v', 'error', '-y', '-i', tmp, '-vf', f'scale={size}:-2:flags=lanczos,format=rgba', out], check=True)
    os.remove(tmp)
    print(f'{out}: {mode}, рамка целиком')
    sys.exit(0)

# Пустые поля прочь, затем — до нужного размера.
kw, kh = probe(tmp)
a = subprocess.run(['ffmpeg', '-v', 'error', '-i', tmp, '-f', 'rawvideo', '-pix_fmt', 'rgba', '-'], capture_output=True).stdout
ys = [y for y in range(kh) if any(a[4 * (y * kw + x) + 3] > 24 for x in range(0, kw, 2))]
xs = [x for x in range(kw) if any(a[4 * (y * kw + x) + 3] > 24 for y in range(0, kh, 2))]
bx0, bx1, by0, by1 = min(xs), max(xs), min(ys), max(ys)
bw, bh = bx1 - bx0 + 1, by1 - by0 + 1
pad = max(bw, bh) // 25
vf = f'crop={bw}:{bh}:{bx0}:{by0}'
if square:
    side = max(bw, bh) + 2 * pad
    vf += f',pad={side}:{side}:(ow-iw)/2:(oh-ih)/2:color=0x00000000,scale={size}:{size}:flags=lanczos'
else:
    vf += f',scale={size}:-2:flags=lanczos' if bw >= bh else f',scale=-2:{size}:flags=lanczos'
subprocess.run(['ffmpeg', '-v', 'error', '-y', '-i', tmp, '-vf', vf + ',format=rgba', out], check=True)
os.remove(tmp)
print(f'{out}: {mode}, поле {cw}x{ch}, фигура {bw}x{bh}')
