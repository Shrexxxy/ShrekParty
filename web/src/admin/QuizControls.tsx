import type { ActiveEventDto } from '../api/types'
import { Avatar } from '../components/Avatar'
import { formatClock } from '../events/countdown'
import type { QuizDetails } from '../events/details'
import { QuizBoard } from '../events/QuizView'
import { useCountdown } from '../events/useCountdown'
import { useAdminAction } from './useAdminAction'
import { Emoji } from '../components/Emoji'

const PHASES: Record<QuizDetails['phase'], string> = {
  Roulette: '<Emoji e="🎰" /> Рулетка',
  Choosing: '<Emoji e="👆" /> Выбор вопроса',
  Reading: '<Emoji e="📖" /> Читаем',
  Answering: '<Emoji e="🗣" /> Отвечают',
  Closed: '<Emoji e="✅" /> Вопрос закрыт',
  Done: '<Emoji e="🏁" /> Раунды сыграны',
  FinalBetting: '<Emoji e="💰" /> Финал: ставки',
  FinalQuestion: '<Emoji e="✍️" /> Финал: ответ',
  FinalJudging: '<Emoji e="⚖️" /> Финал: проверка',
  FinalReveal: '<Emoji e="🎉" /> Финал: раскрытие',
}

function DeadlineClock({ deadline, serverTime }: { deadline: string; serverTime: string }) {
  return <b>{formatClock(useCountdown(deadline, serverTime))}</b>
}

/** Пульт Своей игры: вопрос с ответом, вердикт, медиа, выбор за игрока, финал. */
export function QuizControls({ event, details, serverTime }: { event: ActiveEventDto; details: QuizDetails; serverTime: string }) {
  const { run, error, busy } = useAdminAction()
  const base = `/api/admin/quiz/${event.id}`
  const login = (id: string | null) => details.scores.find((s) => s.userId === id)?.login ?? '—'
  const question = details.question
  const phase = details.phase
  const hasPlayer = question?.mediaType === 'Audio' || question?.mediaType === 'Video'

  return (
    <div className="controls quiz-controls">
      <p>
        {PHASES[phase]} · раунд {details.round} из {details.rounds.length}
        {details.deadline && phase !== 'Choosing' && (
          <>
            {' · '}
            <DeadlineClock deadline={details.deadline} serverTime={serverTime} />
          </>
        )}
      </p>

      {question && (
        <div className="card quiz-admin-question">
          <small className="muted">
            {question.themeName} · {question.price}
          </small>
          <p>{question.text}</p>
          {details.answer && (
            <p className="quiz-admin-answer">
              Ответ: <b>{details.answer}</b>
            </p>
          )}
          {hasPlayer && (phase === 'Reading' || phase === 'Answering') && (
            <div className="bet-sides">
              <button disabled={busy} onClick={() => void run(`${base}/media`, { method: 'POST', body: { action: 'Play' } })}>
                <Emoji e="▶" /> Повторить
              </button>
              <button disabled={busy} onClick={() => void run(`${base}/media`, { method: 'POST', body: { action: 'Pause' } })}>
                <Emoji e="⏸" /> Пауза
              </button>
            </div>
          )}
        </div>
      )}

      {(phase === 'Choosing' || phase === 'Closed') && (
        <>
          <p>Выбирает {login(details.chooserId)} — или выбери за него:</p>
          <QuizBoard
            details={details}
            onChoose={(id, label) => confirm(`Выбрать ${label} за ${login(details.chooserId)}?`) && void run(`${base}/choose`, { method: 'POST', body: { questionId: id } })}
          />
          <button disabled={busy} onClick={() => confirm('Закончить раунд досрочно?') && void run(`${base}/next-round`)}>
            <Emoji e="⏭" /> Следующий раунд
          </button>
        </>
      )}

      {phase === 'Reading' && (
        <>
          {details.wrong.length > 0 && <p className="muted">Ошиблись: {details.wrong.map(login).join(', ')}</p>}
          <button disabled={busy} onClick={() => void run(`${base}/close`)}>
            <Emoji e="✖" /> Закрыть вопрос
          </button>
        </>
      )}

      {phase === 'Answering' && (
        <>
          <p>
            Отвечает <b>{login(details.answererId)}</b>
          </p>
          <div className="bet-sides">
            <button className="primary" disabled={busy} onClick={() => void run(`${base}/verdict`, { method: 'POST', body: { correct: true } })}>
              <Emoji e="✅" /> Верно
            </button>
            <button disabled={busy} onClick={() => void run(`${base}/verdict`, { method: 'POST', body: { correct: false } })}>
              <Emoji e="❌" /> Неверно
            </button>
          </div>
        </>
      )}

      {phase === 'Done' && (
        <button className="primary" disabled={busy} onClick={() => void run(`${base}/final`)}>
          <Emoji e="🏁" /> Финал
        </button>
      )}

      {details.final && phase !== 'Done' && (
        <div className="quiz-admin-final">
          <p>Финал: {details.final.themeName}</p>
          {details.final.finalists.map((f) => (
            <div key={f.userId} className="control-row">
              <span>
                <Avatar url={f.avatarUrl} login={f.login} size={24} /> {f.login} · {f.scoreBefore} · ставка {f.bet ?? '—'}
                {phase !== 'FinalBetting' && <> · «{f.answer ?? '—'}»</>}
              </span>
              {phase === 'FinalJudging' && (
                <span className="bet-sides">
                  <button
                    className={f.correct === true ? 'primary' : ''}
                    disabled={busy}
                    onClick={() => void run(`${base}/final-verdict`, { method: 'POST', body: { userId: f.userId, correct: true } })}
                  >
                    <Emoji e="✅" />
                  </button>
                  <button
                    className={f.correct === false ? 'primary' : ''}
                    disabled={busy}
                    onClick={() => void run(`${base}/final-verdict`, { method: 'POST', body: { userId: f.userId, correct: false } })}
                  >
                    <Emoji e="❌" />
                  </button>
                </span>
              )}
            </div>
          ))}
          {phase === 'FinalJudging' && (
            <button className="primary" disabled={busy} onClick={() => void run(`${base}/reveal`)}>
              <Emoji e="🎉" /> Раскрыть на ТВ
            </button>
          )}
          {phase === 'FinalReveal' && <p className="muted">Когда раскрытие на ТВ закончится — «<Emoji e="⏹" /> Завершить».</p>}
        </div>
      )}

      <div className="quiz-admin-scores">
        {details.scores.map((s) => (
          <div key={s.userId} className="control-row">
            <span>
              <Avatar url={s.avatarUrl} login={s.login} size={24} /> {s.login}
            </span>
            <b>{s.score}</b>
          </div>
        ))}
      </div>
      {error && <p className="error">{error}</p>}
    </div>
  )
}
