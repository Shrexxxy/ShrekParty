import type { ActiveEventDto, PetClass, RegistrationState } from '../api/types'
import { ASSETS } from '../assets'
import type { TapperDetails } from './details'

/** Пауза между раскрытиями ответов на ТВ. */
export const REVEAL_STEP_MS = 2000

/**
 * Сколько пунктов раскрытия уже показано: ответы по одному, последний пункт — правда.
 * Возвращает 1…total+1; total+1 — правда на экране.
 */
export function revealedCount(elapsedMs: number, total: number, stepMs = REVEAL_STEP_MS): number {
  return Math.min(total + 1, Math.floor(elapsedMs / stepMs) + 1)
}

/** Дорожка гонки на ТВ: место по тапам (равные делят место; 0 тапов — без места). */
export type RaceLane = TapperDetails['rows'][number] & { place: number | null }

/** Дорожки в постоянном порядке — по логину, чтобы никто не прыгал вверх-вниз; кто впереди — видно по месту. */
export function raceLanes(rows: TapperDetails['rows']): RaceLane[] {
  return [...rows]
    .sort((a, b) => a.login.localeCompare(b.login, 'ru', { sensitivity: 'base' }))
    .map((r) => ({ ...r, place: r.taps > 0 ? 1 + rows.filter((o) => o.taps > r.taps).length : null }))
}

/** Кадр полёта: 0 — покой (обычная картинка), 1–3 — взмах крыльями. */
export type FlapFrame = 0 | 1 | 2 | 3

/** Кто летит: питомец игрока (кадры взмаха — его картинка с -fly-1…3), без питомца — общий дракон тапалки. */
export function flyerArt(pet: { petClass: PetClass | null; petVariant: number | null }, frame: FlapFrame): string {
  if (!pet.petClass || !pet.petVariant) {
    return frame < 2 ? ASSETS.tapper.dragonUp : ASSETS.tapper.dragonDown
  }

  return `/assets/pets/${pet.petClass.toLowerCase()}-${pet.petVariant}${frame === 0 ? '' : `-fly-${frame}`}.png`
}

/** Взмахи: какой кадр сейчас и сколько взмахов ещё в очереди. */
export interface Flap {
  frame: FlapFrame
  queue: number
}

/**
 * Тап — ещё взмах в очередь. maxQueue — сколько взмахов вперёд можно накопить: на телефоне 1 (перестал тапать —
 * питомец доводит взмах и замирает), на ТВ больше — тапы приходят пачкой раз в секунду, между ними он не должен замирать.
 */
export function flapTap(flap: Flap, taps = 1, maxQueue = 1): Flap {
  return { ...flap, queue: Math.min(maxQueue, flap.queue + taps) }
}

/** Следующий кадр: взмах — три кадра подряд; в очереди есть ещё — сразу следующий, без покоя между ними. */
export function flapTick(flap: Flap): Flap {
  if (flap.frame === 1 || flap.frame === 2) {
    return { ...flap, frame: (flap.frame + 1) as FlapFrame }
  }

  return flap.queue > 0 ? { frame: 1, queue: flap.queue - 1 } : { frame: 0, queue: 0 }
}

/** Темп: сколько тапов было за последнюю секунду (times — моменты тапов, мс). */
export function tapRate(times: number[], now: number, windowMs = 1000): number {
  return times.filter((t) => t > now - windowMs).length
}

/** Небо едет на 45 px за каждый тап в секунду; выше 15 тапов в секунду не ускоряется. */
const PX_PER_TAP = 45
const MAX_RATE = 15

/** Скорость неба (px/с) по темпу тапов: не тапаешь — стоит. */
export function skySpeed(rate: number): number {
  return Math.min(rate, MAX_RATE) * PX_PER_TAP
}

/** Плавный разгон и торможение: за кадр dt скорость проходит часть пути к цели, за секунду — почти весь. */
export function easeSpeed(current: number, target: number, dtSec: number): number {
  return current + (target - current) * (1 - Math.exp(-dtSec * 5))
}

/** Где дракон на трассе: доля от лидера (0…1). */
export function raceProgress(taps: number, leaderTaps: number): number {
  return leaderTaps <= 0 ? 0 : taps / leaderTaps
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
