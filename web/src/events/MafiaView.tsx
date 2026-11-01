import { useState } from 'react'
import { api } from '../api/http'
import { errorText } from '../admin/errors'
import { Avatar } from '../components/Avatar'
import { formatClock } from './countdown'
import type { MafiaDetails } from './details'
import { myStatus, nightOptions, phaseTitle, ROLE_NAMES, voteTally } from './mafia'
import { BonusSheet, NightTasks } from './MafiaTasks'
import { useCountdown } from './useCountdown'

type Player = MafiaDetails['players'][number]

function Clock({ deadline, serverTime }: { deadline: string; serverTime: string }) {
  return <b className="quiz-clock">{formatClock(useCountdown(deadline, serverTime))}</b>
}

function loginOf(details: MafiaDetails, userId: string | null): string {
  return details.players.find((p) => p.userId === userId)?.login ?? '—'
}

/** Стол: живые и выбывшие; роль — только если её положено видеть этому зрителю. */
export function MafiaTable({ details, showRoles = true }: { details: MafiaDetails; showRoles?: boolean }) {
  return (
    <div className="mafia-table">
      {details.players.map((p) => (
        <span key={p.userId} className={['mafia-seat', !p.alive && 'dead', p.userId === details.speakerId && 'speaking'].filter(Boolean).join(' ')}>
          <Avatar url={p.avatarUrl} login={p.login} size={40} />
          <span>{p.login}</span>
          {showRoles && p.role && <small className={`mafia-role ${p.role.toLowerCase()}`}>{ROLE_NAMES[p.role]}</small>}
        </span>
      ))}
    </div>
  )
}

/**
 * Роль — пока держишь палец: соседи не подсмотрят. Там же напарник мафии с его выбором и проверки комиссара.
 */
function RoleCard({ details }: { details: MafiaDetails }) {
  const [shown, setShown] = useState(false)
  const role = details.myRole
  if (!role) {
    return null
  }

  return (
    <button
      className={`mafia-role-card ${shown ? 'shown' : ''}`}
      onPointerDown={() => setShown(true)}
      onPointerUp={() => setShown(false)}
      onPointerLeave={() => setShown(false)}
      onContextMenu={(e) => e.preventDefault()}
    >
      {!shown ? (
        'Удерживай, чтобы увидеть роль'
      ) : (
        <span className="mafia-role-inside">
          <b>{ROLE_NAMES[role]}</b>
          {details.partners.length > 0 && (
            <small>
              Напарник: {details.partners.map((id) => loginOf(details, id)).join(', ')}
              {details.nightActions.map((a) => ` → ${loginOf(details, a.targetId)}`).join('')}
            </small>
          )}
          {details.checks.map((c) => (
            <small key={`${c.day}-${c.targetId}`}>
              Ночь {c.day}: {loginOf(details, c.targetId)} — {c.isMafia ? 'мафия' : 'не мафия'}
            </small>
          ))}
        </span>
      )}
    </button>
  )
}

/** Список игроков для выбора — ночью у всех ролей одинаковый, чтобы со стороны нельзя было понять, кто есть кто. */
function PickList({ players, selected, onPick }: { players: Player[]; selected: string | null; onPick: (id: string) => void }) {
  return (
    <div className="neuro-options">
      {players.map((p) => (
        <button key={p.userId} className={p.userId === selected ? 'on' : ''} onClick={() => onPick(p.userId)}>
          <Avatar url={p.avatarUrl} login={p.login} size={24} /> {p.login}
        </button>
      ))}
    </div>
  )
}

/**
 * Ночь на телефоне: мирный сразу делает задания; роль сначала выбирает, потом задания для вида (мафия и доктор могут передумать).
 */
function NightScreen({ eventId, details, myId, onPick }: { eventId: string; details: MafiaDetails; myId?: string; onPick: (id: string) => void }) {
  const [changing, setChanging] = useState(false)
  const role = details.myRole
  const picking = role !== 'Civilian' && (details.myNightTarget === null || changing)

  if (picking) {
    return (
      <>
        <p>{role === 'Mafia' ? 'Кого убиваем?' : role === 'Doctor' ? 'Кого лечишь?' : 'Кого проверяешь?'}</p>
        <PickList
          players={nightOptions(details, myId)}
          selected={details.myNightTarget}
          onPick={(id) => {
            onPick(id)
            setChanging(false)
          }}
        />
      </>
    )
  }

  return (
    <>
      {role !== 'Civilian' && (
        <small className="muted">
          Выбор: {loginOf(details, details.myNightTarget)}
          {role !== 'Commissar' && (
            <>
              {' · '}
              <button className="link" onClick={() => setChanging(true)}>
                сменить
              </button>
            </>
          )}
        </small>
      )}
      <NightTasks eventId={eventId} details={details} />
    </>
  )
}

/** Раскрытие голосов: за кого сколько. */
function VotesReveal({ details }: { details: MafiaDetails }) {
  const tally = voteTally(details.votes)
  return (
    <div className="mafia-votes">
      {tally.counts.map((c) => (
        <div key={c.targetId} className="control-row">
          <span>
            {loginOf(details, c.targetId)} — <b>{c.voters.length}</b>
          </span>
          <small className="muted">{c.voters.map((v) => loginOf(details, v)).join(', ')}</small>
        </div>
      ))}
      {tally.abstained.length > 0 && <small className="muted">Воздержались: {tally.abstained.map((v) => loginOf(details, v)).join(', ')}</small>}
    </div>
  )
}

