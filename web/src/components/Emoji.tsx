import { DonkeyIcon } from './DonkeyIcon'
import { emojiSrc, splitEmoji } from './emojiMap'
import { Sprite } from './Sprite'

/** Валюта — всегда монетка, а не картинка ослика. */
const COIN = '🫏'

/** Эмодзи картинкой из ComfyUI; картинки нет (или не загрузилась) — сам символ. */
export function Emoji({ e }: { e: string }) {
  if (e === COIN) {
    return <DonkeyIcon />
  }

  const src = emojiSrc(e)
  return src ? <Sprite src={src} fallback={e} className="emoji-icon" /> : <>{e}</>
}

/** Текст, в котором известные эмодзи нарисованы картинками; не строка (false, null) — ничего. */
export function EmojiText({ text }: { text: string | false | null | undefined }) {
  if (typeof text !== 'string') {
    return null
  }

  return (
    <>
      {splitEmoji(text).map((part, i) => (typeof part === 'string' ? part : <Emoji key={i} e={part.emoji} />))}
    </>
  )
}
