# Промпты для «угадай по нейрофото»

Модель: Qwen-Image 2.1 Edit (локально, workflow `neurophoto-workflow.json`).
Пояснения здесь на русском, сами промпты на английском: английский текст модель выполняет точнее, особенно длинные условия вроде «не трогай позы».

## Как собрать промпт

**Промпт = строка темы + общий хвост.** Хвост вставляется после темы через пробел, каждый раз, без изменений. Без него модель охотнее переставляет людей, обрезает кадр и рисует всем одинаковые лица.

В строке темы три части:
1. Стиль: *Turn this photo into ...*
2. Чем заменить фон, реквизит и костюмы этого мира.
3. Что из фото оставить обязательно (колпаки, очки, гитару и т.п.). Если на фото колпаков нет, убери фразу про них.

### Общий хвост «сохранить структуру» (копировать целиком)

```
Keep the exact same composition, camera angle and framing as the original photo, nothing cropped or zoomed. Keep exactly the same number of people, each one in the same place, with the same pose, gestures, arms around each other and head tilt. Keep every person individually recognizable: keep their own face shape, nose, jaw, eyebrows, facial hair, hairstyle, hair color and expression, translated into the new style; do not give everyone the same generic face. Keep the clothing silhouettes and colors, reinterpreted as themed costumes. Do not add, remove or merge people. Only change the art style, and add themed props, costume details and background details that fit the world.
```

Что сработало на тесте (групповое фото, 11 человек):
- `nothing cropped or zoomed`: без этой фразы шрековская версия слегка приблизила кадр и срезала ноги. С ней кадр совпал с оригиналом.
- `same number of people ... Do not add, remove or merge people`: во всех тестах осталось 11 человек на своих местах, руки на плечах тоже сохранились.
- `reinterpreted as themed costumes`: одежда меняется, но силуэт и цвет остаются, и человека можно узнать даже по свитеру.
- Если хочется больше костюма и меньше исходной одежды, замени фразу на `Replace their clothes with themed costumes in similar colors`.

---

## Аниме

**A1. Studio Ghibli** *(проверен: `_review/neuro-test-anime.png`)*
```
Turn this photo into a Studio Ghibli style anime illustration: soft hand-painted watercolor backgrounds, clean anime line art and cel shading. The room becomes a cozy wooden Ghibli-style tavern with warm lanterns, hanging plants and small soot sprites on the floor; the paper party hats stay on their heads.
```

**A2. Сёнэн-экшен (в духе Naruto / Jujutsu Kaisen)**
```
Turn this photo into a dynamic shonen action anime key visual: sharp anime line art, bold cel shading, speed lines and glowing energy auras around each person. Their outfits become ninja and sorcerer school uniforms in similar colors, with headbands, scrolls and weapons; the background becomes a training ground with a dramatic sky.
```

**A3. Аниме 90-х (cel anime, в духе Sailor Moon / Evangelion)**
```
Turn this photo into a 1990s cel-animated anime frame: hand-painted cels, slightly grainy VHS colors, big expressive anime eyes but each person keeps their own hairstyle and hair color. Add a retro Tokyo city night through a window behind them, neon signs in Japanese and a vintage TV in the corner.
```

**A4. Школьное аниме (slice of life)**
```
Turn this photo into a bright slice-of-life anime illustration in the style of Kyoto Animation: delicate line art, soft pastel lighting and sparkles. The room becomes a Japanese high school club room after a festival, with paper lanterns, a chalkboard with doodles and cherry blossoms outside the window; outfits become school uniforms in similar colors.
```

**A5. One Piece (пираты)**
```
Turn this photo into a One Piece anime style illustration: bold Eiichiro Oda style line art, exaggerated cheerful expressions, bright saturated colors. Everyone becomes a member of a pirate crew with straw hats, bandanas, vests and sashes in the colors of their clothes; the room becomes the deck of a pirate ship with a Jolly Roger flag, barrels and the open sea.
```

## Фильмы

**F1. Шрек (в тему вечеринки)** *(проверен: `_review/neuro-test-film.png`; эта же строка стоит в workflow по умолчанию)*
```
Turn this photo into a frame from the animated movie Shrek: stylized DreamWorks 3D CGI animation render, not photorealistic, with exaggerated cartoon proportions and soft rounded shapes. Everyone becomes a cartoon fairy-tale villager of Far Far Away in medieval costumes based on their clothes. The room becomes a swamp-side ogre's wooden cabin party with torches, mud mugs, onions and a 'Keep Out' sign; the paper party hats stay on.
```
Вариант без `not photorealistic ... rounded shapes` (`_review/neuro-test-film-v1.png`) выходит почти фотореалистичным, как кадр фэнтези-фильма. Костюмы там сильнее, но кадр слегка обрезан.

