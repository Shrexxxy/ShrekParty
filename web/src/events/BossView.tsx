import { useCallback, useEffect, useLayoutEffect, useRef, useState, type RefObject } from 'react'
import { api } from '../api/http'
import { codeText, errorText } from '../admin/errors'
import { Avatar } from '../components/Avatar'
import { Sprite } from '../components/Sprite'
import { CLASS_NAMES, petArt } from '../pets/pets'
import {
  ACTION_ICONS,
  duelTarget,
  actionText,
  activeFighter,
  coveringAt,
  type Flight,
  hangingProjectiles,
  projectileFlight,
  BOSS_NAMES,
  bossArt,
  bossBackdrop,
  bossPose,
  bossPoseFiles,
  type BossDetails,
  type BossFighter,
  type BossKind,
  type BossMoveKind,
  feedStep,
  type FighterAction,
  formation,
  FX_SPRITE_FALLBACK,
  type FxRole,
  type FxSprite,
  fxSprite,
  fxSpriteSrc,
  volleyProjectile,
  type Fx,
  fxClass,
  hitsBoss,
  hpAtStep,
  hpPercent,
  MEDAL_ICONS,
  MOVE_ICONS,
  moveArt,
  moveText,
  FX_MS,
  skillArt,
} from './boss'
import { formatClock } from './countdown'
import { useCountdown } from './useCountdown'
import { Emoji, EmojiText } from '../components/Emoji'

/** Запасной вариант, пока нет картинки босса. */
const BOSS_FACES: Record<BossKind, string> = { Peasant: '🧑‍🌾', Farquaad: '🤴', Fairy: '🧚' }

/** Каких картинок поз нет на сервере — общий для всех экранов, чтобы не просить их снова на каждом шаге. */
const MISSING_POSES = new Set<string>()

/** Загружает заранее все позы босса: смена позы на шаге хода — без мигания, отсутствующие — сразу в MISSING_POSES. */
function usePosePreload(boss: BossKind, onMissing: () => void) {
  useEffect(() => {
    for (const src of bossPoseFiles(boss)) {
      if (MISSING_POSES.has(src)) {
        continue
      }
      const img = new Image()
      img.onerror = () => {
        MISSING_POSES.add(src)
        onMissing()
      }
      img.src = src
    }
  }, [boss, onMissing])
}

/** Картинка босса: первая из списка, которая есть; нет ни одной — эмодзи. */
function BossPic({ boss, srcs, className }: { boss: BossKind; srcs?: string[]; className: string }) {
  const [, setMissing] = useState(0)
  const src = (srcs ?? [bossArt(boss)]).find((s) => !MISSING_POSES.has(s))
  if (!src) {
    return <span className={className}>{BOSS_FACES[boss]}</span>
  }

  const missed = () => {
    MISSING_POSES.add(src)
    setMissing((n) => n + 1)
  }
  return <img key={src} src={src} alt="" className={className} draggable={false} onError={missed} />
}

function SkillIcon({ action, className }: { action: FighterAction; className: string }) {
  return <Sprite src={skillArt(action)} fallback={ACTION_ICONS[action]} className={className} />
}

const SHOW_MIN_MS = 3000
const SHOW_TAIL_MS = 2000

/** Драконоослик игрока; оглушённый — тыква поверх. */
function PetPic({ f }: { f: BossFighter }) {
  return (
    <span className="boss-pet">
      <Sprite src={petArt(f)} fallback="🐉" className="boss-pet-img" alt={f.name} />
      {f.stunned && <span className="boss-pet-pumpkin"><Emoji e="🎃" /></span>}
    </span>
  )
}

