import { useEffect, useRef, useState } from 'react'
import { api } from '../api/http'
import { ASSETS } from '../assets'
import { Avatar } from '../components/Avatar'
import { Sprite } from '../components/Sprite'
import { formatClock } from './countdown'
import type { TapperDetails } from './details'
import { myTaps, raceProgress, tapperBanner } from './layer3'
import { useCountdown } from './useCountdown'

const BATCH_MS = 250

/** Дракон игрока: каждый тап — взмах, фон уезжает вниз. Тапы уходят пачками раз в 250 мс. */
function DragonPad({ eventId, initialCount }: { eventId: string; initialCount: number }) {
  // После перезагрузки посреди забега продолжаем с серверного счёта, а не с нуля.
  const [count, setCount] = useState(initialCount)
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
    setCount((c) => c + 1)
  }

  return (
    <button
      className="dragon-pad"
      style={{ backgroundImage: `url(${ASSETS.tapper.sky})`, backgroundPositionY: `${count * 6}px` }}
      onPointerDown={tap}
    >
      <span className={count % 2 === 0 ? 'dragon up' : 'dragon down'}>
        <Sprite src={count % 2 === 0 ? ASSETS.tapper.dragonUp : ASSETS.tapper.dragonDown} fallback="🐉" alt="дракон" />
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
    return left > 0 ? <DragonPad eventId={eventId} initialCount={myTaps(details.rows, myId)} /> : <p className="tapper-done">🏁 Время! Смотри на ТВ</p>
  }

  const leader = details.rows[0]?.taps ?? 0
  const banner = tapperBanner(left)
  return (
    <div className="tapper tv">
      <div className="hide-clock">{formatClock(left)}</div>
      {banner && <div className="tapper-banner">{banner}</div>}
      <div className="race">
        {details.rows.map((r) => (
          <div key={r.userId} className="race-lane">
            <span className="race-login">{r.login}</span>
            <div className="race-track">
              <div className="race-dragon" style={{ left: `${raceProgress(r.taps, leader) * 85}%` }}>
                <Avatar url={r.avatarUrl} login={r.login} size={40} />
                <Sprite className="race-sprite" src={ASSETS.tapper.dragonUp} fallback="🐉" />
              </div>
            </div>
            <b className="race-taps">{r.taps}</b>
          </div>
        ))}
      </div>
    </div>
  )
}
