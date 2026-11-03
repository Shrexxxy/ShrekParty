# Промты

Все промты на английском: модели понимают его лучше. Русское — только пояснения.
Какой воркфлоу брать, написано у каждого раздела: **01** — новая картинка, **02** — правка готовой, **03** — анимация.

## Общие куски (копируй как есть)

**STYLE** — стиль всего приложения:
```
flat vector fairground poster illustration, vintage fairground poster style, bold flat colors, thick dark navy outline #0a1824 around every shape, simple clean shapes, minimal shading, readable at small size, cartoon.
```

**ICON** — один предмет-иконка:
```
A single bold drawing of one object, centered, large, filling most of the image, drawn directly on the background with only the dark navy outline around it.
```

**BG_GREEN** — фон-хромакей. Если в объекте есть зелёное, бери BG_MAGENTA:
```
Plain solid flat pure bright chroma green #00ff00 background, completely uniform and empty: no sparkles, no stars or particles, no ground, no shadow, no floor ellipse, no gradient, no scenery, no frame, no border, no white outline. No text, no letters, no logo, no watermark. No green on the object.
```
```
Plain solid flat pure bright magenta #ff00ff background, completely uniform and empty: no sparkles, no ground, no shadow, no gradient, no scenery, no frame, no border. No text, no letters, no logo, no watermark. No pink, no purple, no magenta on the object.
```

**KEEP** — начало любой правки босса (воркфлоу 02):
```
Edit image 1. Keep exactly the same character: same face, hairstyle, outfit, colors, proportions and the same flat vector fairground poster style with thick dark navy outline. Keep the same size and position in the frame, the character faces left. Do not change the art style. Do not rotate the character.
```

**BG_KEEP** — конец любой правки босса (подставь цвет фона исходника: Крестьянин — magenta `#ff00ff`, остальные — green `#00ff00`):
```
Plain solid flat pure bright chroma green #00ff00 background, completely uniform: no scenery, no shadow, no floor, no sparkles, no frame, no text.
```

Промт правки собирается так: **KEEP + строка из таблицы + BG_KEEP**.

---

## 1. Боссы: модельки по здоровью, удары, ходы (воркфлоу 02)

Исходники уже лежат в ComfyUI → input: `farquaad-hp100.png`, `fairy-hp100.png`, `peasant-hp100.png`. Это текущие картинки на хромакей-фоне, копии в `comfy/source/`.

Что нужно на каждого босса и куда класть готовое (после вырезания фона):

| Файл | Когда показывается | Из чего делать |
|---|---|---|
| `boss/<босс>/hp1.png` | здоровье 100–67 % | это текущая картинка, уже есть |
| `boss/<босс>/hp2.png` | 66–34 % | из hp1 |
| `boss/<босс>/hp3.png` | 33 % и меньше | из **hp2**, чтобы раны копились |
| `boss/<босс>/hurt1.png`, `hurt2.png`, `hurt3.png` | получил удар (на своей стадии) | из hp1 / hp2 / hp3 |
| `boss/<босс>/move-<ход>.png` | делает ход | из hp1 |
| `boss/<босс>/defeated.png` | побеждён | из hp3 |
| `boss/<босс>/victory.png` | команда проиграла | из hp1 |

`<босс>` — `peasant`, `farquaad`, `fairy`. Пути от `web/public/assets/`.

### Общие строки для всех боссов

| Что | Строка изменения |
|---|---|
| hurt | `He is hit hard: recoiling backwards, leaning away to the right, eyes squeezed shut, mouth wide open in a yell, arms flung up, a few short navy motion lines around him.` (для Феи — `She`) |
| victory | `He is laughing triumphantly: head thrown back, mouth wide open, one fist raised high, the other hand on his belly.` |
| defeated | `He is knocked out: sitting slumped on the ground with legs stretched out, eyes as two crosses, tongue out, small cartoon stars circling above his head.` |

### Лорд Фаркуад (`farquaad`, фон зелёный)

