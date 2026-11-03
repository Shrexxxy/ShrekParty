import { useEffect, useRef, useState, type FormEvent } from 'react'
import { api } from '../api/http'
import { clockOffset } from '../api/live'
import { errorText } from '../admin/errors'
import { Avatar } from '../components/Avatar'
import { cachedSrc } from '../tv/quizCache'
import { formatClock } from './countdown'
import type { QuizDetails, QuizFinalist } from './details'
import { REVEAL_STEP_MS, revealedCount } from './layer3'
import { canPress, cellLabel, MS_PER_CHAR, pressTime, rouletteFrames, typedChars } from './quiz'
import { useCountdown } from './useCountdown'
import { Emoji } from '../components/Emoji'

const ROULETTE_FRAMES = 30
const ROULETTE_MS = 5000

/** Логин по id из таблицы счёта. */
function loginOf(details: QuizDetails, userId: string | null): string {
  return details.scores.find((s) => s.userId === userId)?.login ?? '—'
}

/** Текст вопроса «печатной машинкой» по часам сервера — одинаково на ТВ и телефонах. */
function Typed({ text, startedAt }: { text: string; startedAt: string }) {
  const [now, setNow] = useState(() => Date.now())
  const shown = typedChars(text, startedAt, now + clockOffset())
  const done = shown >= text.length

  // Допечатали — больше не перерисовываем (на ТВ в это время может играть видео).
  useEffect(() => {
    if (done) {
      return
    }
    const timer = setInterval(() => setNow(Date.now()), MS_PER_CHAR)
    return () => clearInterval(timer)
  }, [done])

  return (
    <p className="quiz-text">
      {text.slice(0, shown)}
      <span className="quiz-text-rest">{text.slice(shown)}</span>
    </p>
  )
}

/** Обратный отсчёт до дедлайна фазы (если он есть). */
function Clock({ deadline, serverTime }: { deadline: string; serverTime: string }) {
  return <b className="quiz-clock">{formatClock(useCountdown(deadline, serverTime))}</b>
}

/** Табло текущего раунда; клетки кликаются только у того, кто выбирает. */
export function QuizBoard({ details, onChoose }: { details: QuizDetails; onChoose?: (questionId: string, label: string) => void }) {
  return (
    <div className="quiz-board">
      {details.board.map((theme) => (
        <div key={theme.themeId} className="quiz-row">
          <div className="quiz-theme">{theme.name}</div>
          {theme.cells.map((cell) => (
            <button
              key={cell.questionId}
              className="quiz-cell"
              disabled={cell.played || !onChoose}
              onClick={() => onChoose?.(cell.questionId, cellLabel(theme.name, cell.price))}
            >
              {cell.played ? '' : cell.price}
            </button>
          ))}
        </div>
      ))}
    </div>
  )
}

/** Счёт всех игроков. */
function QuizScores({ details, myId }: { details: QuizDetails; myId?: string }) {
  return (
    <div className="quiz-scores">
      {details.scores.map((s) => (
        <span key={s.userId} className={['quiz-score', s.userId === myId && 'mine', details.phase === 'Answering' && s.userId === details.answererId && 'answering'].filter(Boolean).join(' ')}>
          <Avatar url={s.avatarUrl} login={s.login} size={28} />
          {s.login} <b>{s.score}</b>
        </span>
      ))}
    </div>
  )
}

/**
 * Медиа вопроса на ТВ: файл из заранее скачанного кэша; команды «▶ / ⏸» именинника — по номеру.
 */
