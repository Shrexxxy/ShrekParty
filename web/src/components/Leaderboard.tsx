import { useEffect, useState } from 'react'
import { api } from '../api/http'
import type { AppSnapshot, EventDto, LeaderboardRowDto } from '../api/types'
import { Avatar } from './Avatar'
import { Donkeys } from './DonkeyIcon'
import { RegistrationResults } from './RegistrationResults'

const GENERAL = 'general'

/**
 * Таблица лидеров: «Общая» — живая из снапшота, вкладки ивентов — запросом при каждом обновлении снапшота.
 */
export function Leaderboard({ snapshot, highlightUserId }: { snapshot: AppSnapshot | null; highlightUserId?: string }) {
  const [tab, setTab] = useState<string>(GENERAL)
  const [events, setEvents] = useState<EventDto[]>([])
  const [eventRows, setEventRows] = useState<LeaderboardRowDto[]>([])
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

  return (
    <section className="leaderboard">
      <div className="tabs" role="tablist">
        <button role="tab" aria-selected={tab === GENERAL} onClick={() => setTab(GENERAL)}>
          Общая
        </button>
        {events.map((e) => (
          <button key={e.id} role="tab" aria-selected={tab === e.id} onClick={() => setTab(e.id)}>
            {e.title}
          </button>
        ))}
      </div>
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
            </li>
          ))}
        </ol>
      )}
    </section>
  )
}