| Файл | Строка изменения |
|---|---|
| hp2 | `He is lightly battered: his tall red hat is knocked crooked, the black bob hair is messy with a few strands sticking out, a small tear at the bottom of the cape, a small red bump on the forehead, an annoyed offended frown.` |
| hp3 | `He is badly battered: the cape is torn into rags, a white bandage wrapped around his head, a purple black eye, the hat crumpled and bent, the tunic ripped at one sleeve, drops of sweat, he holds his side with one hand, scared but furious face.` |
| move-sword | `He lunges forward to the left holding a long silver sword with a gold hilt in both hands, the blade pointing forward, front leg bent, determined angry face.` |
| move-volley | `He stands proudly, pointing forward to the left with one outstretched arm and a gloved finger, giving an order, mouth open shouting, the other hand on his hip.` |
| move-shield | `He crouches behind a big red heater shield with a gold rim and a gold crown emblem, only his eyes and the top of his hat peek over the shield.` |
| move-windup | `He winds up for a mighty strike: a long silver sword pulled far back behind his head with both hands, body twisted, teeth gritted, eyes narrowed.` |

### Фея-крёстная (`fairy`, фон зелёный)

| Файл | Строка изменения |
|---|---|
| hp2 | `She is lightly battered: the lavender beehive hairdo is lopsided and half fallen, glasses crooked on her nose, a few sequins missing from the gown, an offended pout.` |
| hp3 | `She is badly battered: the beehive hairdo is a messy explosion, one lens of her glasses cracked, the red gown torn at the hem, the wand star bent, smudged makeup, she clutches the wand with both hands, furious and desperate.` |
| move-wand | `She points the star-tipped wand forward to the left at arm's length, a small yellow burst at the star tip, a sly menacing smile.` |
| move-stars | `She raises both arms high above her head, the wand pointing at the sky, eyes closed, singing with an open mouth, dramatic diva pose.` |
| move-pumpkin | `She twirls on one foot and flicks the wand forward to the left, a small orange pumpkin-shaped magic puff at the star tip, a mischievous wink.` |
| move-potion | `She drinks from a small round glass bottle with glowing pink liquid, head tilted back, little finger raised elegantly.` |

### Крестьянин с вилами (`peasant`, фон **пурпурный** `#ff00ff`)

| Файл | Строка изменения |
|---|---|
| hp2 | `He is lightly battered: the brown felt cap is knocked crooked, the mustache is ruffled, the green tunic is torn at one sleeve, the torch flame is smaller, a small red bump on the forehead, grumpy face.` |
| hp3 | `He is badly battered: the cap is gone and the hair is a mess, a black eye, a bandage on the nose, the green tunic in rags, the pitchfork has one bent prong, the torch is only smoking, he leans on the pitchfork, exhausted but stubborn.` |
| move-poke | `He thrusts the pitchfork forward to the left with both hands, prongs first, leaning into the jab, gritted teeth.` |
| move-swing | `He swings the pitchfork sideways in a wide arc held like a bat, body twisted, two bold curved navy motion arcs following the prongs.` |
| move-kvass | `He drinks from a big wooden mug of dark kvass with foam, head tilted back, eyes closed in pleasure, the pitchfork leaning on his shoulder.` |
| move-callneighbours | `He cups one hand to his mouth and shouts loudly to the side, the other hand waves the burning torch high above his head.` |

Совет: делай 2–3 варианта (меняй seed) и выбирай. Не похож на себя — добавь в KEEP описание внешности из раздела «Анимации» ниже.

---

## 2. Анимации (воркфлоу 03)

Первый кадр — картинка из раздела 1, ещё на хромакее. Промт собирается так: **ANIM_START + SUBJECT + движение + ANIM_END**.

**ANIM_START:**
```
Flat 2D cartoon animation in a flat vector fairground poster style with thick dark navy outlines, the drawing style never changes.
```
**ANIM_END:**
```
The camera is completely static, no zoom, no pan. The background stays plain flat green the whole time. No other characters, no text. Silence.
```
(у Крестьянина — `plain flat magenta`)