export function QuizMedia({ details }: { details: QuizDetails }) {
  const question = details.question
  const [src, setSrc] = useState<string | null>(null)
  const player = useRef<HTMLVideoElement & HTMLAudioElement>(null)
  const lastSeq = useRef(0)

  useEffect(() => {
    if (!question?.mediaUrl) {
      return
    }

    let objectUrl: string | null = null
    let cancelled = false
    void cachedSrc(question.mediaUrl).then((resolved) => {
      if (cancelled) {
        if (resolved.startsWith('blob:')) {
          URL.revokeObjectURL(resolved)
        }
        return
      }
      objectUrl = resolved.startsWith('blob:') ? resolved : null
      setSrc(resolved)
    })
    return () => {
      cancelled = true
      setSrc(null)
      if (objectUrl) {
        URL.revokeObjectURL(objectUrl)
      }
    }
  }, [question?.mediaUrl])

  const command = details.mediaCommand
  useEffect(() => {
    const element = player.current
    if (!element || !command || command.seq === lastSeq.current) {
      return
    }

    lastSeq.current = command.seq
    if (command.action === 'Play') {
      element.currentTime = 0
      void element.play().catch(() => undefined)
    } else {
      element.pause()
    }
  }, [command, src])

  if (!question?.mediaType || !src) {
    return null
  }

  if (question.mediaType === 'Image') {
    return <img className="quiz-media" src={src} alt="" />
  }

  // Новый вопрос — играет сразу (звук на ТВ включён кнопкой при загрузке страницы).
  return question.mediaType === 'Video' ? (
    <video ref={player} className="quiz-media" src={src} autoPlay playsInline />
  ) : (
    <audio ref={player} src={src} autoPlay />
  )
}

/** ТВ: рулетка с аватарками, останавливается на выбранном сервером. */
function Roulette({ details }: { details: QuizDetails }) {
  const [frame, setFrame] = useState(0)
  const frames = rouletteFrames(
    details.scores.map((s) => s.userId),
    details.chooserId ?? '',
    ROULETTE_FRAMES,
  )

  useEffect(() => {
    const timer = setInterval(() => setFrame((f) => Math.min(f + 1, ROULETTE_FRAMES - 1)), ROULETTE_MS / ROULETTE_FRAMES)
    return () => clearInterval(timer)
  }, [])

  const current = details.scores.find((s) => s.userId === frames[Math.min(frame, frames.length - 1)])
  return (
    <div className="quiz-roulette">
      <div className="muted">Кто выбирает первым?</div>
      {current && (
        <div className="quiz-roulette-pick">
          <Avatar url={current.avatarUrl} login={current.login} size={120} />
          <div>{current.login}</div>
        </div>
      )}
    </div>
  )
}

/** Финалисты при раскрытии: по одному, от меньшего счёта к большему. */
function FinalReveal({ finalists }: { finalists: QuizFinalist[] }) {
  const [startedAt] = useState(() => Date.now())
  const [now, setNow] = useState(startedAt)

  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 250)
    return () => clearInterval(timer)
  }, [])

  const shown = Math.min(finalists.length, revealedCount(now - startedAt, finalists.length, REVEAL_STEP_MS * 2))
  return (
    <div className="quiz-final-reveal">
      {finalists.length === 0 && <p className="muted">В финал никто не прошёл</p>}
      {finalists.slice(0, shown).map((f) => (
        <div key={f.userId} className={`quiz-finalist ${f.correct ? 'right' : 'wrong'}`}>
          <Avatar url={f.avatarUrl} login={f.login} size={40} />
          <span>{f.login}</span>
          <span className="quiz-final-answer">«{f.answer ?? '—'}»</span>
          <b>
            {f.correct ? '+' : '−'}
            {f.bet ?? 0}
          </b>
          <span>
            {f.scoreBefore} → <b>{f.scoreBefore + (f.correct ? 1 : -1) * (f.bet ?? 0)}</b>
          </span>
        </div>
      ))}
    </div>
  )
}

