import { useCallback, useEffect, useState, type ChangeEvent, type FormEvent } from 'react'
import { Link } from 'react-router'
import { api, ApiError } from '../api/http'
import { errorText } from '../admin/errors'
import { byRounds, FINAL_ROUND, IMPORT_EXAMPLE, nextRound, type QuizQuestionDto, type QuizThemeDto } from './editor'
import { Emoji, EmojiText } from '../components/Emoji'

const MEDIA_ICONS = { Image: '🖼', Audio: '🎵', Video: '🎬' } as const

/** Загрузка файла с процентом: видео до 100 МБ грузится заметно долго. */
function uploadWithProgress(url: string, file: File, onProgress: (percent: number) => void): Promise<void> {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest()
    xhr.open('POST', url)
    xhr.withCredentials = true
    xhr.upload.onprogress = (e) => e.lengthComputable && onProgress(Math.round((e.loaded / e.total) * 100))
    xhr.onload = () => {
      if (xhr.status >= 200 && xhr.status < 300) {
        resolve()
        return
      }
      let body: unknown
      try {
        body = JSON.parse(xhr.responseText)
      } catch {
        body = undefined
      }
      reject(new ApiError(xhr.status, body))
    }
    xhr.onerror = () => reject(new ApiError(0, undefined))
    const form = new FormData()
    form.append('file', file)
    xhr.send(form)
  })
}

/** Общие действия редактора: запрос, перечитать дерево, показать ошибку. */
function useEditor() {
  const [themes, setThemes] = useState<QuizThemeDto[]>([])
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(
    () =>
      api<{ themes: QuizThemeDto[] }>('/api/admin/quiz')
        .then((tree) => setThemes(tree.themes))
        .catch((e) => setError(errorText(e))),
    [],
  )

  useEffect(() => {
    void load()
  }, [load])

  async function act(action: () => Promise<unknown>) {
    setError(null)
    try {
      await action()
    } catch (e) {
      setError(errorText(e))
    }
    await load()
  }

  return { themes, error, act }
}

type Act = (action: () => Promise<unknown>) => Promise<void>

/** Вопрос: текст, ответ, цена сохраняются при уходе с поля; медиа — загрузкой файла. */
function QuestionEditor({ question, isFinal, act }: { question: QuizQuestionDto; isFinal: boolean; act: Act }) {
  const [text, setText] = useState(question.text)
  const [answer, setAnswer] = useState(question.answer)
  const [price, setPrice] = useState(String(question.price))
  const [progress, setProgress] = useState<number | null>(null)

  function save() {
    if (text === question.text && answer === question.answer && Number(price) === question.price) {
      return
    }
    void act(() => api(`/api/admin/quiz/questions/${question.id}`, { method: 'PUT', body: { text, answer, price: Number(price) || null } }))
  }

  function upload(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    e.target.value = ''
    if (file) {
      setProgress(0)
      void act(() => uploadWithProgress(`/api/admin/quiz/questions/${question.id}/media`, file, setProgress)).finally(() => setProgress(null))
    }
  }

  return (
    <div className="quiz-edit-question">
      {!isFinal && (
        <input className="quiz-edit-price" type="number" inputMode="numeric" value={price} onChange={(e) => setPrice(e.target.value)} onBlur={save} />
      )}
      <textarea value={text} onChange={(e) => setText(e.target.value)} onBlur={save} placeholder="Вопрос" rows={2} />
      <input value={answer} onChange={(e) => setAnswer(e.target.value)} onBlur={save} placeholder="Ответ" />
      <div className="quiz-edit-media">
        {question.mediaType && question.mediaUrl && (
          <>
            <a href={question.mediaUrl} target="_blank" rel="noreferrer">
              <Emoji e={MEDIA_ICONS[question.mediaType]} /> файл
            </a>
            <button onClick={() => void act(() => api(`/api/admin/quiz/questions/${question.id}/media`, { method: 'DELETE' }))}><Emoji e="✖" /></button>
          </>
        )}
        <label className="button-like">
          <EmojiText text={progress !== null ? `⏳ ${progress}%` : question.mediaType ? 'Заменить' : '＋ Медиа'} />
          <input type="file" accept="image/*,audio/*,video/*" hidden disabled={progress !== null} onChange={upload} />
        </label>
        <button onClick={() => confirm('Удалить вопрос?') && void act(() => api(`/api/admin/quiz/questions/${question.id}`, { method: 'DELETE' }))}><Emoji e="🗑" /></button>
      </div>
    </div>
  )
}

