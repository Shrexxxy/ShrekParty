import { useEffect, useRef, useState, type RefObject } from 'react'
import { api } from '../api/http'
import { ASSETS } from '../assets'
import { Avatar } from '../components/Avatar'
import { Sprite } from '../components/Sprite'
import { formatClock } from './countdown'
import type { TapperDetails } from './details'
import { easeSpeed, type Flap, type FlapFrame, flapTap, flapTick, flyerArt, myTaps, raceLanes, raceProgress, skySpeed, tapRate, type RaceLane } from './layer3'
import { useCountdown } from './useCountdown'
import { Emoji } from '../components/Emoji'

const BATCH_MS = 250

type Pet = Pick<TapperDetails['rows'][number], 'petClass' | 'petVariant'>

/** Фон полёта на телефоне: вертикальная полоса неба (повтор по вертикали), под ней — старое небо, пока полосы нет. */
const FLIGHT_BG = `url(${ASSETS.tapper.stripVertical}), url(${ASSETS.tapper.sky})`

/** Один кадр взмаха — 130 мс: взмах целиком — около 0,4 с, при частых тапах питомец машет без остановки. */
const FRAME_MS = 130

/** Картинки кадров уже запрошены — второй раз не грузим. */
const PRELOADED = new Set<string>()

/** Кадр полёта по числу тапов: прирост тапов — взмахи в очередь, кадры идут сами, пока очередь не кончится. */
function useFlap(taps: number, maxQueue = 1): FlapFrame {
  const [flap, setFlap] = useState<Flap>({ frame: 0, queue: 0 })
  const [seen, setSeen] = useState(taps)
  if (taps !== seen) {
    setSeen(taps)
    if (taps > seen) {
      setFlap((f) => flapTap(f, taps - seen, maxQueue))
    }
  }

  const flapping = flap.frame !== 0 || flap.queue > 0
  useEffect(() => {
    if (!flapping) {
      return
    }
    const timer = setInterval(() => setFlap(flapTick), FRAME_MS)
    return () => clearInterval(timer)
  }, [flapping])
  return flap.frame
}

/** Сколько тап считается «свежим» для темпа на ТВ: сервер шлёт тапы раз в секунду, после паузы дольше — игрок остановился. */
const TV_RATE_TTL_MS = 1500

/**
 * Небо едет непрерывно, скорость — по темпу тапов (rate), разгон и торможение плавные.
 * Позицию пишем прямо в стиль каждый кадр, без перерисовки компонента.
 */
function useSkyScroll(el: RefObject<HTMLElement | null>, rate: RefObject<() => number>, place: (offset: number) => string, axis: 'x' | 'y') {
  useEffect(() => {
    let speed = 0
    let offset = 0
    let last = performance.now()
    let raf = 0
    const frame = (now: number) => {
      const dt = Math.min(0.1, (now - last) / 1000)
      last = now
      speed = easeSpeed(speed, skySpeed(rate.current()), dt)
      offset += speed * dt
      if (el.current) {
        el.current.style[axis === 'x' ? 'backgroundPositionX' : 'backgroundPositionY'] = place(offset)
      }
      raf = requestAnimationFrame(frame)
    }
    raf = requestAnimationFrame(frame)
    return () => cancelAnimationFrame(raf)
  }, [el, rate, place, axis])
}

/** Небо на телефоне едет вниз (питомец набирает высоту); второй слой — старое небо — стоит. */
const PAD_SKY = (offset: number) => `${offset}px, center`

/** Дорожка на ТВ едет влево. */
const LANE_SKY = (offset: number) => `${-offset}px`

/** Летящий питомец: кадр 0 — обычная картинка, 1–3 — взмах; нет файла кадра — обычная картинка. */
function Flyer({ pet, frame, className }: { pet: Pet; frame: FlapFrame; className: string }) {
  const rest = flyerArt(pet, 0)
  useEffect(() => {
    for (const f of [1, 2, 3] as const) {
      const src = flyerArt(pet, f)
      if (!PRELOADED.has(src)) {
        PRELOADED.add(src)
        new Image().src = src
      }
    }
  }, [pet])
  // Питомцы нарисованы смотрящими влево, а летят вправо; общий дракон тапалки уже смотрит вправо.
  const cls = pet.petClass ? `${className} flip` : className
  return frame === 0 ? (
    <Sprite key={0} src={rest} fallback={<Emoji e="🐉" />} className={cls} />
  ) : (
    <Sprite key={frame} src={flyerArt(pet, frame)} fallback={<img src={rest} alt="" className={cls} draggable={false} />} className={cls} />
  )
}

