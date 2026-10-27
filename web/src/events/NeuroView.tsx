import { api } from '../api/http'
import { Avatar } from '../components/Avatar'
import { formatClock } from './countdown'
import type { NeuroDetails } from './details'
import { useCountdown } from './useCountdown'

/** Угадай по нейрофото: фото, таймер, на телефоне — выбор, кто это; после — раскрытие. */
export function NeuroView({ eventId, details, serverTime, tv }: { eventId: string; details: NeuroDetails; serverTime: string; tv?: boolean }) {
  const left = useCountdown(details.deadline, serverTime)
  const guessing = details.phase === 'Guessing'
  const subject = details.options.find((o) => o.userId === details.subjectId)

  function guess(userId: string) {
    void api(`/api/neuro/${eventId}/guess`, { method: 'POST', body: { userId } }).catch(() => undefined)
  }

  return (
    <div className={tv ? 'neuro tv' : 'neuro'}>
      <div className="neuro-head">
        <span>
          Фото {details.index + 1} из {details.total}
        </span>
        {guessing && <b className="neuro-clock">{formatClock(left)}</b>}
      </div>
      <img className="neuro-photo" src={details.photoUrl} alt="" />

      {guessing ? (
        tv ? (
          <div className="muted">Ответили: {details.guessed}</div>
        ) : (
          <div className="neuro-options">
            {details.options.map((o) => (
              <button key={o.userId} className={o.userId === details.myGuess ? 'on' : ''} onClick={() => guess(o.userId)}>
                <Avatar url={o.avatarUrl} login={o.login} size={24} /> {o.login}
              </button>
            ))}
          </div>
        )
      ) : (
        <div className="neuro-reveal">
          <div className="neuro-subject">Это {subject?.login ?? '?'}!</div>
          {!tv && <div>{details.myGuess === details.subjectId ? '✅ Угадал!' : '❌ Мимо'}</div>}
          <div className="muted">{details.correct.length > 0 ? `Угадали: ${details.correct.join(', ')}` : 'Никто не угадал 😅'}</div>
        </div>
      )}
    </div>
  )
}
