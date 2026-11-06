import { useCallback, useEffect, useRef, useState } from 'react'
import { useSnapshot } from '../api/live'
import { EventView } from '../events/EventView'
import { useWakeLock } from '../events/useWakeLock'
import { useQuizPreload } from '../tv/quizCache'
import { TvAfterClose } from '../tv/TvAfterClose'
import { TvCarousel } from '../tv/TvCarousel'
import { TvMessageOverlay } from '../tv/TvMessageOverlay'
import { TvWaiting } from '../tv/TvWaiting'
import { Emoji } from '../components/Emoji'
import { useCountdown } from '../events/useCountdown'

const RESULTS_MS = 60_000

interface AfterClose {
  closedAt: string
  withClosing: boolean
  tableMs: number
}

export function TvPage() {
  const snapshot = useSnapshot()
  useWakeLock(true)
  useQuizPreload()
  const [sound, setSound] = useState(false)
  const [afterClose, setAfterClose] = useState<AfterClose | null>(null)
  const sawOpen = useRef(false)
  const handledClose = useRef<string | null>(null)

  // Исправлено: ?? '' решает проблему с типом string | null
  const countdownMs = useCountdown(
      snapshot?.activeEvent?.status === 'Starting' ? (snapshot.activeEvent.startedAt ?? '') : '',
      snapshot?.serverTime ?? ''
  )
  const timeLeft = Number.isFinite(countdownMs) ? Math.max(0, Math.ceil(countdownMs / 1000)) : 15

  const state = snapshot?.registration.state
  const closedAt = snapshot?.registration.closedAt ?? null
  const serverTime = snapshot?.serverTime

  useEffect(() => {
    if (state === 'Open') {
      sawOpen.current = true
    }
    if (state !== 'Finished' || closedAt === null || serverTime === undefined || handledClose.current === closedAt) {
      return
    }

    handledClose.current = closedAt
    if (sawOpen.current) {
      setAfterClose({ closedAt, withClosing: true, tableMs: RESULTS_MS })
      return
    }

    const elapsed = Date.parse(serverTime) - Date.parse(closedAt)
    if (elapsed < RESULTS_MS) {
      setAfterClose({ closedAt, withClosing: false, tableMs: RESULTS_MS - elapsed })
    }
  }, [state, closedAt, serverTime])

  const finishAfterClose = useCallback(() => setAfterClose(null), [])

  function unlockSound() {
    const ctx = new AudioContext()
    void ctx.resume().then(() => ctx.close())
    setSound(true)
    void document.documentElement.requestFullscreen?.().catch(() => undefined)
  }

  if (!sound) {
    return (
        <main className="page tv center">
          <button className="primary tv-unlock" onClick={unlockSound}>
            <Emoji e="🔊" /> Включить звук и полный экран
          </button>
        </main>
    )
  }

  if (snapshot === null) {
    return <main className="page tv center muted">Подключаюсь…</main>
  }

  return (
      <main className="page tv">
        <TvMessageOverlay message={snapshot.tvMessage} serverTime={snapshot.serverTime} />
        {!snapshot.activeEvent && snapshot.pets?.beforeFirstEvent && snapshot.pets.hatched < snapshot.pets.total && (
            <div className="tv-pets-counter">
              <Emoji e="🥚" /> Вылупилось драконоосликов: {snapshot.pets.hatched} из {snapshot.pets.total}
            </div>
        )}

        {snapshot.activeEvent ? (
            snapshot.activeEvent.status === 'Starting' ? (
                <div className="tv-hero" style={{ display: 'grid', placeContent: 'center', textAlign: 'center', height: '100%' }}>
                  <h1 className="tv-hero-title" style={{ color: 'var(--lamp)', fontSize: 'clamp(32px, 5vw, 80px)' }}>
                    {snapshot.activeEvent.title}
                  </h1>
                  <div className="tv-hero-number" style={{ fontSize: 'clamp(150px, 35vw, 400px)', lineHeight: 1 }}>
                    {timeLeft}
                  </div>
                  <p className="tv-open-sub" style={{ fontWeight: 800 }}>Приготовьтесь!</p>
                </div>
            ) : (
                <div className="tv-event">
                  <h1 className="tv-title">{snapshot.activeEvent.title}</h1>
                  <EventView event={snapshot.activeEvent} serverTime={snapshot.serverTime} tv />
                </div>
            )
        ) : state === 'Finished' && afterClose ? (
            <TvAfterClose
                key={afterClose.closedAt}
                withClosing={afterClose.withClosing}
                tableMs={afterClose.tableMs}
                onDone={finishAfterClose}
            />
        ) : state === 'Finished' ? (
            <TvCarousel snapshot={snapshot} />
        ) : (
            <TvWaiting snapshot={snapshot} />
        )}
      </main>
  )
}