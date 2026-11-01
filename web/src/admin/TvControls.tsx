import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { api } from '../api/http'
import type { AppSnapshot } from '../api/types'
import { CameraButton } from './CameraButton'
import { useAdminAction } from './useAdminAction'

interface CaptionDto {
  id: string
  text: string
}

interface MemeDto {
  id: string
  url: string
  caption: string | null
}

/** Объявление на весь экран ТВ: текст, сколько держать, необязательно фото. Показали — окно закрывается. */
export function AnnounceForm({ onDone }: { onDone: () => void }) {
  const { run, error, busy } = useAdminAction()
  const [text, setText] = useState('')
  const [seconds, setSeconds] = useState('15')

  function form(extra?: FormData) {
    const data = extra ?? new FormData()
    data.append('text', text)
    data.append('seconds', seconds)
    return data
  }

  async function show(body: FormData) {
    if (await run('/api/admin/tv/message', { method: 'POST', body })) {
      setText('')
      onDone()
    }
  }

  async function submit(e: FormEvent) {
    e.preventDefault()
    await show(form())
  }

  return (
    <form className="form" onSubmit={submit}>
      <label>
        Текст
        <input value={text} onChange={(e) => setText(e.target.value)} placeholder="Торт через 5 минут!" />
      </label>
      <label>
        Сколько секунд держать
        <input type="number" inputMode="numeric" value={seconds} onChange={(e) => setSeconds(e.target.value)} />
      </label>
      <div className="control-row">
        <CameraButton disabled={busy || !text.trim()} onPhoto={(photo) => void show(form(photo))}>
          📸 С фото
        </CameraButton>
        <button type="button" disabled={busy} onClick={() => void run('/api/admin/tv/message', { method: 'DELETE' })}>
          ✖ Убрать с экрана
        </button>
      </div>
      {error && <p className="error">{error}</p>}
      <button type="submit" className="primary" disabled={busy || !text.trim()}>
        Показать
      </button>
    </form>
  )
}

/** Мемы для карусели. */
function Memes() {
  const { run, error, busy } = useAdminAction()
  const [memes, setMemes] = useState<MemeDto[]>([])
  const [caption, setCaption] = useState('')
  const load = useCallback(() => {
    api<MemeDto[]>('/api/admin/memes').then(setMemes).catch(() => undefined)
  }, [])
  useEffect(load, [load])

  async function add(form: FormData) {
    form.append('caption', caption)
    if (await run('/api/admin/memes', { method: 'POST', body: form })) {
      setCaption('')
      load()
    }
  }

  return (
    <div className="form">
      <label>
        Подпись к мему
        <input value={caption} onChange={(e) => setCaption(e.target.value)} />
      </label>
      <CameraButton disabled={busy} onPhoto={(form) => void add(form)}>
        🖼 Добавить мем
      </CameraButton>
      <div className="meme-grid">
        {memes.map((m) => (
          <figure key={m.id}>
            <img src={m.url} alt={m.caption ?? ''} />
            <figcaption>
              {m.caption}
              <button onClick={async () => confirm('Удалить мем?') && (await run(`/api/admin/memes/${m.id}`, { method: 'DELETE' })) && load()}>🗑</button>
            </figcaption>
          </figure>
        ))}
      </div>
      {error && <p className="error">{error}</p>}
    </div>
  )
}

/** Шаблоны подписей к фото матчей: {A} и {B} — игроки. */
function Captions() {
  const { run, error, busy } = useAdminAction()
  const [captions, setCaptions] = useState<CaptionDto[]>([])
  const [text, setText] = useState('')
  const load = useCallback(() => {
    api<CaptionDto[]>('/api/admin/captions').then(setCaptions).catch(() => undefined)
  }, [])
  useEffect(load, [load])

  async function add(e: FormEvent) {
    e.preventDefault()
    if (await run('/api/admin/captions', { method: 'POST', body: { text } })) {
      setText('')
      load()
    }
  }

  return (
    <form className="form" onSubmit={add}>
      <ul className="captions">
        {captions.map((c) => (
          <li key={c.id}>
            <span>{c.text}</span>
            <button type="button" onClick={async () => (await run(`/api/admin/captions/${c.id}`, { method: 'DELETE' })) && load()}>
              🗑
            </button>
          </li>
        ))}
      </ul>
      <div className="row-2">
        <input value={text} onChange={(e) => setText(e.target.value)} placeholder="{A} УНИЧТОЖАЕТ {B}" />
        <button type="submit" disabled={busy || !text.trim()}>
          + Шаблон
        </button>
      </div>
      {error && <p className="error">{error}</p>}
    </form>
  )
}

/** Страница «Экран ТВ»: скачаны ли медиа, мемы, подписи. */
export function TvControls({ preload }: { preload: AppSnapshot['tvPreload'] }) {
  return (
    <section className="card admin-block">
      <p className="muted">
        {preload
          ? `Медиа Своей игры на ТВ: ${preload.loaded} из ${preload.total} ${preload.loaded === preload.total ? '✅' : '⏳'}`
          : 'ТВ ещё не сообщало, скачаны ли медиа Своей игры'}
      </p>
      <details>
        <summary>Мемы для карусели</summary>
        <Memes />
      </details>
      <details>
        <summary>Подписи к фото матчей</summary>
        <Captions />
      </details>
    </section>
  )
}
