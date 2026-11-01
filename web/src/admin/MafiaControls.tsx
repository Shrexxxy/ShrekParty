import type { ActiveEventDto } from '../api/types'
import { formatClock } from '../events/countdown'
import type { MafiaDetails } from '../events/details'
import { MafiaTable } from '../events/MafiaView'
import { phaseTitle } from '../events/mafia'
import { useCountdown } from '../events/useCountdown'
import { useAdminAction } from './useAdminAction'

function Clock({ deadline, serverTime }: { deadline: string; serverTime: string }) {
  return <b>{formatClock(useCountdown(deadline, serverTime))}</b>
}

/** Пульт ведущего мафии: видит все роли, ночные ходы, проверки и голоса; «Дальше» заканчивает фазу раньше. */
export function MafiaControls({ event, details, serverTime }: { event: ActiveEventDto; details: MafiaDetails; serverTime: string }) {
  const { run, error, busy } = useAdminAction()
  const login = (id: string | null) => details.players.find((p) => p.userId === id)?.login ?? '—'
  const phase = details.phase

  return (
    <div className="controls">
      <p>
        {phaseTitle(phase)} · день {details.day}
        {details.deadline && phase !== 'Ended' && (
          <>
            {' · '}
            <Clock deadline={details.deadline} serverTime={serverTime} />
          </>
        )}
      </p>
      <MafiaTable details={details} />

      {phase === 'Night' && (
        <div className="card">
          <b>Ходы ночи</b>
          {details.nightActions.length === 0 && <p className="muted">Пока никто не сходил</p>}
          {details.nightActions.map((a) => (
            <div key={a.userId} className="control-row">
              <span>{login(a.userId)}</span>
              <span>→ {login(a.targetId)}</span>
            </div>
          ))}
          {details.lastHeal && <small className="muted">Прошлой ночью доктор лечил: {login(details.lastHeal)}</small>}
        </div>
      )}

      {details.checks.length > 0 && (
        <details>
          <summary>Проверки комиссара</summary>
          {details.checks.map((c) => (
            <div key={`${c.day}-${c.targetId}`} className="control-row">
              <span>
                Ночь {c.day}: {login(c.targetId)}
              </span>
              <span>{c.isMafia ? 'мафия' : 'не мафия'}</span>
            </div>
          ))}
        </details>
      )}

      {phase === 'Speech' && (
        <p>
          Говорит <b>{login(details.speakerId)}</b>
        </p>
      )}

      {(phase === 'Voting' || phase === 'VoteResult') && (
        <div className="card">
          <b>
            Голоса ({details.votedCount} из {details.players.filter((p) => p.alive || p.userId === details.eliminated?.userId).length})
          </b>
          {details.votes.map((v) => (
            <div key={v.voterId} className="control-row">
              <span>{login(v.voterId)}</span>
              <span>→ {v.targetId ? login(v.targetId) : 'воздержался'}</span>
            </div>
          ))}
        </div>
      )}

      {phase === 'Ended' ? (
        <p className="muted">{details.winner === 'Mafia' ? 'Победила мафия' : 'Победили мирные'} — «⏹ Завершить», чтобы начислить ослики.</p>
      ) : (
        <button className="primary" disabled={busy} onClick={() => void run(`/api/admin/mafia/${event.id}/next?step=${details.step}`)}>
          {phase === 'Speech' ? '⏭ Передать слово' : '▶ Дальше'}
        </button>
      )}
      {error && <p className="error">{error}</p>}
    </div>
  )
}
