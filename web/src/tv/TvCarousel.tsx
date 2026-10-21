import { AnimatePresence, motion } from 'framer-motion'
import { useEffect, useState } from 'react'
import { api } from '../api/http'
import type { AppSnapshot, FactDto } from '../api/types'
import { Leaderboard } from '../components/Leaderboard'
import { factsForCycle } from './carousel'
import { Highlighted } from './Highlighted'

const LEADERBOARD_MS = 60_000
const FACT_MS = 10_000

/** Между ивентами: общая таблица минуту → два факта по 10 с → снова таблица; факты по кругу. */
export function TvCarousel({ snapshot }: { snapshot: AppSnapshot }) {
  const [cycle, setCycle] = useState(0)
  const [facts, setFacts] = useState<FactDto[] | null>(null)
  const [index, setIndex] = useState(0)

  useEffect(() => {
    if (facts === null) {
      const timer = setTimeout(async () => {
        // Перечитываем на каждом круге — появляются новые игроки и факты.
        const all = await api<FactDto[]>('/api/tv/facts').catch(() => [])
        setFacts(factsForCycle(all, cycle))
        setIndex(0)
      }, LEADERBOARD_MS)
      return () => clearTimeout(timer)
    }

    const timer = setTimeout(
      () => {
        if (index + 1 < facts.length) {
          setIndex(index + 1)
        } else {
          setFacts(null)
          setCycle((c) => c + 1)
        }
      },
      facts.length === 0 ? 0 : FACT_MS,
    )
    return () => clearTimeout(timer)
  }, [facts, index, cycle])

  const fact = facts?.[index]

  return (
    <AnimatePresence mode="wait">
      {!fact ? (
        <motion.div key="leaderboard" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }}>
          <h1 className="tv-title">Таблица лидеров</h1>
          <Leaderboard snapshot={snapshot} />
        </motion.div>
      ) : (
        <motion.div
          key={`${cycle}-${index}`}
          className="tv-fact"
          initial={{ opacity: 0, y: 40 }}
          animate={{ opacity: 1, y: 0 }}
          exit={{ opacity: 0, y: -40 }}
        >
          <div className="tv-fact-label">А вы знали?</div>
          <div className="tv-fact-text">
            <Highlighted text={fact.text} logins={fact.logins} />
          </div>
        </motion.div>
      )}
    </AnimatePresence>
  )
}
