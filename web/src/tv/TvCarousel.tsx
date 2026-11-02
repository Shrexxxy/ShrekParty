import { AnimatePresence, motion } from 'framer-motion'
import { useEffect, useState } from 'react'
import { api } from '../api/http'
import type { AppSnapshot } from '../api/types'
import { Leaderboard } from '../components/Leaderboard'
import { NeuroTriple } from '../events/NeuroView'
import { factsForCycle } from './carousel'
import { Highlighted } from './Highlighted'

const LEADERBOARD_MS = 60_000
const FACT_MS = 10_000

/** Слайд карусели: факт, фото (матч, мем, фотоохота) с подписью или тройка нейрофото. */
interface SlideDto {
  kind: 'Fact' | 'Photo' | 'Neuro'
  text: string
  logins: string[]
  imageUrl: string | null
  /** Нейрофото: друг, образ, что получилось. */
  images: string[] | null
}

/** Между ивентами: общая таблица минуту → два слайда (факты и фото) по 10 с → снова таблица; слайды по кругу. */
export function TvCarousel({ snapshot }: { snapshot: AppSnapshot }) {
  const [cycle, setCycle] = useState(0)
  const [facts, setFacts] = useState<SlideDto[] | null>(null)
  const [index, setIndex] = useState(0)

  useEffect(() => {
    if (facts === null) {
      const timer = setTimeout(async () => {
        // Перечитываем на каждом круге — появляются новые фото и факты.
        const all = await api<SlideDto[]>('/api/tv/slides').catch(() => [])
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
          {fact.kind === 'Neuro' && fact.images ? (
            <NeuroTriple friendUrl={fact.images[0]} sourceUrl={fact.images[1]} resultUrl={fact.images[2]} />
          ) : fact.imageUrl ? (
            <img className="tv-slide-photo" src={fact.imageUrl} alt="" />
          ) : (
            <div className="tv-fact-label">А вы знали?</div>
          )}
          <div className={fact.imageUrl || fact.images ? 'tv-slide-caption' : 'tv-fact-text'}>
            <Highlighted text={fact.text} logins={fact.logins} />
          </div>
        </motion.div>
      )}
    </AnimatePresence>
  )
}
