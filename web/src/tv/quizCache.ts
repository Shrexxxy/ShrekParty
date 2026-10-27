import { useEffect } from 'react'
import { api } from '../api/http'

const CACHE_NAME = 'quiz-media'
const RECHECK_MS = 60_000

interface QuizMediaDto {
  url: string
  type: 'Image' | 'Audio' | 'Video'
}

/** Что ещё скачать: адреса без повторов, которых нет в кэше. */
export function missingMedia(urls: string[], cached: Set<string>): string[] {
  return [...new Set(urls)].filter((u) => !cached.has(u))
}

/** Что убрать из кэша: медиа, которых больше нет в игре (заменили или удалили вопрос). */
export function staleMedia(cached: string[], current: string[]): string[] {
  const keep = new Set(current)
  return cached.filter((u) => !keep.has(u))
}

/**
 * Не запускать работу второй раз, пока идёт первая: новый вызов получает тот же промис.
 */
export function singleFlight(work: () => Promise<void>): () => Promise<void> {
  let running: Promise<void> | null = null
  return () => {
    running ??= work().finally(() => {
      running = null
    })
    return running
  }
}

/** Cache Storage есть только в защищённом контексте (https или localhost). */
function cacheAvailable(): boolean {
  return typeof caches !== 'undefined'
}

/** Путь из полного адреса запроса в кэше — чтобы сравнивать с адресами из списка. */
function pathOf(request: Request): string {
  return new URL(request.url).pathname
}

/**
 * Скачивает все медиа Своей игры в кэш браузера (заодно выкидывает ненужные) и сообщает серверу прогресс (его видит именинник).
 * Без Cache Storage просто прогревает HTTP-кэш браузера.
 */
async function preloadQuizMedia(): Promise<void> {
  const list = await api<QuizMediaDto[]>('/api/tv/quiz-media')
  const urls = [...new Set(list.map((m) => m.url))]
  const cache = cacheAvailable() ? await caches.open(CACHE_NAME) : null
  const cachedPaths = cache ? (await cache.keys()).map(pathOf) : []
  for (const stale of staleMedia(cachedPaths, urls)) {
    await cache?.delete(stale)
  }

  const cached = new Set(cachedPaths)
  const missing = missingMedia(urls, cached)
  let loaded = urls.length - missing.length
  const report = () => api('/api/tv/preload', { method: 'POST', body: { loaded, total: urls.length } }).catch(() => undefined)

  await report()
  for (const url of missing) {
    try {
      const response = await fetch(url, { credentials: 'include' })
      if (response.ok) {
        await cache?.put(url, response)
        loaded += 1
        await report()
      }
    } catch {
      // Сеть моргнула — докачаем на следующей проверке.
    }
  }
}

/** Предзагрузка без наложений: большое видео может качаться дольше минуты между проверками. */
export const preloadQuizMediaOnce = singleFlight(preloadQuizMedia)

/**
 * Адрес для плеера: из кэша, если файл уже скачан (blob, без сети), иначе обычный адрес.
 */
export async function cachedSrc(url: string): Promise<string> {
  if (!cacheAvailable()) {
    return url
  }

  const hit = await (await caches.open(CACHE_NAME)).match(url)
  return hit ? URL.createObjectURL(await hit.blob()) : url
}

/** ТВ: докачивает медиа Своей игры сразу и раз в минуту — вдруг именинник добавил новые. */
export function useQuizPreload() {
  useEffect(() => {
    const run = () => void preloadQuizMediaOnce().catch(() => undefined)
    run()
    const timer = setInterval(run, RECHECK_MS)
    return () => clearInterval(timer)
  }, [])
}
