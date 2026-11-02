import { useEffect, useRef, useState } from 'react'
import { api } from '../api/http'
import type { AppSnapshot, EventDto, LeaderboardRowDto } from '../api/types'
import { Avatar } from './Avatar'
import { Donkeys } from './DonkeyIcon'
import { Icon } from './Icon'
import { RegistrationResults } from './RegistrationResults'

const GENERAL = 'general'

/**
 * Таблица лидеров: «Общая» — живая из снапшота, таблицы ивентов — запросом при каждом обновлении снапшота.
 * Какую смотреть — выбирается в окне по кнопке: ивентов за вечер много, вкладки в ряд не помещаются.
 */
export function Leaderboard({ snapshot, highlightUserId }: { snapshot: AppSnapshot | null; highlightUserId?: string }) {
  const [tab, setTab] = useState<string>(GENERAL)
  const [events, setEvents] = useState<EventDto[]>([])
  const [eventRows, setEventRows] = useState<LeaderboardRowDto[]>([])
  const picker = useRef<HTMLDialogElement>(null)
  const version = snapshot?.serverTime

  useEffect(() => {
    api<EventDto[]>('/api/events').then(setEvents).catch(() => undefined)
  }, [version])

  useEffect(() => {
    if (tab !== GENERAL) {
      api<LeaderboardRowDto[]>(`/api/leaderboard?eventId=${tab}`).then(setEventRows).catch(() => undefined)
    }
  }, [tab, version])

  const rows = tab === GENERAL ? (snapshot?.leaderboard ?? []) : eventRows
  const options = [{ id: GENERAL, title: 'Общая' }, ...events]

  function choose(id: string) {
    setTab(id)
    picker.current?.close()
  }

  return (
    <section className="leaderboard">
      <div className="leaderboard-head">
        <span>
          <small className="muted">Таблица</small>
          <b>{options.find((o) => o.id === tab)?.title ?? 'Общая'}</b>
        </span>
        {events.length > 0 && (
          <button className="leaderboard-pick" onClick={() => picker.current?.showModal()} aria-label="Выбрать другую таблицу">
            <Icon name="list" size="26px" />
          </button>
        )}
      </div>
      <dialog ref={picker} className="admin-dialog leaderboard-picker" onClick={(e) => e.target === picker.current && picker.current?.close()}>
        <h2>Какую таблицу показать?</h2>
        <div className="leaderboard-options">
          {options.map((o) => (
            <button key={o.id} aria-pressed={tab === o.id} onClick={() => choose(o.id)}>
              {o.title}
            </button>
          ))}
        </div>
      </dialog>
      {events.find((e) => e.id === tab)?.type === 'Registration' ? (
        <RegistrationResults version={version} highlightUserId={highlightUserId} />
      ) : rows.length === 0 ? (
        <p className="muted">Пока пусто</p>
      ) : (
        <ol className="rows">
          {rows.map((r) => (
            <li key={r.userId} className={r.userId === highlightUserId ? 'row me' : 'row'}>
              <span className="place">{r.place}</span>
              <Avatar url={r.avatarUrl} login={r.login} />
              <span className="login">{r.login}</span>
              <Donkeys value={r.donkeys} />
              {r.cash !== r.donkeys && <span className="cash">на руках {r.cash}</span>}
            </li>
          ))}
        </ol>
      )}
    </section>
  )
}
