import { useEffect, useState } from 'react'
import { snapshotReceivedAt } from '../api/live'
import { remainingMs } from './countdown'

/**
 * Обратный отсчёт до дедлайна по часам сервера; перерисовка 4 раза в секунду.
 */
export function useCountdown(deadline: string, serverTime: string): number {
  const [now, setNow] = useState(() => Date.now())

  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 250)
    return () => clearInterval(timer)
  }, [])

  return remainingMs(deadline, serverTime, snapshotReceivedAt() || now, now)
}
