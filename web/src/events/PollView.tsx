import { motion } from 'framer-motion'
import { useState } from 'react'
import { api } from '../api/http'
import { errorText } from '../admin/errors'
import type { PollDetails } from './details'

/** Голосование: на телефоне — большие кнопки, на ТВ — живые столбики; после закрытия — победитель (и «монетка» при ничьей). */
export function PollView({ eventId, details, tv }: { eventId: string; details: PollDetails; tv?: boolean }) {
  const [error, setError] = useState<string | null>(null)
  const total = details.options.reduce((sum, o) => sum + o.votes, 0)

  async function vote(option: string) {
    setError(null)
    try {
      await api(`/api/events/${eventId}/vote`, { method: 'POST', body: { option } })
    } catch (e) {
      setError(errorText(e))
    }
  }

  if (details.closed) {
    return (
      <div className={tv ? 'poll-result tv' : 'poll-result'}>
        {details.tie && <div className="poll-coin">🪙 Ничья — решила монетка!</div>}
        <div className="poll-winner">Играем в {details.winner}!</div>
      </div>
    )
  }

  return (
    <div className={tv ? 'poll tv' : 'poll'}>
      {!tv && <p className="muted">Голос можно менять, пока голосование открыто</p>}
      {details.options.map((o) => {
        const share = total === 0 ? 0 : o.votes / total
        return tv ? (
          <div key={o.name} className="poll-bar">
            <span className="poll-bar-name">{o.name}</span>
            <motion.span className="poll-bar-fill" animate={{ width: `${Math.max(share * 100, 2)}%` }} />
            <span className="poll-bar-count">{o.votes}</span>
          </div>
        ) : (
          <button key={o.name} className={o.name === details.myVote ? 'primary poll-option mine' : 'poll-option'} onClick={() => void vote(o.name)}>
            {o.name} <span className="muted">· {o.votes}</span>
          </button>
        )
      })}
      {error && <p className="error">{error}</p>}
    </div>
  )
}
