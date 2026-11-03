# Тапалка: питомцы машут крыльями + полоса неба

В тапалке летит питомец каждого игрока. У него 4 кадра:

| Кадр | Файл | Что на нём |
|---|---|---|
| 0 — покой | `fighter-1.png` | обычная картинка, **уже есть** |
| 1 — взмах | `fighter-1-fly-1.png` | крылья подняты высоко вверх, начало взмаха |
| 2 — крыло летит | `fighter-1-fly-2.png` | крылья раскинуты в стороны, середина взмаха |
| 3 — конец взмаха | `fighter-1-fly-3.png` | крылья опущены вниз, под тело |

Каждый тап — один взмах: кадры 1 → 2 → 3 (по 60 мс) и обратно в покой. Тапаешь часто — взмахи идут подряд без остановки, питомец ровно машет. Перестал — успокаивается (больше трёх взмахов вперёд не копится). На ТВ так же, по приросту тапов.
Нет файла кадра — на его месте обычная картинка, так что кадры можно докладывать постепенно.

## 1. Кадры взмаха — 13 питомцев × 3 = 39 картинок (воркфлоу 02)

**image_1** — исходник питомца на хромакее, уже лежат в ComfyUI → input: `pet-fighter-1.png` … `pet-tank-4.png` (копии в `comfy/source/pets/`).
⚠️ Не подавай картинки из `web/public/assets/pets/` — у них прозрачный фон, ComfyUI сделает его чёрным.
Все три кадра делай **из исходника** (не кадр из кадра) — так питомец не «поплывёт».
Вторая Load Image — отключена (Ctrl+B). Один и тот же seed на все три кадра одного питомца — кадры выйдут похожее.

**Промт** = WING_KEEP + строка кадра + WING_END.

**WING_KEEP:**
```
Edit image 1. Keep exactly the same character: same face, ears, outfit, weapon or props, colors, proportions, the same size and position in the frame, facing left, and the same flat vector fairground poster style with thick dark navy outline. The character is flying: body lifted slightly, legs tucked up. Change only the wings:
```
**Строка кадра:**
- кадр 1 (`-fly-1`): `both dragon wings are raised high straight up above the back, wing tips pointing to the sky, at the start of a wing beat.`
- кадр 2 (`-fly-2`): `both dragon wings are spread wide straight out to the sides, fully open and horizontal, in the middle of a wing beat.`
- кадр 3 (`-fly-3`): `both dragon wings are swept down below the body, wing tips pointing to the ground, at the end of a wing beat.`

**WING_END** (подставь фон из таблицы):
```
Both wings stay fully visible. Do not change the art style. Plain solid flat pure bright <ФОН> background, completely uniform: no scenery, no shadow, no floor, no sparkles, no motion lines, no frame, no text.
```

| Исходник | `<ФОН>` | Готовые файлы |
|---|---|---|
| ☐ `pet-fighter-1.png` | chroma green #00ff00 | `fighter-1-fly-1.png`, `-fly-2`, `-fly-3` |
| ☐ `pet-fighter-2.png` | chroma green #00ff00 | `fighter-2-fly-1…3.png` |
| ☐ `pet-fighter-3.png` | chroma green #00ff00 | `fighter-3-fly-1…3.png` |
| ☐ `pet-fighter-4.png` | chroma green #00ff00 | `fighter-4-fly-1…3.png` |
| ☐ `pet-fighter-5.png` | chroma green #00ff00 | `fighter-5-fly-1…3.png` |
| ☐ `pet-mage-1.png` | **magenta #ff00ff** | `mage-1-fly-1…3.png` |
| ☐ `pet-mage-2.png` | chroma green #00ff00 | `mage-2-fly-1…3.png` |
| ☐ `pet-mage-3.png` | **magenta #ff00ff** | `mage-3-fly-1…3.png` |
| ☐ `pet-mage-4.png` | **magenta #ff00ff** | `mage-4-fly-1…3.png` |
| ☐ `pet-tank-1.png` | chroma green #00ff00 | `tank-1-fly-1…3.png` |
| ☐ `pet-tank-2.png` | chroma green #00ff00 | `tank-2-fly-1…3.png` |
| ☐ `pet-tank-3.png` | chroma green #00ff00 | `tank-3-fly-1…3.png` |
| ☐ `pet-tank-4.png` | chroma green #00ff00 | `tank-4-fly-1…3.png` |

