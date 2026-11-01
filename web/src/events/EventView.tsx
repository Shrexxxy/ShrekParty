import type { ActiveEventDto } from '../api/types'
import { CeremonyView } from './CeremonyView'
import { DaysView } from './DaysView'
import { AuctionView } from './AuctionView'
import type {
  AuctionDetails,
  CeremonyDetails,
  DaysDetails,
  HideDetails,
  LaughDetails,
  MafiaDetails,
  NeuroDetails,
  PhotoHuntDetails,
  PollDetails,
  QuizDetails,
  ReactionDetails,
  TapperDetails,
  TournamentDetails,
} from './details'
import { HideView } from './HideView'
import { LaughView } from './LaughView'
import { MafiaView } from './MafiaView'
import { NeuroView } from './NeuroView'
import { PhotoView } from './PhotoView'
import { PollView } from './PollView'
import { QuizView } from './QuizView'
import { ReactionView } from './ReactionView'
import { TapperView } from './TapperView'
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
    case 'Reaction':
      return <ReactionView eventId={event.id} details={details as ReactionDetails} tv={tv} />
    case 'Tapper':
      return <TapperView eventId={event.id} details={details as TapperDetails} serverTime={serverTime} myId={myId} tv={tv} />
    case 'Days':
      return <DaysView eventId={event.id} details={details as DaysDetails} serverTime={serverTime} tv={tv} />
    case 'Mafia':
      return <MafiaView eventId={event.id} details={details as MafiaDetails} serverTime={serverTime} myId={myId} tv={tv} />
    case 'Auction':
      return <AuctionView eventId={event.id} details={details as AuctionDetails} serverTime={serverTime} myId={myId} tv={tv} />
    case 'Quiz':
      return <QuizView eventId={event.id} details={details as QuizDetails} serverTime={serverTime} myId={myId} tv={tv} />
    case 'NeuroPhoto':
      return <NeuroView eventId={event.id} details={details as NeuroDetails} serverTime={serverTime} tv={tv} />
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
