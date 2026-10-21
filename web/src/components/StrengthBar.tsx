/** Полоска сложности пароля 0–4 (как у zxcvbn). */
export function StrengthBar({ score, label }: { score: number; label?: string }) {
  return (
    <div className="strength" data-score={score}>
      <div className="strength-bar">
        {[0, 1, 2, 3, 4].map((i) => (
          <span key={i} className={i <= score ? 'on' : ''} />
        ))}
      </div>
      {label && <span>{label}</span>}
    </div>
  )
}
