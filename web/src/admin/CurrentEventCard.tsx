import { useState } from 'react'
import { api } from '../api/http'
import type { ActiveEventDto } from '../api/types'
import { errorText } from './errors'
import { EventControls } from './EventControls'
import { EVENT_TYPE_LABELS } from './programOrder'

/**
 * Сверху админки: что сейчас идёт и главные кнопки. Админ ведёт вечер с телефона — всё под рукой.
 */
export function CurrentEventCard({ event, serverTime }: { event: ActiveEventDto | null | undefined; serverTime: string }) {
  const [error, setError] = useState<string | null>(null)

  if (!event) {
    return (
      <section className="card current-event idle">
        <div className="muted">Сейчас ничего не идёт — начни следующий пункт программы ниже</div>
      </section>
    )
  }

  async function finish() {
    if (!event || !confirm(`Завершить «${event.title}» и начислить ослики?`)) {
      return
    }
    setError(null)
    try {
      await api(`/api/admin/events/${event.id}/finish`, { method: 'POST' })
    } catch (e) {
      setError(errorText(e))
    }
  }

  return (
    <section className="card current-event">
      <div className="current-event-head">
        <div>
          <small className="muted">Сейчас идёт · {EVENT_TYPE_LABELS[event.type]}</small>
          <h2>{event.title}</h2>
        </div>
        <button className="danger finish" onClick={() => void finish()}>
          ⏹ Завершить
        </button>
      </div>
      <EventControls event={event} serverTime={serverTime} />
      {error && <p className="error">{error}</p>}
    </section>
  )
}
