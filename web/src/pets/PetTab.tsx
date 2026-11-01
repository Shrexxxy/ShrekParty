import { useState, type FormEvent } from 'react'
import { api } from '../api/http'
import type { PetDto } from '../api/types'
import { errorText } from '../admin/errors'
import { DonkeyIcon } from '../components/DonkeyIcon'
import { Icon } from '../components/Icon'
import { Sprite } from '../components/Sprite'
import { CLASS_ICONS, CLASS_NAMES, EGG_TAPS, eggArt, eggStage, petArt, STATS, xpProgress } from './pets'

/** Трещины по стадиям: с каждой стадией их больше. Координаты — в квадрате 100×100 поверх картинки яйца. */
const CRACK_PATHS = ['M38 40l7 6-4 6 8 6', 'M62 32l-5 8 6 5-4 9', 'M40 72l8-5 3 7 8-4']

/** Запасное яйцо, пока нет картинки своего цвета. */
function EggFallback() {
  return (
    <svg viewBox="0 0 120 150" width="100%" height="100%" aria-hidden>
      <path d="M60 6C88 6 110 52 110 92a50 50 0 0 1-100 0C10 52 32 6 60 6z" fill="#fff6df" stroke="#0a1824" strokeWidth="5" />
    </svg>
  )
}

/** Яйцо цвета будущего питомца; трещины проступают с тапами. */
function EggArt({ pet, stage }: { pet: PetDto; stage: number }) {
  return (
    <div className="pet-egg-art">
      <Sprite src={eggArt(pet)} fallback={<EggFallback />} className="pet-egg-img" />
      <svg className="pet-egg-cracks" viewBox="0 0 100 100" aria-hidden>
        <g fill="none" stroke="#0a1824" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round">
          {CRACK_PATHS.slice(0, stage).map((d) => (
            <path key={d} d={d} />
          ))}
        </g>
      </svg>
    </div>
  )
}

/** Яйцо: тапы считает телефон, на последнем — «вылупился» серверу; не дошло — ещё один тап. */
function Egg({ pet, onHatched }: { pet: PetDto; onHatched: () => Promise<boolean> }) {
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
      <EggArt pet={pet} stage={stage} />
      <span className="pet-egg-hint">{stage === 3 ? 'Вылупляется…' : 'Тапай!'}</span>
      {failed && <small className="error">Не получилось — проверь интернет и тапни ещё раз</small>}
    </button>
  )
}

/** Кличка — один раз, сразу после вылупления; потом поменять может только ведущий. */
function NameForm({ pet }: { pet: PetDto }) {
  const [name, setName] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function save(e: FormEvent) {
    e.preventDefault()
    setError(null)
    setBusy(true)
    try {
      await api('/api/pets/name', { method: 'PUT', body: { name } })
    } catch (err) {
      setError(errorText(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <form className="pet-name-form" onSubmit={(e) => void save(e)}>
      <b>Как назовёшь?</b>
      <input value={name} maxLength={20} placeholder={pet.name} onChange={(e) => setName(e.target.value)} autoFocus />
      <small className="muted">Кличку дают один раз — подумай</small>
      <button type="submit" className="primary" disabled={busy || name.trim() === ''}>
        Назвать
      </button>
      {error && <p className="error">{error}</p>}
    </form>
  )
}

/** Карточка: класс, уровень, опыт, характеристики и что они дают в бою. */
function PetCard({ pet, fresh }: { pet: PetDto; fresh: boolean }) {
  const xp = xpProgress(pet)

  return (
    <div className={`pet-card class-${pet.class.toLowerCase()}`}>
      {fresh && <div className="stamp">Знакомься!</div>}
      <Sprite
        src={petArt(pet)}
        alt={pet.name}
        className="pet-art"
        fallback={
          <span className="pet-medallion">
            <Icon name={CLASS_ICONS[pet.class]} size="64px" />
          </span>
        }
      />
      {pet.named ? <h2 className="pet-name">{pet.name}</h2> : <NameForm pet={pet} />}
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

  return <section className="card pet-tab">{pet.hatched ? <PetCard pet={pet} fresh={fresh} /> : <Egg pet={pet} onHatched={hatch} />}</section>
}
