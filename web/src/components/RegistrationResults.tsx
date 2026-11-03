import { useEffect, useState } from 'react'
import { api } from '../api/http'
import type { RegistrationResultRowDto } from '../api/types'
import { Avatar } from './Avatar'
import { crackTimeText } from './crackTime'
import { Donkeys } from './DonkeyIcon'
import { medalFor } from './medals'
import { StrengthBar } from './StrengthBar'
import { EmojiText } from './Emoji'

/** «за ~3 секунды», но «мгновенно» и «дольше, чем…» — без «за». */
function crackPhrase(guessesLog10: number): string {
  const text = crackTimeText(guessesLog10)
  return text.startsWith('~') ? `за ${text}` : text
}

function formatElapsed(seconds: number): string {
  const total = Math.round(seconds)
  return `${Math.floor(total / 60)}:${String(total % 60).padStart(2, '0')}`
}

/**
 * Таблица регистрации: время с медалькой за скорость, сложность пароля и за что сколько осликов.
 * Опоздавшие — в конце, без места и очков. `version` — чтобы перечитывать при обновлениях снапшота.
 */
export function RegistrationResults({
  version,
  highlightUserId,
  includeLate = true,
}: {
  version?: string
  highlightUserId?: string
  includeLate?: boolean
}) {
  const [rows, setRows] = useState<RegistrationResultRowDto[] | null>(null)

  useEffect(() => {
    api<RegistrationResultRowDto[]>('/api/registration/results').then(setRows).catch(() => undefined)
  }, [version])

  if (rows === null) {
    return <p className="muted">Загрузка…</p>
  }

  const visible = includeLate ? rows : rows.filter((r) => !r.isLate)
  if (visible.length === 0) {
    return <p className="muted">Итоги появятся, когда регистрацию закроют</p>
  }

  const lastSpeedPlace = Math.max(0, ...rows.flatMap((r) => (r.speedPlace === null ? [] : [r.speedPlace])))

  return (
    <div className="reg-results">
      <div className="reg-head" aria-hidden>
        <span>#</span>
        <span>Игрок</span>
        <span>Время</span>
        <span>Пароль</span>
        <span>Скорость</span>
        <span>Пароль</span>
        <span>Итого</span>
      </div>
      <ol className="rows">
        {visible.map((r) => (
          <li key={r.userId} className={['reg-row', r.isLate && 'late', r.userId === highlightUserId && 'me'].filter(Boolean).join(' ')}>
            {/* На телефоне — две строки, на широком экране обе раскрываются в колонки (display: contents). */}
            <div className="reg-main">
              <span className="place">{r.place ?? '—'}</span>
              <span className="who">
                <Avatar url={r.avatarUrl} login={r.login} size={32} />
                <span className="reg-login">{r.login}</span>
              </span>
              <span className="total">
                <Donkeys value={r.totalDonkeys} />
              </span>
            </div>
            <div className="reg-details">
              <span className="time" title="Время регистрации от открытия">
                <EmojiText text={r.isLate ? '⏰ опоздал' : '⏱'} /> {formatElapsed(r.elapsedSeconds)}
                {r.speedPlace !== null && (
                  <span className="medal" title="Медалька за скорость">
                    <EmojiText text={medalFor(r.speedPlace, lastSpeedPlace)} />
                  </span>
                )}
              </span>
              <span className="pwd" title="Сложность пароля">
                <StrengthBar score={r.passwordScore} />
                <small className="muted">взломают {crackPhrase(r.passwordGuessesLog10)}</small>
              </span>
              <span className="part" title="За скорость">
                <small className="muted">скорость</small> {r.speedDonkeys}
              </span>
              <span className="part" title="За пароль">
                <small className="muted">пароль</small> {r.passwordDonkeys}
              </span>
            </div>
          </li>
        ))}
      </ol>
    </div>
  )
}
