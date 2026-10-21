import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router'
import { ApiError, api } from '../api/http'
import { homeOf, useMe } from '../api/me'
import type { MeDto } from '../api/types'

export function LoginPage() {
  const { setMe } = useMe()
  const navigate = useNavigate()
  const [login, setLogin] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)

  async function submit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    try {
      const me = await api<MeDto>('/api/auth/login', { method: 'POST', body: { login, password } })
      setMe(me)
      navigate(homeOf(me.role), { replace: true })
    } catch (err) {
      setError(err instanceof ApiError && err.status === 401 ? 'Неверный логин или пароль' : 'Что-то сломалось, попробуй ещё раз')
    }
  }

  return (
    <main className="page narrow">
      <h1>Вход</h1>
      <form className="card form" onSubmit={submit}>
        <label>
          Логин
          <input value={login} onChange={(e) => setLogin(e.target.value)} autoComplete="username" required />
        </label>
        <label>
          Пароль
          <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" required />
        </label>
        {error && <p className="error">{error}</p>}
        <button type="submit" className="primary">
          Войти
        </button>
      </form>
      <p className="muted">
        Нет аккаунта? <Link to="/register">Регистрация</Link>
      </p>
    </main>
  )
}