/** Тема: название, вопросы, добавление вопроса. */
function ThemeEditor({ theme, act }: { theme: QuizThemeDto; act: Act }) {
  const [name, setName] = useState(theme.name)
  const [text, setText] = useState('')
  const [answer, setAnswer] = useState('')
  const isFinal = theme.round === FINAL_ROUND

  function add(e: FormEvent) {
    e.preventDefault()
    void act(async () => {
      await api(`/api/admin/quiz/themes/${theme.id}/questions`, { method: 'POST', body: { text, answer } })
      setText('')
      setAnswer('')
    })
  }

  return (
    <section className="card quiz-edit-theme">
      <div className="quiz-edit-theme-head">
        <input
          value={name}
          onChange={(e) => setName(e.target.value)}
          onBlur={() => name !== theme.name && void act(() => api(`/api/admin/quiz/themes/${theme.id}`, { method: 'PUT', body: { name } }))}
        />
        <button onClick={() => confirm(`Удалить тему «${theme.name}» со всеми вопросами?`) && void act(() => api(`/api/admin/quiz/themes/${theme.id}`, { method: 'DELETE' }))}>
          <Emoji e="🗑" />
        </button>
      </div>
      {theme.questions.map((q) => (
        <QuestionEditor key={q.id} question={q} isFinal={isFinal} act={act} />
      ))}
      {(!isFinal || theme.questions.length === 0) && (
        <form className="quiz-edit-add" onSubmit={add}>
          <input value={text} onChange={(e) => setText(e.target.value)} placeholder="Новый вопрос" />
          <input value={answer} onChange={(e) => setAnswer(e.target.value)} placeholder="Ответ" />
          <button disabled={!text.trim() || !answer.trim()}>＋</button>
        </form>
      )}
    </section>
  )
}

/** Импорт и экспорт JSON. */
function ImportExport({ act }: { act: Act }) {
  const [json, setJson] = useState('')

  async function exportJson() {
    const data = await api<unknown>('/api/admin/quiz/export')
    const link = document.createElement('a')
    link.href = URL.createObjectURL(new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' }))
    link.download = 'quiz.json'
    link.click()
    URL.revokeObjectURL(link.href)
  }

  function importJson() {
    let parsed: unknown
    try {
      parsed = JSON.parse(json)
    } catch {
      alert('Это не JSON — проверь скобки и кавычки')
      return
    }
    if (confirm('Импорт заменит ВСЕ вопросы (и удалит их медиа). Продолжить?')) {
      void act(async () => {
        await api('/api/admin/quiz/import', { method: 'POST', body: parsed })
        setJson('')
      })
    }
  }

  return (
    <details className="card">
      <summary>Импорт / экспорт JSON</summary>
      <button onClick={() => void exportJson()}><Emoji e="⬇" /> Скачать JSON</button>
      <p className="muted">Импорт заменяет всё. Медиа в JSON нет — прикрепляй файлы в редакторе. Цена необязательна. Раунд 0 — финал.</p>
      <pre className="quiz-edit-example">{IMPORT_EXAMPLE}</pre>
      <textarea value={json} onChange={(e) => setJson(e.target.value)} rows={8} placeholder="Вставь JSON" />
      <button className="primary" disabled={!json.trim()} onClick={importJson}>
        Импортировать
      </button>
    </details>
  )
}

/** Редактор Своей игры: раунды → темы → вопросы, финал, импорт/экспорт. */
export function QuizEditorPage() {
  const { themes, error, act } = useEditor()
  const { rounds, final } = byRounds(themes)
  const total = themes.reduce((sum, t) => sum + t.questions.length, 0)

  const addTheme = (round: number) =>
    void act(() => api('/api/admin/quiz/themes', { method: 'POST', body: { round, name: round === FINAL_ROUND ? 'Финал' : 'Новая тема' } }))

  return (
    <main className="page quiz-editor">
      <header className="admin-header">
        <h1>Своя игра</h1>
        <Link to="/admin">← Админка</Link>
      </header>
      <p className="muted">
        {rounds.length} раунд(а), {total} вопрос(ов). Всё сохраняется само при уходе с поля.
      </p>
      {error && <p className="error">{error}</p>}

      {rounds.map((r) => (
        <section key={r.round} className="quiz-edit-round">
          <h2>Раунд {r.round}</h2>
          {r.themes.map((t) => (
            <ThemeEditor key={t.id} theme={t} act={act} />
          ))}
          <button onClick={() => addTheme(r.round)}>＋ Тема в раунд {r.round}</button>
        </section>
      ))}
      <button onClick={() => addTheme(nextRound(themes))}>＋ Новый раунд</button>

      <section className="quiz-edit-round">
        <h2>Финал</h2>
        {final ? <ThemeEditor key={final.id} theme={final} act={act} /> : <button onClick={() => addTheme(FINAL_ROUND)}>＋ Финал</button>}
      </section>

      <ImportExport act={act} />
    </main>
  )
}
