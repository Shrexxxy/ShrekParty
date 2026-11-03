# Боссы: позы на каждой стадии здоровья

Сейчас все ходы, удар и победа нарисованы со стадии 1 (целый босс). Если босс уже в стадии 2 или 3, на ходу он «вылечивается».
Нужно повторить их с ранами своей стадии.

## Имена файлов

Стадия 1 — файлы, которые уже есть, без суффикса. Стадии 2 и 3 — суффиксы `-2` и `-3`:

```
move-poke.png      ← стадия 1 (есть)
move-poke-2.png    ← стадия 2 (раны как на hp2)
move-poke-3.png    ← стадия 3 (раны как на hp3)
```

Клади в ту же папку `web/public/assets/boss/<босс>/`. Фон не вырезай — вырежу сам.

## Как делать — воркфлоу 02, две картинки на входе

- **image_1** — поза, которую сохраняем: например `move-poke.png`.
- **image_2** — стадия, с которой берём раны: `hp2.png` или `hp3.png`.

Обе — те, что ты нагенерил, на пурпурном фоне. Вторая нода Load Image в воркфлоу как раз для image_2: включи её (Ctrl+B), если отключал.

Промт = **POSE_KEEP + раны своей стадии из таблицы ниже + BG**.

**POSE_KEEP:**
```
Edit image 1. Keep exactly the pose, gesture, facial expression, props and composition of the character in image 1, the same size and position in the frame, facing left, and the same flat vector fairground poster style with thick dark navy outline. Apply the same battle damage as on the character in image 2: 
```
**BG:**
```
Do not copy the pose from image 2. Do not change the art style. Plain solid flat pure bright magenta #ff00ff background, completely uniform: no scenery, no shadow, no floor, no sparkles, no frame, no text.
```

### Раны по стадиям

Описание сделано по твоим hp2 и hp3, вставляй сразу после POSE_KEEP.

| Босс | Стадия 2 | Стадия 3 |
|---|---|---|
| Крестьянин | `slightly ruffled hair and mustache, the torch flame is smaller, a little dirt on the green tunic.` | `wild messy hair, the green tunic torn into ragged strips with holes, dirt and scratches, a tired miserable face, the torch is only smoking with a thin grey smoke curl instead of a flame.` |
| Фаркуад | `the black hair is messy with strands sticking out, a small tear at the bottom of the red cape, an annoyed frown.` | `a white bandage wrapped around his head, the hat crooked, the red cape torn into long ragged strips, scratches on his face.` |
| Фея | `the glasses crooked on her nose, the lavender hairdo slightly lopsided, an offended pout.` | `the lavender hairdo a wild messy explosion, the glasses cracked, the hem of the red gown torn and ragged, smudged makeup.` |

Если модель не дорисовывает раны (их видно плохо) — повтори с другим seed или добавь в конец `The damage must be clearly visible.`

## Что сгенерить

Отмечай по мере готовности. Всего 37.

### Крестьянин (`peasant/`) — 12

| Файл | image_1 | image_2 |
|---|---|---|
| ☐ `move-poke-2.png` | move-poke | hp2 |
| ☐ `move-poke-3.png` | move-poke | hp3 |
| ☐ `move-swing-2.png` | move-swing | hp2 |
| ☐ `move-swing-3.png` | move-swing | hp3 |
| ☐ `move-kvass-2.png` | move-kvass | hp2 |
| ☐ `move-kvass-3.png` | move-kvass | hp3 |
| ☐ `move-callneighbours-2.png` | move-callneighbours | hp2 |
| ☐ `move-callneighbours-3.png` | move-callneighbours | hp3 |
| ☐ `hurt-2.png` | hurt | hp2 |
| ☐ `hurt-3.png` | hurt | hp3 |
| ☐ `victory-2.png` | victory | hp2 |
| ☐ `victory-3.png` | victory | hp3 |

### Лорд Фаркуад (`farquaad/`) — 12

| Файл | image_1 | image_2 |
|---|---|---|
| ☐ `move-sword-2.png` | move-sword | hp2 |
| ☐ `move-sword-3.png` | move-sword | hp3 |
| ☐ `move-volley-2.png` | move-volley | hp2 |
| ☐ `move-volley-3.png` | move-volley | hp3 |
| ☐ `move-shield-2.png` | move-shield | hp2 |
| ☐ `move-shield-3.png` | move-shield | hp3 |
| ☐ `move-windup-2.png` | move-windup | hp2 |
| ☐ `move-windup-3.png` | move-windup | hp3 |
| ☐ `hurt-2.png` | hurt | hp2 |
| ☐ `hurt-3.png` | hurt | hp3 |
| ☐ `victory-2.png` | victory | hp2 |
| ☐ `victory-3.png` | victory | hp3 |

### Фея-крёстная (`fairy/`) — 13

| Файл | image_1 | image_2 |
|---|---|---|
| ☐ `move-wand-2.png` | move-wand | hp2 |
| ☐ `move-wand-3.png` | move-wand | hp3 |
| ☐ `move-stars-2.png` | move-stars | hp2 |
| ☐ `move-stars-3.png` | move-stars | hp3 |
| ☐ `move-pumpkin-2.png` | move-pumpkin | hp2 |
| ☐ `move-pumpkin-3.png` | move-pumpkin | hp3 |
| ☐ `move-potion-2.png` | move-potion | hp2 |
| ☐ `move-potion-3.png` | move-potion | hp3 |
| ☐ `hurt-2.png` | hurt | hp2 |
| ☐ `hurt-3.png` | hurt | hp3 |
| ☐ `victory-2.png` | victory | hp2 |
| ☐ `victory-3.png` | victory | hp3 |
| ☐ `defeated.png` | **hp3** (одна картинка, воркфлоу 02 как обычно) | — |

Промт для `fairy/defeated.png` (картинка одна, поэтому KEEP из `prompts.md`, а не POSE_KEEP):
```
Edit image 1. Keep exactly the same character: same face, hairstyle, outfit, colors, proportions and the same flat vector fairground poster style with thick dark navy outline. She is knocked out: sitting slumped on the ground with legs stretched out, the wand dropped beside her, eyes as two crosses, tongue out, small cartoon stars circling above her head. Plain solid flat pure bright magenta #ff00ff background, completely uniform: no scenery, no shadow, no floor, no frame, no text.
```

## Что тогда будет на ТВ

- Стоит: `hp1` / `hp2` / `hp3` по здоровью (больше 66 %, 34–66 %, 33 % и меньше).
- Получил удар: `hurt` своей стадии.
- Делает ход: `move-<ход>` своей стадии.
- Победил команду: `victory` своей стадии.
- Побеждён: `defeated`.

Если какого-то файла стадии нет, возьмётся та же картинка со стадии ниже, так что подключать можно, не дожидаясь всех 37.
