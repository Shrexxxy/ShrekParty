import type { AppSnapshot } from './types'

/**
 * Выбирает, какой снапшот показывать: при переподключениях старый может прийти позже нового.
 */
export function applySnapshot(prev: AppSnapshot | null, next: AppSnapshot): AppSnapshot {
  if (prev === null) {
    return next
  }

  return Date.parse(next.serverTime) >= Date.parse(prev.serverTime) ? next : prev
}
