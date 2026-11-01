import { useWakeLock } from '../events/useWakeLock'
import { useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { ApiError, api } from '../api/http'
import { useSnapshot } from '../api/live'
import { useMe } from '../api/me'
import type { AppSnapshot } from '../api/types'
import { AdminDialog } from '../admin/AdminDialog'
import { CurrentEventCard } from '../admin/CurrentEventCard'
import { Pets } from '../admin/Pets'
import { AnnounceForm } from '../admin/TvControls'
import { DonkeyIcon } from '../components/DonkeyIcon'
import { Icon } from '../components/Icon'
import { Leaderboard } from '../components/Leaderboard'

const STATE_LABELS = { Closed: 'закрыта, гости ждут', Open: 'открыта', Finished: 'закрыта, очки начислены' } as const

/** Пульт регистрации: открыть, закрыть — очки всегда по линейной схеме. */
function RegistrationControl({ snapshot }: { snapshot: AppSnapshot }) {
  const [error, setError] = useState<string | null>(null)
  const { state, waitingCount } = snapshot.registration
  const players = snapshot.leaderboard.length

  async function call(path: string) {
    setError(null)
    try {
      await api(path, { method: 'POST' })
    } catch (e) {
      setError(e instanceof ApiError && e.status === 409 ? 'Уже сделано — обнови страницу' : 'Не получилось')
    }
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
        <button className="primary" onClick={() => void call('/api/admin/registration/close')}>
          🏁 Закрыть и начислить очки
        </button>
      )}
      {error && <p className="error">{error}</p>}
    </section>
  )
}

/** Ручные ослики: игрок, ± число, причина. */
function AwardForm({ snapshot, onDone }: { snapshot: AppSnapshot; onDone: () => void }) {
  const [userId, setUserId] = useState('')
  const [amount, setAmount] = useState('')
  const [reason, setReason] = useState('')
  const [error, setError] = useState<string | null>(null)

  async function submit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    try {
      await api('/api/admin/awards', { method: 'POST', body: { userId, amount: Number(amount), reason } })
      setUserId('')
      setAmount('')
      setReason('')
      onDone()
    } catch {
      setError('Не получилось')
    }
  }

  return (
    <form className="form" onSubmit={submit}>
      <label>
        Кому
        <select value={userId} onChange={(e) => setUserId(e.target.value)} required>
          <option value="">— выбери —</option>
          {snapshot.leaderboard.map((r) => (
            <option key={r.userId} value={r.userId}>
              {r.login} ({r.donkeys})
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
      {error && <p className="error">{error}</p>}
      <button type="submit" className="primary">
        Начислить
      </button>
    </form>
  )
}

/** Сброс перед праздником — с подтверждением словом. */
function ResetForm({ onDone }: { onDone: () => void }) {
  const [confirm, setConfirm] = useState('')
  const [error, setError] = useState<string | null>(null)

  async function reset() {
    setError(null)
    try {
      await api('/api/admin/reset', { method: 'POST' })
      setConfirm('')
      onDone()
    } catch {
      setError('Не получилось')
    }
  }

  return (
    <div className="form">
      <p className="muted">Удалит игроков, ослики, ивенты и аватарки. Админ и ТВ останутся.</p>
      <label>
        Напиши «СБРОС»
        <input value={confirm} onChange={(e) => setConfirm(e.target.value)} />
      </label>
      {error && <p className="error">{error}</p>}
      <button className="danger" disabled={confirm !== 'СБРОС'} onClick={() => void reset()}>
        Сбросить всё
      </button>
    </div>
  )
}

/** Админка именинника. */
export function AdminPage() {
  const snapshot = useSnapshot()
  useWakeLock(Boolean(snapshot?.activeEvent))
  const { logout } = useMe()

  return (
    <main className="page">
      <h1>Админка</h1>
      <nav className="admin-tiles">
        <Link to="/admin/program" className="admin-tile mint">
          <Icon name="list" size="28px" />
          <span>Программа</span>
        </Link>
        {snapshot && (
          <AdminDialog icon={<DonkeyIcon size="28px" />} label="Начислить" title="Начислить ослики" tileClassName="lamp">
            {(close) => <AwardForm snapshot={snapshot} onDone={close} />}
          </AdminDialog>
        )}
        <AdminDialog icon={<Icon name="megaphone" size="28px" />} label="Объявить" title="Объявление на ТВ">
          {(close) => <AnnounceForm onDone={close} />}
        </AdminDialog>
        <Link to="/admin/quiz" className="admin-tile">
          <Icon name="question" size="28px" />
          <span>Своя игра</span>
        </Link>
        <Link to="/admin/tv" className="admin-tile">
          <Icon name="tv" size="28px" />
          <span>Экран ТВ</span>
        </Link>
        <Link to="/admin/neuro" className="admin-tile">
          <Icon name="photo" size="28px" />
          <span>Нейрофото</span>
        </Link>
        <Link to="/admin/auction" className="admin-tile">
          <Icon name="gavel" size="28px" />
          <span>Лоты</span>
        </Link>
      </nav>
      {snapshot === null ? (
        <p className="muted">Подключаюсь…</p>
      ) : (
        <>
          <CurrentEventCard event={snapshot.activeEvent} serverTime={snapshot.serverTime} />
          <RegistrationControl snapshot={snapshot} />
          <Leaderboard snapshot={snapshot} />
          <Pets overview={snapshot.pets} />
          <div className="admin-danger">
            <AdminDialog icon={null} label="Сбросить всё" title="Сбросить всё" tileClassName="danger">
              {(close) => <ResetForm onDone={close} />}
            </AdminDialog>
            <button className="admin-logout" onClick={() => void logout()}>
              Выйти
            </button>
          </div>
        </>
      )}
    </main>
  )
}
