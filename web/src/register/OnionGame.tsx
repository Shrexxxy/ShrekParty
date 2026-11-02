import { AnimatePresence, motion } from 'framer-motion'
import { useEffect, useRef, useState } from 'react'
import { ASSETS } from '../assets'
import { Sprite } from '../components/Sprite'
import { createGame, move, tick, type GameState, type Lane } from './onionRules'

/** Начало и конец жёлоба в процентах поля (поле квадратное). */
const CHUTES: Record<Lane, { from: [number, number]; to: [number, number] }> = {
  0: { from: [4, 12], to: [30, 34] },
  1: { from: [4, 50], to: [26, 70] },
  2: { from: [96, 12], to: [70, 34] },
  3: { from: [96, 50], to: [74, 70] },
}

/** Плечи Шрека — где из рукавов на картинке выходят руки — и где держится корзинка относительно конца жёлоба. */
const SHOULDERS: [number, number][] = [
  [36, 45.7],
  [64, 45.7],
]
const BASKET_OFFSET_Y = 5

/** Пол, где валяются упавшие луки, — на уровне ног Шрека, в процентах поля. */
const FLOOR_Y = 80

/** Луки над краем корзинки — по одному на каждую пятую часть пути к цели. */
const BASKET_ONIONS: [number, number][] = [
  [-30, -42],
  [30, -42],
  [0, -50],
  [-16, -72],
  [16, -72],
]

/** Где на полу лежит упавший лук: под концом жёлоба, чуть в сторону — каждый по-своему. */
function floorSpot(id: number, lane: Lane): { x: number; rotate: number } {
  const end = CHUTES[lane].to[0]
  const side = lane < 2 ? -1 : 1
  return { x: end + side * ((id * 37) % 9) - side * 2, rotate: (id * 53) % 360 }
}

const KEYS: Record<string, Lane> = { q: 0, a: 1, p: 2, l: 3, й: 0, ф: 1, з: 2, д: 3 }

/** Простой огр на SVG — пока нет сгенерированной картинки тела. */
function OgreFallback() {
  return (
    <svg viewBox="0 0 100 120" width="100%" height="100%">
      <ellipse cx="50" cy="95" rx="34" ry="28" fill="#7a5a32" />
      <rect x="30" y="72" width="40" height="30" rx="6" fill="#e9dfc4" />
      <circle cx="50" cy="44" r="26" fill="#8bc34a" />
      <rect x="18" y="30" width="10" height="7" rx="3" fill="#7cb342" transform="rotate(-25 23 33)" />
      <rect x="72" y="30" width="10" height="7" rx="3" fill="#7cb342" transform="rotate(25 77 33)" />
      <circle cx="41" cy="40" r="3.5" fill="#2b2b2b" />
      <circle cx="59" cy="40" r="3.5" fill="#2b2b2b" />
      <path d="M38 56 Q50 64 62 56" stroke="#33691e" strokeWidth="3" fill="none" strokeLinecap="round" />
    </svg>
  )
}

function armStyle(shoulder: [number, number], target: [number, number]) {
  const dx = target[0] - shoulder[0]
  const dy = target[1] - shoulder[1]
  return {
    left: `${shoulder[0]}%`,
    top: `${shoulder[1]}%`,
    height: `${Math.hypot(dx, dy)}%`,
    // Рука висит вниз (по +Y) — поворачиваем её к корзинке.
    rotate: `${(Math.atan2(dy, dx) * 180) / Math.PI - 90}deg`,
  }
}

function chuteStyle(from: [number, number], to: [number, number]) {
  const dx = to[0] - from[0]
  const dy = to[1] - from[1]
  return {
    left: `${from[0]}%`,
    top: `${from[1]}%`,
    width: `${Math.hypot(dx, dy)}%`,
    rotate: `${(Math.atan2(dy, dx) * 180) / Math.PI}deg`,
  }
}

