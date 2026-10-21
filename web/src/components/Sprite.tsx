import { useState, type CSSProperties, type ReactNode } from 'react'

/**
 * Картинка из web/public/assets с запасным вариантом: пока файл не сгенерирован — эмодзи или SVG.
 */
export function Sprite({
  src,
  fallback,
  alt = '',
  className,
  style,
}: {
  src: string
  fallback: ReactNode
  alt?: string
  className?: string
  style?: CSSProperties
}) {
  const [failed, setFailed] = useState(false)
  if (failed) {
    return (
      <span className={className} style={style} role={alt ? 'img' : undefined} aria-label={alt || undefined}>
        {fallback}
      </span>
    )
  }

  return <img src={src} alt={alt} className={className} style={style} draggable={false} onError={() => setFailed(true)} />
}
