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
  name: string
  class: PetClass
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
  intent: { move: BossMoveKind; targets: string[] } | null
  fighters: BossFighter[]
  myId: string | null
  myChoice: { action: FighterAction; targetId: string | null } | null
  /** amount — урон или лечение для подписи на кнопке; у заслона и уклона null. */
  myOptions: { action: FighterAction; unavailable: string | null; amount: number | null }[]
  feed: Fx[]
  cheerBonus: number
  iCheered: boolean
  medal: 'Bronze' | 'Silver' | 'Gold' | null
  survivors: number
  /** Призы этого босса — на экране итога. */
  prizes: BossPrize[]
}

export const BOSS_NAMES: Record<BossKind, string> = {
  Peasant: '🧑‍🌾 Крестьянин с вилами',
  Farquaad: '🤴 Лорд Фаркуад',
  Fairy: '🧚 Фея-крёстная',
}

export const ACTION_ICONS: Record<FighterAction, string> = {
  Attack: '⚔️',
  Dodge: '💨',
  Cover: '🔰',
  Power: '💥',
  Fire: '🔥',
  Heal: '💚',
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
      return { name: 'С разбега', hint: `Двойной урон ${amount}, но увернуться в этот ход нельзя` }
    case 'Fire':
      return { name: 'Драконий чих', hint: `Огнём по боссу: урон ${amount}, 4 маны` }
    case 'Heal':
      return { name: 'Луковый угар', hint: `Лечит союзника на ${amount}, 3 маны` }
  }
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

const FX_MS = 600

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
