import { useEffect, useState } from 'react'
import { ImageCaptcha } from './ImageCaptcha'

type Phase = 'checkbox' | 'checking' | 'grid' | 'challenge'

/** Фейковая капча: «Я не робот» → «Проверка…» → три противные сетки → предложение помочь Шреку. */
export function FakeCaptcha({ onStart }: { onStart: () => void }) {
  const [phase, setPhase] = useState<Phase>('checkbox')

  useEffect(() => {
    if (phase !== 'checking') {
      return
    }
    const timer = setTimeout(() => setPhase('grid'), 2000)
    return () => clearTimeout(timer)
  }, [phase])

  return (
    <div className="captcha card">
      {(phase === 'checkbox' || phase === 'checking') && (
        <button className="captcha-box" disabled={phase === 'checking'} onClick={() => setPhase('checking')}>
          <span className={phase === 'checking' ? 'captcha-check spinning' : 'captcha-check'} />
          <span>Я не робот</span>
          <span className="captcha-brand">
            OgreCAPTCHA
            <small>Конфиденциальность · Условия</small>
          </span>
        </button>
      )}
      {phase === 'grid' && <ImageCaptcha onDone={() => setPhase('challenge')} />}
      {phase === 'challenge' && (
        <div className="captcha-challenge">
          <p>
            <b>Почти готово! Последняя проверка:</b>
            <br />
            помогите Шреку набрать 224 очка — ловите лук!
          </p>
          <button className="primary" onClick={onStart}>
            Начать проверку
          </button>
        </div>
      )}
    </div>
  )
}
