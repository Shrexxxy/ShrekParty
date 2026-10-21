import { AnimatePresence, motion } from 'framer-motion'
import { useEffect, useRef, useState } from 'react'
import type { AppSnapshot } from '../api/types'
import { Avatar } from '../components/Avatar'
import { greetingFor } from './greetings'
import { Highlighted } from './Highlighted'

const GREETING_MS = 3500

interface Greeting {
  id: string
  text: string
  login: string
}

/**
 * Очередь приветствий: на каждого нового игрока — фраза по очереди.
 * Те, кто был до открытия экрана (перезагрузка ТВ), не приветствуются повторно.
 */
function useGreetings(snapshot: AppSnapshot): Greeting | null {
  const seen = useRef<Set<string> | null>(null)
  const used = useRef<number[]>([])
  const [queue, setQueue] = useState<Greeting[]>([])

  useEffect(() => {
    const rows = snapshot.leaderboard
    if (seen.current === null) {
      seen.current = new Set(rows.map((r) => r.userId))
      return
    }

    const fresh = rows.filter((r) => !seen.current!.has(r.userId))
    if (fresh.length === 0) {
      return
    }

    const added = fresh.map((r) => {
      seen.current!.add(r.userId)
      const g = greetingFor(r.login, seen.current!.size, used.current, Math.random)
      used.current = g.used
      return { id: r.userId, text: g.text, login: r.login }
    })
    setQueue((q) => [...q, ...added])
  }, [snapshot.leaderboard])

  const head = queue[0] ?? null
  useEffect(() => {
    if (head === null) {
      return
    }
    const timer = setTimeout(() => setQueue((q) => q.slice(1)), GREETING_MS)
    return () => clearTimeout(timer)
  }, [head])

  return head
}

/** До открытия — огромный счётчик ожидающих; после — кто уже успел, и приветствие каждому. */
export function TvWaiting({ snapshot }: { snapshot: AppSnapshot }) {
  const { state, waitingCount } = snapshot.registration
  const greeting = useGreetings(snapshot)

  if (state === 'Closed') {
    return (
      <div className="tv-hero">
        <div className="tv-hero-title">Ждут регистрации</div>
        <motion.div key={waitingCount} className="tv-hero-number" initial={{ scale: 1.4 }} animate={{ scale: 1 }}>
          {waitingCount}
        </motion.div>
      </div>
    )
  }

  return (
    <div className="tv-open">
      <motion.div className="tv-open-title" animate={{ scale: [1, 1.05, 1] }} transition={{ repeat: Infinity, duration: 1.2 }}>
        РЕГИСТРАЦИЯ ОТКРЫТА!
      </motion.div>
      <div className="tv-open-sub">
        Зарегистрировались: <b>{snapshot.leaderboard.length}</b> · ещё ждут: <b>{waitingCount}</b>
      </div>
      <ul className="tv-arrivals">
        <AnimatePresence>
          {snapshot.leaderboard.map((r) => (
            <motion.li key={r.userId} layout initial={{ opacity: 0, scale: 0.5 }} animate={{ opacity: 1, scale: 1 }}>
              <Avatar url={r.avatarUrl} login={r.login} size={72} />
              <span>{r.login}</span>
            </motion.li>
          ))}
        </AnimatePresence>
      </ul>
      <AnimatePresence>
        {greeting && (
          <motion.div
            key={greeting.id}
            className="tv-greeting"
            initial={{ opacity: 0, y: 60, scale: 0.8 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: -40 }}
          >
            <Highlighted text={greeting.text} logins={[greeting.login]} />
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  )
}
