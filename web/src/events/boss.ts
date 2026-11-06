import type { PetClass } from '../api/types'

export type BossKind = 'Peasant' | 'Farquaad' | 'Fairy'
export type BossPhase = 'Intro' | 'Choosing' | 'Showing' | 'Won' | 'Lost'
export type FighterAction = 'Attack' | 'Dodge' | 'Cover' | 'Power' | 'Fire' | 'Heal'
export type BossMoveKind =
  | 'Poke'
  | 'Swing'
  | 'Kvass'
  | 'CallNeighbours'
  | 'Sword'
  | 'Volley'
  | 'Shield'
  | 'Windup'
  | 'Wand'
  | 'Stars'
  | 'Pumpkin'
  | 'Potion'
export type FxKind =
  | 'Heal'
  | 'Cover'
  | 'Attack'
  | 'Weak'
  | 'Power'
  | 'Fire'
  | 'Stunned'
  | 'BossHit'
  | 'Dodged'
  | 'Covered'
  | 'BossHeal'
  | 'BossShield'
  | 'BossWindup'
  | 'Knockout'
  | 'Absorbed'

/** Событие ленты хода — одна анимация. */
export interface Fx {
  kind: FxKind
  actorId: string | null
  targetId: string | null
  amount: number
}

export interface BossFighter {
  userId: string
  login: string
  avatarUrl: string | null
  name: string
  class: PetClass
  /** Уровень питомца на старте боя. */
  level: number
  /** Рисунок питомца внутри класса — картинка на экранах. */
  variant: number
  hp: number
  maxHp: number
  mana: number
  maxMana: number
  alive: boolean
  stunned: boolean
  /** Выбор в этот ход — виден всем. */
  choice: FighterAction | null
  /** Подтвердил выбор. */
  confirmed: boolean
}

export interface BossDetails {
  phase: BossPhase
  turn: number
  step: number
  deadline: string | null
  boss: BossKind
  bossHp: number
  bossMax: number
  rage: number
  /** Объявленный ход; damage — примерный урон по каждой цели (null — ход не бьёт). */
  intent: { move: BossMoveKind; targets: string[]; damage: number | null } | null
  /** Ход босса, который сейчас проигрывается. */
  playingMove: BossMoveKind | null
  fighters: BossFighter[]
  myId: string | null
  myChoice: { action: FighterAction; targetId: string | null } | null
  /** amount — урон или лечение для подписи на кнопке; у заслона и уклона null. */
  myOptions: { action: FighterAction; unavailable: string | null; amount: number | null }[]
  feed: Fx[]
  cheerBonus: number
  iCheered: boolean
  /** Я подтвердил выбор — менять можно только через «Отменить». */
  myConfirmed: boolean
  medal: 'Bronze' | 'Silver' | 'Gold' | null
  survivors: number
  /** Призы этого босса — на экране итога. */
  prizes: BossPrize[]
}

export const BOSS_NAMES: Record<BossKind, string> = {
  Peasant: 'Крестьянин с вилами',
  Farquaad: 'Лорд Фаркуад',
  Fairy: 'Фея-крёстная',
}

export const ACTION_ICONS: Record<FighterAction, string> = {
  Attack: '⚔️',
  Dodge: '💨',
  Cover: '🔰',
  Power: '💥',
  Fire: '🔥',
  Heal: '💚',
}

/**
 * Кто стоит перед боссом: если он целится в одного — тот выходит из строя на подготовку
 * и остаётся на розыгрыш хода (в розыгрыше намерения уже нет — помним прошлый выбор).
 */
export function duelTarget(phase: BossPhase, intent: BossDetails['intent'], previous: string | null): string | null {
  if (phase === 'Choosing') {
    return intent?.targets.length === 1 ? intent.targets[0] : null
  }

  return phase === 'Showing' ? previous : null
}

/**
 * Строй команды на ТВ слева направо: выбывшие — самым задним рядом, дальше маги, бойцы,
 * танки спереди — ближе к боссу.
 */
