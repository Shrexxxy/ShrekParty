# Задники для боёв с боссами

Задник стоит на ТВ за ареной: слева строй драконоосликов, справа босс, сверху полоса здоровья, снизу описание хода.
Поэтому задник должен **не мешать** персонажам:

- без людей и животных — только место;
- спокойный и чуть приглушённый: не ярче персонажей, без мелкой пестроты по всему полю;
- главный объект места (пень-дом, башня, сцена) — **в глубине по центру или чуть правее**, не во весь экран;
- нижняя треть — ровная «земля» или пол, на ней стоят персонажи;
- тот же постерный стиль с тёмно-синими контурами, что у персонажей. Иначе персонажи будут выглядеть вырезанными и вклеенными.

Я положу поверх лёгкое затемнение, так что чуть светлее, чем нужно, — не страшно.

## Как делать

**Воркфлоу 01** (Z-Image Turbo), размер в EmptySD3LatentImage — **1344 × 768** (16:9, как ТВ).
Делай 3 варианта (seed), выбирай. Фон **не вырезать**, хромакей не нужен.

Готовое → `web/public/assets/boss/bg/<босс>.jpg`: `peasant.jpg`, `farquaad.jpg`, `fairy.jpg`. Можно png, я пережму в jpg.

Промт = **BG_STYLE + место + BG_END**.

**BG_STYLE:**
```
Wide flat vector fairground poster illustration of an empty fairy-tale location, background scenery for a cartoon game, vintage fairground poster style, bold flat colors in large calm shapes, thick dark navy outline #0a1824 around shapes, simple clean shapes, minimal shading, slightly muted colors.
```

**BG_END:**
```
The lower third of the image is an open flat empty ground where characters will stand, nothing on it in the center. No people, no characters, no animals, no creatures. No text, no letters, no logo, no watermark, no frame, no border.
```

## Крестьянин — болото у пня-дома

```
A misty swamp clearing at warm late-afternoon light. In the back, slightly right of center, a huge hollow old tree stump turned into a cozy house: a round wooden door, a small yellow-lit window, a thick roof of long shaggy grass hanging over it, twisted roots like legs around it. Tall dark mossy swamp trees with hanging willow branches frame the left and right edges. Soft beams of light fall through the haze, a few floating fireflies. Murky green water and reeds at the far sides, a few rotting wooden stumps and a small wooden sign in the foreground corners. Flat sandy-brown ground in front. Palette: deep swamp greens, olive, mossy teal, warm lamp yellow #ffd166 light, dark navy shadows.
```

Если выходит слишком тёмным — замени `late-afternoon` на `golden morning`.

## Лорд Фаркуад — Дюлок

```
A neat perfect fairy-tale kingdom on a sunny day seen from a distance across a field. In the center back stands one enormously tall, plain, square stone castle tower with small battlements on top, comically too tall for the town. Around its base a tidy walled town of identical small houses with pointed roofs and a long low stone wall with little square towers. Rolling gentle green hills on the horizon, a few flat puffy clouds in a soft sky blue #9cc3e6 sky. In front: perfectly even striped fields in yellow-green and a flat beige dirt square. Everything looks too clean and too symmetrical. Palette: sandy beige stone, soft sky blue, fresh yellow-green, cream #fff6df, dark navy outlines.
```

Если небо слишком яркое и спорит с полосой здоровья — добавь `soft evening light, slightly faded sky`.

## Фея-крёстная — королевский бальный зал

```
A grand royal ballroom of a fairy-tale kingdom prepared for a pop-star show at night. In the back a huge glowing arched stage shaped like a giant shell, with rows of sparkling light bulbs along the arch, purple and pink spotlights, a few big floating soap-bubble lights and a giant golden gong at the side. Tall marble columns with gold tops and heavy red velvet curtains with gold tassels frame the left and right edges. Crystal chandeliers hang from the ceiling. In front a wide polished checkered dance floor in cream and deep purple, empty. Palette: deep purple and violet, magenta-pink spotlights, gold, red velvet, cream, dark navy outlines.
```

Если пол выходит слишком пёстрым — замени `checkered dance floor` на `plain polished marble floor`.

## Вариант Б: перерисовать твои референсы

**Воркфлоу 02** (Qwen Edit). В **image_1** — кадр-референс, вторую Load Image отключи (Ctrl+B).
Размер задаёт **`resolution`** на синем блоке, а не width/height. Это общий бюджет пикселей: при 0 картинка выходит размером с кадр (1280×704). Поставь **1440** — выйдет около 1920×1088 (≈ 2 Мп, пропорции от кадра). `custom_size` оставь выключенным: width/height работают только с ним, и тогда правка может съехать.
Улучшатель промта (`refine_prompt`) — выключен.

Промт = **REDRAW + что убрать/поправить в этой сцене + REDRAW_END**.

**REDRAW:**
```
Redraw image 1 as a flat vector fairground poster illustration, keeping the same location, layout, composition, camera angle and main objects. Vintage fairground poster style: bold flat colors in large calm shapes, thick dark navy outline #0a1824 around shapes, simple clean shapes, minimal shading, no realistic textures, no 3D rendering, slightly muted colors.
```
**REDRAW_END:**
```
Remove all people, characters and animals completely, leave the place empty. The ground in the front is open and empty. No text, no letters, no logo, no watermark, no frame, no border.
```

Что вставить посередине:

| Босс | Кадр | Середина промта |
|---|---|---|
| Крестьянин | болото с пнём-домом | `Keep the hollow tree stump house with the grass roof, the twisted roots, the hanging swamp trees and the light beams through the mist. Make the light a bit warmer and brighter, so the scene is not too dark.` |
| Фаркуад | Дюлок | `Keep the very tall square stone tower, the small walled town around it, the striped fields, the green hills and the clouds. Make the sky a softer, less saturated blue.` |
| Фея | концертная сцена | `Keep the giant glowing arched stage with light bulbs, the spotlights, the floating bubble lights and the gongs. Remove the band, the singers and the whole crowd of spectators: the stage and the front area are empty. Turn the dark audience area in front into an empty polished royal ballroom floor.` |

Что может пойти не так:
- Люди не пропали (особенно толпа у Феи) — прогони результат ещё раз через 02 с промтом `Edit image 1. Remove all remaining people and figures, fill their place with the same floor and background. Keep everything else exactly the same.`
- Осталось «по-фотографически» — добавь в конец `Flat 2D illustration only, flat color fills, no gradients, no realistic lighting.`
- Сцена у Феи уехала в сторону, как на кадре, — это нормально: справа на ТВ всё равно стоит босс, сцена слева-по центру даже лучше.

## Если стиль уплывает в 3D или фото

Добавь в конец промта: `Flat 2D illustration only, no 3D render, no photorealism, no gradients except soft sky.`

Если главный объект огромный во весь экран — добавь `seen from far away, small in the distance`.
