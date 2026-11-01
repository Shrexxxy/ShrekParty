import { useState } from 'react'
import { useSnapshot } from '../api/live'
import { useMe } from '../api/me'
import { Avatar } from '../components/Avatar'
import { Donkeys } from '../components/DonkeyIcon'
import { Leaderboard } from '../components/Leaderboard'
import { ActiveEventPanel } from '../events/EventView'
import { isDarkPhase } from '../events/hide'
import { selfieAllowed } from '../events/layer3'
import { HideDark } from '../events/HideView'
import { useWakeLock } from '../events/useWakeLock'
import { LevelUpToast } from '../pets/LevelUpToast'
import { markPetOpened, petOpened, switchToGame } from '../pets/pets'
import { PetTab } from '../pets/PetTab'
import { AvatarUpload } from './AvatarUpload'

/** Главная игрока: сверху — текущий ивент, ниже — таблица лидеров. */
export function HomePage() {
  const { me } = useMe()
  const snapshot = useSnapshot()
  const dark = isDarkPhase(snapshot?.activeEvent)
  // В темноте экран должен гаснуть сам, как обычно.
  useWakeLock(Boolean(snapshot?.activeEvent) && !dark)
  const myRow = snapshot?.leaderboard.find((r) => r.userId === me?.id)
  const canUploadAvatar = snapshot ? selfieAllowed(snapshot.registration.state, snapshot.activeEvent) : false
  const pet = snapshot?.myPet ?? null
  const [tab, setTab] = useState<'game' | 'pet'>('game')
  const [openedNow, setOpenedNow] = useState(false)
  const opened = openedNow || (pet !== null && petOpened(pet.userId))
  const eventId = snapshot?.activeEvent?.id ?? null
  const [lastEventId, setLastEventId] = useState(eventId)

  // Ивент начался, пока гость смотрел на питомца, — возвращаем на «Игру».
  if (eventId !== lastEventId) {
    if (switchToGame(lastEventId, eventId)) {
      setTab('game')
    }
    setLastEventId(eventId)
  }

  function openPet() {
    setTab('pet')
    setOpenedNow(true)
    if (pet) {
      markPetOpened(pet.userId)
    }
  }

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
      {pet && <LevelUpToast pet={pet} />}
      {pet && (
        <div className="tabs pet-switch" role="tablist">
          <button role="tab" aria-selected={tab === 'game'} onClick={() => setTab('game')}>
            Игра
          </button>
          <button role="tab" aria-selected={tab === 'pet'} onClick={openPet}>
            🐉 Мой дракоша{pet.hatched ? '' : ' 🥚'}
          </button>
        </div>
      )}
      {pet && !pet.hatched && !opened && tab === 'game' && (
        <button className="card pet-notice" onClick={openPet}>
          У тебя есть драконоослик 🥚 — загляни во вкладку «Мой дракоша»
        </button>
      )}
      {pet && tab === 'pet' ? (
        <PetTab pet={pet} />
      ) : (
        <>
          {snapshot?.activeEvent && <ActiveEventPanel event={snapshot.activeEvent} serverTime={snapshot.serverTime} myId={me?.id} />}
          {canUploadAvatar && <AvatarUpload />}
          <Leaderboard snapshot={snapshot} highlightUserId={me?.id} />
        </>
      )}
    </main>
  )
}
