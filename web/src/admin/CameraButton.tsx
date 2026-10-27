import { useRef, type ChangeEvent, type ReactNode } from 'react'
import { photoForm } from '../components/imageUpload'

const MAX_SIDE = 1280

/** Кнопка «сфоткать» основной камерой: уменьшает фото и отдаёт готовую форму. */
export function CameraButton({
  onPhoto,
  fields,
  disabled,
  children,
}: {
  onPhoto: (form: FormData) => void
  fields?: Record<string, string>
  disabled?: boolean
  children: ReactNode
}) {
  const input = useRef<HTMLInputElement>(null)

  async function onFile(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    e.target.value = ''
    if (file) {
      onPhoto(await photoForm(file, MAX_SIDE, fields))
    }
  }

  return (
    <>
      <input ref={input} type="file" accept="image/*" capture="environment" hidden onChange={onFile} />
      <button type="button" disabled={disabled} onClick={() => input.current?.click()}>
        {children}
      </button>
    </>
  )
}