/** Итог фазы (утро, голосование, конец) — одинаково на ТВ и телефонах. */
function PhaseNews({ details }: { details: MafiaDetails }) {
  switch (details.phase) {
    case 'Morning':
      return <div className="mafia-news">{details.nightVictim ? `Ночью убит ${loginOf(details, details.nightVictim)}` : 'Ночью никто не погиб'}</div>
    case 'VoteResult':
    case 'LastWord':
      return (
        <>
          {details.phase === 'VoteResult' && <VotesReveal details={details} />}
          <div className="mafia-news">
            {details.eliminated
              ? `Выбывает ${loginOf(details, details.eliminated.userId)} — ${ROLE_NAMES[details.eliminated.role]}`
              : 'Никто не выбывает'}
          </div>
        </>
      )
    case 'Ended':
      return (
        <div className="mafia-news">
          {details.winner === 'Mafia' ? 'Победила мафия 🔫' : 'Победили мирные жители 🕊'}
          {details.eliminated && (
            <small>
              {' '}
              (голосованием выгнали {loginOf(details, details.eliminated.userId)} — {ROLE_NAMES[details.eliminated.role]})
            </small>
          )}
          {details.nightVictim && <small> (последняя ночь: убит {loginOf(details, details.nightVictim)})</small>}
        </div>
      )
    default:
      return null
  }
}

/** Мафия: на телефоне — роль, ночные ходы, речь, голос; на ТВ — стол, говорящий и итоги, без ролей. */
export function MafiaView({ eventId, details, serverTime, myId, tv }: { eventId: string; details: MafiaDetails; serverTime: string; myId?: string; tv?: boolean }) {
  const [error, setError] = useState<string | null>(null)
  const phase = details.phase
  const others = details.players.filter((p) => p.alive && p.userId !== myId)
  const status = myStatus(details)

  async function send(path: string, body?: unknown) {
    setError(null)
    try {
      await api(`/api/mafia/${eventId}/${path}`, { method: 'POST', body })
    } catch (e) {
      setError(errorText(e))
    }
  }

  const header = (
    <div className="mafia-head">
      <span>{phaseTitle(phase)}</span>
      {details.deadline && phase !== 'Ended' && <Clock deadline={details.deadline} serverTime={serverTime} />}
    </div>
  )

  if (tv) {
    return (
      <div className="mafia tv">
        {header}
        {phase === 'Speech' && details.speakerId && (
          <div className="mafia-news">
            Говорит {loginOf(details, details.speakerId)}
            <small className="muted"> · дальше: {details.speechQueue.slice(details.speechQueue.indexOf(details.speakerId) + 1).map((id) => loginOf(details, id)).join(', ') || '—'}</small>
          </div>
        )}
        {phase === 'Voting' && (
          <div className="mafia-news">
            Проголосовали {details.votedCount} из {details.players.filter((p) => p.alive).length}
          </div>
        )}
        {phase === 'LastWord' && details.eliminated && <div className="mafia-news">Последнее слово: {loginOf(details, details.eliminated.userId)}</div>}
        {phase === 'Night' && <div className="mafia-news">Выполнено заданий: {details.tasksTotal}</div>}
        <PhaseNews details={details} />
        <MafiaTable details={details} />
        {phase === 'Ended' && <BonusSheet details={details} />}
      </div>
    )
  }

  return (
    <div className="mafia">
      {header}
      <RoleCard details={details} />
      {status === 'outside' && phase !== 'Ended' && <p className="muted">Ты не в этой партии — смотри на ТВ 📺</p>}
      {status === 'dead' && phase !== 'Ended' && <p className="muted">Ты выбыл — смотри и молчи 🤫</p>}

      {phase === 'Intro' && status !== 'outside' && <p className="muted">Ночь знакомства. Мафия знакомится — подсмотри свою роль и жди утра.</p>}

      {phase === 'Night' && status === 'alive' && (
        <NightScreen details={details} myId={myId} onPick={(id) => void send('night', { targetId: id })} eventId={eventId} />
      )}

      {phase === 'Speech' &&
        (details.speakerId === myId ? (
          <>
            <div className="mafia-news">Твоё слово!</div>
            <button className="primary" onClick={() => void send('speech-done')}>
              Я закончил
            </button>
          </>
        ) : (
          <div className="mafia-news">Говорит {loginOf(details, details.speakerId)}</div>
        ))}

      {phase === 'Voting' &&
        details.iAmAlive &&
        (details.iVoted ? (
          <p className="muted">
            Голос принят. Ждём остальных ({details.votedCount} из {details.players.filter((p) => p.alive).length})
          </p>
        ) : (
          <>
            <p>Кого выгоняем? Голос не поменять.</p>
            <PickList players={others} selected={null} onPick={(id) => confirm(`Голосовать против ${loginOf(details, id)}?`) && void send('vote', { targetId: id })} />
            <button onClick={() => confirm('Воздержаться?') && void send('vote', { targetId: null })}>Воздержаться</button>
          </>
        ))}

      <PhaseNews details={details} />
      {phase === 'Ended' && <MafiaTable details={details} />}
      {phase !== 'Ended' && details.bonuses.length > 0 && (
        <details className="card">
          <summary>Твои ослики за партию: 🫏 {details.bonuses.reduce((sum, b) => sum + b.amount, 0)}</summary>
          <BonusSheet details={details} />
        </details>
      )}
      {phase === 'Ended' && <BonusSheet details={details} />}
      {error && <p className="error">{error}</p>}
    </div>
  )
}
