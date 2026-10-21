import type { ZxcvbnFactory } from '@zxcvbn-ts/core'
import { useEffect, useState, type FormEvent } from 'react'
import { StrengthBar } from '../components/StrengthBar'

/** Минимальная оценка — та же, что требует сервер (он и решает окончательно). */
export const MIN_SCORE = 3

const LABELS = ['Очень слабый', 'Слабый', 'Так себе', 'Сложный', 'Очень сложный']

/** Словари тяжёлые — грузим только на этом шаге. */
async function loadZxcvbn(): Promise<ZxcvbnFactory> {
  const [core, common, en] = await Promise.all([
    import('@zxcvbn-ts/core'),
    import('@zxcvbn-ts/language-common'),
    import('@zxcvbn-ts/language-en'),
  ])
  return new core.ZxcvbnFactory({
    translations: en.translations,
    graphs: common.adjacencyGraphs,
    dictionary: { ...common.dictionary, ...en.dictionary },
  })
}

/** Фейковая ошибка «пароль слишком простой» и ввод нового — обязательно сложного. */
export function StrongPasswordStep({ error, busy, onSubmit }: { error: string | null; busy: boolean; onSubmit: (password: string) => void }) {
  const [zxcvbn, setZxcvbn] = useState<ZxcvbnFactory | null>(null)
  const [password, setPassword] = useState('')
  const [visible, setVisible] = useState(false)

  useEffect(() => {
    loadZxcvbn().then(setZxcvbn).catch(() => undefined)
  }, [])

  const score = zxcvbn && password ? zxcvbn.check(password).score : null
  // Словари не загрузились — пускаем: сервер всё равно проверит.
  const canSubmit = password.length > 0 && !busy && (score === null ? zxcvbn === null : score >= MIN_SCORE)

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
      {score !== null && <StrengthBar score={score} label={LABELS[score]} />}
      {error && <p className="error">{error}</p>}
      <button type="submit" className="primary" disabled={!canSubmit}>
        {busy ? 'Проверяю…' : 'Готово'}
      </button>
    </form>
  )
}
