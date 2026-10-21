import { AnimatePresence, motion } from 'framer-motion'
import { useEffect, useState } from 'react'
import { api } from '../api/http'
import type { RegistrationResultRowDto } from '../api/types'
import { RegistrationResults } from '../components/RegistrationResults'
import { closingGreetings } from './greetings'
import { Highlighted } from './Highlighted'

const CLOSING_MS = 4500

/**
 * Сразу после закрытия регистрации: фразы предпоследнему и последнему, затем таблица регистрации.
 * `withClosing` — только если ТВ видел само закрытие (после перезагрузки фразы не повторяем).
 */
export function TvAfterClose({ withClosing, tableMs, onDone }: { withClosing: boolean; tableMs: number; onDone: () => void }) {
  const [closing, setClosing] = useState<{ text: string; login: string }[] | null>(withClosing ? null : [])
  const [index, setIndex] = useState(0)

  useEffect(() => {
    if (!withClosing) {
      return
    }
    api<RegistrationResultRowDto[]>('/api/registration/results')
      .then((rows) => {
        const bySpeed = rows
          .filter((r) => !r.isLate)
          .sort((a, b) => (a.speedPlace ?? 0) - (b.speedPlace ?? 0))
          .map((r) => r.login)
        const texts = closingGreetings(bySpeed)
        setClosing(texts.map((text, i) => ({ text, login: bySpeed[bySpeed.length - texts.length + i] })))
      })
      .catch(() => setClosing([]))
  }, [withClosing])

  const showingClosing = closing !== null && index < closing.length

  useEffect(() => {
    if (closing === null) {
      return
    }
    const timer = showingClosing ? setTimeout(() => setIndex((i) => i + 1), CLOSING_MS) : setTimeout(onDone, tableMs)
    return () => clearTimeout(timer)
  }, [closing, index, showingClosing, tableMs, onDone])

  if (closing === null) {
    return null
  }

  return (
    <AnimatePresence mode="wait">
      {showingClosing ? (
        <motion.div
          key={index}
          className="tv-fact"
          initial={{ opacity: 0, scale: 0.8 }}
          animate={{ opacity: 1, scale: 1 }}
          exit={{ opacity: 0 }}
        >
          <div className="tv-fact-text">
            <Highlighted text={closing[index].text} logins={[closing[index].login]} />
          </div>
        </motion.div>
      ) : (
        <motion.div key="table" initial={{ opacity: 0 }} animate={{ opacity: 1 }}>
          <h1 className="tv-title">Итоги регистрации</h1>
          <RegistrationResults includeLate={false} />
        </motion.div>
      )}
    </AnimatePresence>
  )
}