/** Телефон в финале: ставка и ответ. */
function FinalPhone({ eventId, details, serverTime, myId }: { eventId: string; details: QuizDetails; serverTime: string; myId?: string }) {
  const final = details.final!
  const myScore = details.scores.find((s) => s.userId === myId)?.score ?? 0
  const [value, setValue] = useState('')
  const [error, setError] = useState<string | null>(null)

  async function send(e: FormEvent, path: string, body: unknown) {
    e.preventDefault()
    setError(null)
    try {
      await api(path, { method: 'POST', body })
    } catch (err) {
      setError(errorText(err))
    }
  }

  if (!final.iAmFinalist) {
    return <p className="muted">В финале играют те, у кого счёт больше нуля. Болей за них!</p>
  }

  if (details.phase === 'FinalBetting') {
    return (
      <form className="form" onSubmit={(e) => void send(e, `/api/quiz/${eventId}/bet`, { amount: Number(value) })}>
        {details.deadline && <Clock deadline={details.deadline} serverTime={serverTime} />}
        <label>
          Ставка (от 1 до {myScore})
          <input type="number" inputMode="numeric" min={1} max={myScore} value={value} onChange={(e) => setValue(e.target.value)} />
        </label>
        <button className="primary" disabled={value === ''}>
          {final.myBet === null ? 'Поставить' : `Изменить (сейчас ${final.myBet})`}
        </button>
        {error && <p className="error">{error}</p>}
      </form>
    )
  }

  if (details.phase === 'FinalQuestion') {
    return (
      <form className="form" onSubmit={(e) => void send(e, `/api/quiz/${eventId}/final-answer`, { text: value })}>
        {details.deadline && <Clock deadline={details.deadline} serverTime={serverTime} />}
        {details.question && <p className="quiz-text">{details.question.text}</p>}
        <label>
          Твой ответ
          <input value={value} onChange={(e) => setValue(e.target.value)} />
        </label>
        <button className="primary" disabled={value.trim() === ''}>
          {final.myAnswer === null ? 'Ответить' : 'Изменить ответ'}
        </button>
        {final.myAnswer !== null && <p className="muted">Твой ответ: «{final.myAnswer}»</p>}
        {error && <p className="error">{error}</p>}
      </form>
    )
  }

  return <p className="muted">{details.phase === 'FinalJudging' ? 'Именинник проверяет ответы…' : 'Смотри на ТВ!'}</p>
}

/** Большая кнопка: момент тапа уходит на сервер по его часам. */
function PressButton({ eventId, details, myId }: { eventId: string; details: QuizDetails; myId?: string }) {
  const [pressedFor, setPressedFor] = useState<string | null>(null)
  const key = `${details.question?.id}-${details.wrong.length}`
  const allowed = canPress(details, myId)
  const pressed = pressedFor === key

  function press() {
    if (!allowed || pressed) {
      return
    }

    setPressedFor(key)
    void api(`/api/quiz/${eventId}/press`, { method: 'POST', body: { clientTime: pressTime(Date.now(), clockOffset()) } }).catch(() => undefined)
  }

  return (
    <button className={`quiz-press ${pressed ? 'pressed' : ''}`} disabled={!allowed} onPointerDown={press}>
      {!allowed ? (details.wrong.includes(myId ?? '') ? 'Ты уже ответил' : '…') : pressed ? 'Нажал!' : 'ЖМУ!'}
    </button>
  )
}

