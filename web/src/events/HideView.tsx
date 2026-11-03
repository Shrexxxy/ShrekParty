import { useState } from 'react'
import { api } from '../api/http'
import { errorText } from '../admin/errors'
import { formatClock } from './countdown'
import type { HideDetails } from './details'
import { useCountdown } from './useCountdown'
import { Emoji, EmojiText } from '../components/Emoji'

/** Чёрный экран на всё окно: в темноте телефон не должен выдавать хозяина. */
export function HideDark() {
  return (
    <div className="hide-dark">
      <span><Emoji e="🤫" /></span>
    </div>
  )
}

/** Стена снимков; игрок голосует за самое смешное (можно передумать). */
export function HideShotWall({ eventId, details, canVote }: { eventId: string; details: HideDetails; canVote: boolean }) {
  const [error, setError] = useState<string | null>(null)

  async function vote(shotId: string) {
    setError(null)
    try {
      await api(`/api/hide/${eventId}/vote`, { method: 'POST', body: { shotId } })
    } catch (e) {
      setError(errorText(e))
    }
  }

  if (details.shots.length === 0) {
    return <p className="muted">Ни одного фото — никого не поймали <Emoji e="😎" /></p>
  }

  return (
    <>
      <div className="hide-wall">
        {details.shots.map((s) => (
          <figure
            key={s.id}
            className={s.id === details.myVote ? 'mine' : ''}
            onClick={canVote ? () => void vote(s.id) : undefined}
          >
            <img src={s.url} alt="" loading="lazy" />
            <figcaption>
              <EmojiText text={s.id === details.myVote ? '✅ ' : ''} /><Emoji e="😂" /> {s.votes}
            </figcaption>
          </figure>
        ))}
      </div>
      {error && <p className="error">{error}</p>}
    </>
  )
}

/** Прятки: в темноте — чёрный экран, потом голосование за самое смешное фото. */
export function HideView({ eventId, details, serverTime, tv }: { eventId: string; details: HideDetails; serverTime: string; tv?: boolean }) {
  const left = useCountdown(details.deadline, serverTime)
  if (details.phase !== 'Voting') {
    return <HideDark />
  }

  return (
    <div className={tv ? 'hide tv' : 'hide'}>
      <div className="hide-phase"><Emoji e="😂" /> Самое смешное фото</div>
      <div className="hide-clock">{formatClock(left)}</div>
      {!tv && <p className="muted">Тапни по фото, которое смешнее всех. Герои фото получат +15 <Emoji e="🫏" /></p>}
      <HideShotWall eventId={eventId} details={details} canVote={!tv} />
    </div>
  )
}
