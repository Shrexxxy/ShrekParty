import { useRef, useState, type ChangeEvent } from 'react'
import { api } from '../api/http'
import { errorText } from '../admin/errors'
import { photoForm } from '../components/imageUpload'
import { formatClock } from './countdown'
import type { PhotoHuntDetails } from './details'
import { useCountdown } from './useCountdown'
import { Emoji, EmojiText } from '../components/Emoji'

const MAX_SIDE = 1280

/** Фотоохота: задание и таймер; съёмка с телефона; после — стена фото и лайки. */
export function PhotoView({
  eventId,
  details,
  serverTime,
  myId,
  tv,
}: {
  eventId: string
  details: PhotoHuntDetails
  serverTime: string
  myId?: string
  tv?: boolean
}) {
  const left = useCountdown(details.deadline, serverTime)
  const input = useRef<HTMLInputElement>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const shooting = details.phase === 'Shooting' && left > 0

  async function onFile(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    e.target.value = ''
    if (!file) {
      return
    }
    setBusy(true)
    setError(null)
    try {
      await api(`/api/photo/${eventId}`, { method: 'POST', body: await photoForm(file, MAX_SIDE) })
    } catch (err) {
      setError(errorText(err))
    } finally {
      setBusy(false)
    }
  }

  async function toggleLike(ownerId: string, liked: boolean) {
    setError(null)
    try {
      await api(`/api/photo/${eventId}/likes/${ownerId}`, { method: liked ? 'DELETE' : 'PUT' })
    } catch (err) {
      setError(errorText(err))
    }
  }

  return (
    <div className={tv ? 'hunt tv' : 'hunt'}>
      <div className="hunt-task"><Emoji e="📸" /> {details.task}</div>
      {shooting ? (
        <>
          <div className="hide-clock">{formatClock(left)}</div>
          <div className="muted">
            Сдали: {details.submitted} / {details.total}
          </div>
          {!tv && (
            <>
              {details.myPhotoUrl && <img className="hunt-mine" src={details.myPhotoUrl} alt="Моё фото" />}
              <input ref={input} type="file" accept="image/*" capture="environment" hidden onChange={onFile} />
              <button className="primary" disabled={busy} onClick={() => input.current?.click()}>
                <EmojiText text={busy ? 'Загружаю…' : details.myPhotoUrl ? '🔁 Переснять' : '📷 Сфоткать'} />
              </button>
            </>
          )}
        </>
      ) : (
        <>
          <div className="muted">{tv ? 'Лайкайте с телефонов!' : 'Время вышло — лайкай фото других'}</div>
          <div className="hunt-wall">
            {details.photos.map((p) => {
              const mine = p.userId === myId
              return (
                <figure key={p.userId}>
                  <img src={p.photoUrl} alt={p.login} />
                  <figcaption>
                    <span>{p.login}</span>
                    {tv || mine ? (
                      <span><Emoji e="❤️" /> {p.likes}</span>
                    ) : (
                      <button className={p.likedByMe ? 'like on' : 'like'} onClick={() => void toggleLike(p.userId, p.likedByMe)}>
                        <EmojiText text={p.likedByMe ? '❤️' : '🤍'} /> {p.likes}
                      </button>
                    )}
                  </figcaption>
                </figure>
              )
            })}
          </div>
        </>
      )}
      {error && <p className="error">{error}</p>}
    </div>
  )
}
