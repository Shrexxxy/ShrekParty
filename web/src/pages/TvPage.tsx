import { useCallback, useEffect, useRef, useState } from 'react'
import { useSnapshot } from '../api/live'
import { EventView } from '../events/EventView'
import { useWakeLock } from '../events/useWakeLock'
import { TvAfterClose } from '../tv/TvAfterClose'
import { TvCarousel } from '../tv/TvCarousel'
import { TvMessageOverlay } from '../tv/TvMessageOverlay'
import { TvWaiting } from '../tv/TvWaiting'

const RESULTS_MS = 60_000

interface AfterClose {
  closedAt: string
  withClosing: boolean
  tableMs: number
}

/** Экран ТВ: только показывает, режим выбирается по снапшоту. */
export function TvPage() {
  const snapshot = useSnapshot()
  // Ноутбук у ТВ не должен засыпать никогда.
  useWakeLock(true)
  const [sound, setSound] = useState(false)
  const [afterClose, setAfterClose] = useState<AfterClose | null>(null)
  const sawOpen = useRef(false)
  const handledClose = useRef<string | null>(null)

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

    // ТВ перезагрузили сразу после закрытия — досматриваем оставшуюся часть минуты (время — по серверу).
    const elapsed = Date.parse(serverTime) - Date.parse(closedAt)
    if (elapsed < RESULTS_MS) {
      setAfterClose({ closedAt, withClosing: false, tableMs: RESULTS_MS - elapsed })
    }
  }, [state, closedAt, serverTime])

  const finishAfterClose = useCallback(() => setAfterClose(null), [])

  function unlockSound() {
    // Браузер разрешает звук только после клика на каждой загрузке страницы — отсюда кнопка.
    const ctx = new AudioContext()
    void ctx.resume().then(() => ctx.close())
    setSound(true)
    void document.documentElement.requestFullscreen?.().catch(() => undefined)
  }

  if (!sound) {
    return (
      <main className="page tv center">
        <button className="primary tv-unlock" onClick={unlockSound}>
          🔊 Включить звук и полный экран
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
      {snapshot.activeEvent ? (
        <div className="tv-event">
          <h1 className="tv-title">{snapshot.activeEvent.title}</h1>
          <EventView event={snapshot.activeEvent} serverTime={snapshot.serverTime} tv />
        </div>
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