export function formation<T extends Pick<BossFighter, 'class'> & { alive?: boolean }>(fighters: T[]): T[][] {
  const dead = fighters.filter((f) => f.alive === false)
  const alive = fighters.filter((f) => f.alive !== false)
  // Лежачих не больше трёх в колонке, иначе не влезают; лишние — ещё дальше назад.
  const deadColumns: T[][] = []
  for (let i = 0; i < dead.length; i += DEAD_PER_COLUMN) {
    deadColumns.unshift(dead.slice(i, i + DEAD_PER_COLUMN))
  }

  return [...deadColumns, ...(['Mage', 'Fighter', 'Tank'] as PetClass[]).map((c) => alive.filter((f) => f.class === c))].filter((rank) => rank.length > 0)
}

const DEAD_PER_COLUMN = 3

/**
 * Полоски по ходу розыгрыша: сервер присылает итог хода сразу, а на экране урон и выбывание
 * должны случаться только когда до них дошла лента. applied — сколько событий ленты уже показано.
 */
export function hpAtStep<T extends Pick<BossFighter, 'userId' | 'hp' | 'maxHp' | 'alive'>>(
  bossHp: number,
  fighters: T[],
  feed: Fx[],
  applied: number,
): { bossHp: number; fighters: T[] } {
  const pending = feed.slice(applied)
  const back = new Map<string, number>()
  const add = (id: string | null, amount: number) => id && back.set(id, (back.get(id) ?? 0) + amount)
  let boss = bossHp
  for (const e of pending) {
    if (hitsBoss(e)) {
      boss += e.amount
    } else if (e.kind === 'BossHeal') {
      boss -= e.amount
    } else if (e.kind === 'BossHit') {
      add(e.targetId, e.amount)
    } else if (e.kind === 'Covered') {
      add(e.actorId, e.amount)
    } else if (e.kind === 'Heal') {
      add(e.targetId, -e.amount)
    }
  }

  const knockedLater = new Set(pending.filter((e) => e.kind === 'Knockout').map((e) => e.targetId))
  return {
    bossHp: boss,
    fighters: fighters.map((f) => ({
      ...f,
      hp: Math.max(0, Math.min(f.maxHp, f.hp + (back.get(f.userId) ?? 0))),
      alive: f.alive || knockedLater.has(f.userId),
    })),
  }
}

/** Спрайт эффекта поверх картинки: /assets/fx/<имя>.png. */
export type FxSprite = 'hit' | 'slash' | 'fireball' | 'shield' | 'pumpkin' | 'onion-cloud' | 'heal' | 'dust' | 'arrow' | 'star' | 'arrow-break' | 'star-break'

/** Запасные эмодзи, пока спрайтов нет. */
export const FX_SPRITE_FALLBACK: Record<FxSprite, string> = {
  hit: '💥',
  slash: '⚔️',
  fireball: '🔥',
  shield: '🛡',
  pumpkin: '🎃',
  'onion-cloud': '🧅',
  heal: '💚',
  dust: '💨',
  arrow: '🏹',
  star: '⭐',
  'arrow-break': '🏹',
  'star-break': '⭐',
}

/** Снаряд удара по всем: висит над боссом, пока выбирают, и летит в каждого; null — удар не такой. */
export function volleyProjectile(move: BossMoveKind | null | undefined): 'arrow' | 'star' | null {
  return move === 'Volley' ? 'arrow' : move === 'Stars' ? 'star' : null
}

/** Полёт снаряда на ТВ поверх арены: откуда (место над боссом или питомец), куда и чем кончается. */
export interface Flight {
  from: { slot: string | null; fighter: string | null }
  to: string | 'boss'
  sprite: 'fireball' | 'arrow' | 'star'
  end: 'hit' | 'break' | 'miss'
}

/** Какой снаряд летит в этом событии; null — ничего не летит (удар на месте). */
export function projectileFlight(fx: Fx, move: BossMoveKind | null | undefined): Flight | null {
  if (fx.kind === 'Fire' && fx.actorId) {
    return { from: { slot: null, fighter: fx.actorId }, to: 'boss', sprite: 'fireball', end: 'hit' }
  }

  const projectile = volleyProjectile(move)
  if (!projectile || !fx.targetId || !['BossHit', 'Dodged', 'Absorbed'].includes(fx.kind)) {
    return null
  }

  const absorbed = fx.kind === 'Absorbed'
  return {
    from: { slot: fx.targetId, fighter: null },
    to: absorbed && fx.actorId ? fx.actorId : fx.targetId,
    sprite: projectile,
    end: absorbed ? 'break' : fx.kind === 'Dodged' ? 'miss' : 'hit',
  }
}

