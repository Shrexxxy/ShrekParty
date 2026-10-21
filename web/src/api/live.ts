import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr'
import { useSyncExternalStore } from 'react'
import { applySnapshot } from './snapshot'
import type { AppSnapshot } from './types'

// Одно соединение на всё приложение; React подписывается через useSyncExternalStore.
let connection: HubConnection | null = null
let current: AppSnapshot | null = null
const listeners = new Set<() => void>()

function emit(next: AppSnapshot) {
  const applied = applySnapshot(current, next)
  if (applied !== current) {
    current = applied
    listeners.forEach((l) => l())
  }
}

function start() {
  const conn = new HubConnectionBuilder()
    .withUrl('/hub')
    // Телефоны засыпают и теряют Wi-Fi — переподключаемся бесконечно.
    .withAutomaticReconnect({ nextRetryDelayInMilliseconds: (ctx) => Math.min(1000 * 2 ** ctx.previousRetryCount, 10_000) })
    .configureLogging(LogLevel.Warning)
    .build()

  conn.on('Snapshot', emit)
  conn.onclose(() => {
    if (connection === conn) {
      setTimeout(() => connection === conn && conn.start().catch(() => undefined), 3000)
    }
  })
  connection = conn
  conn.start().catch(() => setTimeout(() => connection === conn && start(), 3000))
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
