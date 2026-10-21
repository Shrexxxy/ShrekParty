import { highlightLogins } from './highlight'

/** Текст с выделенными цветом никами. */
export function Highlighted({ text, logins }: { text: string; logins: string[] }) {
  return (
    <>
      {highlightLogins(text, logins).map((part, i) =>
        part.login ? (
          <span key={i} className="nick">
            {part.text}
          </span>
        ) : (
          <span key={i}>{part.text}</span>
        ),
      )}
    </>
  )
}
