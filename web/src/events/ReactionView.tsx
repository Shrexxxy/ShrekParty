import { useEffect, useRef, useState } from 'react'
import { api } from '../api/http'
import { Avatar } from '../components/Avatar'
import type { ReactionDetails } from './details'
import { reactionTap } from './layer3'
import { Emoji, EmojiText } from '../components/Emoji'

/** Табличка времён: попытки по раундам и лучшая. */
function ReactionBoard({ details }: { details: ReactionDetails }) {
    return (
        <div className="reaction-board-wrapper">
            <table className="reaction-board">
                <thead>
                <tr>
                    <th />
                    {Array.from({ length: details.totalRounds }, (_, i) => (
                        <th key={i}>{i + 1}</th>
                    ))}
                    <th>Лучшее</th>
                </tr>
                </thead>
                <tbody>
                {details.rows.map((r) => (
                    <tr key={r.userId}>
                        <td className="reaction-player">
                            <Avatar url={r.avatarUrl} login={r.login} size={28} />
                            <span className="reaction-login">{r.login}</span>
                        </td>
                        {Array.from({ length: details.totalRounds }, (_, i) => {
                            const attempt = r.attempts.find((a) => a.round === i + 1)
                            return (
                                <td key={i}>
                                    {!attempt ? '—' : attempt.falseStart ? <Emoji e="🚫" /> : attempt.ms}
                                </td>
                            )
                        })}
                        <td className="reaction-best">{r.best ?? '—'}</td>
                    </tr>
                ))}
                </tbody>
            </table>
        </div>
    )
}

/**
 * Зелёный экран: время меряем от кадра, в котором телефон позеленел, до касания — сеть на результат не влияет.
 */
function GreenScreen({ onTap }: { onTap: (ms: number) => void }) {
  const greenAt = useRef<number | null>(null)

  useEffect(() => {
    const frame = requestAnimationFrame(() => {
      greenAt.current = performance.now()
    })
    return () => cancelAnimationFrame(frame)
  }, [])

  return (
    <button
      className="reaction-screen green"
      onPointerDown={() => {
        const ms = reactionTap(greenAt.current, performance.now())
        if (ms !== null) {
          onTap(ms)
        }
      }}
    >
      ЖМИ!
    </button>
  )
}

/** Реакция Шрека: на телефоне — красный/зелёный экран на весь дисплей, на ТВ — табличка. */
export function ReactionView({ eventId, details, tv }: { eventId: string; details: ReactionDetails; tv?: boolean }) {
  // Отправленный тап — до прихода снапшота экран уже не должен ловить касания.
  const [tappedKey, setTappedKey] = useState<string | null>(null)
  const key = `${details.round}-${details.phase}`
  const done = details.myAttempt !== null || details.myFalseStart || tappedKey === key

  async function send(path: string, body?: unknown) {
    setTappedKey(key)
    await api(path, { method: 'POST', body }).catch(() => undefined)
  }

  if (tv) {
    return (
      <div className="reaction tv">
        <div className={`reaction-phase ${details.phase.toLowerCase()}`}>
          Раунд {details.round} из {details.totalRounds} · {details.phase === 'Red' ? 'Ждём…' : details.phase === 'Green' ? 'ЖМИ!' : 'Итоги раунда'}
        </div>
        <ReactionBoard details={details} />
      </div>
    )
  }

  if (!done && details.phase === 'Red') {
    return (
      <button className="reaction-screen red" onPointerDown={() => void send(`/api/reaction/${eventId}/false-start`)}>
        Жди зелёного…
      </button>
    )
  }

  if (!done && details.phase === 'Green') {
    return <GreenScreen key={key} onTap={(ms) => void send(`/api/reaction/${eventId}/attempt`, { ms })} />
  }

  return (
    <div className="reaction">
      <div className="reaction-phase">
        Раунд {details.round} из {details.totalRounds}
      </div>
      <div className="reaction-mine">
        <EmojiText text={details.myFalseStart ? '🚫 Фальстарт — жди следующего раунда' : details.myAttempt !== null ? `⚡ ${details.myAttempt} мс` : 'Раунд прошёл без тебя'} />
      </div>
      <ReactionBoard details={details} />
    </div>
  )
}