Готовое клади в `web/public/assets/pets/` с этими именами, фон не вырезай — вырежу сам.

Что может пойти не так:
- Модель перерисовала всего питомца, а не крылья — добавь `Only the wings change, everything else stays pixel-identical.`
- Кадры 1 и 3 похожи — в кадре 1 добавь `the wings form a V shape above the head`, в кадре 3 — `the wings hang down like a closed umbrella`.
- Щит или оружие закрывают крыло — это нормально, главное, чтобы видно было второе.

## 2. Полоса неба (воркфлоу 01)

Длинная картинка, которая едет за питомцами: на ТВ — фон каждой дорожки, на телефоне — фон во весь экран. Повторяется по горизонтали, поэтому левый и правый край должны стыковаться.

Размер: **2048 × 384** (Z-Image хуже держит совсем узкие полосы вроде 2500×250; я сам уменьшу до нужной высоты).
Сделай 3 варианта.

```
flat vector fairground poster illustration, vintage fairground poster style, bold flat colors in large calm shapes, thick dark navy outline #0a1824 around shapes, simple clean shapes, minimal shading. A very wide seamless horizontal panorama of a sky for a flying game: soft evening sky with gentle horizontal bands of sky blue #9cc3e6, cream #fff6df and warm coral #ef6f5e near the bottom, a row of flat round fluffy cream clouds of different sizes evenly spread along the whole width, a thin line of distant dark green swamp treetops at the very bottom edge. The image tiles seamlessly left to right: the left and right edges match. No sun, no moon, no birds, no characters, no buildings in the middle, nothing large. No text, no letters, no logo, no watermark, no frame, no border.
```

Готовое → `web/public/assets/tapper/strip.jpg` (можно png, пережму).

Если стык виден — не страшно, скажи, я сделаю зеркальный повтор.

## 3. Полоса неба для телефона — вертикальная (воркфлоу 01)

На телефоне питомец набирает высоту: фон повторяется **по вертикали** и едет вниз с каждым тапом.
Поэтому верх и низ должны стыковаться, а значит — **никаких полос и градиентов сверху вниз** (на ТВ-полосе они горизонтальные, тут бы получились ступеньки на стыке). Небо одного тона, облака раскиданы равномерно.

Размер: **768 × 1920**. Сделай 3 варианта.

```
flat vector fairground poster illustration, vintage fairground poster style, bold flat colors in large calm shapes, thick dark navy outline #0a1824 around shapes, simple clean shapes, minimal shading. A tall seamless vertical sky for a flying game, seen while flying upward: an even soft sky blue #9cc3e6 sky of one single tone from top to bottom, flat round fluffy cream #fff6df clouds of different sizes spread evenly over the whole height, some clouds cut by the left and right edges, a few small faint lamp yellow #ffd166 sparkles. The image tiles seamlessly top to bottom: the top and bottom edges match. No horizon, no ground, no trees, no sun, no moon, no birds, no characters, no color bands, no gradient. No text, no letters, no logo, no watermark, no frame, no border.
```

Готовое → `web/public/assets/tapper/strip-vertical.jpg`. Про рамку: если модель обведёт картинку тонкой линией по краю — не страшно, срежу (на ТВ-полосе так и было).

## 4. Перегенерировать (кадры не того размера)

Эти кадры заметно мельче покоя и соседних кадров — при быстром взмахе питомец будет «пульсировать». Сделай заново тем же промтом, из исходника, и добавь в конец WING_KEEP: `The character stays exactly the same size as in image 1.`

| Кадр | Что не так |
|---|---|
| ☐ `fighter-1-fly-3.png` | нет файла |
| ☐ `fighter-1-fly-2.png` | питомец мельче примерно на треть |
| ☐ `fighter-3-fly-1.png` | мельче |
| ☐ `mage-3-fly-3.png` | мельче |
| ☐ `tank-2-fly-3.png` | мельче (и был на сером градиенте вместо хромакея) |
| ☐ `tank-3-fly-3.png` | мельче почти вдвое |

И общее: 7 кадров пришли на **белом** фоне вместо хромакея (`mage-1-fly-1`, `mage-3-fly-1`, `tank-1-fly-1`, `tank-1-fly-3`, `tank-3-fly-1`, `tank-3-fly-2`, `tank-4-fly-1`). Я вырезал и их (`comfy/tools/key_flood.py`), но белый фон режется хуже — проверь, что в промте в конце стоит именно `chroma green #00ff00` / `magenta #ff00ff`.