**SUBJECT** — кто в кадре:
- Фаркуад: `A very short pompous medieval lord with a black bob haircut, a tall red hat and a red tunic with a long red cape stands on a plain flat bright green background, facing left.`
- Фея: `A plump fairy godmother with a tall lavender beehive hairdo, small glasses and a red sequin gown, holding a star-tipped wand, stands on a plain flat bright green background, facing left.`
- Крестьянин: `An angry medieval peasant with a brown felt cap, a thick droopy mustache and a baggy green tunic with a cream collar, holding a pitchfork and a burning torch, stands on a plain flat bright magenta background, facing left.`

**Движения** (одно на ролик; длительность в скобках):

| Файл | Движение |
|---|---|
| `anim/idle.webm` (4 с) | `He breathes in slowly, his chest rises, he shifts his weight from one foot to the other, blinks once, then returns to exactly the same pose as at the start.` |
| `anim/hurt.webm` (2 с) | `He is suddenly hit: he jerks backwards, squeezes his eyes shut and yells, then quickly straightens up angrily and returns to exactly the same pose as at the start.` |
| `anim/defeated.webm` (3 с) | `He staggers, spins around once, his knees give way and he collapses slowly onto the ground, sitting slumped with crossed-out eyes.` |
| `anim/victory.webm` (3 с) | `He throws his head back and laughs, shaking with laughter, raises one fist in triumph, then returns to the same pose.` |
| Фаркуад `anim/move-sword.webm` (2 с) | `He raises the sword, takes one quick step forward to the left and slashes down hard, then steps back to the starting pose.` |
| Фаркуад `anim/move-volley.webm` (2 с) | `He raises one arm high, holds it for a moment, then swings it down pointing forward to the left while shouting.` |
| Фаркуад `anim/move-shield.webm` (2 с) | `He quickly ducks down behind his big red shield and peeks over the top, eyes darting left and right.` |
| Фаркуад `anim/move-windup.webm` (2 с) | `He slowly pulls the sword far back behind his head, his whole body twisting and trembling with effort, and holds the pose.` |
| Фея `anim/move-wand.webm` (2 с) | `She flicks the wand forward to the left, a burst of yellow light flashes at the star tip, she giggles.` |
| Фея `anim/move-stars.webm` (2 с) | `She raises both arms up and waves the wand in a big circle above her head, small yellow stars appear around the wand tip.` |
| Фея `anim/move-pumpkin.webm` (2 с) | `She spins around on one foot, then points the wand forward to the left and an orange puff of magic shoots out.` |
| Фея `anim/move-potion.webm` (2 с) | `She lifts a small pink glowing bottle, drinks it in one gulp, shivers happily and a soft pink glow pulses around her.` |
| Крестьянин `anim/move-poke.webm` (2 с) | `He pulls the pitchfork back, then jabs it forward to the left hard twice, then returns to the starting pose.` |
| Крестьянин `anim/move-swing.webm` (2 с) | `He swings the pitchfork in a wide horizontal arc from right to left, nearly losing his balance, then recovers.` |
| Крестьянин `anim/move-kvass.webm` (3 с) | `He raises a wooden mug, gulps the kvass, wipes his beard with his sleeve and lets out a satisfied sigh.` |
| Крестьянин `anim/move-callneighbours.webm` (2 с) | `He cups a hand to his mouth and shouts to the right, waving the torch high above his head back and forth.` |

Для Феи меняй `He/his` на `She/her`. Для `hurt` на стадиях 2 и 3 бери первым кадром `hp2` / `hp3`: ролики `anim/hurt2.webm`, `anim/hurt3.webm`.

### Драконоослики (по желанию)

Первый кадр — картинка питомца из `web/public/assets/pets/` на хромакее. SUBJECT: `A small cute chubby dragon-donkey creature with long donkey ears and small dragon wings stands on a plain flat bright green background, facing right.`

