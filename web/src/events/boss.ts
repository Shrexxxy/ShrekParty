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
  myOptions: { action: FighterAction; unavailable: string | null }[]
  feed: Fx[]
  cheerBonus: number
  iCheered: boolean
  medal: 'Bronze' | 'Silver' | 'Gold' | null
  survivors: number
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
