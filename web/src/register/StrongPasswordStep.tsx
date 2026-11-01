import { useState, type FormEvent } from 'react'

/**
 * Фейковая ошибка «пароль слишком простой» и ввод нового. Сложность не показываем и не требуем:
 * увидев шкалу, все бы дотянули до максимума, а так ослики за пароль честно различаются.
 */
export function StrongPasswordStep({ error, busy, onSubmit }: { error: string | null; busy: boolean; onSubmit: (password: string) => void }) {
  const [password, setPassword] = useState('')
  const [visible, setVisible] = useState(false)
  const canSubmit = password.length > 0 && !busy

  function submit(e: FormEvent) {
    e.preventDefault()
    if (canSubmit) {
      onSubmit(password)
    }
  }

  return (
    <form className="card form" onSubmit={submit}>
      <div className="fake-error">
        <b>⚠️ Пароль слишком простой</b>
        <span>Пожалуйста, придумайте сложный пароль</span>
      </div>
      <label>
        Новый пароль
        <div className="password-row">
          <input
            type={visible ? 'text' : 'password'}
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete="new-password"
            autoFocus
          />
          <button type="button" onClick={() => setVisible((v) => !v)} aria-label="Показать пароль">
            {visible ? '🙈' : '👁'}
          </button>
        </div>
      </label>
      <small className="muted">Сложный — длинный, из разных слов, с цифрами и символами</small>
      {error && <p className="error">{error}</p>}
      <button type="submit" className="primary" disabled={!canSubmit}>
        {busy ? 'Проверяю…' : 'Готово'}
      </button>
    </form>
  )
}
