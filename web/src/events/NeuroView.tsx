import { api } from '../api/http'
import { formatClock } from './countdown'
import type { NeuroDetails } from './details'
import { useCountdown } from './useCountdown'

/** Друг + образ = что получилось — на раскрытии ТВ и в карусели. */
export function NeuroTriple({ friendUrl, sourceUrl, resultUrl }: { friendUrl: string; sourceUrl: string; resultUrl: string }) {
  return (
    <div className="neuro-triple">
      <img src={friendUrl} alt="" />
      <span className="neuro-sign">+</span>
      <img src={sourceUrl} alt="" />
      <span className="neuro-sign">=</span>
      <img src={resultUrl} alt="" />
    </div>
  )
}

/** Группа кнопок выбора: кто или под кого. */
function Choice({ title, options, picked, onPick }: { title: string; options: string[]; picked: string | null; onPick: (value: string) => void }) {
  return (
    <section className="neuro-choice">
      <h3>{title}</h3>
      <div className="neuro-options">
        {options.map((o) => (
          <button key={o} className={o === picked ? 'on' : ''} onClick={() => onPick(o)}>
            {o}
          </button>
        ))}
      </div>
    </section>
  )
}

/** Угадай по нейрофото: маскировка, таймер, на телефоне — кто и под кого; после — раскрытие. */
export function NeuroView({ eventId, details, serverTime, tv }: { eventId: string; details: NeuroDetails; serverTime: string; tv?: boolean }) {
  const left = useCountdown(details.deadline, serverTime)
  const guessing = details.phase === 'Guessing'

  function guess(body: { person: string } | { character: string }) {
    void api(`/api/neuro/${eventId}/guess`, { method: 'POST', body }).catch(() => undefined)
  }

  return (
    <div className={tv ? 'neuro tv' : 'neuro'}>
      <div className="neuro-head">
        <span>
          Фото {details.index + 1} из {details.total}
        </span>
        {guessing && <b className="neuro-clock">{formatClock(left)}</b>}
      </div>

      {tv && !guessing && details.friendUrl && details.sourceUrl ? (
        <NeuroTriple friendUrl={details.friendUrl} sourceUrl={details.sourceUrl} resultUrl={details.photoUrl} />
      ) : (
        <img className="neuro-photo" src={details.photoUrl} alt="" />
      )}

      {guessing ? (
        tv ? (
          <div className="muted">Ответили: {details.guessed}</div>
        ) : (
          <>
            <Choice title="Кто это?" options={details.people} picked={details.myPerson} onPick={(person) => guess({ person })} />
            <Choice title="В образе кого?" options={details.characters} picked={details.myCharacter} onPick={(character) => guess({ character })} />
            <small className="muted">Человек +2, персонаж +2, оба сразу +5</small>
          </>
        )
      ) : (
        <div className="neuro-reveal">
          <div className="neuro-subject">
            Это {details.answer} в образе: {details.character}!
          </div>
          {!tv && (
            <div className="neuro-verdict">
              <span>{details.myPerson === details.answer ? 'Человек — угадал' : 'Человек — мимо'}</span>
              <span>{details.myCharacter === details.character ? 'Персонаж — угадал' : 'Персонаж — мимо'}</span>
              <b>+{details.myPoints ?? 0} осликов</b>
            </div>
          )}
          {tv && <div className="muted">{details.correct.length > 0 ? `Угадали, кто это: ${details.correct.join(', ')}` : 'Никто не угадал, кто это'}</div>}
        </div>
      )}
    </div>
  )
}