/** Мини-игра под видом капчи. Тап по четверти экрана — Шрек тянет корзинку к этому жёлобу. */
export function OnionGame({ onDone }: { onDone: () => void }) {
  const [state, setState] = useState<GameState>(() => createGame())
  const stateRef = useRef(state)
  const doneRef = useRef(false)

  useEffect(() => {
    let frame = 0
    let last = performance.now()
    const loop = (now: number) => {
      // Ограничиваем шаг: после сворачивания вкладки не должно прилететь всё разом.
      const dt = Math.min(now - last, 100)
      last = now
      stateRef.current = tick(stateRef.current, dt)
      setState(stateRef.current)
      if (stateRef.current.done) {
        if (!doneRef.current) {
          doneRef.current = true
          setTimeout(onDone, stateRef.current.tiredOut ? 2500 : 900)
        }
        return
      }
      frame = requestAnimationFrame(loop)
    }
    frame = requestAnimationFrame(loop)
    return () => cancelAnimationFrame(frame)
  }, [onDone])

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const lane = KEYS[e.key.toLowerCase()]
      if (lane !== undefined) {
        go(lane)
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])

  function go(lane: Lane) {
    stateRef.current = move(stateRef.current, lane)
    setState(stateRef.current)
  }

  const end = CHUTES[state.shrekLane].to
  const basket: [number, number] = [end[0], end[1] + BASKET_OFFSET_Y]
  const progress = Math.min(100, (state.score / state.target) * 100)

  return (
    <div className="onion-game">
      <div className="onion-hud">
        <span>
          Очки: <b>{state.score}</b> / {state.target}
        </span>
        {state.streak >= 2 && (
          <motion.span key={state.streak} className="onion-streak" initial={{ scale: 1.5 }} animate={{ scale: 1 }}>
            🔥 Стрик ×{state.streak}
          </motion.span>
        )}
      </div>
      <div className="onion-progress">
        <span style={{ width: `${progress}%` }} />
      </div>
      <div className="onion-field" style={{ backgroundImage: `url(${ASSETS.game.background})` }}>
        {([0, 1, 2, 3] as Lane[]).map((lane) => {
          const { from, to } = CHUTES[lane]
          return <span key={lane} className="chute" style={chuteStyle(from, to)} />
        })}

        <Sprite className="ogre-body" src={ASSETS.game.body} fallback={<OgreFallback />} alt="Шрек" />
        {/* Руки поверх тела — выходят из-под края рукава, иначе на нижних полках кисти прячутся за Шреком.
            Предплечье тянется, кисть — картинка как есть. */}
        {SHOULDERS.map((shoulder, i) => (
          <span key={i} className={i === 0 ? 'ogre-arm' : 'ogre-arm mirrored'} style={armStyle(shoulder, basket)}>
            <span className="ogre-forearm" />
            <Sprite className="ogre-hand" src={ASSETS.game.hand} fallback={<span className="ogre-hand-fallback" />} />
          </span>
        ))}
        {state.dropped.map((d) => {
          const end = CHUTES[d.lane].to
          const spot = floorSpot(d.id, d.lane)
          return (
            <motion.span
              key={d.id}
              className="onion-dropped"
              initial={{ left: `${end[0]}%`, top: `${end[1]}%`, rotate: 0 }}
              animate={{ left: `${spot.x}%`, top: `${FLOOR_Y}%`, rotate: spot.rotate }}
              transition={{ duration: 0.45, ease: 'easeIn' }}
            >
              <Sprite className="onion-img" src={ASSETS.game.onion} fallback="🧅" />
            </motion.span>
          )
        })}

        {/* Корзинка наполняется по мере набора очков. */}
        <div className="ogre-basket" style={{ left: `${basket[0]}%`, top: `${basket[1]}%` }}>
          {BASKET_ONIONS.slice(0, Math.floor((progress / 100) * BASKET_ONIONS.length)).map(([x, y], i) => (
            <Sprite key={i} className="basket-onion" src={ASSETS.game.onion} fallback="🧅" style={{ translate: `${x}% ${y}%` }} />
          ))}
          <Sprite className="ogre-basket-img" src={ASSETS.game.basket} fallback="🧺" />
        </div>

        {state.onions.map((o) => {
          const { from, to } = CHUTES[o.lane]
          const t = Math.max(0, Math.min(1, o.progressMs / o.durationMs))
          return (
            <Sprite
              key={o.id}
              className="onion"
              src={ASSETS.game.onion}
              fallback="🧅"
              style={{ left: `${from[0] + (to[0] - from[0]) * t}%`, top: `${from[1] + (to[1] - from[1]) * t}%`, rotate: `${t * 540}deg` }}
            />
          )
        })}

        <AnimatePresence>
          {state.lastCatch && (
            <motion.span
              key={state.lastCatch.id}
              className="onion-pop"
              style={{ left: `${basket[0]}%`, top: `${basket[1] - 8}%` }}
              initial={{ opacity: 1, y: 0, scale: 0.8 }}
              animate={{ opacity: 0, y: -40, scale: 1.3 }}
              transition={{ duration: 0.8 }}
            >
              +{state.lastCatch.points}
            </motion.span>
          )}
        </AnimatePresence>

        <div className="cookie-taps">
          {/* Сетка 2×2: лев. верх, прав. верх, лев. низ, прав. низ. */}
          {([0, 2, 1, 3] as Lane[]).map((lane) => (
            <button key={lane} aria-label={`Жёлоб ${lane + 1}`} onPointerDown={() => go(lane)} />
          ))}
        </div>
      </div>
      {state.done && (
        <div className="cookie-done">{state.tiredOut ? '😴 Шрек устал ждать. Засчитано!' : '✅ Проверка пройдена'}</div>
      )}
    </div>
  )
}
