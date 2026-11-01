import type { ReactNode } from 'react'

/** Иконки ярмарки — один контур, одна толщина; цвет берут от текста (currentColor). */
const PATHS = {
  shield: <path d="M12 3l7.5 3v5.5c0 4.6-3.1 8.1-7.5 10-4.4-1.9-7.5-5.4-7.5-10V6z" />,
  sword: (
    <>
      <path d="M14.5 3.5H20.5V9.5L10 20H4V14z" />
      <path d="M7 11l6 6" />
    </>
  ),
  spark: <path d="M12 2.5l2.4 7.1 7.1 2.4-7.1 2.4L12 21.5l-2.4-7.1-7.1-2.4 7.1-2.4z" />,
  strength: (
    <>
      <path d="M8.5 9.5a3.5 3.5 0 0 1 7 0" />
      <circle cx="12" cy="15" r="6" />
    </>
  ),
  agility: (
    <>
      <path d="M5 19L19 5" />
      <path d="M13.5 5H19v5.5" />
      <path d="M5 14.5V19h4.5" />
    </>
  ),
  heart: <path d="M12 20.5s-8-4.6-8-10.4A4.4 4.4 0 0 1 12 7.6a4.4 4.4 0 0 1 8 2.5c0 5.8-8 10.4-8 10.4z" />,
  drop: <path d="M12 3c3.2 4.2 6.5 7.6 6.5 11.3a6.5 6.5 0 0 1-13 0C5.5 10.6 8.8 7.2 12 3z" />,
  pencil: (
    <>
      <path d="M4 20l1-4.5L16 4.5 19.5 8 8.5 19z" />
      <path d="M13.5 7l3.5 3.5" />
    </>
  ),
} satisfies Record<string, ReactNode>

export type IconName = keyof typeof PATHS

/** Иконка из набора; подпись — для экранного чтения, без неё иконка декоративная. */
export function Icon({ name, size = '1.2em', label }: { name: IconName; size?: string; label?: string }) {
  return (
    <svg
      className="icon"
      viewBox="0 0 24 24"
      width={size}
      height={size}
      fill="none"
      stroke="currentColor"
      strokeWidth="2.2"
      strokeLinecap="round"
      strokeLinejoin="round"
      role={label ? 'img' : undefined}
      aria-label={label}
      aria-hidden={label ? undefined : true}
    >
      {PATHS[name]}
    </svg>
  )
}
