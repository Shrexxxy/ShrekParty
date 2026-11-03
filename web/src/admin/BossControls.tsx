import type { ActiveEventDto } from '../api/types'
import { ACTION_ICONS, BOSS_NAMES, type BossDetails, MEDAL_ICONS, MOVE_ICONS, moveArt, openLots, skillArt } from '../events/boss'
import { Sprite } from '../components/Sprite'
import { formatClock } from '../events/countdown'
import { useCountdown } from '../events/useCountdown'
import { CLASS_NAMES } from '../pets/pets'
import { useAdminAction } from './useAdminAction'
import { Emoji, EmojiText } from '../components/Emoji'

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
        <Emoji e="❤️" /> {details.bossHp} / {details.bossMax}
        <EmojiText text={details.rage > 1 && ` · 😡 ×${details.rage}`} />
        {details.intent && (
          <>
            {' · '}
            <Sprite src={moveArt(details.intent.move)} fallback={MOVE_ICONS[details.intent.move]} className="emoji-icon" />
          </>
        )}
        <EmojiText text={details.medal && ` · ${MEDAL_ICONS[details.medal]}`} />
      </p>
      <div className="card">
        {details.fighters.map((f) => (
          <div key={f.userId} className="control-row">
            <span>
              <EmojiText text={f.alive ? '🐉' : '💀'} /> {f.login} · {CLASS_NAMES[f.class]}
            </span>
            <span>
              <Emoji e="❤️" /> {f.hp}/{f.maxHp} <Emoji e="💧" /> {f.mana}/{f.maxMana} {f.choice ? (
                <Sprite src={skillArt(f.choice)} fallback={ACTION_ICONS[f.choice]} className="emoji-icon" />
              ) : details.phase === 'Choosing' && f.alive ? (
                '…'
              ) : (
                ''
              )}
            </span>
          </div>
        ))}
      </div>
      {details.phase === 'Won' &&
        (openLots(details.prizes) > 0 ? (
          <button className="primary" disabled={busy} onClick={() => void run(`/api/admin/boss/${event.id}/auction`)}>
            <Emoji e="▶" /> Аукцион: {openLots(details.prizes)} лот(а)
          </button>
        ) : (
          <p className="muted">Открытых лотов нет — «<Emoji e="⏹" /> Завершить»; призы босса уйдут в финальный аукцион.</p>
        ))}
      {details.phase === 'Lost' && <p className="muted">«<Emoji e="⏹" /> Завершить» снимет до 25 <Emoji e="🫏" /> с каждого участника.</p>}
      {ended ? null : (
        <button className="primary" disabled={busy} onClick={() => void run(`/api/admin/boss/${event.id}/next?step=${details.step}`)}>
          <Emoji e="▶" /> Дальше
        </button>
      )}
      {error && <p className="error">{error}</p>}
    </div>
  )
}
