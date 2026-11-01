import { useState } from 'react'
import { api } from '../api/http'
import type { PetDto } from '../api/types'
import { errorText } from '../admin/errors'
import { CLASS_NAMES, EGG_TAPS, eggStage, STATS, xpProgress } from './pets'

const CRACKS = ['', '⚡', '⚡⚡'] as const

/** Яйцо: тапы считает телефон, на последнем — «вылупился» серверу; не дошло — ещё один тап. */
function Egg({ onHatched }: { onHatched: () => Promise<boolean> }) {
  const [taps, setTaps] = useState(0)
  const [failed, setFailed] = useState(false)
  const stage = eggStage(taps)

  async function tap() {
    const next = taps + 1
    setTaps(next)
    setFailed(false)
    if (eggStage(next) === 3 && !(await onHatched())) {
      setTaps(EGG_TAPS - 1)
      setFailed(true)
    }
  }

  return (
    <button className={`pet-egg stage-${stage}`} onClick={() => void tap()} disabled={stage === 3}>
      <span className="pet-egg-shell">🥚</span>
      <span className="pet-egg-crack">{CRACKS[Math.min(stage, 2)]}</span>
      <small className="muted">{stage === 3 ? 'Вылупляется…' : `Тапай! Ещё ${EGG_TAPS - taps}`}</small>
      {failed && <small className="error">Не получилось — проверь интернет и тапни ещё раз</small>}
    </button>
  )
}

/** Карточка: класс, уровень, опыт, характеристики и что они дают в бою. */
function PetCard({ pet, fresh }: { pet: PetDto; fresh: boolean }) {
  const [editing, setEditing] = useState(false)
  const [name, setName] = useState(pet.name)
  const [error, setError] = useState<string | null>(null)
  const xp = xpProgress(pet)

  async function save() {
    setError(null)
    try {
      await api('/api/pets/name', { method: 'PUT', body: { name } })
      setEditing(false)
    } catch (e) {
      setError(errorText(e))
    }
  }

  return (
    <div className="pet-card">
      {fresh && <div className="mafia-news">Знакомься!</div>}
      <div className="pet-avatar">🐉</div>
      {editing ? (
        <div className="pet-rename">
          <input value={name} maxLength={20} onChange={(e) => setName(e.target.value)} autoFocus />
          <button className="primary" onClick={() => void save()}>
            Сохранить
          </button>
        </div>
      ) : (
        <h2 className="pet-name">
          {pet.name}{' '}
          <button
            className="link"
            onClick={() => {
              // Ведущий мог переименовать — правим текущую кличку.
              setName(pet.name)
              setEditing(true)
            }}
            aria-label="Переименовать"
          >
            ✏️
          </button>
        </h2>
      )}
      <div className="pet-class">
        {CLASS_NAMES[pet.class]} · {pet.level} уровень
      </div>
      <div className="pet-xp">
        <div className="pet-xp-bar">
          <span style={{ width: `${xp.percent}%` }} />
        </div>
        <small className="muted">
          ещё {xp.left} 🫏 до {pet.level + 1} уровня
        </small>
      </div>
      <div className="pet-stats">
        {STATS.map((s) => (
          <div key={s.key} className="control-row">
            <span>
              {s.icon} {s.name}
            </span>
            <b>{pet[s.key]}</b>
          </div>
        ))}
      </div>
      <div className="pet-derived">
        <span>❤️ {pet.health}</span>
        <span>⚔️ {pet.damage}</span>
        <span>💧 {pet.mana}</span>
      </div>
      <small className="muted">Сила даёт здоровье, ловкость — урон, интеллект — ману. Растёт от заработанных осликов: 50 🫏 — уровень.</small>
      {error && <p className="error">{error}</p>}
    </div>
  )
}

/** Вкладка «Мой дракоша»: пока яйцо — тапать, потом — карточка. */
export function PetTab({ pet }: { pet: PetDto }) {
  const [fresh, setFresh] = useState(false)

  async function hatch(): Promise<boolean> {
    try {
      await api('/api/pets/hatch', { method: 'POST' })
      setFresh(true)
      return true
    } catch {
      return false
    }
  }

  return <section className="card pet-tab">{pet.hatched ? <PetCard pet={pet} fresh={fresh} /> : <Egg onHatched={hatch} />}</section>
}
