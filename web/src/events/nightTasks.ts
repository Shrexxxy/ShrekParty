/** Ночные задания мафии: логика без React — компоненты в MafiaTasks.tsx только рисуют и ловят пальцы. */

import { ApiError } from '../api/http'

export type TaskKind = 'onion' | 'flies' | 'gingerbread'

/** Заданий за ночь — столько же считает сервер. */
export const MAX_TASKS = 5

/** Повторять отправку задания стоит только на «слишком быстро»; конец ночи, лимит, выбывание — окончательно. */
export function retryTask(error: unknown): boolean {
  return error instanceof ApiError && error.code === 'too_fast'
}

export const TASK_KINDS: TaskKind[] = ['onion', 'flies', 'gingerbread']

/** Следующее задание — случайное, но не то же, что только что. */
export function nextTask(prev: TaskKind | null, random: () => number = Math.random): TaskKind {
  const pool = TASK_KINDS.filter((k) => k !== prev)
  return pool[Math.min(pool.length - 1, Math.floor(random() * pool.length))]
}

/** Слоёв у лука. */
export const ONION_LAYERS = 5

const SWIPE_PX = 60

/** Свайп снимает слой: палец ушёл от 60 px в любую сторону. */
export function isSwipe(dx: number, dy: number): boolean {
  return Math.hypot(dx, dy) >= SWIPE_PX
}

export interface Fly {
  id: number
  /** Положение в процентах поля. */
  x: number
  y: number
}

const FLY_COUNT = 8

/** Мухи в случайных местах, не у самого края — чтобы палец доставал. */
export function makeFlies(random: () => number = Math.random): Fly[] {
  return Array.from({ length: FLY_COUNT }, (_, id) => ({ id, x: 10 + random() * 80, y: 10 + random() * 80 }))
}

export function swat(flies: Fly[], id: number): Fly[] {
  return flies.filter((f) => f.id !== id)
}

/** Поле пряни в пикселях: контур сверху, детали лежат внизу. */
export const GINGER_FIELD = { width: 300, height: 340 }

export interface GingerPart {
  id: string
  label: string
  /** Центр своего места на контуре. */
  slot: { x: number; y: number }
  /** Где деталь лежит в начале. */
  start: { x: number; y: number }
}

export const GINGER_PARTS: GingerPart[] = [
  { id: 'head', label: 'Голова', slot: { x: 150, y: 45 }, start: { x: 40, y: 300 } },
  { id: 'leftArm', label: 'Рука', slot: { x: 85, y: 115 }, start: { x: 113, y: 300 } },
  { id: 'rightArm', label: 'Рука', slot: { x: 215, y: 115 }, start: { x: 187, y: 300 } },
  { id: 'legs', label: 'Ноги', slot: { x: 150, y: 200 }, start: { x: 260, y: 300 } },
]

const SNAP_PX = 40

/** Отпустили рядом со своим местом — деталь прилипает. */
export function snaps(part: GingerPart, drop: { x: number; y: number }): boolean {
  return Math.hypot(drop.x - part.slot.x, drop.y - part.slot.y) <= SNAP_PX
}

export function placePart(placed: string[], id: string): string[] {
  return placed.includes(id) ? placed : [...placed, id]
}
