import { useState } from 'react'
import { api } from '../api/http'
import { errorText } from '../admin/errors'
import { Avatar } from '../components/Avatar'
import { bidButtons, goingCall } from './auction'
import { formatClock } from './countdown'
import type { AuctionDetails } from './details'
import { useCountdown } from './useCountdown'

/** Таймер торгов и «Раз… два…» под конец. */
function BiddingClock({ deadline, serverTime, tv }: { deadline: string; serverTime: string; tv?: boolean }) {
  const left = useCountdown(deadline, serverTime)
  const call = goingCall(left)
  return (
    <div className="auction-clock">
      <b>{formatClock(left)}</b>
      {call && tv && <div className="auction-call">{call}</div>}
    </div>
  )
}

/** «На руках» у всех — живой баланс на ТВ. */
function Balances({ details }: { details: AuctionDetails }) {
  return (
    <div className="quiz-scores">
      {details.balances.map((b) => (
        <span key={b.userId} className={['quiz-score', b.userId === details.leader?.userId && 'answering'].filter(Boolean).join(' ')}>
          <Avatar url={b.avatarUrl} login={b.login} size={28} />
          {b.login} <b>{b.balance}</b>
        </span>
      ))}
    </div>
  )
}

/** Аукцион: на ТВ — лот, цена, лидер, «Раз… два… ПРОДАНО!»; на телефоне — кнопки ставок. */
export function AuctionView({ eventId, details, serverTime, myId, tv }: { eventId: string; details: AuctionDetails; serverTime: string; myId?: string; tv?: boolean }) {
  const [error, setError] = useState<string | null>(null)
  const { phase, lot } = details
  const buttons =
    lot && details.myBalance !== null
      ? bidButtons({ price: details.price, startPrice: lot.startPrice, balance: details.myBalance, leader: details.leader?.userId === myId })
      : []

  async function bid(step: number) {
    setError(null)
    try {
      await api(`/api/auction/${eventId}/bid`, { method: 'POST', body: { step, expected: details.price } })
    } catch (e) {
      setError(errorText(e))
    }
  }

  const result =
    (phase === 'Sold' || phase === 'Unsold') && details.last ? (
      <div className={`auction-result ${details.last.sold ? 'sold' : ''}`}>
        {details.last.sold ? `ПРОДАНО! ${details.last.lotName} — ${details.last.login} за ${details.last.price} 🫏` : `«${details.last.lotName}» — без ставок`}
      </div>
    ) : null

  return (
    <div className={tv ? 'auction tv' : 'auction'}>
      {phase === 'Bidding' && lot && (
        <>
          <div className="auction-lot-name">{lot.name}</div>
          {lot.photoUrl && <img className="auction-photo" src={lot.photoUrl} alt="" />}
          <div className="auction-price">
            {details.price === null ? `Старт: ${lot.startPrice} 🫏` : `${details.price} 🫏`}
            {details.leader && (
              <span className="auction-leader">
                <Avatar url={details.leader.avatarUrl} login={details.leader.login} size={tv ? 48 : 28} /> {details.leader.login}
              </span>
            )}
          </div>
          {details.deadline && <BiddingClock deadline={details.deadline} serverTime={serverTime} tv={tv} />}
          {!tv && details.myBalance !== null && (
            <div className="bet-sides auction-bids">
              {buttons.map((b) => (
                <button key={b.step} className="primary" disabled={!b.enabled} onClick={() => void bid(b.step)}>
                  {b.step === 0 ? `Беру за ${b.amount}` : `+${b.step} → ${b.amount}`}
                </button>
              ))}
            </div>
          )}
          {!tv && details.leader?.userId === myId && <p className="muted">Ты лидер — жди, перебьют ли</p>}
          {!tv && details.leader?.userId !== myId && buttons.length > 0 && buttons.every((b) => !b.enabled) && (
            <p className="muted">Не хватает осликов на руках</p>
          )}
        </>
      )}
      {result}
      {phase === 'Idle' && <p className="muted">Следующий лот — сейчас! Лотов впереди: {details.left}</p>}
      {phase === 'Done' && <div className="auction-result">Аукцион окончен! 🎉</div>}
      {!tv && details.myBalance !== null && <p className="quiz-my-score">На руках: {details.myBalance} 🫏</p>}
      {error && <p className="error">{error}</p>}
      {tv && <Balances details={details} />}
    </div>
  )
}
