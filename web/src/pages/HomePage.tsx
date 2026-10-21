import { useSnapshot } from '../api/live'
import { useMe } from '../api/me'
import { Avatar } from '../components/Avatar'
import { Donkeys } from '../components/DonkeyIcon'
import { Leaderboard } from '../components/Leaderboard'
import { AvatarUpload } from './AvatarUpload'

/** Главная игрока: сверху — активный ивент (появится в следующих слоях), ниже — таблица лидеров. */
export function HomePage() {
  const { me } = useMe()
  const snapshot = useSnapshot()
  const myRow = snapshot?.leaderboard.find((r) => r.userId === me?.id)
  const canUploadAvatar = snapshot?.registration.state === 'Finished'

  return (
    <main className="page">
      <header className="me-header">
        <Avatar url={me?.avatarUrl ?? null} login={me?.login ?? ''} size={56} />
        <div>
          <div className="login">{me?.login}</div>
          {myRow && (
            <div className="muted">
              {myRow.place}-е место · <Donkeys value={myRow.donkeys} />
            </div>
          )}
        </div>
      </header>
      {canUploadAvatar && <AvatarUpload />}
      <Leaderboard snapshot={snapshot} highlightUserId={me?.id} />
    </main>
  )
}