/**
 * Какие снаряды ещё висят над боссом (по целям, в порядке строя): на выборе — по всем целям,
 * в розыгрыше — пока до цели не дошла лента (текущий шаг уже летит).
 */
export function hangingProjectiles(order: string[], intentTargets: string[] | null, feed: Fx[], step: number): string[] {
  const pending = intentTargets
    ? new Set(intentTargets)
    : new Set(feed.flatMap((e, i) => (i > step && e.targetId && ['BossHit', 'Dodged', 'Absorbed'].includes(e.kind) ? [e.targetId] : [])))
  return order.filter((id) => pending.has(id))
}

/** Танки, которые уже встали стеной в этом ходу: щит висит перед ними до конца хода. */
export function coveringAt(feed: Fx[], step: number): Set<string> {
  return new Set(feed.flatMap((e, i) => (i <= step && e.kind === 'Cover' && e.actorId ? [e.actorId] : [])))
}

/** Файл спрайта: «разбитый» снаряд — та же картинка, разбивается анимацией. */
export function fxSpriteSrc(sprite: FxSprite): string {
  return `/assets/fx/${sprite.replace('-break', '')}.png`
}

/** Кто на экране участвует в событии: тот, кто действует, его цель или босс. */
export type FxRole = 'actor' | 'target' | 'boss'

const FX_SPRITES: Partial<Record<FxKind, Partial<Record<FxRole, FxSprite>>>> = {
  Attack: { boss: 'hit' },
  Weak: { boss: 'hit' },
  Power: { boss: 'slash' },
  Fire: { actor: 'fireball', boss: 'hit' },
  Heal: { target: 'onion-cloud' },
  Cover: { actor: 'shield' },
  Covered: { actor: 'shield' },
  BossHit: { target: 'hit' },
  Dodged: { target: 'dust' },
  Stunned: { actor: 'pumpkin' },
  BossShield: { boss: 'shield' },
  BossHeal: { boss: 'heal' },
}

/** Какой спрайт показать на участнике события; null — только движение картинки. move — разыгрываемый ход босса. */
export function fxSprite(kind: FxKind, role: FxRole, move?: BossMoveKind | null): FxSprite | null {
  const projectile = volleyProjectile(move)
  if (kind === 'BossHit' && role === 'target') {
    return projectile ?? (move === 'Sword' || move === 'Swing' ? 'slash' : 'hit')
  }
  if (kind === 'Absorbed' && role === 'actor') {
    return projectile ? `${projectile}-break` : 'shield'
  }

  return FX_SPRITES[kind]?.[role] ?? null
}

/** Задник боя на ТВ: болото Крестьянина, Дюлок Фаркуада, бальный зал Феи. */
export function bossBackdrop(boss: BossKind): string {
  return `/assets/boss/bg/${boss.toLowerCase()}.jpg`
}

/** Картинка босса; нет файла — эмодзи-заглушка. */
export function bossArt(boss: BossKind): string {
  return `/assets/boss/${boss.toLowerCase()}.png`
}

/** События хода босса (по команде или себе) — на них босс в позе своего хода. */
const BOSS_MOVE_FX: FxKind[] = ['BossHit', 'Dodged', 'Covered', 'Absorbed', 'BossHeal', 'BossShield', 'BossWindup']

/**
 * Картинка босса на этот момент: побеждён, победил, получает удар, делает ход или просто стоит — в позе своей стадии
 * здоровья (больше 66 %, от 34 %, ниже). Список — по убыванию: нет файла этой стадии, берётся стадия ниже, потом общая картинка.
 */
export function bossPose(at: {
  boss: BossKind
  phase: BossDetails['phase']
  hp: number
  max: number
  fx: Fx | null
  move: BossMoveKind | null
}): string[] {
  const dir = `/assets/boss/${at.boss.toLowerCase()}`
  const percent = at.max > 0 ? (at.hp * 100) / at.max : 0
  const stage = percent > 66 ? 1 : percent >= 34 ? 2 : 3
  const stages = [3, 2, 1].filter((s) => s <= stage)
  const idle = stages.map((s) => `${dir}/hp${s}.png`)
  const staged = (name: string) => stages.map((s) => `${dir}/${name}${s === 1 ? '' : `-${s}`}.png`)

  let pose: string[] = []
  if (at.phase === 'Won') {
    pose = staged('defeated')
  } else if (at.phase === 'Lost') {
    pose = staged('victory')
  } else if (at.fx && hitsBoss(at.fx)) {
    pose = staged('hurt')
  } else if (at.fx && at.move && BOSS_MOVE_FX.includes(at.fx.kind)) {
    pose = staged(`move-${at.move.toLowerCase()}`)
  }

  return [...pose, ...idle, bossArt(at.boss)]
}

