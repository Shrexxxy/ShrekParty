import { useRef, useState, type ChangeEvent } from 'react'
import { api } from '../api/http'
import { useMe } from '../api/me'
import type { MeDto } from '../api/types'
import { photoForm } from '../components/imageUpload'

const MAX_SIDE = 512

/** Кнопка селфи: только фронтальная камера, +5 осликов за первое фото. */
export function AvatarUpload() {
  const { me, setMe } = useMe()
  const input = useRef<HTMLInputElement>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function onFile(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    e.target.value = ''
    if (!file) {
      return
    }

    setBusy(true)
    setError(null)
    try {
      setMe(await api<MeDto>('/api/me/avatar', { method: 'POST', body: await photoForm(file, MAX_SIDE) }))
    } catch {
      setError('Не получилось загрузить, попробуй ещё раз')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="card avatar-upload">
      <input ref={input} type="file" accept="image/*" capture="user" hidden onChange={onFile} />
      <button className="primary" disabled={busy} onClick={() => input.current?.click()}>
        {busy ? 'Загружаю…' : me?.avatarUrl ? '📸 Переснять селфи' : '📸 Сделать селфи (+5 🫏)'}
      </button>
      {error && <p className="error">{error}</p>}
    </div>
  )
}