| Файл | Движение |
|---|---|
| `pets/anim/<класс>-<n>-attack.webm` | `It rears up and kicks forward to the right with its front hooves, then lands back in the starting pose.` |
| `…-dodge.webm` | `It hops quickly to the side and back, ears flapping.` |
| `…-cover.webm` | `It plants its hooves firmly and puffs up its chest, wings spread wide like a shield.` |
| `…-fire.webm` | `It takes a deep breath and sneezes a puff of orange fire forward to the right.` |
| `…-hurt.webm` | `It flinches backwards with its eyes squeezed shut, then shakes its head and stands back up.` |
| `…-knockout.webm` | `It wobbles, its eyes become crosses and it falls slowly onto its back with its legs up.` |

---

## 3. Эффекты боя (воркфлоу 01)

Промт: **STYLE + ICON + описание + BG_GREEN**. Готовое → `web/public/assets/fx/<файл>.png` (уже есть hit, slash, fireball, arrow, star, onion-cloud, shield, pumpkin, heal; нет только `dust`).

| Файл | Описание |
|---|---|
| `dust.png` | `A small cloud of beige dust puffs with three swirly round puffs and two tiny pebbles, as left behind by a quick jump.` |
| `arrow-break.png` | `A wooden arrow snapped in half in the middle, the two pieces flying apart with small splinters, a grey arrowhead and white feather fletching.` |
| `star-break.png` | `A lamp yellow #ffd166 five-pointed star shattering into four sharp pieces flying apart, small sparks between them.` |

---

## 4. Иконки вместо эмодзи (воркфлоу 01)

Промт: **STYLE + ICON + описание + BG_GREEN** (или BG_MAGENTA, где помечено **M**). Размер 1024×1024, потом скрипт уменьшает.
Готовое → `web/public/assets/icons/<файл>.png`. Когда нарисуешь, скажи — подключу их в коде вместо эмодзи.

### Валюта и главное

| Эмодзи | Файл | Описание |
|---|---|---|
| 🫏 | `donkey.png` | `A cute round grey donkey head, front view, long upright ears, big friendly eyes, cream muzzle, a short dark mane tuft, a happy grin, like a coin mascot.` |
| 🐉 | `dragon-donkey.png` | `A tiny cute chubby baby dragon-donkey: donkey head with long ears, small leathery wings, a short spade-tipped tail, sitting.` |
| 🥚 | `egg.png` | `A big speckled dragon egg, cream #fff6df with coral #ef6f5e and sky blue spots, a small crack at the top.` |
| 🧅 | `onion.png` **M** | `A shiny round golden-brown onion with a little green sprout on top and thin roots at the bottom.` |
| 💚 | `heart-green.png` **M** | `A plump glossy mint #6ee7b7 heart with a small white highlight.` |
| ❤️ | `heart.png` | `A plump glossy coral red #ef6f5e heart with a small white highlight.` |
| 🤍 | `heart-empty.png` | `An empty heart outline, cream #fff6df inside, thick navy outline.` |
| 💧 | `mana.png` | `A single glossy sky blue #9cc3e6 water drop with a white highlight, magical glow lines.` |
| 💀 | `skull.png` | `A cute cartoon cream skull with round black eye holes and a goofy toothy grin.` |
| 😡 | `rage.png` | `A round angry coral red face with furrowed brows, gritted teeth and a small steam puff above it.` |

### Медали и награды

| Эмодзи | Файл | Описание |
|---|---|---|
| 🥇 | `medal-gold.png` | `A round gold medal with a lamp yellow #ffd166 shine and a number 1 embossed, hanging on a coral red ribbon.` |
| 🥈 | `medal-silver.png` | `A round silver medal with a number 2 embossed, hanging on a sky blue ribbon.` |
| 🥉 | `medal-bronze.png` | `A round bronze copper medal with a number 3 embossed, hanging on a navy ribbon.` |
| 🏅 | `medal.png` | `A round plain gold medal with a star in the middle, hanging on a coral ribbon.` |
| 🏆 | `trophy.png` | `A golden trophy cup with two big handles on a navy base.` |
| 🎁 | `gift.png` | `A gift box in coral red with a lamp yellow ribbon and a big bow.` |
| 🔒 | `lock.png` | `A chunky gold padlock, closed, with a keyhole.` |
| 🐢 | `turtle.png` **M** | `A slow sleepy turtle with a green shell, walking, half-closed eyes.` |
| 🎉 | `party.png` | `A party popper cone shooting out colorful confetti and streamers.` |
| 🎂 | `cake.png` | `A round birthday cake with cream frosting, coral icing drips and three lit candles.` |

