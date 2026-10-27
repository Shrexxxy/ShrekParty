import { useState } from 'react'
import { api } from '../api/http'
import { errorText } from './errors'

/**
 * Действие пульта: вызов API + ошибка по-русски. Обновление экрана придёт снапшотом.
 */
export function useAdminAction() {
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function run(path: string, init: Parameters<typeof api>[1] = { method: 'POST' }): Promise<boolean> {
    setError(null)
    setBusy(true)
    try {
      await api(path, init)
      return true
    } catch (e) {
      setError(errorText(e))
      return false
    } finally {
      setBusy(false)
    }
  }

  return { run, error, busy }
}