function Bar({ value, max, kind, showText }: { value: number; max: number; kind: 'hp' | 'mana' | 'boss'; showText?: boolean }) {
  return (
      <div style={{ display: 'flex', alignItems: 'center', gap: '6px', width: '100%' }}>
        <div className={`boss-bar ${kind}`} style={{ flex: 1, minWidth: 0 }}>
          <span style={{ width: `${hpPercent(value, max)}%` }} />
        </div>
        {showText && (
            <span className="boss-bar-text">
          {value}/{max}
        </span>
        )}
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

/** Снаряды, которые на ТВ летят отдельным слоем: на карточке вместо них — только попадание. */
const FLYING: Partial<Record<FxSprite, FxSprite | null>> = { fireball: null, arrow: 'hit', star: 'hit', 'arrow-break': null, 'star-break': null }

/** Спрайт эффекта поверх картинки (удар, огненный шар, щит…); пока файла нет — эмодзи. */
function FxSpriteView({ fx, role, move, flying }: { fx: Fx; role: FxRole; move: BossMoveKind | null; flying?: boolean }) {
  const own = fxSprite(fx.kind, role, move)
  const sprite = flying && own && own in FLYING ? (FLYING[own] ?? null) : own
  return sprite ? <Sprite src={fxSpriteSrc(sprite)} fallback={FX_SPRITE_FALLBACK[sprite]} className={`boss-sprite boss-sprite-${sprite}`} /> : null
}

/** Центр элемента внутри арены. */
function centerIn(arena: HTMLElement, selector: string): { x: number; y: number } | null {
  const el = arena.querySelector(selector)
  if (!el) {
    return null
  }

  const a = arena.getBoundingClientRect()
  const r = el.getBoundingClientRect()
  return { x: r.left + r.width / 2 - a.left, y: r.top + r.height / 2 - a.top }
}

/**
 * Летящий снаряд на ТВ: из середины питомца (или со своего места над боссом) в середину цели.
 * Координаты берём с экрана — раскладка арены от этого не зависит.
 */
function FlightLayer({ arena, fx, move, step }: { arena: RefObject<HTMLDivElement | null>; fx: Fx | null; move: BossMoveKind | null; step: number }) {
  const flight = fx ? projectileFlight(fx, move) : null
  const path = useRef<HTMLDivElement>(null)
  // Меряем до отрисовки и пишем путь прямо в стиль: снаряд стартует с первого кадра, без лишнего рендера.
  useLayoutEffect(() => {
    const own = fx ? projectileFlight(fx, move) : null
    if (!arena.current || !path.current || !own) {
      return
    }

    const from = own.from.fighter
      ? centerIn(arena.current, `[data-fighter="${own.from.fighter}"] .boss-pet`)
      : centerIn(arena.current, `[data-slot="${own.from.slot}"]`)
    const to = own.to === 'boss' ? centerIn(arena.current, '[data-boss] .boss-face') : centerIn(arena.current, `[data-fighter="${own.to}"] .boss-pet`)
    const el = path.current
    el.hidden = !from || !to
    if (from && to) {
      el.style.left = `${from.x}px`
      el.style.top = `${from.y}px`
      el.style.setProperty('--dx', `${to.x - from.x}px`)
      el.style.setProperty('--dy', `${to.y - from.y}px`)
      el.style.setProperty('--angle', `${Math.atan2(to.y - from.y, to.x - from.x)}rad`)
    }
  }, [arena, fx, move, step])

  if (!flight) {
    return null
  }

  return (
    <div className="boss-flights">
      <div key={step} ref={path} className="boss-flight-path">
        <span className={`boss-flight boss-flight-${flight.sprite} end-${flight.end}`}>
          <Sprite src={fxSpriteSrc(flight.sprite)} fallback={FX_SPRITE_FALLBACK[flight.sprite]} />
        </span>
      </div>
    </div>
  )
}

/**
 * Удар по всем на ТВ: снаряды висят над боссом — по одному на цель, и каждый вылетает со своего места.
 * Улетевший не убираем, а прячем: места остальных не сдвигаются.
 */
function HangingVolley({ details, step }: { details: BossDetails; step: number }) {
  const showing = details.phase === 'Showing'
  const projectile = volleyProjectile(showing ? details.playingMove : details.intent?.move)
  if (!projectile || (details.phase !== 'Choosing' && !showing)) {
    return null
  }

  const order = details.fighters.map((f) => f.userId)
  const intentTargets = showing ? null : (details.intent?.targets ?? [])
  const all = hangingProjectiles(order, intentTargets, details.feed, -1)
  const hanging = new Set(hangingProjectiles(order, intentTargets, details.feed, step))
  return (
    <div className="boss-volley">
      {all.map((id) => (
        <span key={id} data-slot={id} className={hanging.has(id) ? 'boss-volley-slot' : 'boss-volley-slot fired'}>
          <Sprite src={fxSpriteSrc(projectile)} fallback={FX_SPRITE_FALLBACK[projectile]} className={`boss-volley-${projectile}`} />
        </span>
      ))}
    </div>
  )
}

/** Удар по всем: над боссом висят снаряды — по одному на каждого, пока игроки выбирают. */
function Volley({ details }: { details: BossDetails }) {
  const projectile = volleyProjectile(details.intent?.move)
  if (!projectile || !details.intent) {
    return null
  }

  return (
    <div className="boss-volley">
      {details.intent.targets.map((id) => (
        <Sprite key={id} src={fxSpriteSrc(projectile)} fallback={FX_SPRITE_FALLBACK[projectile]} className={`boss-volley-${projectile}`} />
      ))}
    </div>
  )
}

/**
 * Анимируется только картинка: обёртка пересоздаётся на каждом шаге ленты (key),
 * иначе одинаковые события подряд не проигрались бы заново.
 */
function FighterCard({
  f,
  fx,
  step,
  move,
  targeted,
  big,
  flight,
  covering,
}: {
  f: BossFighter
  fx: Fx | null
  step: number
  move: BossMoveKind | null
  targeted: boolean
  big?: boolean
  /** ТВ: снаряд этого шага летит слоем поверх арены — на карточке только попадание, когда долетит. */
  flight?: Flight | null
  /** Танк уже встал стеной в этом ходу — щит висит перед ним до конца хода. */
  covering?: boolean
}) {
  const role: FxRole | null = fx?.actorId === f.userId ? 'actor' : fx?.targetId === f.userId ? 'target' : null
  const late = flight != null && flight.to === f.userId
  const raising = fx?.kind === 'Cover' && role === 'actor'
  return (
    <div data-fighter={f.userId} className={['boss-fighter', !f.alive && 'dead', targeted && 'targeted', big && 'big'].filter(Boolean).join(' ')}>
      {f.choice && <SkillIcon action={f.choice} className="boss-choice" />}
      {f.confirmed && <span className="boss-ready" aria-label="подтвердил"><Emoji e="✓" /></span>}
      <div key={step} className={['boss-anim', fx && role && fxClass(fx), role && `fx-${role}`, late && 'late'].filter(Boolean).join(' ')}>
        <PetPic f={f} />
        {covering && !raising && f.alive && (
          <Sprite src={fxSpriteSrc('shield')} fallback={FX_SPRITE_FALLBACK.shield} className="boss-sprite boss-sprite-shield held" />
        )}
        {fx && role && <FxSpriteView fx={fx} role={role} move={move} flying={flight !== undefined} />}
      </div>
      {fx && role && fx.amount > 0 && (
        <span key={`n${step}`} className={['boss-number', fx.kind === 'Heal' && 'heal', late && 'late'].filter(Boolean).join(' ')}>
          {fx.kind === 'Heal' ? `+${fx.amount}` : `−${fx.amount}`}
        </span>
      )}
      <span className="boss-fighter-player">
        <Avatar url={f.avatarUrl} login={f.login} size={20} />
        <small className="boss-fighter-name">{f.login}</small>
      </span>
      <small className="muted">
        {CLASS_NAMES[f.class]} · {f.level} ур.
      </small>
      <Bar value={f.hp} max={f.maxHp} kind="hp" showText />
      <Bar value={f.mana} max={f.maxMana} kind="mana" showText />
    </div>
  )
}

/** Ходы босса по команде — босс делает выпад влево, к питомцам. */
const BOSS_STRIKES: Fx['kind'][] = ['BossHit', 'Covered', 'Dodged', 'Stunned', 'Absorbed']

/** Имя, здоровье и ярость босса — сверху, первым делом видно, сколько осталось. */
function BossTop({ details }: { details: BossDetails }) {
  return (
    <>
      <b>{BOSS_NAMES[details.boss]}</b>
      <Bar value={details.bossHp} max={details.bossMax} kind="boss" showText />
      {details.rage > 1 && <span className="boss-rage"><Emoji e="😡" /> ×{details.rage.toFixed(2).replace(/\.?0+$/, '')}</span>}
    </>
  )
}

/** Картинка босса с анимацией шага; flight — на ТВ огненный шар летит слоем, босс вздрагивает, когда долетит. */
function BossFigure({ details, fx, step, flight }: { details: BossDetails; fx: Fx | null; step: number; flight?: Flight | null }) {
  const onBoss = fx !== null && hitsBoss(fx)
  const self = fx !== null && fx.actorId === null && fx.targetId === null
  const bossFx = fx && self ? fxClass(fx) : onBoss ? 'boss-fx-boss-hurt' : fx && BOSS_STRIKES.includes(fx.kind) ? 'boss-fx-strike' : null
  const late = flight?.to === 'boss'
  return (
    <>
      <div key={step} className={['boss-anim', bossFx, late && 'late'].filter(Boolean).join(' ')}>
        <BossPic
          boss={details.boss}
          srcs={bossPose({ boss: details.boss, phase: details.phase, hp: details.bossHp, max: details.bossMax, fx, move: details.playingMove })}
          className="boss-face"
        />
        {fx && (onBoss || self) && <FxSpriteView fx={fx} role="boss" move={details.playingMove} flying={flight !== undefined} />}
      </div>
      {onBoss && fx.amount > 0 && (
        <span key={`n${step}`} className={late ? 'boss-number late' : 'boss-number'}>
          −{fx.amount}
        </span>
      )}
    </>
  )
}

function Boss({ details, fx, step }: { details: BossDetails; fx: Fx | null; step: number }) {
  return (
    <div className={['boss-boss', details.phase === 'Intro' && 'boss-fx-enter', details.phase === 'Won' && 'boss-fx-defeated'].filter(Boolean).join(' ')}>
      <BossTop details={details} />
      {details.phase === 'Choosing' && <Volley details={details} />}
      <BossFigure details={details} fx={fx} step={step} />
    </div>
  )
}

function Intent({ details }: { details: BossDetails }) {
  if (!details.intent) {
    return null
  }

  const { move, damage } = details.intent
  const text = moveText(move, damage)
  const targets = details.intent.targets.map((id) => details.fighters.find((f) => f.userId === id)?.login).filter(Boolean)
  const all = targets.length > 0 && targets.length === details.fighters.filter((f) => f.alive).length
  return (
    <div className="boss-intent">
      <Sprite src={moveArt(move)} fallback={MOVE_ICONS[move]} className="boss-intent-icon" />
      <span className="boss-intent-text">
        <b>
          {text.name}
          {targets.length > 0 && <> → {all ? 'всех' : targets.join(', ')}</>}
        </b>
        <small>{text.hint}</small>
      </span>
    </div>
  )
}

function Header({ details, serverTime }: { details: BossDetails; serverTime: string }) {
  const remaining = useCountdown(details.deadline ?? new Date(0).toISOString(), serverTime)
  return (
    <div className="boss-head">
      <span>{details.turn > 0 ? `Ход ${details.turn}` : ''}</span>
      {details.phase === 'Choosing' &&
        (details.deadline ? <b className="quiz-clock">{formatClock(remaining)}</b> : <span className="muted">Ждём, пока все подтвердят</span>)}
    </div>
  )
}

function Result({ details }: { details: BossDetails }) {
  if (details.phase === 'Won' && details.medal) {
    return (
      <div className="boss-result boss-fx-medal">
        <span className="boss-medal">
          <Emoji e={MEDAL_ICONS[details.medal]} />
        </span>
        <span>
          <Emoji e="🐉" /> {details.survivors} / {details.fighters.length}
        </span>
        <div className="boss-prizes">
          {details.prizes
            .filter((p) => !p.sold)
            .map((p) => (
              <span key={p.medal} className={p.open ? 'open' : 'locked'}>
                <EmojiText text={`${MEDAL_ICONS[p.medal]} ${p.open ? `🎁 ${p.name}` : '🔒'}`} />
              </span>
            ))}
        </div>
      </div>
    )
  }

  return details.phase === 'Lost' ? <div className="boss-result boss-fx-lost"><Emoji e="💀" /> до −25 <Emoji e="🫏" /></div> : null
}

/** Действия на телефоне: кнопки с иконками; недоступные — серые с причиной. */
function Actions({ eventId, details, me }: { eventId: string; details: BossDetails; me: BossFighter }) {
  const [healing, setHealing] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function send(what: 'confirm' | 'cancel') {
    setError(null)
    try {
      await api(`/api/boss/${eventId}/${what}`, { method: 'POST' })
    } catch (e) {
      setError(errorText(e))
    }
  }

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
                <PetPic f={f} /> {f.login}
                <Bar value={f.hp} max={f.maxHp} kind="hp" showText />  {/* <-- ДОБАВИТЬ showText ЗДЕСЬ */}
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
        {details.myOptions.map((o) => {
          const text = actionText(o.action, o.amount)
          return (
            <button
              key={o.action}
              className={details.myChoice?.action === o.action ? 'primary' : ''}
              disabled={o.unavailable !== null || details.myConfirmed}
              onClick={() => (o.action === 'Heal' ? setHealing(true) : void act(o.action))}
            >
              <SkillIcon action={o.action} className="boss-action-icon" />
              <span className="boss-action-text">
                <b>{text.name}</b>
                <small>{o.unavailable ? codeText(o.unavailable) : text.hint}</small>
              </span>
            </button>
          )
        })}
      </div>
      {details.myChoice &&
        (details.myConfirmed ? (
          <div className="boss-confirm">
            <span>Подтверждено — ждём остальных</span>
            <button onClick={() => void send('cancel')}>Отменить</button>
          </div>
        ) : (
          <button className="primary boss-confirm-btn" onClick={() => void send('confirm')}>
            Подтвердить
          </button>
        ))}
      {!me.alive && <p className="muted"><Emoji e="💀" /></p>}
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
      <Emoji e="📣" /> {details.iCheered && <Emoji e="✓" />}
    </button>
  )
}

/**
 * Бой с боссом. ТВ — вся арена; телефон — свой драконоослик, кнопки и тот же ход крупным планом.
 * Анимации — заглушки по классам `boss-fx-*`.
 */
export function BossView({ eventId, details: raw, serverTime, tv }: { eventId: string; details: BossDetails; serverTime: string; tv?: boolean }) {
  const elapsed = usePlayback(raw, serverTime)
  const { fx, step } = raw.phase === 'Showing' ? current(raw, elapsed) : { fx: null, step: 0 }
  // Сервер присылает итог хода сразу — полоски и выбывание догоняют ленту: урон виден, когда до него дошёл удар.
  const details: BossDetails =
    raw.phase === 'Showing' ? { ...raw, ...hpAtStep(raw.bossHp, raw.fighters, raw.feed, Math.min(raw.feed.length, step + 1)) } : raw
  const targets = new Set(details.phase === 'Choosing' ? (details.intent?.targets ?? []) : [])
  const me = details.fighters.find((f) => f.userId === details.myId) ?? null
  const [duel, setDuel] = useState<string | null>(null)
  const arena = useRef<HTMLDivElement>(null)
  const [, setMissing] = useState(0)
  usePosePreload(
    details.boss,
    useCallback(() => setMissing((n) => n + 1), []),
  )
  const nextDuel = duelTarget(details.phase, details.intent, duel)
  if (nextDuel !== duel) {
    setDuel(nextDuel)
  }

  if (tv) {
    const duelist = details.fighters.find((f) => f.userId === nextDuel) ?? null
    const flight = fx ? projectileFlight(fx, details.playingMove) : null
    const covering = details.phase === 'Showing' ? coveringAt(details.feed, step) : new Set<string>()
    const card = (f: BossFighter) => (
      <FighterCard
        key={f.userId}
        f={f}
        fx={fx}
        step={step}
        move={details.playingMove}
        targeted={targets.has(f.userId)}
        flight={flight}
        covering={covering.has(f.userId)}
      />
    )
    return (
      <div className={`boss tv phase-${details.phase}`}>
        {/* Задник места боя — на весь экран под всем, с затемнением, чтобы персонажи читались. */}
        <div className="boss-backdrop" style={{ backgroundImage: `url(${bossBackdrop(details.boss)})` }} />
        <Header details={details} serverTime={serverTime} />
        {/*
          Арена: команда слева строем (маги — бойцы — танки), посередине экрана — цель одиночного удара,
          справа босс. Ряды фиксированной высоты: снаряды и описание хода не двигают босса.
        */}
        <div className="boss-arena" ref={arena}>
          <div className="boss-formation">
            {formation(details.fighters.filter((f) => f !== duelist)).map((rank, i) => (
              <div key={`${i}-${rank[0].class}-${rank[0].alive}`} className="boss-rank">
                {rank.map(card)}
              </div>
            ))}
          </div>
          <div className="boss-top">
            <BossTop details={details} />
            {details.cheerBonus > 0 && details.phase === 'Choosing' && <span className="boss-cheer-bonus"><Emoji e="📣" /> +{details.cheerBonus}</span>}
          </div>
          <div className="boss-volley-row">
            <HangingVolley details={details} step={step} />
          </div>
          {duelist && <div className="boss-duelist">{card(duelist)}</div>}
          <div
            data-boss
            className={['boss-figure', details.phase === 'Intro' && 'boss-fx-enter', details.phase === 'Won' && 'boss-fx-defeated'].filter(Boolean).join(' ')}
          >
            <BossFigure details={details} fx={fx} step={step} flight={flight} />
          </div>
          <div className="boss-intent-row">{details.phase === 'Choosing' && <Intent details={details} />}</div>
          <FlightLayer arena={arena} fx={fx} move={details.playingMove} step={step} />
        </div>
        <Result details={details} />
      </div>
    )
  }

  // На телефоне в разыгрывании — крупно тот, кто сейчас действует, после ленты — снова свой.
  const spotlightId = details.phase === 'Showing' ? (step < details.feed.length ? activeFighter(details.feed, step) : me?.userId) : me?.userId
  const spotlight = details.fighters.find((f) => f.userId === spotlightId) ?? null
  const bossTurn = details.phase === 'Showing' && step < details.feed.length && spotlightId === null

  return (
    <div className={`boss phase-${details.phase}`}>
      <Header details={details} serverTime={serverTime} />
      {(details.phase === 'Intro' || bossTurn || details.phase === 'Won' || details.phase === 'Lost') && <Boss details={details} fx={fx} step={step} />}
      {details.phase === 'Choosing' && (
          <div className="boss-mini">
            <BossPic boss={details.boss} className="boss-mini-face" />
            <Bar value={details.bossHp} max={details.bossMax} kind="boss" showText /> {/* <-- ДОБАВИТЬ showText ЗДЕСЬ */}
          </div>
      )}
      {details.phase === 'Choosing' && <Intent details={details} />}
      {!bossTurn && spotlight && <FighterCard f={spotlight} fx={fx} step={step} move={details.playingMove} targeted={targets.has(spotlight.userId)} big />}
      {details.phase === 'Choosing' && me && me.alive && <Actions eventId={eventId} details={details} me={me} />}
      {details.phase === 'Choosing' && me && !me.alive && <Cheer eventId={eventId} details={details} />}
      {!me && <p className="muted"><Emoji e="👀" /></p>}
      <Result details={details} />
    </div>
  )
}
