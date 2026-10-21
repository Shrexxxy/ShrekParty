import { useState } from 'react'
import { ASSETS } from '../assets'
import { Sprite } from '../components/Sprite'
import { ROUNDS, isCorrect, shuffleRound } from './annoyingCaptcha'

/** «Противная» капча: три сетки 3×3 подряд. Ошибся — тряска и новая раскладка. */
export function ImageCaptcha({ onDone }: { onDone: () => void }) {
  const [round, setRound] = useState(0)
  const [tiles, setTiles] = useState(() => shuffleRound(ROUNDS[0], Math.random))
  const [selected, setSelected] = useState<Set<number>>(new Set())
  const [wrong, setWrong] = useState(false)

  function toggle(i: number) {
    // Функциональное обновление: быстрые тапы подряд не должны затирать друг друга.
    setSelected((prev) => {
      const next = new Set(prev)
      if (next.has(i)) {
        next.delete(i)
      } else {
        next.add(i)
      }
      return next
    })
    setWrong(false)
  }

  function verify() {
    if (!isCorrect(tiles, selected)) {
      setWrong(true)
      setTiles(shuffleRound(ROUNDS[round], Math.random))
      setSelected(new Set())
      return
    }

    if (round + 1 >= ROUNDS.length) {
      onDone()
      return
    }

    setRound(round + 1)
    setTiles(shuffleRound(ROUNDS[round + 1], Math.random))
    setSelected(new Set())
    setWrong(false)
  }

  return (
    <div className={wrong ? 'image-captcha shake' : 'image-captcha'}>
      <div className="image-captcha-prompt">
        <small>Проверка {round + 1} из {ROUNDS.length}</small>
        <b>{ROUNDS[round].prompt}</b>
        <small>Отметьте все подходящие и нажмите «Проверить»</small>
      </div>
      <div className="image-captcha-grid">
        {tiles.map((tile, i) => (
          <button
            key={`${round}-${tile.id}`}
            className={selected.has(i) ? 'tile selected' : 'tile'}
            onClick={() => toggle(i)}
            aria-pressed={selected.has(i)}
          >
            <Sprite className="tile-img" src={ASSETS.captcha(tile.id)} fallback={tile.emoji} />
          </button>
        ))}
      </div>
      {wrong && <p className="captcha-wrong">Неверно, попробуйте ещё раз</p>}
      <button className="captcha-verify" onClick={verify}>
        ПРОВЕРИТЬ
      </button>
    </div>
  )
}
