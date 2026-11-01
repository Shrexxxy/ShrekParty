import { useCallback, useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { ApiError, api } from '../api/http'
import { useSnapshot } from '../api/live'
import { ASSETS } from '../assets'
import { useMe } from '../api/me'
import type { MeDto } from '../api/types'
import { OnionGame } from '../register/OnionGame'
import { FakeCaptcha } from '../register/FakeCaptcha'
import { registerView } from '../register/registerView'
import { StrongPasswordStep } from '../register/StrongPasswordStep'

type Step = 'form' | 'captcha' | 'game' | 'strong'

interface Progress {
  step: Step
  attemptId: string | null
}

const STORAGE_KEY = 'shrek.registration'

// Прогресс переживает перезагрузку страницы: иначе человек упрётся в «логин занят».
function loadProgress(): Progress {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY)
    if (raw) {
      return JSON.parse(raw) as Progress
    }
  } catch {
    // Приватный режим без storage — просто начинаем сначала.
  }
  return { step: 'form', attemptId: null }
}

function saveProgress(progress: Progress | null) {
  try {
    if (progress) {
      sessionStorage.setItem(STORAGE_KEY, JSON.stringify(progress))
    } else {
      sessionStorage.removeItem(STORAGE_KEY)
    }
  } catch {
    // См. loadProgress.
  }
}

function startError(e: unknown): string {
  if (e instanceof ApiError) {
    switch (e.status) {
      case 409:
        return 'Этот логин уже занят'
      case 400:
        return 'Логин — от 3 до 20 букв, цифр или _, пароль не пустой'
      case 403:
        return 'Регистрация ещё закрыта'
    }
  }
  return 'Что-то сломалось, попробуй ещё раз'
}

export function RegisterPage() {
  const snapshot = useSnapshot()
  const { setMe } = useMe()
  const [progress, setProgressState] = useState<Progress>(loadProgress)
  const [login, setLogin] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const setProgress = useCallback((next: Progress) => {
    saveProgress(next)
    setProgressState(next)
  }, [])

  const state = snapshot?.registration.state
  const view = registerView(state)

  async function start(e: FormEvent) {
    e.preventDefault()
    setError(null)
    setBusy(true)
    try {
      const { attemptId } = await api<{ attemptId: string }>('/api/registration/start', {
        method: 'POST',
        body: { login, password },
      })
      setProgress({ step: 'captcha', attemptId })
    } catch (err) {
      setError(startError(err))
    } finally {
      setBusy(false)
    }
  }

  async function complete(newPassword: string) {
    setError(null)
    setBusy(true)
    try {
      const me = await api<MeDto>('/api/registration/complete', {
        method: 'POST',
        body: { attemptId: progress.attemptId, newPassword },
      })
      saveProgress(null)
      setMe(me)
    } catch (err) {
      if (err instanceof ApiError && err.code === 'same') {
        setError('Это тот же пароль 🙂 Придумай новый')
      } else if (err instanceof ApiError && (err.status === 404 || err.status === 409)) {
        // Попытку стёрли (сброс) или она уже завершена — начинаем заново.
        setProgress({ step: 'form', attemptId: null })
        setError('Регистрацию нужно пройти заново')
      } else {
        setError('Что-то сломалось, попробуй ещё раз')
      }
    } finally {
      setBusy(false)
    }
  }

  const onGameDone = useCallback(() => setProgress({ ...loadProgress(), step: 'strong' }), [setProgress])

  return (
    <main className="page narrow register-page">
      <div className="register-backdrop" style={{ backgroundImage: `url(${ASSETS.ui.registerBackground})` }} aria-hidden />
      <div className="bunting" aria-hidden />
      <h1>Регистрация</h1>

      {progress.step === 'form' && (
        <>
          {view.connecting && <p className="muted">Подключаюсь…</p>}
          {view.waiting && (
            <div className="card waiting">
              <div className="waiting-title">Регистрация скоро откроется</div>
              <div className="waiting-count">
                Ждут: <b>{snapshot?.registration.waitingCount ?? '…'}</b>
              </div>
            </div>
          )}
          {state === 'Open' && <div className="card open-banner">Регистрация открыта! Быстрее!</div>}
          {view.form && (
            <form className="ticket form" onSubmit={start}>
              <fieldset disabled={busy}>
                <label>
                  Логин
                  <input value={login} onChange={(e) => setLogin(e.target.value)} autoComplete="username" required />
                </label>
                <label>
                  Пароль
                  <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="new-password" required />
                </label>
                {error && <p className="error">{error}</p>}
                <button type="submit" className="primary">
                  РЕГИСТРАЦИЯ
                </button>
              </fieldset>
            </form>
          )}
          {view.loginLink && (
            <p className="muted">
              Уже есть аккаунт? <Link to="/login">Войти</Link>
            </p>
          )}
        </>
      )}

      {progress.step === 'captcha' && <FakeCaptcha onStart={() => setProgress({ ...progress, step: 'game' })} />}

      {progress.step === 'game' && <OnionGame onDone={onGameDone} />}

      {progress.step === 'strong' && <StrongPasswordStep error={error} busy={busy} onSubmit={complete} />}
    </main>
  )
}
