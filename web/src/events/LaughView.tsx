import { Avatar } from '../components/Avatar'
import type { LaughDetails } from './details'

/** Смехлыст: очки трёх партий, лучшая — жирным; не игравшие — внизу. */
export function LaughView({ details, tv }: { details: LaughDetails; tv?: boolean }) {
  return (
    <table className={tv ? 'laugh tv' : 'laugh'}>
      <thead>
        <tr>
          <th>Игрок</th>
          <th>1</th>
          <th>2</th>
          <th>3</th>
        </tr>
      </thead>
      <tbody>
        {details.rows.map((r) => (
          <tr key={r.userId}>
            <td className="laugh-player">
              <Avatar url={r.avatarUrl} login={r.login} size={tv ? 48 : 28} />
              {r.login}
            </td>
            {r.games.map((g, i) => (
              <td key={i} className={g !== null && g === r.best ? 'best' : ''}>
                {g ?? '—'}
              </td>
            ))}
          </tr>
        ))}
      </tbody>
    </table>
  )
}
