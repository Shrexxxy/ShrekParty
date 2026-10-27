import { AnimatePresence, motion } from 'framer-motion'
import { useEffect, useState } from 'react'
import type { TvMessageDto } from '../api/types'
import { remainingMs } from '../events/countdown'

/** Ручное сообщение именинника поверх всего экрана ТВ; прячется само, когда истекло. */
export function TvMessageOverlay({ message, serverTime }: { message: TvMessageDto | null | undefined; serverTime: string }) {
  const [hiddenFor, setHiddenFor] = useState<string | null>(null)

  useEffect(() => {
    if (!message) {
      return
    }
    // Сервер по истечении не шлёт снапшот — прячем по таймеру сами.
    const left = remainingMs(message.until, serverTime, Date.now(), Date.now())
    const timer = setTimeout(() => setHiddenFor(message.until), left)
    return () => clearTimeout(timer)
  }, [message, serverTime])

  const visible = message && hiddenFor !== message.until
  return (
    <AnimatePresence>
      {visible && (
        <motion.div key={message.until} className="tv-message" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }}>
          {message.imageUrl && <img src={message.imageUrl} alt="" />}
          <div className="tv-message-text">{message.text}</div>
        </motion.div>
      )}
    </AnimatePresence>
  )
}
