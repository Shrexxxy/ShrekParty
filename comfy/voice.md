# Реплики боссов для озвучки (ElevenLabs v3)

Каждая строка — отдельный mp3, имя файла в начале строки. Готовое → `web/public/assets/voice/boss/`.
Теги в квадратных скобках — эмоции для модели v3. Их можно менять, вслух они не читаются.
На ход — три реплики, на ТВ будет играть случайная.

Когда звучит:
- **intro** — выход босса;
- **ход** — босс делает этот ход;
- **hurt** — по боссу попали сильно (больше 10 % здоровья за удар);
- **low** — здоровье упало ниже трети (один раз за бой);
- **defeat** — босс побеждён;
- **victory** — команда проиграла.

---

## Крестьянин с вилами (`peasant`)

**Голос:** мужской, низкий, хрипловатый, деревенский говорок. Ворчливый, вечно недовольный, но простодушный. Темп неторопливый, в злости срывается на крик.

**intro**
- `peasant-intro-1`: «[grumbling] Это кто тут по моему огороду топчется? [shouting] А ну, пошли вон, ослы крылатые!»
- `peasant-intro-2`: «[sighs] Только картошку посадил... [angry] И опять драконы! [shouting] Ну всё, держитесь!»

**Тычок вилами (poke)**
- `peasant-poke-1`: «[grunts] На-ка, вилами в бочок!»
- `peasant-poke-2`: «[mischievously] Тыц! [laughs] Как стог ворошу.»
- `peasant-poke-3`: «[angry] Это тебе за грядку с морковкой!»

**Размах вилами (swing)**
- `peasant-swing-1`: «[shouting] Расступись, огород иду полоть!»
- `peasant-swing-2`: «[grunts] Эх, раззудись плечо! [laughs] Двоих разом!»
- `peasant-swing-3`: «[annoyed] Что вы тут столпились? [shouting] Вот вам по-соседски!»

**Квас (kvass)**
- `peasant-kvass-1`: «[gulps] [satisfied sigh] Ох, хорош кваском... Сразу силы вернулись.»
- `peasant-kvass-2`: «[calmly] Погодите, ребятки. [gulps] Перерыв. [burps] Продолжаем.»
- `peasant-kvass-3`: «[happily] Бабкин квас — лучше всякого зелья!»

**Зовёт соседей (callneighbours)**
- `peasant-callneighbours-1`: «[shouting] Петрович! Михалыч! [loudly] Тащите вилы, тут драконы!»
- `peasant-callneighbours-2`: «[shouting] Мужики-и-и! [pause] [menacingly] Сейчас вся деревня придёт.»
- `peasant-callneighbours-3`: «[yelling] Звоните в колокол! [grumbling] Сейчас мы вас всем миром...»

**hurt**
- `peasant-hurt-1`: «[yelps] Ай! [angry] Ну, это ты зря.»
- `peasant-hurt-2`: «[groans] Ох, поясница моя...»

**low**
- `peasant-low-1`: «[out of breath] Ничего... [stubbornly] Я и не такой урожай отстаивал!»

**defeat**
- `peasant-defeat-1`: «[groans] Всё... Сдаюсь... [whining] Только картошку не трогайте!»

**victory**
- `peasant-victory-1`: «[laughs] Ха! [proudly] Вот так у нас в деревне с драконами! [calmly] Пойду квасу налью.»

---

## Лорд Фаркуад (`farquaad`)

**Голос:** мужской, высокий, надменный, капризно-театральный. Говорит свысока, торжественно и с самолюбованием. Темп средний, в конце фраз самодовольно тянет.

**intro**
- `farquaad-intro-1`: «[clears throat] [dramatically] Внимание, подданные! [smugly] Перед вами идеальный правитель. [annoyed] Почему так много ослов?»
- `farquaad-intro-2`: «[proudly] Да, это я. [pause] Нет, автографы потом.»

**Удар мечом (sword)**
- `farquaad-sword-1`: «[smugly] Подойди поближе, ослик. [pause] Ниже. [condescending] Ещё ниже. [shouting] Вот так!»
- `farquaad-sword-2`: «[clears throat] Королевская сталь. [proudly] Ковали три недели. [laughs] Для тебя — три секунды.»
- `farquaad-sword-3`: «[annoyed] Ты стоишь в моей тени. [sarcastically] Ах да, её почти нет. [shouting] Получай!»

**Залп лучников (volley)**
- `farquaad-volley-1`: «[dramatically] Лучники! [pause] Стреляйте во всех. [casually] Да, и в того тоже.»
- `farquaad-volley-2`: «[excited] Дождь из стрел! [laughs] Зонтиков, увы, не выдали.»
- `farquaad-volley-3`: «[bored] Сто лучников. Одна команда ослов. [sighs] Даже нечестно. [shouting] Огонь!»

