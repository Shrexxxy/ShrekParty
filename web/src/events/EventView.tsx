import type { ActiveEventDto } from '../api/types'
import { CeremonyView } from './CeremonyView'
import type { CeremonyDetails, HideDetails, LaughDetails, PhotoHuntDetails, PollDetails, TournamentDetails } from './details'
import { HideView } from './HideView'
import { LaughView } from './LaughView'
import { PhotoView } from './PhotoView'
import { PollView } from './PollView'
import { TournamentView } from './TournamentView'

/** Экран текущего ивента — общий для телефона и ТВ (tv меняет раскладку). */
export function EventView({ event, serverTime, myId, tv }: { event: ActiveEventDto; serverTime: string; myId?: string; tv?: boolean }) {
  const details = event.details
  switch (event.type) {
    case 'FightPoll':
      return <PollView eventId={event.id} details={details as PollDetails} tv={tv} />
    case 'Tournament':
      return <TournamentView eventId={event.id} details={details as TournamentDetails} myId={myId} tv={tv} />
    case 'Laugh':
      return <LaughView details={details as LaughDetails} tv={tv} />
    case 'HideAndSeek':
      return <HideView eventId={event.id} details={details as HideDetails} serverTime={serverTime} tv={tv} />
    case 'PhotoHunt':
      return <PhotoView eventId={event.id} details={details as PhotoHuntDetails} serverTime={serverTime} myId={myId} tv={tv} />
    case 'Ceremony':
      return <CeremonyView details={details as CeremonyDetails} tv={tv} />
    default:
      return null
  }
}

/** Карточка ивента на главной игрока — над таблицей лидеров. */
export function ActiveEventPanel({ event, serverTime, myId }: { event: ActiveEventDto; serverTime: string; myId?: string }) {
  return (
    <section className="card active-event">
      <h2>{event.title}</h2>
      <EventView event={event} serverTime={serverTime} myId={myId} />
    </section>
  )
}
