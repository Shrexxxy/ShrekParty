import type { ActiveEventDto } from '../api/types'
import type { AuctionDetails } from '../events/details'
import { EventView } from '../events/EventView'
import { useAdminAction } from './useAdminAction'

/** Пульт аукциона: вид ТВ, «Следующий лот», отмена продажи, история покупок. */
export function AuctionControls({ event, details, serverTime }: { event: ActiveEventDto; details: AuctionDetails; serverTime: string }) {
  const { run, error, busy } = useAdminAction()
  const base = `/api/admin/auction/${event.id}`

  return (
    <div className="controls">
      <EventView event={event} serverTime={serverTime} tv />
      {details.phase !== 'Bidding' && details.phase !== 'Done' && (
        <button className="primary" disabled={busy} onClick={() => void run(`${base}/next`)}>
          ▶ Следующий лот {details.left > 0 ? `(впереди ${details.left})` : '(лотов нет — закончить)'}
        </button>
      )}
      {details.phase === 'Sold' && (
        <button disabled={busy} onClick={() => confirm('Отменить продажу? Ослики вернутся, лот снова выйдет первым.') && void run(`${base}/undo`)}>
          ↩ Отменить продажу
        </button>
      )}
      {details.phase === 'Done' && <p className="muted">Все лоты сыграны — «⏹ Завершить».</p>}
      {details.sales.length > 0 && (
        <details>
          <summary>Продано ({details.sales.length})</summary>
          {details.sales.map((s, i) => (
            <div key={i} className="control-row">
              <span>{s.lotName}</span>
              <span>
                {s.login} · {s.price} 🫏
              </span>
            </div>
          ))}
        </details>
      )}
      {error && <p className="error">{error}</p>}
    </div>
  )
}
