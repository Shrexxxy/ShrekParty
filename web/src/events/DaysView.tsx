import { useEffect, useRef, useState, type FormEvent } from 'react'
import { api } from '../api/http'
import { onConnectionLost } from '../api/live'
import { errorText } from '../admin/errors'
import { Avatar } from '../components/Avatar'
import { formatClock } from './countdown'
import type { DaysDetails } from './details'
import { REVEAL_STEP_MS, revealedCount } from './layer3'
import { useCountdown } from './useCountdown'

const CHEAT_RETRIES = 5

/**
 * Пока идёт ответ: свернул браузер, ушёл в другое окно или потерял связь — сообщаем серверу (с повтором, связь может вернуться не сразу).
 */
function useCheatReport(eventId: string, active: boolean) {
  const reported = useRef(false)

  useEffect(() => {
    if (!active) {
      return
    }

    let timer: ReturnType<typeof setTimeout> | undefined
    const report = (attempt = 0) => {
      if (reported.current && attempt === 0) {
        return
      }
      reported.current = true
      api(`/api/days/${eventId}/cheat`, { method: 'POST' }).catch(() => {
        if (attempt + 1 < CHEAT_RETRIES) {
          timer = setTimeout(() => report(attempt + 1), 2000)
        }
      })
    }
    const onVisibility = () => document.visibilityState === 'hidden' && report()
    const onBlur = () => report()

    document.addEventListener('visibilitychange', onVisibility)
    window.addEventListener('blur', onBlur)
    const unsubscribe = onConnectionLost(() => report())
    return () => {
      document.removeEventListener('visibilitychange', onVisibility)
      window.removeEventListener('blur', onBlur)
      unsubscribe()
      clearTimeout(timer)
    }
  }, [eventId, active])
}

/** Раскрытие на ТВ: ответы по одному от самого далёкого, затем правда. */
function DaysReveal({ details }: { details: DaysDetails }) {
  const [startedAt] = useState(() => Date.now())
  const [now, setNow] = useState(startedAt)

  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 250)
    return () => clearInterval(timer)
  }, [])

  const shown = revealedCount(now - startedAt, details.answers.length, REVEAL_STEP_MS)
  return (
    <div className="days-reveal">
      {details.answers.slice(0, shown).map((a) => (
        <div key={a.login} className="days-answer">
          <Avatar url={a.avatarUrl} login={a.login} size={36} />
          <span>{a.login}</span>
          <b>{a.days}</b>
          <small className="muted">±{a.diff}</small>
        </div>
      ))}
      {shown > details.answers.length && <div className="days-truth">Правильно: {details.truth} дней 🎂</div>}
    </div>
  )
}

/** Сколько мне дней: ответ за 30 секунд без сворачивания браузера. */
export function DaysView({ eventId, details, serverTime, tv }: { eventId: string; details: DaysDetails; serverTime: string; tv?: boolean }) {
  const left = useCountdown(details.deadline, serverTime)
  const answering = details.phase === 'Answering'
  const [value, setValue] = useState('')
  const [error, setError] = useState<string | null>(null)
  useCheatReport(eventId, !tv && answering && !details.iAmDisqualified)

  async function submit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    try {
      await api(`/api/days/${eventId}/answer`, { method: 'POST', body: { days: Number(value) } })
    } catch (err) {
      setError(errorText(err))
    }
  }

  if (tv) {
    return (
      <div className="days tv">
        {answering ? (
          <>
            <div className="hide-phase">🎂 Сколько дней имениннику?</div>
            <div className="hide-clock">{formatClock(left)}</div>
            <div className="muted">
              Ответили: {details.answered} / {details.total}
            </div>
          </>
        ) : (
          <DaysReveal details={details} />
        )}
        {details.disqualified.length > 0 && <div className="days-cheaters">🚫 Недобросовестные: {details.disqualified.join(', ')}</div>}
      </div>
    )
  }

  if (details.iAmDisqualified) {
    return <div className="days cheater">🚫 Недобросовестный! Ты свернул браузер или потерял связь.</div>
  }

  if (!answering) {
    return (
      <div className="days">
        <p>Твой ответ: {details.myAnswer ?? '—'}</p>
        {details.truth !== null && <p className="days-truth">Правильно: {details.truth}</p>}
        <p className="muted">Смотри раскрытие на ТВ</p>
      </div>
    )
  }

  return (
    <form className="days form" onSubmit={submit}>
      <p className="days-warning">⚠️ Не сворачивай браузер и не переключай вкладки — дисквалификация!</p>
      <div className="hide-clock">{formatClock(left)}</div>
      <label>
        Сколько дней имениннику сегодня?
        <input type="number" inputMode="numeric" min={0} value={value} onChange={(e) => setValue(e.target.value)} />
      </label>
      <button className="primary" disabled={value === ''}>
        {details.myAnswer === null ? 'Ответить' : 'Изменить ответ'}
      </button>
      {details.myAnswer !== null && <p className="muted">Твой ответ: {details.myAnswer}</p>}
      {error && <p className="error">{error}</p>}
    </form>
  )
}
