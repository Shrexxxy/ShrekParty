import { useRef, type ReactNode } from 'react'

/**
 * Квадратная плитка админки, открывающая окно. Содержимое получает close — закрыть после успешного действия.
 * Клик по затемнению вокруг окна тоже закрывает.
 */
export function AdminDialog({
  icon,
  label,
  title,
  tileClassName = '',
  children,
}: {
  icon: ReactNode
  label: string
  title: string
  tileClassName?: string
  children: (close: () => void) => ReactNode
}) {
  const dialog = useRef<HTMLDialogElement>(null)
  const close = () => dialog.current?.close()

  return (
    <>
      <button type="button" className={`admin-tile ${tileClassName}`} onClick={() => dialog.current?.showModal()}>
        {icon}
        <span>{label}</span>
      </button>
      <dialog ref={dialog} className="admin-dialog" onClick={(e) => e.target === dialog.current && close()}>
        <h2>{title}</h2>
        {children(close)}
      </dialog>
    </>
  )
}