/** Ходы каждого босса — как на сервере (BossRules). */
export const BOSS_MOVES: Record<BossKind, BossMoveKind[]> = {
  Peasant: ['Poke', 'Swing', 'Kvass', 'CallNeighbours'],
  Farquaad: ['Sword', 'Volley', 'Shield', 'Windup'],
  Fairy: ['Wand', 'Stars', 'Pumpkin', 'Potion'],
}

/** Все картинки, которые может взять bossPose для этого босса, — чтобы загрузить их заранее. */
export function bossPoseFiles(boss: BossKind): string[] {
  const hit: Fx = { kind: 'Attack', actorId: 'x', targetId: null, amount: 1 }
  const strike: Fx = { kind: 'BossHit', actorId: null, targetId: 'x', amount: 1 }
  const files = [100, 50, 10].flatMap((hp) => {
    const at = { boss, hp, max: 100, move: null }
    return [
      ...bossPose({ ...at, phase: 'Choosing', fx: null }),
      ...bossPose({ ...at, phase: 'Showing', fx: hit }),
      ...bossPose({ ...at, phase: 'Won', fx: null }),
      ...bossPose({ ...at, phase: 'Lost', fx: null }),
      ...BOSS_MOVES[boss].flatMap((move) => bossPose({ ...at, phase: 'Showing', fx: strike, move })),
    ]
  })
  return [...new Set(files)]
}

/** Иконка скилла; нет файла — эмодзи из ACTION_ICONS. */
export function skillArt(action: FighterAction): string {
  return `/assets/skills/${action.toLowerCase()}.png`
}

/** Название скилла и что он делает — подпись на кнопке; число — для своего питомца. */
export function actionText(action: FighterAction, amount: number | null): { name: string; hint: string } {
  switch (action) {
    case 'Attack':
      return { name: 'Лягнуть', hint: `Бьёт копытом: урон ${amount}` }
    case 'Dodge':
      return { name: 'Увернуться', hint: 'Удар босса пролетит мимо. Два хода подряд нельзя' }
    case 'Cover':
      return { name: 'Встать стеной', hint: 'Закрывает всех собой: весь урон по команде на тебя, вдвое меньше' }
    case 'Power':
      return { name: 'С разбега', hint: `Урон ${amount}. HP>50%: x2, иначе x1.5. Кулдаун 1 ход.` }
    case 'Fire':
      return { name: 'Драконий чих', hint: `Огнём по боссу: урон ${amount}, 4 маны` }
    case 'Heal':
      return { name: 'Луковый угар', hint: `Лечит союзника на ${amount}, 3 маны` }
  }
}

/** Ход босса словами — пока игроки выбирают, чтобы понимали, что сейчас будет; урон примерный, с сервера. */
export function moveText(move: BossMoveKind, damage: number | null): { name: string; hint: string } {
  const dmg = `~${damage} урона`
  switch (move) {
    case 'Poke':
      return { name: 'Тычок вилами', hint: `Бьёт одного: ${dmg}` }
    case 'Swing':
      return { name: 'Размах вилами', hint: `Бьёт двоих: ${dmg} каждому` }
    case 'Kvass':
      return { name: 'Квас', hint: 'Пьёт квас и лечится' }
    case 'CallNeighbours':
      return { name: 'Зовёт соседей', hint: 'Следующий удар будет двойным' }
    case 'Sword':
      return { name: 'Удар мечом', hint: `Бьёт одного: ${dmg}` }
    case 'Volley':
      return { name: 'Залп лучников', hint: `Стрелы по всем: ${dmg} каждому` }
    case 'Shield':
      return { name: 'Щит', hint: 'Прикрывается: урон по нему вдвое меньше' }
    case 'Windup':
      return { name: 'Замах', hint: 'Следующий удар будет двойным' }
    case 'Wand':
      return { name: 'Волшебная палочка', hint: `Бьёт одного: ${dmg}` }
    case 'Stars':
      return { name: 'Звёздный дождь', hint: `Звёзды по всем: ${dmg} каждому` }
    case 'Pumpkin':
      return { name: 'В тыкву', hint: `Превращает одного в тыкву: ${dmg} и пропуск хода` }
    case 'Potion':
      return { name: 'Зелье', hint: 'Пьёт зелье и лечится' }
  }
}

