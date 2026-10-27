import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { api } from '../api/http'
import type { EventType, ProgramItemDto, ScoringScheme } from '../api/types'
import { errorText } from './errors'
import { EVENT_TYPE_LABELS, moveOrder, nextDraft } from './programOrder'

const CREATABLE: EventType[] = ['FightPoll', 'Tournament', 'Laugh', 'HideAndSeek', 'PhotoHunt', 'Reaction', 'Tapper', 'Days', 'NeuroPhoto', 'Quiz', 'Ceremony']
const WITH_DONKEYS: EventType[] = ['Tournament', 'Laugh', 'HideAndSeek', 'PhotoHunt', 'Reaction', 'Tapper', 'Days', 'NeuroPhoto', 'Quiz']
const DEFAULT_MAX: Partial<Record<EventType, number>> = {
  Tournament: 100,
  Laugh: 100,
  HideAndSeek: 100,
  PhotoHunt: 20,
  Reaction: 30,
  Tapper: 30,
  Days: 30,
  NeuroPhoto: 50,
  Quiz: 100,
}
const STATUS_LABELS = { Draft: '⏳', Active: '▶️ идёт', Finished: '✅' } as const

/** Форма нового пункта программы: общие поля + настройки типа. */
function AddEventForm({ onAdded }: { onAdded: () => void }) {
  const [type, setType] = useState<EventType>('PhotoHunt')
  const [title, setTitle] = useState('')
  const [scheme, setScheme] = useState<ScoringScheme>('Linear')
  const [maxDonkeys, setMaxDonkeys] = useState('')
  const [task, setTask] = useState('')
  const [duration, setDuration] = useState('180')
  const [options, setOptions] = useState('Mortal Kombat, UFC')
  const [error, setError] = useState<string | null>(null)

  function settings() {
    if (type === 'PhotoHunt') {
      return { task: task.trim() || null, durationSec: Number(duration) || 180 }
    }
    if (type === 'FightPoll') {
      return { options: options.split(',').map((o) => o.trim()).filter(Boolean) }
    }
    return undefined
  }

  async function submit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    try {
      await api('/api/admin/events', {
        method: 'POST',
        body: {
          type,
          title: title.trim() || EVENT_TYPE_LABELS[type],
          scheme,
          maxDonkeys: maxDonkeys ? Number(maxDonkeys) : undefined,
          settings: settings(),
        },
      })
      setTitle('')
      setTask('')
      onAdded()
    } catch (err) {
      setError(errorText(err))
    }
  }

  return (
    <form className="form add-event" onSubmit={submit}>
      <label>
        Что
        <select value={type} onChange={(e) => setType(e.target.value as EventType)}>
          {CREATABLE.map((t) => (
            <option key={t} value={t}>
              {EVENT_TYPE_LABELS[t]}
            </option>
          ))}
        </select>
      </label>
      <label>
        Название
        <input value={title} onChange={(e) => setTitle(e.target.value)} placeholder={EVENT_TYPE_LABELS[type]} />
      </label>
      {type === 'PhotoHunt' && (
        <>
          <label>
            Задание
            <input value={task} onChange={(e) => setTask(e.target.value)} placeholder="Сфоткайтесь втроём на столе" />
          </label>
          <label>
            Секунд на съёмку
            <input type="number" inputMode="numeric" value={duration} onChange={(e) => setDuration(e.target.value)} />
          </label>
        </>
      )}
      {type === 'FightPoll' && (
        <label>
          Варианты через запятую
          <input value={options} onChange={(e) => setOptions(e.target.value)} />
        </label>
      )}
      {WITH_DONKEYS.includes(type) && (
        <div className="row-2">
          <label>
            Схема
            <select value={scheme} onChange={(e) => setScheme(e.target.value as ScoringScheme)}>
              <option value="Linear">Линейная</option>
              <option value="Podium">Призёры</option>
            </select>
          </label>
          <label>
            Макс. 🫏
            <input
              type="number"
              inputMode="numeric"
              value={maxDonkeys}
              placeholder={String(DEFAULT_MAX[type] ?? '')}
              onChange={(e) => setMaxDonkeys(e.target.value)}
            />
          </label>
        </div>
      )}
      {error && <p className="error">{error}</p>}
      <button type="submit" className="primary">
        + Добавить в программу
      </button>
    </form>
  )
}

/** Программа вечера: порядок, удаление черновиков, «Начать» у следующего. */
export function Program({ version }: { version?: string }) {
  const [items, setItems] = useState<ProgramItemDto[]>([])
  const [error, setError] = useState<string | null>(null)
  const [adding, setAdding] = useState(false)

  const load = useCallback(() => {
    api<ProgramItemDto[]>('/api/admin/events').then(setItems).catch((e) => setError(errorText(e)))
  }, [])

  // Перечитываем при каждом снапшоте — статусы меняются от действий в других местах.
  useEffect(load, [load, version])

  async function act(path: string, init: Parameters<typeof api>[1]) {
    setError(null)
    try {
      await api(path, init)
      load()
    } catch (e) {
      setError(errorText(e))
    }
  }

  const next = nextDraft(items)

  return (
    <section className="card admin-block">
      <h2>Программа вечера</h2>
      {items.length === 0 && <p className="muted">Пусто — добавь первый ивент</p>}
      <ol className="program">
        {items.map((item) => (
          <li key={item.id} className={item.id === next?.id ? 'program-item next' : 'program-item'}>
            <span className="program-status">{STATUS_LABELS[item.status]}</span>
            <span className="program-title">
              <b>{item.title}</b>
              <small className="muted">
                {EVENT_TYPE_LABELS[item.type]}
                {item.maxDonkeys > 0 && ` · до ${item.maxDonkeys} 🫏`}
              </small>
            </span>
            {item.status === 'Draft' && (
              <span className="program-actions">
                <button
                  aria-label="Выше"
                  disabled={moveOrder(items, item.id, -1) === null}
                  onClick={() => void act(`/api/admin/events/${item.id}/order`, { method: 'PUT', body: { order: moveOrder(items, item.id, -1) } })}
                >
                  ↑
                </button>
                <button
                  aria-label="Ниже"
                  disabled={moveOrder(items, item.id, 1) === null}
                  onClick={() => void act(`/api/admin/events/${item.id}/order`, { method: 'PUT', body: { order: moveOrder(items, item.id, 1) } })}
                >
                  ↓
                </button>
                <button
                  aria-label="Удалить"
                  onClick={() => confirm(`Удалить «${item.title}»?`) && void act(`/api/admin/events/${item.id}`, { method: 'DELETE' })}
                >
                  🗑
                </button>
              </span>
            )}
            {item.id === next?.id && (
              <button className="primary program-start" onClick={() => void act(`/api/admin/events/${item.id}/start`, { method: 'POST' })}>
                ▶ Начать
              </button>
            )}
          </li>
        ))}
      </ol>
      {error && <p className="error">{error}</p>}
      {adding ? (
        <AddEventForm
          onAdded={() => {
            setAdding(false)
            load()
          }}
        />
      ) : (
        <button onClick={() => setAdding(true)}>+ Ивент</button>
      )}
    </section>
  )
}