**F2. Звёздные войны**
```
Turn this photo into a cinematic still from a Star Wars movie: film grain, dramatic rim lighting, practical sci-fi look. Their clothes become Jedi robes, rebel pilot jackets and smuggler vests in similar colors, a few hold glowing lightsabers in their existing hand positions; the room becomes a Mos Eisley cantina with alien patrons in the far background and droids at the bar.
```

**F3. Гарри Поттер**
```
Turn this photo into a still from a Harry Potter movie: warm candlelit cinematic lighting, slight film grain. Everyone wears Hogwarts robes with house scarves (Gryffindor, Slytherin, Ravenclaw, Hufflepuff) chosen to match the colors of their clothes, some holding wands; the room becomes the Great Hall with floating candles, long wooden tables and house banners.
```

**F4. Властелин колец**
```
Turn this photo into a still from The Lord of the Rings movies: epic cinematic color grading, misty golden light. Everyone becomes a member of a fellowship: elven cloaks, dwarven armor, hobbit waistcoats and ranger leathers in the colors of their clothes; the room becomes a hall in Rivendell with carved wooden arches, waterfalls outside and a map of Middle-earth on the wall.
```

**F5. Пираты Карибского моря**
```
Turn this photo into a still from Pirates of the Caribbean: cinematic warm lantern light, salty weathered textures. Everyone becomes a pirate: tricorn hats, bandanas, long coats, corsets and sashes in the colors of their clothes, gold earrings and a few cutlasses; the room becomes the lower deck of a pirate ship with rum barrels, a treasure chest and a parrot.
```

**F6. Матрица**
```
Turn this photo into a still from The Matrix: green-tinted cinematic color grade, high contrast. Everyone wears long black leather coats, sunglasses and sleek outfits keeping their clothing silhouettes; falling green digital code rain on the walls and a ringing old telephone on a table.
```

## Мультики

**C1. Pixar / 3D-мультфильм** *(проверен: `_review/neuro-test-cartoon.png`)*
```
Turn this photo into a still from a Pixar-style 3D animated movie: stylized characters, soft subsurface skin, detailed fabric textures and cinematic warm lighting. The party room gets colorful balloons, streamers, a birthday cake on a table in the back and glowing fairy lights; the paper party hats stay on.
```
Слово «Pixar» тянет всех к одному шаблонному лицу с большими глазами. Узнают по причёске, волосам и одежде, а черты лица теряются. Мои варианты формулировок (`semi-realistic`, без имени студии) на том же seed дали почти ту же картинку. Помогает другой seed и фото покрупнее.

**C2. Симпсоны**
```
Turn this photo into a frame from The Simpsons: flat 2D cartoon, yellow skin, overbite, big round eyes, thick black outlines, but each person keeps their own hairstyle, hair color, glasses and facial hair. The room becomes Moe's Tavern in Springfield with a pink donut, Duff beer signs and a window to Springfield.
```

**C3. Советская мультипликация (Союзмультфильм)**
```
Turn this photo into a frame from a classic 1970s Soviet Soyuzmultfilm cartoon: hand-drawn 2D animation, soft gouache backgrounds, gentle muted colors and warm outlines like "Nu, pogodi!" and "The Bremen Town Musicians". The room becomes a cozy Soviet New Year celebration with a decorated fir tree, garlands, a tangerine bowl and a TV set in the corner.
```

**C4. Disney 2D (классика 90-х)**
```
Turn this photo into a hand-drawn Disney Renaissance animation frame (1990s, like Aladdin and The Little Mermaid): clean ink lines, painted backgrounds, expressive faces. The room becomes a magical palace ballroom with chandeliers, flowers and sparkling magic dust; their clothes become fairy-tale outfits in similar colors.
```

**C5. Пластилин / stop-motion (Aardman)**
```
Turn this photo into a claymation stop-motion scene in the style of Aardman (Wallace and Gromit): everyone sculpted from plasticine with visible fingerprints and slightly lumpy shapes, each keeping their own hairstyle and face shape. The room becomes a cozy British living room party with a teapot, cheese plate and bunting flags.
```

**C6. Рисованный 2D без имени студии (когда лица важнее стиля)**
```
Turn this photo into a hand-drawn 2D cartoon illustration with clean outlines and flat bright colors, faithful caricature of each real person. The room becomes a colorful cartoon party with balloons, confetti and a big birthday cake; the paper party hats stay on.
```

## Игры

