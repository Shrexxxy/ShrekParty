import sys, subprocess, statistics
# usage: key.py in.png out.png green|magenta [hard=0.55] [soft=0.15]
# Фон = пиксели с сильным доминированием хромы (доля от медианы по краю); мягкая альфа только в 2px-полосе у фона,
# чтобы цвета внутри персонажа (бордовый на маджента-фоне) не становились полупрозрачными.
inp, out, mode = sys.argv[1], sys.argv[2], sys.argv[3]
hk = float(sys.argv[4]) if len(sys.argv) > 4 else 0.55
sk = float(sys.argv[5]) if len(sys.argv) > 5 else 0.15
probe = subprocess.run(["ffprobe", "-v", "error", "-select_streams", "v:0", "-show_entries", "stream=width,height", "-of", "csv=p=0", inp], capture_output=True, text=True).stdout.strip().split(",")
W, H = int(probe[0]), int(probe[1])
raw = subprocess.run(["ffmpeg", "-v", "error", "-i", inp, "-f", "rawvideo", "-pix_fmt", "rgb24", "-"], capture_output=True).stdout
N = W * H
r = raw[0::3]; g = raw[1::3]; b = raw[2::3]
if mode == "green":
    D = [g[i] - max(r[i], b[i]) for i in range(N)]
else:
    D = [min(r[i], b[i]) - g[i] for i in range(N)]
border = [D[i] for i in range(W)] + [D[(H - 1) * W + i] for i in range(W)] + [D[y * W] for y in range(H)] + [D[y * W + W - 1] for y in range(H)]
Dbg = statistics.median(border)
hard, soft = Dbg * hk, Dbg * sk
bg = bytearray(1 if d >= hard else 0 for d in D)
# полоса 2px вокруг фона
band = bytearray(bg)
for _ in range(2):
    nb = bytearray(band)
    for y in range(H):
        row = y * W
        for x in range(W):
            i = row + x
            if band[i]: continue
            if (x > 0 and band[i-1]) or (x < W-1 and band[i+1]) or (y > 0 and band[i-W]) or (y < H-1 and band[i+W]):
                nb[i] = 1
    band = nb
o = bytearray(N * 4)
semi = 0
for i in range(N):
    R, G, B = r[i], g[i], b[i]
    if bg[i]:
        a = 0
    elif band[i]:
        d = D[i]
        a = 255 if d <= soft else int(max(0, min(255, 255 - (d - soft) * 255 / (hard - soft))))
        if d > 0:
            if mode == "green": G = min(G, max(R, B))
            else: R -= d; B -= d
        if 0 < a < 255: semi += 1
    else:
        a = 255
    o[4*i:4*i+4] = bytes((R, G, B, a)) if a else b"\0\0\0\0"
subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "rawvideo", "-pix_fmt", "rgba", "-s", f"{W}x{H}", "-i", "-", "-pix_fmt", "rgba", out], input=bytes(o), check=True)
corners = [o[3], o[4*(W-1)+3], o[4*(N-W)+3], o[4*(N-1)+3]]
opaque = sum(1 for i in range(N) if o[4*i+3] == 255)
print(f"{out}: Dbg={Dbg} corners={corners} opaque={opaque/N:.3f} semi={semi}")