/** Иконка хода босса; нет файла — эмодзи из MOVE_ICONS. */
export function moveArt(move: BossMoveKind): string {
  return `/assets/boss-moves/${move.toLowerCase()}.png`
}

/** Иконка хода босса — на баннере; текста по минимуму. */
export const MOVE_ICONS: Record<BossMoveKind, string> = {
  Poke: '🔱',
  Swing: '🌾',
  Kvass: '🍺',
  CallNeighbours: '📣',
  Sword: '🗡',
  Volley: '🏹',
  Shield: '🛡',
  Windup: '💪',
  Wand: '🪄',
  Stars: '✨',
  Pumpkin: '🎃',
  Potion: '🧪',
}

export const MEDAL_ICONS = { Gold: '🥇', Silver: '🥈', Bronze: '🥉' } as const

export type Medal = keyof typeof MEDAL_ICONS

/** Приз босса на экране итога. */
export interface BossPrize {
  name: string
  medal: Medal
  /** Медаль боя его открыла. */
  open: boolean
  sold: boolean
}

const MEDALS: Medal[] = ['Bronze', 'Silver', 'Gold']

/** Значение поля «Куда» у лота: '' — финальный аукцион, иначе «Босс:Медаль». */
export function prizeKey(boss: BossKind | null, medal: Medal | null): string {
  return boss && medal ? `${boss}:${medal}` : ''
}

export function parsePrize(key: string): { boss: BossKind | null; medal: Medal | null } {
  const [boss, medal] = key.split(':')
  return boss && medal ? { boss: boss as BossKind, medal: medal as Medal } : { boss: null, medal: null }
}

/** Варианты поля «Куда»: финальный и каждый босс × медаль. */
export const PRIZE_OPTIONS: { key: string; label: string }[] = [
  { key: '', label: 'Финальный аукцион' },
  ...(Object.keys(BOSS_NAMES) as BossKind[]).flatMap((b) => MEDALS.map((m) => ({ key: prizeKey(b, m), label: `${BOSS_NAMES[b]} ${MEDAL_ICONS[m]}` }))),
]

/** Сколько лотов уйдёт на аукцион боя: открытые медалью и непроданные. */
export function openLots(prizes: BossPrize[]): number {
  return prizes.filter((p) => p.open && !p.sold).length
}

/** Одно событие ленты на экране — столько же ждёт сервер. Медленно, чтобы успеть понять, кто что сделал. */
export const FX_MS = 1500

/** Какое событие ленты сейчас на экране: по 0,6 с на событие, после конца — длина ленты. */
export function feedStep(length: number, elapsedMs: number): number {
  return Math.min(length, Math.floor(Math.max(0, elapsedMs) / FX_MS))
}

/** Чей драконоослик сейчас в кадре: тот, кто действует, или в кого бьёт босс; null — в кадре босс. */
export function activeFighter(feed: Fx[], step: number): string | null {
  const fx = feed[step]
  return fx ? (fx.actorId ?? fx.targetId) : null
}

/** Имя анимации-заглушки: `boss-fx-<тип>` — в слое красоты заменим на настоящие. */
export function fxClass(fx: Fx): string {
  return `boss-fx-${fx.kind.replace(/([a-z])([A-Z])/g, '$1-$2').toLowerCase()}`
}

const PLAYER_HITS: FxKind[] = ['Attack', 'Weak', 'Power', 'Fire']

/** Событие — удар игрока по боссу (заслон, лечение и тыква — нет, хоть у них и есть актёр). */
export function hitsBoss(fx: Fx): boolean {
  return PLAYER_HITS.includes(fx.kind)
}

/** Полоса здоровья в процентах. */
export function hpPercent(hp: number, max: number): number {
  return max <= 0 ? 0 : Math.max(0, Math.round((hp / max) * 100))
}
