"""
Вырезает ровный нехромакейный фон (белый, серый, чёрный): цвет фона берётся по краю кадра,
фоном считается всё близкое к нему и связанное с краем — заливка от краёв, поэтому белое или тёмное
внутри фигуры не трогается: его отделяет тёмно-синий контур. На границе — мягкая альфа.

usage: python key_flood.py in.png out.png [допуск=40]
"""
import statistics
import subprocess
import sys
from collections import deque

inp, out = sys.argv[1], sys.argv[2]
T = int(sys.argv[3]) if len(sys.argv) > 3 else 40
OUTLINE = (10, 24, 36)  # #0a1824

w, h = map(int, subprocess.run(['ffprobe', '-v', 'error', '-select_streams', 'v:0', '-show_entries', 'stream=width,height', '-of', 'csv=p=0', inp],
                               capture_output=True, text=True).stdout.strip().split(',')[:2])
raw = subprocess.run(['ffmpeg', '-v', 'error', '-i', inp, '-f', 'rawvideo', '-pix_fmt', 'rgba', '-'], capture_output=True).stdout
px = [raw[4 * i:4 * i + 4] for i in range(w * h)]
edge = list(range(w)) + list(range(w * (h - 1), w * h)) + [y * w for y in range(h)] + [y * w + w - 1 for y in range(h)]
bgc = tuple(int(statistics.median(px[i][c] for i in edge)) for c in range(3))


def dist(p):
    if p[3] < 20:  # уже прозрачное — тоже фон
        return 0
    return max(abs(p[c] - bgc[c]) for c in range(3))


d = [dist(p) for p in px]
bg = bytearray(w * h)
q = deque(i for i in edge if d[i] <= T)
for i in q:
    bg[i] = 1
while q:
    i = q.popleft()
    x, y = i % w, i // w
    for j in (i - 1 if x else -1, i + 1 if x < w - 1 else -1, i - w if y else -1, i + w if y < h - 1 else -1):
        if j >= 0 and not bg[j] and d[j] <= T:
            bg[j] = 1
            q.append(j)

band = bytearray(bg)
for _ in range(2):
    nb = bytearray(band)
    for i in range(w * h):
        if band[i]:
            continue
        x, y = i % w, i // w
        if (x and band[i - 1]) or (x < w - 1 and band[i + 1]) or (y and band[i - w]) or (y < h - 1 and band[i + w]):
            nb[i] = 1
    band = nb

o = bytearray(w * h * 4)
for i in range(w * h):
    if bg[i]:
        continue
    r, g, b, a = px[i]
    if band[i] and d[i] < 3 * T:
        # Полупрозрачный край — цвет контура, без светлой/тёмной каймы от старого фона.
        a = min(a, max(0, (d[i] - T) * 255 // (2 * T)))
        r, g, b = OUTLINE
    o[4 * i:4 * i + 4] = bytes((r, g, b, a))
subprocess.run(['ffmpeg', '-v', 'error', '-y', '-f', 'rawvideo', '-pix_fmt', 'rgba', '-s', f'{w}x{h}', '-i', '-', '-pix_fmt', 'rgba', out], input=bytes(o), check=True)
print(f'{out}: фон {bgc}, вырезано {sum(bg) / (w * h):.2f}')
