import { useRef, useState } from 'react'
import { api } from '../api/http'
import type { MafiaDetails } from './details'
import { bonusSheet, ROLE_NAMES } from './mafia'
import { GINGER_FIELD, GINGER_PARTS, isSwipe, makeFlies, MAX_TASKS, nextTask, ONION_LAYERS, placePart, retryTask, snaps, swat, type TaskKind } from './nightTasks'

/** Лук: каждый свайп по луковице снимает слой. */
function Onion({ onDone }: { onDone: () => void }) {
  const [layers, setLayers] = useState(ONION_LAYERS)
  const start = useRef<{ x: number; y: number } | null>(null)

  function release(e: React.PointerEvent) {
    if (start.current && isSwipe(e.clientX - start.current.x, e.clientY - start.current.y)) {
      const left = layers - 1
      setLayers(left)
      if (left === 0) {
        onDone()
      }
    }
    start.current = null
  }

  return (
    <div
      className="mafia-task task-onion"
      onPointerDown={(e) => (start.current = { x: e.clientX, y: e.clientY })}
      onPointerUp={release}
      onPointerCancel={() => (start.current = null)}
    >
      <span className="task-onion-bulb" style={{ transform: `scale(${0.4 + layers * 0.12})` }}>
        🧅
      </span>
      <small className="muted">Почисти лук свайпами — слоёв осталось: {layers}</small>
    </div>
  )
}

/** Мухи: тапни каждую. */
function Flies({ onDone }: { onDone: () => void }) {
  const [flies, setFlies] = useState(() => makeFlies())

  function hit(id: number) {
    const left = swat(flies, id)
    setFlies(left)
    if (left.length === 0) {
      onDone()
    }
  }

  return (
    <div className="mafia-task task-flies">
      {flies.map((f) => (
        <button key={f.id} className="task-fly" style={{ left: `${f.x}%`, top: `${f.y}%`, animationDelay: `${-f.id * 0.4}s` }} onPointerDown={() => hit(f.id)}>
          🪰
        </button>
      ))}
      <small className="muted task-flies-hint">Отгони мух: осталось {flies.length}</small>
    </div>
  )
}

/** Пряня: перетащи детали на контур — рядом со своим местом деталь прилипает. */
function Gingerbread({ onDone }: { onDone: () => void }) {
  const field = useRef<HTMLDivElement>(null)
  const [placed, setPlaced] = useState<string[]>([])
  const [drag, setDrag] = useState<{ id: string; x: number; y: number } | null>(null)

  function point(e: React.PointerEvent) {
    const rect = field.current!.getBoundingClientRect()
    // Поле может быть ужато по ширине экрана — переводим в его собственные координаты.
    return { x: ((e.clientX - rect.left) / rect.width) * GINGER_FIELD.width, y: ((e.clientY - rect.top) / rect.height) * GINGER_FIELD.height }
  }

  function drop(e: React.PointerEvent) {
    const part = GINGER_PARTS.find((p) => p.id === drag?.id)
    setDrag(null)
    if (!part || !snaps(part, point(e))) {
      return
    }

    const next = placePart(placed, part.id)
    setPlaced(next)
    if (next.length === GINGER_PARTS.length) {
      onDone()
    }
  }

  const pct = (x: number, y: number) => ({ left: `${(x / GINGER_FIELD.width) * 100}%`, top: `${(y / GINGER_FIELD.height) * 100}%` })

  return (
    <div
      ref={field}
      className="mafia-task task-ginger"
      style={{ aspectRatio: `${GINGER_FIELD.width} / ${GINGER_FIELD.height}` }}
      onPointerMove={(e) => drag && setDrag({ id: drag.id, ...point(e) })}
      onPointerUp={drop}
      onPointerCancel={() => setDrag(null)}
    >
      <span className="task-ginger-body" style={pct(150, 120)}>
        🍪
      </span>
      {GINGER_PARTS.map((p) => (
        <span key={`slot-${p.id}`} className="task-ginger-slot" style={pct(p.slot.x, p.slot.y)} />
      ))}
      {GINGER_PARTS.map((p) => {
        const where = placed.includes(p.id) ? p.slot : drag?.id === p.id ? drag : p.start
        return (
          <span
            key={p.id}
            className={`task-ginger-part ${placed.includes(p.id) ? 'placed' : ''}`}
            style={pct(where.x, where.y)}
            onPointerDown={(e) => {
              if (!placed.includes(p.id)) {
                field.current!.setPointerCapture(e.pointerId)
                setDrag({ id: p.id, ...point(e) })
              }
            }}
          >
            {p.label}
          </span>
        )
      })}
      <small className="muted task-ginger-hint">Собери пряню: перетащи детали на контур</small>
    </div>
  )
}

/**
 * Ночные задания одно за другим, до пяти. Роли делают их для вида — сервер сам решает, кому ослики.
 */
export function NightTasks({ eventId, details }: { eventId: string; details: MafiaDetails }) {
  const [kind, setKind] = useState<TaskKind>(() => nextTask(null))
  // Номер нужен, чтобы то же задание после «Дальше» начиналось с нуля.
  const [round, setRound] = useState(0)
  const [sending, setSending] = useState(false)
  const done = details.myTasksDone

  async function complete() {
    setSending(true)
    for (let attempt = 0; attempt < 3; attempt++) {
      try {
        await api(`/api/mafia/${eventId}/task-done`, { method: 'POST' })
        break
      } catch (e) {
        if (!retryTask(e)) {
          break
        }

        // Задание быстрее полутора секунд — сервер просит подождать.
        await new Promise((r) => setTimeout(r, 1600))
      }
    }

    setKind((k) => nextTask(k))
    setRound((r) => r + 1)
    setSending(false)
  }

  if (done >= MAX_TASKS) {
    return <div className="mafia-news">Все задания сделаны 💪 Жди утра</div>
  }

  const Task = { onion: Onion, flies: Flies, gingerbread: Gingerbread }[kind]
  return (
    <div className="mafia-tasks">
      <b>
        Задание {done + 1} из {MAX_TASKS}
      </b>
      {sending ? <div className="mafia-task muted">Засчитываем…</div> : <Task key={round} onDone={() => void complete()} />}
    </div>
  )
}

/** Ослики за партию: кто за что сколько. Игроку во время игры — только своя строка. */
export function BonusSheet({ details }: { details: MafiaDetails }) {
  const ids = new Set(details.bonuses.map((b) => b.userId))
  const rows = bonusSheet(details).filter((r) => details.phase === 'Ended' || ids.has(r.userId))
  if (rows.length === 0) {
    return null
  }

  return (
    <div className="mafia-bonuses">
      {rows.map((r) => (
        <div key={r.userId} className="mafia-bonus-row">
          <div className="control-row">
            <span>
              <b>{r.login}</b>
              {r.role && <small className="muted"> · {ROLE_NAMES[r.role]}</small>}
            </span>
            <b>🫏 {r.total}</b>
          </div>
          {details.phase === 'Ended' && <small>{r.win > 0 ? `Победа команды: ${r.win}` : 'Победа команды: —'}</small>}
          {r.lines.map((line) => (
            <small key={line}>{line}</small>
          ))}
        </div>
      ))}
    </div>
  )
}
