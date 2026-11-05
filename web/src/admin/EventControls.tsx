import { useEffect, useRef, useState } from 'react'
import type { ActiveEventDto } from '../api/types'
import { Avatar } from '../components/Avatar'
import type { AuctionDetails, CeremonyDetails, MafiaDetails, DaysDetails, HideDetails, QuizDetails, LaughDetails, MatchDto, PhotoHuntDetails, TournamentDetails } from '../events/details'
import { formatClock } from '../events/countdown'
import { EventView } from '../events/EventView'
import { PhotoView } from '../events/PhotoView'
import { ROUND_TITLES } from '../events/tournament'
import { useCountdown } from '../events/useCountdown'
import type { BossDetails } from '../events/boss'
import { AuctionControls } from './AuctionControls'
import { BossControls } from './BossControls'
import { CameraButton } from './CameraButton'
import { MafiaControls } from './MafiaControls'
import { QuizControls } from './QuizControls'
import { useAdminAction } from './useAdminAction'
import { Emoji, EmojiText } from '../components/Emoji'

/** Карточка матча в пульте: начать, победитель со счётом, отмена, фото. */
function MatchControl({ match }: { match: MatchDto }) {
  const { run, error, busy } = useAdminAction()
  const [score, setScore] = useState('')
  const { playerA: a, playerB: b } = match

  return (
    <div className={`card match-control ${match.status.toLowerCase()}`}>
      <small className="muted">
        {ROUND_TITLES[match.round]} · {match.status === 'Pending' ? 'ждёт' : match.status === 'Live' ? 'идёт' : 'сыгран'}
        <EmojiText text={match.pool > 0 && ` · банк ${match.pool} 🫏`} />
      </small>
      <div className="match-players">
        {[a, b].map((p, i) =>
          p ? (
            <span key={p.id} className={match.winnerId === p.id ? 'won' : ''}>
              <Avatar url={p.avatarUrl} login={p.login} size={24} /> {p.login}
            </span>
          ) : (
            <span key={i} className="muted">
              …
            </span>
          ),
        )}
      </div>
      {a && b && match.status !== 'Done' && (
        <>
          {match.status === 'Pending' && (
            <button disabled={busy} onClick={() => void run(`/api/admin/matches/${match.id}/start`)}>
              <Emoji e="▶" /> Начать (закрыть ставки)
            </button>
          )}
          <input value={score} onChange={(e) => setScore(e.target.value)} placeholder="Счёт, например 3:1" />
          <div className="bet-sides">
            {[a, b].map((p) => (
              <button
                key={p.id}
                className="primary"
                disabled={busy}
                onClick={() =>
                  confirm(`Победил ${p.login}?`) &&
                  void run(`/api/admin/matches/${match.id}/winner`, { method: 'POST', body: { winnerId: p.id, score: score || null } })
                }
              >
                <Emoji e="🏆" /> {p.login}
              </button>
            ))}
          </div>
        </>
      )}
      <div className="control-row">
        {match.status === 'Done' && (
          <button disabled={busy} onClick={() => confirm('Отменить результат?') && void run(`/api/admin/matches/${match.id}/undo`)}>
            ↩ Отменить
          </button>
        )}
        {a && b && (
          <CameraButton disabled={busy} onPhoto={(form) => void run(`/api/admin/matches/${match.id}/photos`, { method: 'POST', body: form })}>
            <Emoji e="📸" /> Фото {match.photos > 0 && `(${match.photos})`}
          </CameraButton>
        )}
      </div>
      {error && <p className="error">{error}</p>}
    </div>
  )
}

function TournamentControls({ details }: { details: TournamentDetails }) {
  const playable = details.matches.filter((m) => !m.isBye)
  const open = playable.filter((m) => m.status !== 'Done')
  const done = playable.filter((m) => m.status === 'Done').reverse()
  return (
    <div className="controls">
      {details.challenge.state === 'Offered' && <p><Emoji e="⏳" /> Победитель решает про вызов… Завершишь турнир сейчас — засчитается отказ.</p>}
      {open.map((m) => (
        <MatchControl key={m.id} match={m} />
      ))}
      {done.length > 0 && (
        <details>
          <summary>Сыгранные ({done.length})</summary>
          {done.map((m) => (
            <MatchControl key={m.id} match={m} />
          ))}
        </details>
      )}
    </div>
  )
}

