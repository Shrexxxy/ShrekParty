import { useState } from 'react'
import { api } from '../api/http'
import { codeText, errorText } from '../admin/errors'
import { CLASS_NAMES } from '../pets/pets'
import {
  ACTION_ICONS,
  activeFighter,
  BOSS_NAMES,
  type BossDetails,
  type BossFighter,
  type BossKind,
  feedStep,
  type FighterAction,
  type Fx,
  fxClass,
  hitsBoss,
  hpPercent,
  MEDAL_ICONS,
  MOVE_ICONS,
} from './boss'
import { formatClock } from './countdown'
import { useCountdown } from './useCountdown'

/** Пока без картинок — эмодзи; в слое красоты заменим на арт. */
const BOSS_FACES: Record<BossKind, string> = { Peasant: '🧑‍🌾', Farquaad: '🤴', Fairy: '🧚' }

const SHOW_MIN_MS = 3000
const SHOW_TAIL_MS = 1000
const FX_MS = 600

function Bar({ value, max, kind }: { value: number; max: number; kind: 'hp' | 'mana' | 'boss' }) {
  return (
    <div className={`boss-bar ${kind}`}>
      <span style={{ width: `${hpPercent(value, max)}%` }} />
    </div>
  )
}

/** Сколько миллисекунд проиграно с начала разыгрывания хода (по дедлайну и длине ленты). */
function usePlayback(details: BossDetails, serverTime: string): number {
  const remaining = useCountdown(details.deadline ?? new Date(0).toISOString(), serverTime)
  if (details.phase !== 'Showing' || !details.deadline) {
    return Number.MAX_SAFE_INTEGER
  }

  const total = Math.max(SHOW_MIN_MS, details.feed.length * FX_MS + SHOW_TAIL_MS)
  return total - remaining
}

/** Текущее событие ленты и класс анимации-заглушки для того, на ком оно. */
function current(details: BossDetails, elapsed: number): { fx: Fx | null; step: number } {
  const step = feedStep(details.feed.length, elapsed)
  return { fx: details.feed[step] ?? null, step }
}

function FighterCard({ f, fx, targeted, big }: { f: BossFighter; fx: Fx | null; targeted: boolean; big?: boolean }) {
  const onMe = fx && (fx.actorId === f.userId || fx.targetId === f.userId)
  return (
    <div className={['boss-fighter', !f.alive && 'dead', targeted && 'targeted', big && 'big', onMe && fxClass(fx)].filter(Boolean).join(' ')}>
      {f.choice && <span className="boss-choice">{ACTION_ICONS[f.choice]}</span>}
      <span className="boss-pet">{f.stunned ? '🎃' : '🐉'}</span>
      {onMe && fx.amount > 0 && <span className="boss-number">{fx.kind === 'Heal' ? `+${fx.amount}` : `−${fx.amount}`}</span>}
      <small className="boss-fighter-name">{f.name}</small>
      <small className="muted">{CLASS_NAMES[f.class]}</small>
      <Bar value={f.hp} max={f.maxHp} kind="hp" />
      <Bar value={f.mana} max={f.maxMana} kind="mana" />
    </div>
  )
}

function Boss({ details, fx }: { details: BossDetails; fx: Fx | null }) {
  const onBoss = fx !== null && hitsBoss(fx)
  const bossFx = fx && fx.actorId === null && fx.targetId === null ? fxClass(fx) : onBoss ? 'boss-fx-boss-hurt' : null
  return (
    <div className={['boss-boss', details.phase === 'Intro' && 'boss-fx-enter', details.phase === 'Won' && 'boss-fx-defeated', bossFx].filter(Boolean).join(' ')}>
      <span className="boss-face">{BOSS_FACES[details.boss]}</span>
      {onBoss && fx.amount > 0 && <span className="boss-number">−{fx.amount}</span>}
      <b>{BOSS_NAMES[details.boss]}</b>
      <Bar value={details.bossHp} max={details.bossMax} kind="boss" />
      {details.rage > 1 && <span className="boss-rage">😡 ×{details.rage.toFixed(2).replace(/\.?0+$/, '')}</span>}
    </div>
  )
}

function Intent({ details }: { details: BossDetails }) {
  if (!details.intent) {
    return null
  }

  const targets = details.intent.targets.map((id) => details.fighters.find((f) => f.userId === id)?.name).filter(Boolean)
  return (
    <div className="boss-intent">
      <span className="boss-intent-icon">{MOVE_ICONS[details.intent.move]}</span>
      {targets.length > 0 && targets.length < details.fighters.filter((f) => f.alive).length && <span>→ {targets.join(', ')}</span>}
      {targets.length > 0 && targets.length === details.fighters.filter((f) => f.alive).length && <span>→ всех</span>}
    </div>
  )
}

function Header({ details, serverTime }: { details: BossDetails; serverTime: string }) {
  const remaining = useCountdown(details.deadline ?? new Date(0).toISOString(), serverTime)
  return (
    <div className="boss-head">
      <span>{details.turn > 0 ? `Ход ${details.turn}` : ''}</span>
      {details.phase === 'Choosing' && <b className="quiz-clock">{formatClock(remaining)}</b>}
    </div>
  )
}