### Бой с боссом (иконки ходов и скиллов уже есть, тут остальное)

| Эмодзи | Файл | Описание |
|---|---|---|
| 📣 | `cheer.png` | `A coral red megaphone with a lamp yellow rim, three short shout lines coming out of it.` |
| 🎃 | `pumpkin.png` | `A round orange pumpkin with a short curly stem, no face.` |
| ✓ | `ready.png` **M** | `A bold mint #6ee7b7 check mark inside a round badge.` |

Портреты-заглушки боссов (🧑‍🌾 🤴 🧚) не нужны: на их месте и так картинки боссов.

### Ведущий и пульт (админка)

| Эмодзи | Файл | Описание |
|---|---|---|
| ⏹ | `stop.png` | `A rounded square coral red stop button with a white square in the middle.` |
| ▶ | `play.png` | `A rounded square mint play button with a white triangle.` |
| ⏸ | `pause.png` | `A rounded square sky blue pause button with two white bars.` |
| ⏭ | `next.png` | `A rounded square sky blue skip button with a white double triangle and bar.` |
| 🗑 | `trash.png` | `A grey metal trash can with a lid slightly open.` |
| ✏️ | `pencil.png` | `A yellow pencil with a pink eraser, tilted diagonally.` |
| ✅ | `yes.png` **M** | `A mint green rounded square with a bold white check mark.` |
| ❌ | `no.png` | `A coral red rounded square with a bold white cross.` |
| ✖ | `close.png` | `A bold navy cross mark inside a cream circle.` |
| ⏳ | `hourglass.png` | `An hourglass with a wooden frame and lamp yellow sand flowing down.` |
| 🏁 | `finish.png` | `A black and white checkered finish flag on a pole, waving.` |
| 🚀 | `rocket.png` | `A cartoon rocket in cream and coral flying up diagonally, flame from the nozzle.` |
| 📸 | `camera.png` | `A boxy cartoon photo camera with a big lens and a flash, navy and cream.` |
| 📷 | `camera-small.png` | (тот же `camera.png`, отдельный файл не нужен) |
| 🔁 | `retake.png` | `Two curved arrows in a circle chasing each other, sky blue.` |
| 🖼 | `picture.png` | `A framed picture with a gold frame showing a simple green hill and a yellow sun.` |
| 🎵 | `music.png` | `Two joined navy music notes with a lamp yellow glow.` |
| 🎬 | `clapper.png` | `A black and white film clapperboard, slightly open.` |
| ⬇ | `download.png` | `A bold sky blue arrow pointing down into an open tray.` |
| 🔊 | `sound.png` | `A cream speaker with three curved sound waves coming out.` |
| 📺 | `tv.png` | `A retro TV set with antennas and a sky blue screen.` |
| 🎰 | `roulette.png` | `A spinning roulette wheel in coral and navy with a little white ball.` |
| 👆 | `pointer.png` | `A cartoon hand with the index finger pointing up.` |
| 📖 | `book.png` | `An open book with cream pages and a coral cover.` |
| 🗣 | `speaking.png` | `A navy head silhouette in profile with three shout lines from the mouth.` |
| 💰 | `money-bag.png` | `A tied cloth money bag with a gold coin symbol, a few coins beside it.` |
| ✍️ | `writing.png` | `A hand writing with a quill pen on a scroll.` |
| ⚖️ | `scales.png` | `Golden balance scales, both pans level.` |

### Игры