/** Своя игра: ТВ — табло и вопрос, телефон — выбор клетки, кнопка и финал. */
export function QuizView({ eventId, details, serverTime, myId, tv }: { eventId: string; details: QuizDetails; serverTime: string; myId?: string; tv?: boolean }) {
  const [error, setError] = useState<string | null>(null)
  const phase = details.phase
  const isFinal = phase.startsWith('Final')

  async function choose(questionId: string, label: string) {
    if (!confirm(`Точно ${label}?`)) {
      return
    }
    setError(null)
    try {
      await api(`/api/quiz/${eventId}/choose`, { method: 'POST', body: { questionId } })
    } catch (err) {
      setError(errorText(err))
    }
  }

  const question = details.question
  const head = question && (phase === 'Reading' || phase === 'Answering' || phase === 'Closed' || phase === 'Done') && (
    <div className="quiz-head">
      {question.themeName} · {question.price}
    </div>
  )

  if (tv) {
    return (
      <div className="quiz tv">
        {phase === 'Roulette' && <Roulette details={details} />}
        {phase === 'Choosing' && (
          <>
            <QuizBoard details={details} />
            <div className="quiz-who">Выбирает {loginOf(details, details.chooserId)}</div>
          </>
        )}
        {(phase === 'Reading' || phase === 'Answering') && question && details.readingStartedAt && (
          <>
            {head}
            <Typed text={question.text} startedAt={details.readingStartedAt} />
            <QuizMedia details={details} />
            {phase === 'Answering' && (
              <div className="quiz-who">
                Отвечает {loginOf(details, details.answererId)} {details.deadline && <Clock deadline={details.deadline} serverTime={serverTime} />}
              </div>
            )}
          </>
        )}
        {(phase === 'Closed' || phase === 'Done') && (
          <>
            {head}
            {details.answer && <div className="quiz-answer">Ответ: {details.answer}</div>}
            {phase === 'Closed' ? (
              <>
                <QuizBoard details={details} />
                <div className="quiz-who">Выбирает {loginOf(details, details.chooserId)}</div>
              </>
            ) : (
              <div className="quiz-who">Раунды сыграны — впереди финал!</div>
            )}
          </>
        )}
        {isFinal && details.final && (
          <div className="quiz-final">
            <div className="quiz-head">Финал: {details.final.themeName}</div>
            {details.deadline && phase !== 'FinalReveal' && <Clock deadline={details.deadline} serverTime={serverTime} />}
            {phase === 'FinalBetting' && <p>Финалисты делают ставки: {details.final.finalists.map((f) => f.login).join(', ')}</p>}
            {phase === 'FinalQuestion' && question && <p className="quiz-text">{question.text}</p>}
            {phase === 'FinalJudging' && <p>Именинник проверяет ответы…</p>}
            {phase === 'FinalReveal' && (
              <>
                {details.answer && <div className="quiz-answer">Ответ: {details.answer}</div>}
                <FinalReveal finalists={details.final.finalists} />
              </>
            )}
          </div>
        )}
        <QuizScores details={details} />
      </div>
    )
  }

  const myChoice = details.chooserId === myId
  return (
    <div className="quiz">
      {phase === 'Roulette' && <p className="muted">Крутим рулетку — смотри на ТВ!</p>}
      {(phase === 'Choosing' || phase === 'Closed') && (
        <>
          {phase === 'Closed' && details.answer && <div className="quiz-answer">Ответ: {details.answer}</div>}
          <div className="quiz-who">{myChoice ? 'Твой выбор!' : `Выбирает ${loginOf(details, details.chooserId)}`}</div>
          <QuizBoard details={details} onChoose={myChoice ? (id, label) => void choose(id, label) : undefined} />
        </>
      )}
      {(phase === 'Reading' || phase === 'Answering') && question && details.readingStartedAt && (
        <>
          {head}
          <Typed text={question.text} startedAt={details.readingStartedAt} />
          {question.mediaType === 'Image' && question.mediaUrl && <img className="quiz-media" src={question.mediaUrl} alt="" />}
          {(question.mediaType === 'Audio' || question.mediaType === 'Video') && <p className="muted"><Emoji e={question.mediaType === 'Video' ? '🎬' : '🎵'} /> Смотри и слушай на ТВ</p>}
          {phase === 'Reading' ? (
            <PressButton eventId={eventId} details={details} myId={myId} />
          ) : (
            <div className="quiz-who">
              {details.answererId === myId ? 'Отвечай вслух!' : `Отвечает ${loginOf(details, details.answererId)}`}{' '}
              {details.deadline && <Clock deadline={details.deadline} serverTime={serverTime} />}
            </div>
          )}
        </>
      )}
      {phase === 'Done' && <p className="muted">Раунды сыграны — скоро финал!</p>}
      {isFinal && details.final && (
        <>
          <div className="quiz-head">Финал: {details.final.themeName}</div>
          {phase === 'FinalReveal' ? <p className="muted">Смотри раскрытие на ТВ!</p> : <FinalPhone key={phase} eventId={eventId} details={details} serverTime={serverTime} myId={myId} />}
        </>
      )}
      {error && <p className="error">{error}</p>}
      <p className="quiz-my-score">Твой счёт: {details.scores.find((s) => s.userId === myId)?.score ?? 0}</p>
    </div>
  )
}
