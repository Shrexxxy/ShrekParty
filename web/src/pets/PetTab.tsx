import { useState } from 'react'
import { api } from '../api/http'
import type { PetDto } from '../api/types'
import { errorText } from '../admin/errors'
import { DonkeyIcon } from '../components/DonkeyIcon'
import { Icon } from '../components/Icon'
import { CLASS_ICONS, CLASS_NAMES, EGG_TAPS, eggStage, STATS, xpProgress } from './pets'

/** Трещины по стадиям: с каждой стадией их больше. */
const CRACK_PATHS = ['M44 62l10 9-6 9 11 8', 'M76 48l-8 11 9 7-7 12', 'M50 104l12-7 4 10 12-6']

/** Яйцо в крапинку; трещины проступают с тапами. */
function EggArt({ stage }: { stage: number }) {
  return (
    <svg className="pet-egg-art" viewBox="0 0 120 150" aria-hidden>
      <path d="M60 6C88 6 110 52 110 92a50 50 0 0 1-100 0C10 52 32 6 60 6z" fill="#fff6df" stroke="#0a1824" strokeWidth="5" />
      <g fill="#9cc3e6">
        <circle cx="42" cy="44" r="6" />
        <circle cx="78" cy="78" r="8" />
        <circle cx="36" cy="100" r="7" />
        <circle cx="86" cy="116" r="5" />
        <circle cx="62" cy="128" r="4" />
      </g>
      <g fill="none" stroke="#0a1824" strokeWidth="4" strokeLinecap="round" strokeLinejoin="round">
        {CRACK_PATHS.slice(0, stage).map((d) => (
          <path key={d} d={d} />
        ))}
      </g>
    </svg>
  )
}

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
      <EggArt stage={stage} />
      <span className="pet-egg-hint">{stage === 3 ? 'Вылупляется…' : `Тапай! Ещё ${EGG_TAPS - taps}`}</span>
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
    <div className={`pet-card class-${pet.class.toLowerCase()}`}>
      {fresh && <div className="stamp">Знакомься!</div>}
      {/* Пока нет арта драконоослика — медальон класса. */}
      <div className="pet-medallion">
        <Icon name={CLASS_ICONS[pet.class]} size="64px" />
      </div>
      {editing ? (
        <div className="pet-rename">
          <input value={name} maxLength={20} onChange={(e) => setName(e.target.value)} autoFocus />
          <button className="primary" onClick={() => void save()}>
            Сохранить
          </button>
        </div>
      ) : (
        <h2 className="pet-name">
          {pet.name}
          <button
            className="pet-rename-btn"
            onClick={() => {
              // Ведущий мог переименовать — правим текущую кличку.
              setName(pet.name)
              setEditing(true)
            }}
            aria-label="Переименовать"
          >
            <Icon name="pencil" />
          </button>
        </h2>
      )}
      <div className="pet-class">
        <span className="class-chip">
          <Icon name={CLASS_ICONS[pet.class]} />
          {CLASS_NAMES[pet.class]}
        </span>
        <span className="pet-level">{pet.level} уровень</span>
      </div>
      <div className="pet-xp">
        <div className="pet-xp-bar">
          <span style={{ width: `${xp.percent}%` }} />
        </div>
        <small className="muted">
          ещё {xp.left} <DonkeyIcon /> до {pet.level + 1} уровня
        </small>
      </div>
      <div className="pet-stats">
        {STATS.map((s) => (
          <div key={s.key} className="pet-stat">
            <Icon name={s.icon} />
            <span>{s.name}</span>
            <b>{pet[s.key]}</b>
          </div>
        ))}
      </div>
      <div className="pet-derived">
        <span className="hp">
          <Icon name="heart" label="здоровье" /> {pet.health}
        </span>
        <span className="dmg">
          <Icon name="sword" label="урон" /> {pet.damage}
        </span>
        <span className="mana">
          <Icon name="drop" label="мана" /> {pet.mana}
        </span>
      </div>
      <small className="muted">
        Сила даёт здоровье, ловкость — урон, интеллект — ману. Растёт от заработанных осликов: 50 <DonkeyIcon /> — уровень.
      </small>
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
