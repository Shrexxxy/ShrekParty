import type { ActiveEventDto } from '../api/types'
import { ACTION_ICONS, BOSS_NAMES, type BossDetails, MEDAL_ICONS, MOVE_ICONS } from '../events/boss'
import { formatClock } from '../events/countdown'
import { useCountdown } from '../events/useCountdown'
import { CLASS_NAMES } from '../pets/pets'
import { useAdminAction } from './useAdminAction'

const PHASES = { Intro: 'Появление', Choosing: 'Выбор', Showing: 'Разыгрывание', Won: 'Победа', Lost: 'Поражение' } as const

function Clock({ deadline, serverTime }: { deadline: string; serverTime: string }) {
  return <b>{formatClock(useCountdown(deadline, serverTime))}</b>
}

/** Пульт боя: здоровье босса и всех, кто что выбрал; «Дальше» закрывает фазу раньше. */
export function BossControls({ event, details, serverTime }: { event: ActiveEventDto; details: BossDetails; serverTime: string }) {
  const { run, error, busy } = useAdminAction()
  const ended = details.phase === 'Won' || details.phase === 'Lost'

  return (
    <div className="controls">
      <p>
        {BOSS_NAMES[details.boss]} · {PHASES[details.phase]}
        {details.turn > 0 && ` · ход ${details.turn}`}
        {details.deadline && (
          <>
            {' · '}
            <Clock deadline={details.deadline} serverTime={serverTime} />
          </>
        )}
      </p>
      <p>
        ❤️ {details.bossHp} / {details.bossMax}
        {details.rage > 1 && ` · 😡 ×${details.rage}`}
        {details.intent && ` · ${MOVE_ICONS[details.intent.move]}`}
        {details.medal && ` · ${MEDAL_ICONS[details.medal]}`}
      </p>
      <div className="card">
        {details.fighters.map((f) => (
          <div key={f.userId} className="control-row">
            <span>
              {f.alive ? '🐉' : '💀'} {f.login} · {CLASS_NAMES[f.class]}
            </span>
            <span>
              ❤️ {f.hp}/{f.maxHp} 💧 {f.mana}/{f.maxMana} {f.choice ? ACTION_ICONS[f.choice] : details.phase === 'Choosing' && f.alive ? '…' : ''}
            </span>
          </div>
        ))}
      </div>
      {ended ? (
        <p className="muted">{details.phase === 'Won' ? 'Победа' : 'Поражение'} — «⏹ Завершить», когда всё покажете.</p>
      ) : (
        <button className="primary" disabled={busy} onClick={() => void run(`/api/admin/boss/${event.id}/next?step=${details.step}`)}>
          ▶ Дальше
        </button>
      )}
      {error && <p className="error">{error}</p>}
    </div>
  )
}
