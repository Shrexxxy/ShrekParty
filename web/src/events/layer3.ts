import type { ActiveEventDto, RegistrationState } from '../api/types'

/** Пауза между раскрытиями ответов на ТВ. */
export const REVEAL_STEP_MS = 2000

/**
 * Сколько пунктов раскрытия уже показано: ответы по одному, последний пункт — правда.
 * Возвращает 1…total+1; total+1 — правда на экране.
 */
export function revealedCount(elapsedMs: number, total: number, stepMs = REVEAL_STEP_MS): number {
  return Math.min(total + 1, Math.floor(elapsedMs / stepMs) + 1)
}

/** Где дракон на трассе: доля от лидера (0…1). */
export function raceProgress(taps: number, leaderTaps: number): number {
  return leaderTaps <= 0 ? 0 : taps / leaderTaps
}

/** Плашка на ТВ во время тапалки: отсечка «30 секунд» (3 с) и «последние 10» до конца. */
export function tapperBanner(msLeft: number): string | null {
  if (msLeft <= 0) {
    return null
  }
  if (msLeft <= 10_000) {
    return 'Последние 10!'
  }
  if (msLeft <= 30_000 && msLeft > 27_000) {
    return 'Осталось 30 секунд!'
  }

  return null
}

/**
 * Время реакции от кадра, в котором экран позеленел; кадра ещё не было — тап не засчитывается
 * (иначе вышла бы «1 мс», которую не перебить).
 */
export function reactionTap(greenAt: number | null, tapAt: number): number | null {
  return greenAt === null ? null : Math.max(1, Math.round(tapAt - greenAt))
}

/** Селфи только между ивентами: камера сворачивает страницу, а в «днях» это дисквалификация. */
export function selfieAllowed(registration: RegistrationState, activeEvent: ActiveEventDto | null | undefined): boolean {
  return registration === 'Finished' && !activeEvent
}

/** С какого числа начинать счётчик дракона: после перезагрузки — с серверного. */
export function myTaps(rows: { userId: string; taps: number }[], myId: string | undefined): number {
  return rows.find((r) => r.userId === myId)?.taps ?? 0
}