| Эмодзи | Файл | Описание |
|---|---|---|
| 😂 | `laugh.png` | `A round lamp yellow face laughing so hard with tears of joy flying out.` |
| 🙈 | `hide.png` | `A cute monkey covering its eyes with both hands.` |
| 🤫 | `shh.png` | `A round yellow face with a finger pressed to its lips.` |
| 😎 | `cool.png` | `A round yellow face with black sunglasses and a smug smile.` |
| 🚫 | `forbidden.png` | `A coral red prohibition sign, a circle with a diagonal line.` |
| ⚠️ | `warning.png` | `A lamp yellow triangle warning sign with a bold navy exclamation mark.` |
| ⚡ | `lightning.png` | `A bold lamp yellow lightning bolt.` |
| ⚔️ | `swords.png` | `Two crossed silver swords with gold hilts.` |
| 🎮 | `gamepad.png` | `A chunky cartoon game controller in navy with coral buttons.` |
| 🔥 | `fire.png` | `A bold flame in orange and lamp yellow with a curly tip.` |
| 🌙 | `moon.png` | `A lamp yellow crescent moon with a sleepy face.` |
| ☀️ | `sun.png` | `A bright lamp yellow sun with short rays and a smiling face.` |
| 🔫 | `mafia-gun.png` | `A cartoon toy water pistol in coral red.` |
| 🕊 | `dove.png` | `A white dove flying with an olive branch in its beak.` |
| 🪰 | `fly.png` | `A cartoon housefly with big red eyes and see-through wings.` |
| 🍪 | `cookie.png` | `A round golden chocolate chip cookie with a bite taken out.` |
| 💪 | `muscle.png` | `A flexed arm showing a big round bicep.` |
| ✨ | `sparkles.png` | `Three lamp yellow four-pointed sparkle stars of different sizes.` |
| 👀 | `eyes.png` | `A pair of big cartoon eyes looking to the side.` |
| ⏱ | `stopwatch.png` | `A silver stopwatch with a navy face and a coral hand.` |
| ⏰ | `alarm.png` | `A ringing red alarm clock with two bells on top, shaking lines.` |
| 😴 | `sleep.png` | `A round yellow face sleeping with closed eyes and a navy "z" bubble.` (буква тут — исключение, убери `No letters` из BG) |
| 👁 | `eye.png` | `A single open cartoon eye, white with a sky blue iris.` |
| 🧺 | `basket.png` | (уже есть `game/basket.png`) |

Стрелки и значки `→ ← ↑ ↓ ↩` — это типографика, их лучше оставить символами.

### Капча на регистрации (воркфлоу 01, **фото**, не постер)

Это «противная» капча, клетки должны выглядеть как настоящие фотки. Промт:
`A slightly blurry amateur smartphone photo of <объект>, centered, natural daylight, plain background, square crop, realistic photo.`
Размер 512×512. Готовое → `web/public/assets/captcha/<id>.jpg` (фон не вырезать).

`onion-1`, `onion-2`, `onion-3` — `a brown onion` (3 разных seed), `garlic` — `a head of garlic`, `potato`, `apple` — `a red apple`, `pear`, `carrot`, `egg` — `a chicken egg`, `lemon`,
`ogre` — `a big green ogre costume mask on a table`, `frog`, `crocodile`, `lion`, `penguin`, `camel`, `giraffe`, `panda`, `koala`, `kangaroo`,
`donkey-1`…`donkey-3` — `a grey donkey`, `horse-1`…`horse-4` — `a brown horse`, `zebra`, `unicorn` — `a white horse with a party unicorn horn headband`.

### Остальные картинки, которых ещё нет

| Файл | Воркфлоу | Промт |
|---|---|---|
| `ui/donkey.png` | 01 | тот же `donkey.png` из иконок |
| `ui/avatar-default.png` | 01 | `STYLE + ICON + A round avatar badge with a cute grey donkey face, cream background circle + BG_GREEN` |
| `tapper/dragon-up.png` | 01 | `STYLE + A cute chubby dragon-donkey flying, wings raised high up, side view facing right + BG_GREEN` |
| `tapper/dragon-down.png` | 02 из dragon-up | `KEEP + The wings are now pushed down low. + BG_KEEP` |
| `tapper/sky.jpg` | 01, 1344×768 | `STYLE + A wide evening sky over a swamp, soft coral and navy gradient bands, a few flat round clouds, a lamp yellow moon. No characters.` |