function LaughControls({ event, details }: { event: ActiveEventDto; details: LaughDetails }) {
  const { run, error } = useAdminAction()

  function save(userId: string, game: number, value: string) {
    const points = value.trim() === '' ? null : Number(value)
    void run(`/api/admin/laugh/${event.id}/scores`, { method: 'PUT', body: { userId, game, points } })
  }

  return (
    <div className="controls">
      <table className="laugh">
        <thead>
          <tr>
            <th>Игрок</th>
            <th>1</th>
            <th>2</th>
            <th>3</th>
          </tr>
        </thead>
        <tbody>
          {details.rows.map((r) => (
            <tr key={r.userId}>
              <td>{r.login}</td>
              {r.games.map((g, i) => (
                <td key={i}>
                  <input
                    // Ключ по значению: пришло новое из снапшота — ячейка обновится.
                    key={`${r.userId}-${i}-${g ?? ''}`}
                    className="score-input"
                    type="number"
                    inputMode="numeric"
                    defaultValue={g ?? ''}
                    onBlur={(e) => e.target.value !== String(g ?? '') && save(r.userId, i + 1, e.target.value)}
                  />
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
      <small className="muted">Пусто — не играл. Сохраняется, когда уходишь из ячейки.</small>
      {error && <p className="error">{error}</p>}
    </div>
  )
}

const HIDE_PHASES: Record<HideDetails['phase'], React.ReactNode> = {
  Hiding: <><Emoji e="🙈" /> Прячутся</>,
  Shooting: <><Emoji e="📸" /> Съёмка</>,
  Voting: <><Emoji e="😂" /> Голосование</>,
};

/** Экран отметок: фото и галочки, кто на нём; пусто — фото без людей. */
function HideMarker({
  photoUrl,
  players,
  initial,
  busy,
  onSave,
  onCancel,
}: {
  photoUrl: string
  players: HideDetails['players']
  initial: string[]
  busy: boolean
  onSave: (userIds: string[]) => void
  onCancel: () => void
}) {
  const [marked, setMarked] = useState<string[]>(initial)
  const toggle = (id: string) => setMarked((m) => (m.includes(id) ? m.filter((x) => x !== id) : [...m, id]))

  return (
    <div className="card hide-marker">
      <img src={photoUrl} alt="" />
      <div className="hide-marks">
        {players.map((p) => (
          <label key={p.userId} className={marked.includes(p.userId) ? 'on' : ''}>
            <input type="checkbox" checked={marked.includes(p.userId)} onChange={() => toggle(p.userId)} />
            <Avatar url={p.avatarUrl} login={p.login} size={24} /> {p.login}
          </label>
        ))}
      </div>
      <div className="bet-sides">
        <button disabled={busy} onClick={onCancel}>
          Отмена
        </button>
        <button className="primary" disabled={busy} onClick={() => onSave(marked)}>
          Сохранить{marked.length === 0 && ' (никого)'}
        </button>
      </div>
    </div>
  )
}

function HideControls({ event, details, serverTime }: { event: ActiveEventDto; details: HideDetails; serverTime: string }) {
  const { run, error, busy } = useAdminAction()
  const left = useCountdown(details.deadline, serverTime)
  const [pending, setPending] = useState<{ form: FormData; url: string } | null>(null)
  const [editing, setEditing] = useState<string | null>(null)
  const logins = new Map(details.players.map((p) => [p.userId, p.login]))
  const editShot = details.shots.find((s) => s.id === editing)
  const marking = Boolean(pending || editShot)
  const top = useRef<HTMLDivElement>(null)

  // Экран отметок выше остального пульта: после «Сохранить» страница остаётся внизу — возвращаем к кнопке «Фото».
  useEffect(() => {
    top.current?.scrollIntoView({ block: 'start' })
  }, [marking])

  function takePhoto(form: FormData) {
    setPending({ form, url: URL.createObjectURL(form.get('file') as Blob) })
  }

  function closePending() {
    if (pending) {
      URL.revokeObjectURL(pending.url)
    }
    setPending(null)
  }

  async function saveShot(userIds: string[]) {
    pending!.form.set('userIds', userIds.join(','))
    if (await run(`/api/admin/hide/${event.id}/shots`, { method: 'POST', body: pending!.form })) {
      closePending()
    }
  }

  async function saveMarks(userIds: string[]) {
    if (await run(`/api/admin/hide/shots/${editing}/players`, { method: 'PUT', body: { userIds } })) {
      setEditing(null)
    }
  }

  if (pending) {
    return (
      <div ref={top}>
        <HideMarker photoUrl={pending.url} players={details.players} initial={[]} busy={busy} onSave={(ids) => void saveShot(ids)} onCancel={closePending} />
      </div>
    )
  }

  if (editShot) {
    return (
      <div ref={top}>
        <HideMarker
          photoUrl={editShot.url}
          players={details.players}
          initial={editShot.userIds}
          busy={busy}
          onSave={(ids) => void saveMarks(ids)}
          onCancel={() => setEditing(null)}
        />
      </div>
    )
  }

  return (
    <div className="controls" ref={top}>
      <p>
        {HIDE_PHASES[details.phase]} · {formatClock(left)}
      </p>
      {details.phase === 'Hiding' && <p className="muted">Жди: съёмка начнётся сама. Вспышку включи в камере заранее.</p>}
      {details.phase === 'Shooting' && (
        <div className="bet-sides">
          <CameraButton disabled={busy} onPhoto={takePhoto}>
            <Emoji e="📸" /> Фото
          </CameraButton>
          <button disabled={busy} onClick={() => confirm(details.shots.length > 0 ? 'Закончить съёмку и начать голосование?' : 'Снимков нет — завершить прятки?') && void run(`/api/admin/hide/${event.id}/stop`)}>
            <Emoji e="⏹" /> Закончить съёмку
          </button>
        </div>
      )}

      {[...details.players]
        .sort((a, b) => a.hits - b.hits)
        .map((p) => (
          <div key={p.userId} className="control-row">
            <span>
              <Avatar url={p.avatarUrl} login={p.login} size={24} /> {p.login}
            </span>
            <b><Emoji e="📸" /> {p.hits}</b>
          </div>
        ))}

      {details.shots.length > 0 && (
        <div className="hide-wall admin">
          {details.shots.map((s) => (
            <figure key={s.id}>
              <img src={s.url} alt="" loading="lazy" />
              <figcaption>
                {s.userIds.map((id) => logins.get(id)).join(', ') || 'никого'}
                <EmojiText text={details.phase === 'Voting' && ` · 😂 ${s.votes}`} />
              </figcaption>
              <div className="bet-sides">
                <button disabled={busy} onClick={() => setEditing(s.id)}>
                  <Emoji e="✏️" />
                </button>
                {details.phase !== 'Voting' && (
                  <button disabled={busy} onClick={() => confirm('Удалить фото?') && void run(`/api/admin/hide/shots/${s.id}`, { method: 'DELETE' })}>
                    <Emoji e="🗑" />
                  </button>
                )}
              </div>
            </figure>
          ))}
        </div>
      )}
      {error && <p className="error">{error}</p>}
    </div>
  )
}

function CeremonyControls({ event, details }: { event: ActiveEventDto; details: CeremonyDetails }) {
  const { run, error, busy } = useAdminAction()
  const last = details.step + 1 >= details.totalSteps
  return (
    <div className="controls">
      <p>
        Шаг {details.step + 1} из {details.totalSteps}
      </p>
      <button className="primary ceremony-next" disabled={busy || last} onClick={() => void run(`/api/admin/ceremony/${event.id}/next`)}>
        <EmojiText text={last ? 'Это финал 🎉' : 'Дальше ▶'} />
      </button>
      {error && <p className="error">{error}</p>}
    </div>
  )
}

/** Пульт текущего ивента по типу. */
export function EventControls({ event, serverTime }: { event: ActiveEventDto; serverTime: string }) {
  const details = event.details
  switch (event.type) {
    case 'Tournament':
      return <TournamentControls details={details as TournamentDetails} />
    case 'Laugh':
      return <LaughControls event={event} details={details as LaughDetails} />
    case 'HideAndSeek':
      return <HideControls event={event} details={details as HideDetails} serverTime={serverTime} />
    case 'PhotoHunt':
      return <PhotoView eventId={event.id} details={details as PhotoHuntDetails} serverTime={serverTime} tv />
    case 'Ceremony':
      return <CeremonyControls event={event} details={details as CeremonyDetails} />
    case 'BossFight':
      return <BossControls event={event} details={details as BossDetails} serverTime={serverTime} />
    case 'Mafia':
      return <MafiaControls event={event} details={details as MafiaDetails} serverTime={serverTime} />
    case 'Auction':
      return <AuctionControls event={event} details={details as AuctionDetails} serverTime={serverTime} />
    case 'Quiz':
      return <QuizControls event={event} details={details as QuizDetails} serverTime={serverTime} />
    case 'Reaction':
    case 'Tapper':
    case 'NeuroPhoto':
      // Идут сами по таймерам — пульту достаточно видеть то же, что ТВ.
      return <EventView event={event} serverTime={serverTime} tv />
    case 'Days':
      return (
        <>
          <EventView event={event} serverTime={serverTime} tv />
          {(details as DaysDetails).phase === 'Reveal' && <p className="muted">Когда раскрытие на ТВ закончится — «<Emoji e="⏹" /> Завершить».</p>}
        </>
      )
    default:
      return null
  }
}
