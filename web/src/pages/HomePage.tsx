import { useSnapshot } from '../api/live'
import { useMe } from '../api/me'
import { Avatar } from '../components/Avatar'
import { Donkeys } from '../components/DonkeyIcon'
import { Leaderboard } from '../components/Leaderboard'
import { ActiveEventPanel } from '../events/EventView'
import { isDarkPhase } from '../events/hide'
import { HideDark } from '../events/HideView'
import { useWakeLock } from '../events/useWakeLock'
import { AvatarUpload } from './AvatarUpload'

/** Главная игрока: сверху — текущий ивент, ниже — таблица лидеров. */
export function HomePage() {
  const { me } = useMe()
  const snapshot = useSnapshot()
  const dark = isDarkPhase(snapshot?.activeEvent)
  // В темноте экран должен гаснуть сам, как обычно.
  useWakeLock(Boolean(snapshot?.activeEvent) && !dark)
  const myRow = snapshot?.leaderboard.find((r) => r.userId === me?.id)
  const canUploadAvatar = snapshot?.registration.state === 'Finished'

  if (dark) {
    return <HideDark />
  }

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
      {snapshot?.activeEvent && <ActiveEventPanel event={snapshot.activeEvent} serverTime={snapshot.serverTime} myId={me?.id} />}
      {canUploadAvatar && <AvatarUpload />}
      <Leaderboard snapshot={snapshot} highlightUserId={me?.id} />
    </main>
  )
}
