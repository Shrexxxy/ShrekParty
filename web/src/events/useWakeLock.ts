import { useEffect } from 'react'

/**
 * Не даёт экрану погаснуть, пока active = true. Браузер снимает блокировку при сворачивании —
 * при возврате на вкладку берём её снова. Где Wake Lock не поддерживается, просто ничего не делаем.
 */
export function useWakeLock(active: boolean) {
  useEffect(() => {
    if (!active || !('wakeLock' in navigator)) {
      return
    }

    let lock: WakeLockSentinel | null = null
    let disposed = false

    const acquire = async () => {
      try {
        const sentinel = await navigator.wakeLock.request('screen')
        if (disposed) {
          await sentinel.release()
        } else {
          lock = sentinel
        }
      } catch {
        // Отказ браузера (низкий заряд, нет жеста) — не критично.
      }
    }

    const onVisible = () => {
      if (document.visibilityState === 'visible') {
        void acquire()
      }
    }

    void acquire()
    document.addEventListener('visibilitychange', onVisible)
    return () => {
      disposed = true
      document.removeEventListener('visibilitychange', onVisible)
      void lock?.release()
    }
  }, [active])
}
