import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { api } from '../api/http'
import type { EventType, ProgramItemDto } from '../api/types'
import { BOSS_NAMES, type BossKind } from '../events/boss'
import { errorText } from './errors'
import { EVENT_TYPE_LABELS, moveOrder, nextDraft } from './programOrder'
import { Emoji, EmojiText } from '../components/Emoji'

const CREATABLE: EventType[] = ['Tournament', 'Laugh', 'HideAndSeek', 'PhotoHunt', 'Reaction', 'Tapper', 'Days', 'NeuroPhoto', 'Quiz', 'Mafia', 'BossFight', 'Ceremony', 'Auction']
const WITH_DONKEYS: EventType[] = ['Tournament', 'Laugh', 'HideAndSeek', 'PhotoHunt', 'Reaction', 'Tapper', 'Days', 'Quiz', 'Mafia']
const DEFAULT_MAX: Partial<Record<EventType, number>> = {
  Tournament: 100,
  Laugh: 100,
  HideAndSeek: 100,
  PhotoHunt: 20,
  Reaction: 30,
  Tapper: 30,
  Days: 30,
  Quiz: 100,
  Mafia: 25,
}
const STATUS_LABELS = { Draft: '⏳', Starting: '⏱️ старт', Active: '▶️ идёт', Finished: '✅' } as const

/** Форма нового пункта программы: общие поля + настройки типа. */
function AddEventForm({ onAdded }: { onAdded: () => void }) {
  const [type, setType] = useState<EventType>('PhotoHunt')
  // Название сразу по типу — правится руками; ручную правку смена типа не затирает.
  const [title, setTitle] = useState(EVENT_TYPE_LABELS.PhotoHunt ?? '')
  const [maxDonkeys, setMaxDonkeys] = useState('')
  const [task, setTask] = useState('')
  const [duration, setDuration] = useState('180')
  const [boss, setBoss] = useState<BossKind>('Peasant')
  const [chooseSeconds, setChooseSeconds] = useState('60')
  const [error, setError] = useState<string | null>(null)

  function settings() {
    if (type === 'PhotoHunt') {
      return { task: task.trim() || null, durationSec: Number(duration) || 180 }
    }
    if (type === 'BossFight') {
      // У Крестьянина таймера нет — ждём, пока все подтвердят.
      return { boss, chooseSeconds: boss === 'Peasant' ? null : Number(chooseSeconds) || 60 }
    }
    return undefined
  }

  function changeType(next: EventType) {
    if (title === (EVENT_TYPE_LABELS[type] ?? '')) {
      setTitle(EVENT_TYPE_LABELS[next] ?? '')
    }
    setType(next)
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
          maxDonkeys: maxDonkeys ? Number(maxDonkeys) : undefined,
          settings: settings(),
        },
      })
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
        <select value={type} onChange={(e) => changeType(e.target.value as EventType)}>
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
      {type === 'BossFight' && (
        <>
          <label>
            Босс
            <select value={boss} onChange={(e) => setBoss(e.target.value as BossKind)}>
              {(Object.keys(BOSS_NAMES) as BossKind[]).map((b) => (
                <option key={b} value={b}>
                  {BOSS_NAMES[b]}
                </option>
              ))}
            </select>
          </label>
          {boss === 'Peasant' ? (
            <small className="muted">Без таймера: ход начинается, когда все подтвердят</small>
          ) : (
            <label>
              Секунд на выбор хода
              <input type="number" inputMode="numeric" value={chooseSeconds} onChange={(e) => setChooseSeconds(e.target.value)} />
            </label>
          )}
        </>
      )}
      {WITH_DONKEYS.includes(type) && (
        <div className="row-2">
          {/* В мафии мест нет: поле — сколько каждому победителю, бонусы считаются сами. */}
          <label>
            <EmojiText text={type === 'Mafia' ? 'За победу (каждому) 🫏' : 'Макс. 🫏'} />
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
      {/* Добавление — сверху: программа длинная, листать до конца неудобно. */}
      {adding ? (
        <AddEventForm
          onAdded={() => {
            setAdding(false)
            load()
          }}
        />
      ) : (
          <button
              onClick={() => setAdding(true)}
              style={{ marginBottom: '16px' }}
          >
            + Ивент
          </button>
      )}
      {items.length === 0 && <p className="muted">Пусто — добавь первый ивент</p>}
      <ol className="program">
        {items.map((item) => (
          <li key={item.id} className={item.id === next?.id ? 'program-item next' : 'program-item'}>
            <span className="program-status">
              <EmojiText text={STATUS_LABELS[item.status]} />
            </span>
            <span className="program-title">
              <b>{item.title}</b>
              <small className="muted">
                {EVENT_TYPE_LABELS[item.type]}
                <EmojiText text={item.maxDonkeys > 0 && ` · до ${item.maxDonkeys} 🫏`} />
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
                  <Emoji e="🗑" />
                </button>
              </span>
            )}
            {item.id === next?.id && (
              <button className="primary program-start" onClick={() => void act(`/api/admin/events/${item.id}/start`, { method: 'POST' })}>
                <Emoji e="▶" /> Начать
              </button>
            )}
          </li>
        ))}
      </ol>
      {error && <p className="error">{error}</p>}
    </section>
  )
}
