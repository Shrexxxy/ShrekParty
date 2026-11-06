import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router'
import { api } from '../api/http'
import { useSnapshot } from '../api/live'
import { useMe } from '../api/me'
import type { EventDto, LeaderboardRowDto } from '../api/types'
import { Avatar } from '../components/Avatar'
import { Donkeys } from '../components/DonkeyIcon'
import { eventPageState } from '../events/eventPage'
import { EventView } from '../events/EventView'
import { isDarkPhase } from '../events/hide'
import { HideDark } from '../events/HideView'
import { useWakeLock } from '../events/useWakeLock'
import { useCountdown } from '../events/useCountdown'

/** Итоги прошедшего ивента — его таблица осликов. */
function EventResults({ eventId, highlightUserId, version }: { eventId: string; highlightUserId?: string; version?: string }) {
    const [rows, setRows] = useState<LeaderboardRowDto[] | null>(null)

    useEffect(() => {
        api<LeaderboardRowDto[]>(`/api/leaderboard?eventId=${eventId}`)
            .then(setRows)
            .catch(() => setRows([]))
    }, [eventId, version])

    if (rows === null) {
        return <p className="muted">Загружаю итоги…</p>
    }

    return rows.every((r) => r.donkeys === 0) ? (
        <p className="muted">Осликов в этом ивенте не раздавали</p>
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
    )
}

/** Страница одного ивента: идёт — сам ивент; прошёл — итоги; иначе — «ещё не начался». */
export function EventPage() {
    const { id = '' } = useParams()
    const { me } = useMe()
    const snapshot = useSnapshot()
    const [events, setEvents] = useState<EventDto[] | null>(null)
    const version = snapshot?.serverTime
    const active = snapshot?.activeEvent ?? null
    const [seen, setSeen] = useState<{ id: string; title: string } | null>(null)

    if (active?.id === id && seen?.id !== id) {
        setSeen({ id, title: active.title })
    }

    const state = eventPageState(id, active, events, seen?.id === id)

    // Исправлено: ?? '' решает проблему с типом string | null
    const countdownMs = useCountdown(
        active?.status === 'Starting' ? (active.startedAt ?? '') : '',
        snapshot?.serverTime ?? ''
    )
    // Защита от NaN (если startedAt пустой), дефолт 15
    const timeLeft = Number.isFinite(countdownMs) ? Math.max(0, Math.ceil(countdownMs / 1000)) : 15

    const dark = state === 'live' && isDarkPhase(active)
    useWakeLock(state === 'live' && !dark)

    useEffect(() => {
        api<EventDto[]>('/api/events')
            .then(setEvents)
            .catch(() => setEvents([]))
    }, [version])

    if (dark) {
        return <HideDark />
    }

    const title = state === 'live' ? active?.title : (events?.find((e) => e.id === id)?.title ?? (seen?.id === id ? seen.title : undefined))

    if (state === 'live' && active && active.status === 'Starting') {
        return (
            <main className="page center">
                <div className="bunting" aria-hidden />
                <h1 className="event-title" style={{ color: 'var(--lamp)' }}>{active.title}</h1>
                <div className="tv-hero-number" style={{ fontSize: '120px', lineHeight: 1, margin: '20px 0' }}>
                    {timeLeft}
                </div>
                <p style={{ fontSize: '24px', fontWeight: 800 }}>Приготовьтесь!</p>
                <p className="muted">Игра начнется автоматически.</p>
            </main>
        )
    }

    return (
        <main className="page">
            <div className="bunting" aria-hidden />
            <Link to="/" className="back-link">
                ← На главную
            </Link>
            {title && <h1 className="event-title">{title}</h1>}
            {state === 'live' && active && snapshot && (
                <section className="card active-event">
                    <EventView event={active} serverTime={snapshot.serverTime} myId={me?.id} />
                </section>
            )}
            {state === 'finished' && (
                <>
                    <div className="stamp">Ивент закончился</div>
                    <EventResults eventId={id} highlightUserId={me?.id} version={version} />
                </>
            )}
            {(state === 'upcoming' || state === 'unknown') && <p className="muted">Этот ивент ещё не начался или такого нет.</p>}
            {state === 'loading' && <p className="muted">Подключаюсь…</p>}
        </main>
    )
}