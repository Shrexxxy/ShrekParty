import { useState } from 'react'
import { Link } from 'react-router'
import { useSnapshot } from '../api/live'
import { useMe } from '../api/me'
import { Avatar } from '../components/Avatar'
import { Donkeys } from '../components/DonkeyIcon'
import { Leaderboard } from '../components/Leaderboard'
import { isDarkPhase } from '../events/hide'
import { selfieAllowed } from '../events/layer3'
import { HideDark } from '../events/HideView'
import { LevelUpToast } from '../pets/LevelUpToast'
import { markPetOpened, petOpened, switchToGame } from '../pets/pets'
import { PetTab } from '../pets/PetTab'
import { AvatarUpload } from './AvatarUpload'

/** Главная игрока: место и ослики, плашка «сейчас идёт → перейти», питомец и таблица лидеров. Сам ивент — на своей странице. */
export function HomePage() {
  const { me } = useMe()
  const snapshot = useSnapshot()
  // В темноте пряток гаснут все телефоны, даже на главной — иначе светящийся экран выдаст.
  const dark = isDarkPhase(snapshot?.activeEvent)
  const myRow = snapshot?.leaderboard.find((r) => r.userId === me?.id)
  const canUploadAvatar = snapshot ? selfieAllowed(snapshot.registration.state, snapshot.activeEvent) : false
  const pet = snapshot?.myPet ?? null
  const [tab, setTab] = useState<'game' | 'pet'>('game')
  const [openedNow, setOpenedNow] = useState(false)
  const opened = openedNow || (pet !== null && petOpened(pet.userId))
  const eventId = snapshot?.activeEvent?.id ?? null
  const [lastEventId, setLastEventId] = useState(eventId)

  // Ивент начался — возвращаем на «Игру» к плашке и коротко вибрируем, чтобы гость заметил.
  if (eventId !== lastEventId) {
    if (switchToGame(lastEventId, eventId)) {
      setTab('game')
      navigator.vibrate?.(200)
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
      <div className="bunting" aria-hidden />
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
            Мой дракоша{!pet.hatched && <span className="tab-dot" aria-label="есть невылупленное яйцо" />}
          </button>
        </div>
      )}
      {pet && !pet.hatched && !opened && tab === 'game' && (
        <button className="card pet-notice" onClick={openPet}>
          У тебя есть драконоослик — загляни во вкладку «Мой дракоша»
        </button>
      )}
      {pet && tab === 'pet' ? (
        <PetTab pet={pet} />
      ) : (
        <>
          {snapshot?.activeEvent && (
            <Link to={`/event/${snapshot.activeEvent.id}`} className="now-playing">
              <small>Сейчас идёт</small>
              <b>{snapshot.activeEvent.title}</b>
              <span className="now-playing-go">Перейти →</span>
            </Link>
          )}
          {canUploadAvatar && <AvatarUpload />}
          <Leaderboard snapshot={snapshot} highlightUserId={me?.id} />
        </>
      )}
    </main>
  )
}