function Result({ details }: { details: BossDetails }) {
  if (details.phase === 'Won' && details.medal) {
    return (
      <div className="boss-result boss-fx-medal">
        <span className="boss-medal">{MEDAL_ICONS[details.medal]}</span>
        <span>
          🐉 {details.survivors} / {details.fighters.length}
        </span>
        <div className="boss-prizes">
          {details.prizes
            .filter((p) => !p.sold)
            .map((p) => (
              <span key={p.medal} className={p.open ? 'open' : 'locked'}>
                {MEDAL_ICONS[p.medal]} {p.open ? `🎁 ${p.name}` : '🔒'}
              </span>
            ))}
        </div>
      </div>
    )
  }

  return details.phase === 'Lost' ? <div className="boss-result boss-fx-lost">💀 до −25 🫏</div> : null
}

/** Действия на телефоне: кнопки с иконками; недоступные — серые с причиной. */
function Actions({ eventId, details, me }: { eventId: string; details: BossDetails; me: BossFighter }) {
  const [healing, setHealing] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function act(action: FighterAction, targetId?: string) {
    setError(null)
    setHealing(false)
    try {
      await api(`/api/boss/${eventId}/act`, { method: 'POST', body: { action, targetId } })
    } catch (e) {
      setError(errorText(e))
    }
  }

  if (healing) {
    return (
      <div className="boss-heal-pick">
        {details.fighters
          .filter((f) => f.alive)
          .map((f) => (
            <button key={f.userId} onClick={() => void act('Heal', f.userId)}>
              🐉 {f.name}
              <Bar value={f.hp} max={f.maxHp} kind="hp" />
            </button>
          ))}
        <button className="link" onClick={() => setHealing(false)}>
          ↩
        </button>
      </div>
    )
  }

  return (
    <>
      <div className="boss-actions">
        {details.myOptions.map((o) => (
          <button
            key={o.action}
            className={details.myChoice?.action === o.action ? 'primary' : ''}
            disabled={o.unavailable !== null}
            onClick={() => (o.action === 'Heal' ? setHealing(true) : void act(o.action))}
          >
            <span className="boss-action-icon">{ACTION_ICONS[o.action]}</span>
            {o.unavailable && <small className="muted">{codeText(o.unavailable)}</small>}
          </button>
        ))}
      </div>
      {!me.alive && <p className="muted">💀</p>}
      {error && <p className="error">{error}</p>}
    </>
  )
}

/** Поддержка с того света: раз за ход, прибавка к ударам живых в следующем ходу. */
function Cheer({ eventId, details }: { eventId: string; details: BossDetails }) {
  return (
    <button
      className="primary boss-cheer"
      disabled={details.iCheered}
      onClick={() => void api(`/api/boss/${eventId}/cheer`, { method: 'POST' }).catch(() => undefined)}
    >
      📣 {details.iCheered ? '✓' : ''}
    </button>
  )
}

/**
 * Бой с боссом. ТВ — вся арена; телефон — свой драконоослик, кнопки и тот же ход крупным планом.
 * Анимации — заглушки по классам `boss-fx-*`.
 */
export function BossView({ eventId, details, serverTime, tv }: { eventId: string; details: BossDetails; serverTime: string; tv?: boolean }) {
  const elapsed = usePlayback(details, serverTime)
  const { fx, step } = details.phase === 'Showing' ? current(details, elapsed) : { fx: null, step: 0 }
  const targets = new Set(details.phase === 'Choosing' ? (details.intent?.targets ?? []) : [])
  const me = details.fighters.find((f) => f.userId === details.myId) ?? null

  if (tv) {
    return (
      <div className="boss tv">
        <Header details={details} serverTime={serverTime} />
        <Boss details={details} fx={fx} />
        {details.phase === 'Choosing' && <Intent details={details} />}
        <div className="boss-team">
          {details.fighters.map((f) => (
            <FighterCard key={f.userId} f={f} fx={fx} targeted={targets.has(f.userId)} />
          ))}
        </div>
        {details.cheerBonus > 0 && details.phase === 'Choosing' && <div className="boss-cheer-bonus">📣 +{details.cheerBonus}</div>}
        <Result details={details} />
      </div>
    )
  }

  // На телефоне в разыгрывании — крупно тот, кто сейчас действует, после ленты — снова свой.
  const spotlightId = details.phase === 'Showing' ? (step < details.feed.length ? activeFighter(details.feed, step) : me?.userId) : me?.userId
  const spotlight = details.fighters.find((f) => f.userId === spotlightId) ?? null
  const bossTurn = details.phase === 'Showing' && step < details.feed.length && spotlightId === null

  return (
    <div className="boss">
      <Header details={details} serverTime={serverTime} />
      {(details.phase === 'Intro' || bossTurn || details.phase === 'Won' || details.phase === 'Lost') && <Boss details={details} fx={fx} />}
      {details.phase === 'Choosing' && (
        <div className="boss-mini">
          <span>{BOSS_FACES[details.boss]}</span>
          <Bar value={details.bossHp} max={details.bossMax} kind="boss" />
        </div>
      )}
      {details.phase === 'Choosing' && <Intent details={details} />}
      {!bossTurn && spotlight && <FighterCard f={spotlight} fx={fx} targeted={targets.has(spotlight.userId)} big />}
      {details.phase === 'Choosing' && me && me.alive && <Actions eventId={eventId} details={details} me={me} />}
      {details.phase === 'Choosing' && me && !me.alive && <Cheer eventId={eventId} details={details} />}
      {!me && <p className="muted">👀</p>}
      <Result details={details} />
    </div>
  )
}