/** Дорожка гонки на ТВ: питомец машет крыльями, пока растут его тапы. */
function Lane({ lane, leader }: { lane: RaceLane; leader: number }) {
  // На ТВ тапы приходят раз в секунду: три взмаха вперёд покрывают секунду до следующего обновления.
  const frame = useFlap(lane.taps, 3)
  // Темп на ТВ — по приросту тапов между обновлениями с сервера.
  const track = useRef<HTMLDivElement>(null)
  const seen = useRef({ taps: lane.taps, at: 0, rate: 0 })
  useEffect(() => {
    const now = performance.now()
    const prev = seen.current
    if (lane.taps > prev.taps) {
      seen.current = { taps: lane.taps, at: now, rate: ((lane.taps - prev.taps) * 1000) / Math.max(250, now - prev.at) }
    }
  }, [lane.taps])
  const rate = useRef(() => (performance.now() - seen.current.at < TV_RATE_TTL_MS ? seen.current.rate : 0))
  useSkyScroll(track, rate, LANE_SKY, 'x')
  return (
    <div className={lane.place === 1 ? 'race-lane leader' : 'race-lane'}>
      <b className="race-place">{lane.place ?? ''}</b>
      <span className="race-login">
        <Avatar url={lane.avatarUrl} login={lane.login} size={32} />
        {lane.login}
      </span>
      <div ref={track} className="race-track" style={{ backgroundImage: `url(${ASSETS.tapper.strip})` }}>
        <div className="race-dragon" style={{ left: `${raceProgress(lane.taps, leader) * 88}%` }}>
          <Flyer pet={lane} frame={frame} className="race-sprite" />
        </div>
      </div>
      <b className="race-taps">{lane.taps}</b>
    </div>
  )
}

/** Свой питомец: каждый тап — взмах, небо уезжает влево. Тапы уходят пачками раз в 250 мс. */
function DragonPad({ eventId, initialCount, pet }: { eventId: string; initialCount: number; pet: Pet }) {
  // После перезагрузки посреди забега продолжаем с серверного счёта, а не с нуля.
  const [count, setCount] = useState(initialCount)
  const frame = useFlap(count)
  const pad = useRef<HTMLButtonElement>(null)
  const taps = useRef<number[]>([])
  const rate = useRef(() => tapRate(taps.current, performance.now()))
  useSkyScroll(pad, rate, PAD_SKY, 'y')
  const pending = useRef(0)

  useEffect(() => {
    const flush = () => {
      const batch = pending.current
      if (batch > 0) {
        pending.current = 0
        void api(`/api/tapper/${eventId}/taps`, { method: 'POST', body: { count: batch } }).catch(() => undefined)
      }
    }
    const timer = setInterval(flush, BATCH_MS)
    return () => {
      clearInterval(timer)
      flush()
    }
  }, [eventId])

  function tap() {
    pending.current += 1
    const now = performance.now()
    taps.current = [...taps.current.filter((t) => t > now - 1000), now]
    setCount((c) => c + 1)
  }

  return (
    <button ref={pad} className="dragon-pad" style={{ backgroundImage: FLIGHT_BG }} onPointerDown={tap}>
      <span className={frame === 3 ? 'dragon down' : 'dragon'}>
        <Flyer pet={pet} frame={frame} className="dragon-img" />
      </span>
      <span className="dragon-count">{count}</span>
    </button>
  )
}

/** Тапалка с драконом: на телефоне — дракон под палец, на ТВ — гонка всех драконов. */
export function TapperView({
  eventId,
  details,
  serverTime,
  myId,
  tv,
}: {
  eventId: string
  details: TapperDetails
  serverTime: string
  myId?: string
  tv?: boolean
}) {
  const left = useCountdown(details.deadline, serverTime)

  if (details.phase === 'Countdown') {
    return <div className={tv ? 'tapper-countdown tv' : 'tapper-countdown'}>{Math.max(1, Math.ceil(left / 1000))}</div>
  }

  if (!tv) {
    const me = details.rows.find((r) => r.userId === myId) ?? { petClass: null, petVariant: null }
    return left > 0 ? (
      <DragonPad eventId={eventId} initialCount={myTaps(details.rows, myId)} pet={me} />
    ) : (
      <p className="tapper-done">
        <Emoji e="🏁" /> Время! Смотри на ТВ
      </p>
    )
  }

  const leader = Math.max(0, ...details.rows.map((r) => r.taps))
  return (
    <div className="tapper tv">
      <div className="hide-clock">{formatClock(left)}</div>
      {/* Дорожки стоят на месте (по логину), кто впереди — видно по месту слева и по тому, кто дальше улетел. */}
      <div className="race">
        {raceLanes(details.rows).map((r) => (
          <Lane key={r.userId} lane={r} leader={leader} />
        ))}
      </div>
    </div>
  )
}
