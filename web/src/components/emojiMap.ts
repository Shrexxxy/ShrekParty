/** Эмодзи → картинка из ComfyUI (имя файла в /assets/icons). Варианты с U+FE0F сводятся к базовому. */
const ICONS: Record<string, string> = {
  '⏰': 'alarm',
  '📖': 'book',
  '🎂': 'cake',
  '📷': 'camera-small',
  '📸': 'camera',
  '📣': 'cheer',
  '🎬': 'clapper',
  '✖': 'close',
  '🍪': 'cookie',
  '😎': 'cool',
  '🫏': 'donkey',
  '🕊': 'dove',
  '⬇': 'download',
  '🐉': 'dragon-donkey',
  '🥚': 'egg',
  '👁': 'eye',
  '👀': 'eyes',
  '🏁': 'finish',
  '🔥': 'fire',
  '🪰': 'fly',
  '🚫': 'forbidden',
  '🎮': 'gamepad',
  '🎁': 'gift',
  '🤍': 'heart-empty',
  '❤': 'heart',
  '💚': 'heart-green',
  '🙈': 'hide',
  '⏳': 'hourglass',
  '😂': 'laugh',
  '⚡': 'lightning',
  '🔒': 'lock',
  '🔫': 'mafia-gun',
  '💧': 'mana',
  '🥉': 'medal-bronze',
  '🥇': 'medal-gold',
  '🥈': 'medal-silver',
  '🏅': 'medal',
  '💰': 'money-bag',
  '🌙': 'moon',
  '💪': 'muscle',
  '🎵': 'music',
  '⏭': 'next',
  '❌': 'no',
  '🎉': 'party',
  '⏸': 'pause',
  '✏': 'pencil',
  '🖼': 'picture',
  '▶': 'play',
  '👆': 'pointer',
  '🎃': 'pumpkin',
  '😡': 'rage',
  '✓': 'ready',
  '🔁': 'retake',
  '🚀': 'rocket',
  '🎰': 'roulette',
  '⚖': 'scales',
  '🤫': 'shh',
  '💀': 'skull',
  '😴': 'sleep',
  '🔊': 'sound',
  '✨': 'sparkles',
  '🗣': 'speaking',
  '⏹': 'stop',
  '⏱': 'stopwatch',
  '☀': 'sun',
  '⚔': 'swords',
  '🗑': 'trash',
  '🏆': 'trophy',
  '🐢': 'turtle',
  '📺': 'tv',
  '⚠': 'warning',
  '✍': 'writing',
  '✅': 'yes',
}

/** Картинки не из набора иконок: уже нарисованы для игр. */
const OTHER: Record<string, string> = {
  '🧅': '/assets/game/onion.png',
}

const base = (emoji: string) => emoji.replace(/\uFE0F/g, '')

const PATTERN = new RegExp(
  [...Object.keys(ICONS), ...Object.keys(OTHER)]
    .sort((a, b) => b.length - a.length)
    .map((e) => `${e.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}\uFE0F?`)
    .join('|'),
  'gu',
)

/** Картинка для эмодзи; null — рисовать сам символ (стрелки, капча и прочее без картинки). */
export function emojiSrc(emoji: string): string | null {
  const key = base(emoji)
  return OTHER[key] ?? (ICONS[key] ? `/assets/icons/${ICONS[key]}.png` : null)
}

/** Текст, разрезанный на куски: строки как есть, известные эмодзи — с путём к картинке. */
export function splitEmoji(text: string): Array<string | { emoji: string; src: string }> {
  const parts: Array<string | { emoji: string; src: string }> = []
  let last = 0
  for (const match of text.matchAll(PATTERN)) {
    if (match.index > last) {
      parts.push(text.slice(last, match.index))
    }
    parts.push({ emoji: match[0], src: emojiSrc(match[0])! })
    last = match.index + match[0].length
  }
  if (last < text.length) {
    parts.push(text.slice(last))
  }
  return parts
}
