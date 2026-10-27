import { useWakeLock } from '../events/useWakeLock'
import { useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { ApiError, api } from '../api/http'
import { useSnapshot } from '../api/live'
import { useMe } from '../api/me'
import type { AppSnapshot, ScoringScheme } from '../api/types'
import { CurrentEventCard } from '../admin/CurrentEventCard'
import { Program } from '../admin/Program'
import { NeuroPhotos } from '../admin/NeuroPhotos'
import { TvControls } from '../admin/TvControls'
import { Leaderboard } from '../components/Leaderboard'

const STATE_LABELS = { Closed: 'закрыта, гости ждут', Open: 'открыта', Finished: 'закрыта, очки начислены' } as const

/** Пульт регистрации: открыть, закрыть со схемой очков. */
function RegistrationControl({ snapshot }: { snapshot: AppSnapshot }) {
  const [scheme, setScheme] = useState<ScoringScheme>('Linear')
  const [custom, setCustom] = useState<Record<number, string>>({})
  const [error, setError] = useState<string | null>(null)
  const { state, waitingCount } = snapshot.registration
  const players = snapshot.leaderboard.length

  async function call(path: string, body?: unknown) {
    setError(null)
    try {
      await api(path, { method: 'POST', body })
    } catch (e) {
      setError(e instanceof ApiError && e.status === 409 ? 'Уже сделано — обнови страницу' : 'Не получилось')
    }
  }

  function close() {
    const customTable =
      scheme === 'Custom'
        ? Object.fromEntries(Object.entries(custom).filter(([, v]) => v !== '').map(([k, v]) => [k, Number(v)]))
        : undefined
    void call('/api/admin/registration/close', { scheme, customTable })
  }

  return (
    <section className="card admin-block">
      <h2>Регистрация</h2>
      <p>
        Статус: <b>{STATE_LABELS[state]}</b> · ждут: <b>{waitingCount}</b> · зарегистрировались: <b>{players}</b>
      </p>
      {state === 'Closed' && (
        <button className="primary" onClick={() => void call('/api/admin/registration/open')}>
          🚀 Открыть регистрацию
        </button>
      )}
      {state === 'Open' && (
        <div className="form">
          <label>
            Схема очков (по 10 🫏 за скорость и за пароль)
            <select value={scheme} onChange={(e) => setScheme(e.target.value as ScoringScheme)}>
              <option value="Linear">Линейная</option>
              <option value="Podium">С упором на призёров</option>
              <option value="Custom">Своя таблица</option>
            </select>
          </label>
          {scheme === 'Custom' && (
            <div className="custom-table">
              {Array.from({ length: Math.max(players, 1) }, (_, i) => i + 1).map((place) => (
                <label key={place}>
                  {place}-е место
                  <input
                    type="number"
                    inputMode="numeric"
                    value={custom[place] ?? ''}
                    onChange={(e) => setCustom({ ...custom, [place]: e.target.value })}
                  />
                </label>
              ))}
            </div>
          )}
          <button className="primary" onClick={close}>
            🏁 Закрыть и начислить очки
          </button>
        </div>
      )}
      {error && <p className="error">{error}</p>}
    </section>
  )
}

/** Ручные ослики: игрок, ± число, причина. */
function ManualAward({ snapshot }: { snapshot: AppSnapshot }) {
  const [userId, setUserId] = useState('')
  const [amount, setAmount] = useState('')
  const [reason, setReason] = useState('')
  const [message, setMessage] = useState<string | null>(null)

  async function submit(e: FormEvent) {
    e.preventDefault()
    setMessage(null)
    try {
      await api('/api/admin/awards', { method: 'POST', body: { userId, amount: Number(amount), reason } })
      setAmount('')
      setReason('')
      setMessage('Готово ✅')
    } catch {
      setMessage('Не получилось')
    }
  }

  return (
    <form className="card admin-block form" onSubmit={submit}>
      <h2>Ручные ослики</h2>
      <label>
        Игрок
        <select value={userId} onChange={(e) => setUserId(e.target.value)} required>
          <option value="">— выбери —</option>
          {snapshot.leaderboard.map((r) => (
            <option key={r.userId} value={r.userId}>
              {r.login} ({r.donkeys} 🫏)
            </option>
          ))}
        </select>
      </label>
      <label>
        Сколько (минус — штраф)
        <input type="number" inputMode="numeric" value={amount} onChange={(e) => setAmount(e.target.value)} required />
      </label>
      <label>
        За что
        <input value={reason} onChange={(e) => setReason(e.target.value)} required />
      </label>
      <button type="submit" className="primary">
        Начислить
      </button>
      {message && <p className="muted">{message}</p>}
    </form>
  )
}

/** Сброс перед праздником — с подтверждением словом. */
function ResetBlock() {
  const [confirm, setConfirm] = useState('')
  const [message, setMessage] = useState<string | null>(null)

  async function reset() {
    try {
      await api('/api/admin/reset', { method: 'POST' })
      setConfirm('')
      setMessage('Всё сброшено')
    } catch {
      setMessage('Не получилось')
    }
  }

  return (
    <section className="card admin-block danger-zone">
      <h2>Сбросить всё</h2>
      <p className="muted">Удалит игроков, ослики, ивенты и аватарки. Админ и ТВ останутся.</p>
      <label>
        Напиши «СБРОС»
        <input value={confirm} onChange={(e) => setConfirm(e.target.value)} />
      </label>
      <button className="danger" disabled={confirm !== 'СБРОС'} onClick={() => void reset()}>
        Сбросить
      </button>
      {message && <p className="muted">{message}</p>}
    </section>
  )
}

/** Админка именинника. */
export function AdminPage() {
  const snapshot = useSnapshot()
  useWakeLock(Boolean(snapshot?.activeEvent))
  const { logout } = useMe()

  return (
    <main className="page">
      <header className="admin-header">
        <h1>Админка</h1>
        <Link to="/admin/quiz">✏️ Своя игра</Link>
        <button onClick={() => void logout()}>Выйти</button>
      </header>
      {snapshot === null ? (
        <p className="muted">Подключаюсь…</p>
      ) : (
        <>
          <CurrentEventCard event={snapshot.activeEvent} serverTime={snapshot.serverTime} />
          <RegistrationControl snapshot={snapshot} />
          <Program version={snapshot.serverTime} />
          <Leaderboard snapshot={snapshot} />
          <ManualAward snapshot={snapshot} />
          <TvControls preload={snapshot.tvPreload} />
          <NeuroPhotos snapshot={snapshot} />
          <ResetBlock />
        </>
      )}
    </main>
  )
}
