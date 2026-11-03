"""
Вырезает чёрный фон (когда в ComfyUI подали картинку с прозрачностью — Load Image делает её чёрной).
Фон — всё почти чёрное, что связано с краем кадра: заливка от краёв, поэтому тёмное внутри фигуры
(волосы, тени) не трогается — его отделяет тёмно-синий контур. На границе — мягкая альфа по яркости.

usage: python key_black.py in.png out.png [порог=14]
"""
import subprocess
import sys
from collections import deque

inp, out = sys.argv[1], sys.argv[2]
T = int(sys.argv[3]) if len(sys.argv) > 3 else 14
OUTLINE = 36  # самый яркий канал контура #0a1824

w, h = map(int, subprocess.run(['ffprobe', '-v', 'error', '-select_streams', 'v:0', '-show_entries', 'stream=width,height', '-of', 'csv=p=0', inp],
                               capture_output=True, text=True).stdout.strip().split(',')[:2])
raw = subprocess.run(['ffmpeg', '-v', 'error', '-i', inp, '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-'], capture_output=True).stdout
m = [max(raw[3 * i], raw[3 * i + 1], raw[3 * i + 2]) for i in range(w * h)]

bg = bytearray(w * h)
q = deque(i for i in list(range(w)) + list(range(w * (h - 1), w * h)) + [y * w for y in range(h)] + [y * w + w - 1 for y in range(h)] if m[i] <= T)
for i in q:
    bg[i] = 1
while q:
    i = q.popleft()
    x, y = i % w, i // w
    for j in (i - 1 if x else -1, i + 1 if x < w - 1 else -1, i - w if y else -1, i + w if y < h - 1 else -1):
        if j >= 0 and not bg[j] and m[j] <= T:
            bg[j] = 1
            q.append(j)

# Полоса 2 px вокруг фона — мягкий край по яркости.
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
    r, g, b = raw[3 * i], raw[3 * i + 1], raw[3 * i + 2]
    a = 255
    if band[i] and m[i] < OUTLINE:
        a = max(0, min(255, (m[i] - T) * 255 // (OUTLINE - T)))
        r, g, b = 10, 24, 36  # край — цвет контура, без чёрной каймы
    o[4 * i:4 * i + 4] = bytes((r, g, b, a))
subprocess.run(['ffmpeg', '-v', 'error', '-y', '-f', 'rawvideo', '-pix_fmt', 'rgba', '-s', f'{w}x{h}', '-i', '-', '-pix_fmt', 'rgba', out], input=bytes(o), check=True)
print(f'{out}: фон {sum(bg) / (w * h):.2f}')
