import { useRef, useState, type ChangeEvent } from 'react'
import { api } from '../api/http'
import { useMe } from '../api/me'
import type { MeDto } from '../api/types'

const MAX_SIDE = 512

/** Уменьшает фото до 512 px по большей стороне: селфи с iPhone весят мегабайты. */
async function downscale(file: File): Promise<Blob> {
  const bitmap = await createImageBitmap(file)
  const scale = Math.min(1, MAX_SIDE / Math.max(bitmap.width, bitmap.height))
  const canvas = document.createElement('canvas')
  canvas.width = Math.round(bitmap.width * scale)
  canvas.height = Math.round(bitmap.height * scale)
  canvas.getContext('2d')!.drawImage(bitmap, 0, 0, canvas.width, canvas.height)
  bitmap.close()
  return new Promise((resolve, reject) =>
    canvas.toBlob((blob) => (blob ? resolve(blob) : reject(new Error('toBlob'))), 'image/jpeg', 0.85),
  )
}

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
      const form = new FormData()
      form.append('file', await downscale(file), 'selfie.jpg')
      setMe(await api<MeDto>('/api/me/avatar', { method: 'POST', body: form }))
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
