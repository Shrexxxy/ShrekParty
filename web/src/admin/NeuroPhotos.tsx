import { useCallback, useEffect, useState, type ChangeEvent } from 'react'
import { api } from '../api/http'
import { useMe } from '../api/me'
import type { AppSnapshot } from '../api/types'
import { photoForm } from '../components/imageUpload'
import { useAdminAction } from './useAdminAction'

const MAX_SIDE = 1600

interface NeuroPhotoDto {
  id: string
  url: string
  subjectId: string | null
}

/** Контент нейрофото: загрузить заранее, после регистрации отметить, кто на каждом. */
export function NeuroPhotos({ snapshot }: { snapshot: AppSnapshot }) {
  const { me } = useMe()
  const { run, error, busy } = useAdminAction()
  const [photos, setPhotos] = useState<NeuroPhotoDto[]>([])

  const load = useCallback(
    () =>
      api<NeuroPhotoDto[]>('/api/admin/neuro/photos')
        .then(setPhotos)
        .catch(() => undefined),
    [],
  )

  useEffect(() => {
    void load()
  }, [load])

  async function upload(e: ChangeEvent<HTMLInputElement>) {
    const files = [...(e.target.files ?? [])]
    e.target.value = ''
    for (const file of files) {
      await run('/api/admin/neuro/photos', { method: 'POST', body: await photoForm(file, MAX_SIDE) })
    }
    await load()
  }

  async function mark(id: string, subjectId: string) {
    await run(`/api/admin/neuro/photos/${id}`, { method: 'PUT', body: { subjectId: subjectId || null } })
    await load()
  }

  async function remove(id: string) {
    if (confirm('Удалить фото?') && (await run(`/api/admin/neuro/photos/${id}`, { method: 'DELETE' }))) {
      await load()
    }
  }

  const unmarked = photos.filter((p) => p.subjectId === null).length
  return (
    <section className="card admin-block">
      <p className="muted">
        {photos.length} фото{unmarked > 0 && ` · не отмечено ${unmarked}`}. Порядок показа — порядок загрузки.
      </p>
      <label className="button-like">
        ＋ Загрузить
        <input type="file" accept="image/*" multiple hidden disabled={busy} onChange={(e) => void upload(e)} />
      </label>
      <div className="neuro-admin">
        {photos.map((p, i) => (
          <figure key={p.id}>
            <img src={p.url} alt="" loading="lazy" />
            <figcaption>
              <span>{i + 1}.</span>
              <select value={p.subjectId ?? ''} disabled={busy} onChange={(e) => void mark(p.id, e.target.value)}>
                <option value="">— кто это? —</option>
                {me && <option value={me.id}>Именинник</option>}
                {snapshot.leaderboard.map((r) => (
                  <option key={r.userId} value={r.userId}>
                    {r.login}
                  </option>
                ))}
              </select>
              <button disabled={busy} onClick={() => void remove(p.id)}>
                🗑
              </button>
            </figcaption>
          </figure>
        ))}
      </div>
      {error && <p className="error">{error}</p>}
    </section>
  )
}