**G1. GTA, обложка / загрузочный экран** *(проверен: `_review/neuro-test-game.png`)*
```
Turn this photo into a Grand Theft Auto loading screen illustration: bold comic-book outlines, flat saturated colors and dramatic contrast, the classic GTA cover art style. Behind the group add a neon-lit Vice City nightclub with palm trees visible through a window, a sports car and sunset colors; the paper party hats stay on.
```

**G2. Minecraft**
```
Turn this photo into a Minecraft scene: everyone becomes a blocky voxel Minecraft character with pixel textures reproducing their hair, clothes and colors. The room becomes a Minecraft wooden house interior with torches, a crafting table, chests and a cake block; a creeper peeks from a doorway in the background.
```

**G3. Пиксель-арт 16-бит (SNES)**
```
Turn this photo into 16-bit SNES era pixel art: limited color palette, crisp pixels, no anti-aliasing, a classic JRPG party lineup. Each person is a pixel sprite keeping their hairstyle, hair color and outfit colors; the background becomes a pixel-art tavern with a fireplace, and a retro HP/MP status bar runs along the bottom.
```

**G4. Fortnite**
```
Turn this photo into Fortnite key art: bright stylized 3D game render, chunky proportions, glossy colorful materials. Everyone wears Fortnite-style skins based on their clothes, some holding pickaxes and colorful gliders on their backs; the background becomes a sunny battle royale island with a Battle Bus flying in the sky and a supply drop.
```

**G5. Ведьмак**
```
Turn this photo into a still from The Witcher 3: dark fantasy game art, realistic painterly textures, moody tavern lighting. Their clothes become leather armor, cloaks, medallions and medieval dresses in similar colors, a couple of swords on backs; the room becomes a Novigrad inn with a Gwent card table, candles and a bard playing a lute.
```

**G6. Cyberpunk 2077**
```
Turn this photo into Cyberpunk 2077 game art: neon magenta and cyan lighting, rain-slick reflections, futuristic street wear in their clothing colors, glowing cybernetic implants on arms and faces. The room becomes a Night City bar with holographic ads, neon kanji signs and a futuristic jukebox.
```

---

## Если лицо меняется слишком сильно или слишком мало

**Denoise у этой модели нет.** Она перерисовывает картинку целиком по тексту, и ползунка «на сколько процентов изменить» нет. Настраивать можно только это:

| Что не так | Что делать |
|---|---|
| Лица «одинаковые», друга не узнать | Убери имя студии (Pixar/Disney/Ghibli), опиши стиль словами (см. C6). Оставь в хвосте фразу про *face shape, nose, jaw, eyebrows*. Попробуй 3–4 других seed. |
| Лица мелкие (групповое фото) | `resolution` 1328 (так в workflow). На 1024 мелкие лица упрощаются сильнее. Или обрежь фото до 2–5 человек. |
| Человек пропал, добавился или двое слились | В хвосте должна быть фраза *Do not add, remove or merge people*. Смени seed. |
| Кадр обрезан или приближен | В хвосте должна быть фраза *nothing cropped or zoomed*. |
| Похоже на фильтр, а не на другой мир | Начни с *Fully redraw as ...*, добавь *exaggerated proportions*, распиши больше костюмов и реквизита, а фразу про лица сократи до *keep hairstyles and hair colors*. |
| Стиль не тот | Название вселенной (Shrek, Minecraft, The Simpsons) работает сильнее общего описания. Добавь 2–3 характерных предмета этого мира. |

**Seed.** С одним и тем же seed результат почти не меняется, даже если поправить формулировку: на тесте три разные версии Pixar-промпта с seed 42 дали почти одинаковые картинки. Если не нравится, сначала смени seed, а потом уже переписывай промпт.

## Пакетно: одно фото, несколько вариантов

1. В ноде «3. SEED / STEPS» оставь `control after generate = randomize`.
2. Справа от кнопки **Queue / Run** поставь счётчик (batch count) на 4 и нажми один раз. Получится 4 варианта с разными seed, около 3–4 минут на RTX 5070.
3. Выбери лучший. Его seed виден в ноде после запуска, а ещё он сохранён в метаданных PNG: перетащи картинку обратно в ComfyUI, и весь граф восстановится.
4. Чтобы сравнить темы на одном seed, поставь `fixed` и меняй только строку темы.

Для скриптов есть `neurophoto-workflow-api.json` (API-формат). В нём меняются `"4".inputs.image` (имя файла в `ComfyUI/input`), `"5".inputs.prompt` и `"6".inputs.seed`.

## Скорость

RTX 5070 12 ГБ, 25 шагов, resolution 1328 (фото 1600×1066 → 1632×1088): **≈ 46–50 с на картинку**, когда модели уже загружены. Первый запуск после старта ComfyUI дольше, потому что модели грузятся с диска.
