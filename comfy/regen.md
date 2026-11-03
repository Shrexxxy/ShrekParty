# Перегенерировать

Всё из этого списка — воркфлоу **01**. Промт = **STYLE + ICON + описание + фон** (куски STYLE и ICON — в `prompts.md`).
Два правила, из-за которых в прошлый раз не получилось:

1. **Фон — только сплошной хромакей до самого края.** Без белой или кремовой рамки, без карточки, без цветного квадрата под объектом. Рамку скрипт срезает, но кремовый или жёлтый фон вырезать нельзя.
2. **Цвет объекта не должен совпадать с фоном.** Зелёный объект — на пурпурном фоне (BG_MAGENTA), всё остальное — на зелёном (BG_GREEN).

Чтобы рамки не было, в конец промта добавь: `The background color fills the whole image edge to edge. No card, no frame, no border, no white margin.`

Готовые файлы клади, как обычно, без обработки. Фон вырежу сам скриптом `comfy/tools/prep.py`. Оригиналы прошлых попыток лежат в `web/public/_review/rejected/` и `_review/icons-raw/`.

## Что не так и промты

| Файл | Что было не так | Фон | Описание |
|---|---|---|---|
| `fx/dust.png` | Нарисовался цирковой шатёр, а не пыль | GREEN | `A small cloud of beige and light brown dust: three round swirly puffs and two tiny pebbles flying, as left behind by a quick sideways jump. Only the dust cloud, nothing else, no buildings, no tent.` |
| `fx/arrow-break.png` | Две целые скрещённые стрелы в рамке на кремовом фоне | GREEN | `One wooden arrow snapped in half in the middle: the front half with a grey arrowhead and the back half with white feather fletching fly apart, small wooden splinters between them.` |
| `fx/star-break.png` | Звезда в рамке на жёлтом фоне, вырезать нельзя | GREEN | `A lamp yellow #ffd166 five-pointed star cracked and shattering into four sharp pieces flying apart, tiny sparks between the pieces.` |
| `icons/dragon-donkey.png` | Зелёный дракоша на зелёном фоне — пропадает при вырезке | **MAGENTA** | `A tiny cute chubby baby dragon-donkey sitting: grey donkey head with long ears, small coral #ef6f5e leathery wings, a short spade-tipped tail. Grey and cream colors, no green, no pink, no purple.` |
| `icons/lock.png` | Замок нарисован в тёмной квадратной рамке, рамка остаётся на иконке | GREEN | `A chunky gold padlock, closed, with a dark keyhole, front view. Only the padlock, no square, no frame around it.` |
| `icons/shh.png` | Палец у губ вышел похожим на средний — на телефоне это читается как неприличный жест | GREEN | `A round lamp yellow face with closed smiling eyes, holding one INDEX finger (the pointing finger) upright against its lips in a "shh" gesture, the other fingers curled.` |
| `tapper/dragon-up.png` | Заднее крыло тёмно-зелёное — при вырезке становится полупрозрачным | GREEN | `STYLE + A cute chubby grey dragon-donkey flying, both coral #ef6f5e wings raised high up, side view facing right, cream belly, long donkey ears. No green on the character.` (без ICON) |
| `tapper/dragon-down.png` | Сделай заново из нового dragon-up, чтобы совпадали | GREEN | Воркфлоу **02** из нового `dragon-up`: `KEEP + Both wings are now pushed down low below the body. + BG_KEEP` |

## Иконок не хватает

В коде они уже стоят. Пока файла нет, показывается эмодзи.

| Эмодзи | Файл | Фон | Описание |
|---|---|---|---|
| 💚 | `icons/heart-green.png` | **MAGENTA** | `A plump glossy mint #6ee7b7 heart with a small white highlight.` |
| 🗣 | `icons/speaking.png` | GREEN | `A navy head silhouette in profile facing right with three bold coral shout lines coming from the mouth.` |