**Щит (shield)**
- `farquaad-shield-1`: «[offended] Не по лицу! [smugly] Моё лицо — достояние королевства.»
- `farquaad-shield-2`: «[mischievously] Попробуйте-ка теперь. [laughs] Я за стеной. Высокой стеной!»
- `farquaad-shield-3`: «[whispers] Щит, щит, щит... [loudly] Королю не больно. Королю никогда не больно!»

**Замах (windup)**
- `farquaad-windup-1`: «[menacingly] Сейчас будет больно. [pause] Мне — нет. [laughs] Вам — очень.»
- `farquaad-windup-2`: «[whispers] Запомните этот момент. [dramatically] Это последнее, что вы запомните.»
- `farquaad-windup-3`: «[inhales deeply] Королевский замах... [proudly] Тренировался перед зеркалом. [smugly] Зеркало, кстати, разбилось.»

**hurt**
- `farquaad-hurt-1`: «[shrieks] Ай! [outraged] Это была королевская мантия!»
- `farquaad-hurt-2`: «[whining] Не по причёске!»

**low**
- `farquaad-low-1`: «[panicking] Стража! Стража-а! [pause] [annoyed] Где вообще вся стража?»

**defeat**
- `farquaad-defeat-1`: «[shocked] Нет... Нет! [whining] Это нечестно! [shouting] Я буду жаловаться в королевскую канцелярию!»

**victory**
- `farquaad-victory-1`: «[laughs] Ха-ха! [smugly] Некоторыми можно пожертвовать. [pause] Например, всеми вами.»

---

## Фея-крёстная (`fairy`)

**Голос:** женский, бархатный, сладкий до приторности, как у эстрадной дивы. Тон «солнышко моё», а под ним угроза. Улыбка слышна в голосе, иногда напевает.

**intro**
- `fairy-intro-1`: «[sings] Ла-ла-ла... [sweetly] Здравствуйте, мои сладкие! [menacingly] Кто тут не верит в сказки?»
- `fairy-intro-2`: «[giggles] Тётушка пришла! [sweetly] И тётушка очень, очень недовольна.»

**Волшебная палочка (wand)**
- `fairy-wand-1`: «[sings] Бибиди-бабиди... [sweetly] а теперь бум, дорогуша!»
- `fairy-wand-2`: «[sweetly] Иди к тётушке, солнышко. Тётушка не кусается. [menacingly] Тётушка колдует!»
- `fairy-wand-3`: «[giggles] Немножко волшебства... [sharply] прямо в лоб!»

**Звёздный дождь (stars)**
- `fairy-stars-1`: «[dreamy] Загадайте желание! [mock surprise] Ой, кажется, звёзды падают прямо на вас.»
- `fairy-stars-2`: «[sings] Звёздочки для моих любимых осликов! [laughs] Все до единого!»
- `fairy-stars-3`: «[dramatically] Финальный номер! [sweetly] Аплодисменты... и звездопад!»

**В тыкву (pumpkin)**
- `fairy-pumpkin-1`: «[sweetly] Полночь, мой сладкий! [giggles] Карета превращается... в тебя!»
- `fairy-pumpkin-2`: «[strict] А ну-ка, тыковка, посиди и подумай о своём поведении.»
- `fairy-pumpkin-3`: «[sings] Тыква, тыква, тыквочка... [laughs] Тебе идёт!»

**Зелье (potion)**
- `fairy-potion-1`: «[sighs] Глоточек для бодрости. [sweetly] Мамочке надо освежиться.»
- `fairy-potion-2`: «[whispers] Ой, не смотрите. [innocently] Это просто витаминки. Волшебные.»
- `fairy-potion-3`: «[gulps] [refreshed] М-м-м, клубничное! [sweetly] Продолжим, дорогие?»

**hurt**
- `fairy-hurt-1`: «[gasps] Причёска! [outraged] Вы знаете, сколько стоит эта причёска?!»
- `fairy-hurt-2`: «[hissing] Ах ты, гадкий ослик...»

**low**
- `fairy-low-1`: «[sweetly, through gritted teeth] Хорошо, солнышки. [menacingly] Теперь тётушка разозлилась по-настоящему.»

**defeat**
- `fairy-defeat-1`: «[dramatically] Нет! [whining] Моя карьера! Мои фанаты! [sobbing] Мой контракт...»

**victory**
- `fairy-victory-1`: «[sings] И жили они долго и счастливо... [laughs] Но не вы, мои сладкие!»
