/**
 * Логика мини-игры «Шрек ловит лук» (как «Ну, погоди!»), без DOM.
 * Жёлоба: 0 — левый верхний, 1 — левый нижний, 2 — правый верхний, 3 — правый нижний.
 * Очки за стрик: k-й лук подряд даёт +k; промах обнуляет стрик и возвращает базовый темп.
 */

export type Lane = 0 | 1 | 2 | 3

export interface Onion {
  id: number
  lane: Lane
  /** Сколько уже катится, мс. */
  progressMs: number
  /** Сколько катится до края, мс (фиксируется при появлении). */
  durationMs: number
}

export interface Catch {
  id: number
  lane: Lane
  points: number
}

export interface GameState {
  target: number
  score: number
  streak: number
  missed: number
  shrekLane: Lane
  onions: Onion[]
  sinceSpawnMs: number
  nextId: number
  elapsedMs: number
  /** Когда докатится последний из уже выпущенных луков (время игры). */
  lastArrivalMs: number
  /** Последний пойманный — для всплывающего «+k». */
  lastCatch: Catch | null
  done: boolean
  /** Закончилось по таймауту, а не по очкам. */
  tiredOut: boolean
  random: () => number
}

export const TARGET_SCORE = 224
export const TIMEOUT_MS = 5 * 60_000
export const MIN_ARRIVAL_GAP_MS = 350

const START_ROLL_MS = 3000
const MIN_ROLL_MS = 700
const SPEEDUP_PER_STREAK_MS = 120
const START_SPAWN_FACTOR = 0.6
const SPAWN_FACTOR_STEP = 0.025
const MIN_SPAWN_FACTOR = 0.25

/** Время качения при данном стрике. */
export function rollMs(streak: number): number {
  return Math.max(MIN_ROLL_MS, START_ROLL_MS - SPEEDUP_PER_STREAK_MS * streak)
}

/** Интервал появления: доля от времени качения, на поле до ~4 луков. */
export function spawnIntervalMs(streak: number): number {
  return rollMs(streak) * Math.max(MIN_SPAWN_FACTOR, START_SPAWN_FACTOR - SPAWN_FACTOR_STEP * streak)
}

function spawn(state: GameState): GameState {
  const lane = Math.min(3, Math.floor(state.random() * 4)) as Lane
  // Никогда два лука не докатываются почти одновременно — иначе поймать оба нельзя.
  const arrival = Math.max(state.elapsedMs + rollMs(state.streak), state.lastArrivalMs + MIN_ARRIVAL_GAP_MS)
  const onion: Onion = { id: state.nextId, lane, progressMs: 0, durationMs: arrival - state.elapsedMs }
  return { ...state, onions: [...state.onions, onion], nextId: state.nextId + 1, lastArrivalMs: arrival }
}

/** Новая игра; первый лук появляется сразу. */
export function createGame(target = TARGET_SCORE, random: () => number = Math.random): GameState {
  return spawn({
    target,
    score: 0,
    streak: 0,
    missed: 0,
    shrekLane: 0,
    onions: [],
    sinceSpawnMs: 0,
    nextId: 1,
    elapsedMs: 0,
    lastArrivalMs: -MIN_ARRIVAL_GAP_MS,
    lastCatch: null,
    done: false,
    tiredOut: false,
    random,
  })
}

/** Шрек поворачивается к жёлобу. */
export function move(state: GameState, lane: Lane): GameState {
  return state.done || state.shrekLane === lane ? state : { ...state, shrekLane: lane }
}

/** Продвигает время: лук катится, долетевший ловится или падает, появляется новый. */
export function tick(state: GameState, dtMs: number): GameState {
  if (state.done) {
    return state
  }

  let { score, streak, missed, lastCatch } = state
  const rolling: Onion[] = []
  // Сначала те, что докатились раньше, — порядок важен для стрика.
  for (const onion of [...state.onions].sort((a, b) => a.durationMs - a.progressMs - (b.durationMs - b.progressMs))) {
    const progressMs = onion.progressMs + dtMs
    if (progressMs < onion.durationMs) {
      rolling.push({ ...onion, progressMs })
    } else if (onion.lane === state.shrekLane) {
      streak++
      score += streak
      lastCatch = { id: onion.id, lane: onion.lane, points: streak }
    } else {
      streak = 0
      missed++
    }
  }

  const elapsedMs = state.elapsedMs + dtMs
  let next: GameState = {
    ...state,
    score,
    streak,
    missed,
    lastCatch,
    onions: rolling,
    elapsedMs,
    sinceSpawnMs: state.sinceSpawnMs + dtMs,
  }

  if (score >= state.target) {
    return { ...next, onions: [], done: true }
  }
  if (elapsedMs >= TIMEOUT_MS) {
    return { ...next, onions: [], done: true, tiredOut: true }
  }

  const interval = spawnIntervalMs(streak)
  if (next.sinceSpawnMs >= interval) {
    next = spawn({ ...next, sinceSpawnMs: next.sinceSpawnMs - interval })
  }

  return next
}
