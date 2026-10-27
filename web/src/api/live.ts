import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr'
import { useSyncExternalStore } from 'react'
import { bestOffset } from '../events/quiz'
import { applySnapshot } from './snapshot'
import type { AppSnapshot } from './types'

// Одно соединение на всё приложение; React подписывается через useSyncExternalStore.
let connection: HubConnection | null = null
let current: AppSnapshot | null = null
// Локальное время прихода текущего снапшота — для отсчётов по часам сервера.
let receivedAt = 0
const listeners = new Set<() => void>()
const lostListeners = new Set<() => void>()
// Замеры «часы сервера − часы телефона» по последним снапшотам — для честного времени нажатия.
const offsetSamples: number[] = []
const OFFSET_SAMPLES = 20

function emit(next: AppSnapshot) {
  offsetSamples.push(Date.parse(next.serverTime) - Date.now())
  if (offsetSamples.length > OFFSET_SAMPLES) {
    offsetSamples.shift()
  }

  const applied = applySnapshot(current, next)
  if (applied !== current) {
    current = applied
    receivedAt = Date.now()
    listeners.forEach((l) => l())
  }
}

/**
 * Когда (по часам телефона) пришёл текущий снапшот.
 */
export function snapshotReceivedAt(): number {
  return receivedAt
}

function start() {
  const conn = new HubConnectionBuilder()
    .withUrl('/hub')
    // Телефоны засыпают и теряют Wi-Fi — переподключаемся бесконечно.
    .withAutomaticReconnect({ nextRetryDelayInMilliseconds: (ctx) => Math.min(1000 * 2 ** ctx.previousRetryCount, 10_000) })
    .configureLogging(LogLevel.Warning)
    .build()

  conn.on('Snapshot', emit)
  conn.onreconnecting(() => lostListeners.forEach((l) => l()))
  conn.onclose(() => {
    if (connection === conn) {
      lostListeners.forEach((l) => l())
      setTimeout(() => connection === conn && conn.start().catch(() => undefined), 3000)
    }
  })
  connection = conn
  conn.start().catch(() => setTimeout(() => connection === conn && start(), 3000))
}

/**
 * На сколько часы сервера впереди часов телефона (лучшая оценка по последним снапшотам).
 */
export function clockOffset(): number {
  return bestOffset(offsetSamples)
}

/**
 * Подписка на обрыв живого канала (переподключение или закрытие); возвращает отписку.
 */
export function onConnectionLost(listener: () => void): () => void {
  lostListeners.add(listener)
  return () => lostListeners.delete(listener)
}

/**
 * Пересоздаёт соединение — после входа/выхода, чтобы сервер увидел новую cookie.
 */
export async function reconnectLive(): Promise<void> {
  const old = connection
  connection = null
  current = null
  await old?.stop()
  start()
}

function subscribe(listener: () => void) {
  if (connection === null) {
    start()
  }

  listeners.add(listener)
  return () => listeners.delete(listener)
}

/**
 * Последний снапшот состояния; null, пока не пришёл первый.
 */
export function useSnapshot(): AppSnapshot | null {
  return useSyncExternalStore(subscribe, () => current)
}
