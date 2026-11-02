import { useState } from 'react'
import { api } from '../api/http'
import { errorText } from '../admin/errors'
import { Avatar } from '../components/Avatar'
import type { MatchDto, TournamentDetails, TournamentPlayer } from './details'
import { bracketColumns, oddsText, ROUND_TITLES } from './tournament'

const MAX_BET = 25

function PlayerRow({ player, match, isBye }: { player: TournamentPlayer | null; match: MatchDto; isBye?: boolean }) {
  if (!player) {
    return <div className="bracket-player empty">{isBye ? 'проходит дальше' : '…'}</div>
  }

  const won = match.winnerId === player.id
  const lost = match.status === 'Done' && !won && !match.isBye
  return (
    <div className={['bracket-player', won && 'won', lost && 'lost'].filter(Boolean).join(' ')}>
      <span className={player.isAdmin ? 'admin-ring' : ''}>
        <Avatar url={player.avatarUrl} login={player.login} size={28} />
      </span>
      <span className="bracket-login">{player.login}</span>
    </div>
  )
}

/** Сетка: колонки по раундам, на телефоне — горизонтальный скролл; именинник — с мигающей рамкой. */
export function Bracket({ details, myId }: { details: TournamentDetails; myId?: string }) {
  return (
    <div className="bracket">
      {bracketColumns(details.matches).map((column) => (
        <div key={column.title} className="bracket-column">
          <div className="bracket-title">{column.title}</div>
          <div className="bracket-matches">
            {column.matches.map((m) => (
              <div
                key={m.id}
                className={['bracket-match', m.status === 'Live' && 'live', m.id === details.myNextMatchId && 'mine'].filter(Boolean).join(' ')}
              >
                {m.round === 'ThirdPlace' && <small className="muted">За 3-е место</small>}
                <PlayerRow player={m.playerA} match={m} />
                <PlayerRow player={m.playerB} match={m} isBye={m.isBye} />
                {m.score && <small className="bracket-score">{m.score}</small>}
                {m.pool > 0 && <small className="muted">банк {m.pool} 🫏</small>}
                {m.myBet && myId && <small className="my-bet">твоя ставка {m.myBet.amount}</small>}
              </div>
            ))}
          </div>
        </div>
      ))}
    </div>
  )
}

/** Ставка на один матч: на кого и сколько (1–25), коэффициенты живые. */
function BetCard({ match }: { match: MatchDto }) {
  const [amount, setAmount] = useState('10')
  const [error, setError] = useState<string | null>(null)

  async function bet(onPlayerId: string) {
    setError(null)
    try {
      await api(`/api/matches/${match.id}/bets`, { method: 'POST', body: { onPlayerId, amount: Number(amount) } })
    } catch (e) {
      setError(errorText(e))
    }
  }

  const sides: [TournamentPlayer, number | null][] = [
    [match.playerA!, match.oddsA],
    [match.playerB!, match.oddsB],
  ]

  return (
    <div className="card bet-card">
      <small className="muted">
        {ROUND_TITLES[match.round]} · банк {match.pool} 🫏
      </small>
      {match.myBet ? (
        <p>
          Ставка {match.myBet.amount} 🫏 на {sides.find(([p]) => p.id === match.myBet!.onPlayerId)?.[0].login}
        </p>
      ) : (
        <>
          <div className="bet-sides">
            {sides.map(([player, odds]) => (
              <button key={player.id} onClick={() => void bet(player.id)}>
                {player.login}
                <small>{oddsText(odds)}</small>
              </button>
            ))}
          </div>
          <label>
            Сколько (до {MAX_BET})
            <input type="number" inputMode="numeric" min={1} max={MAX_BET} value={amount} onChange={(e) => setAmount(e.target.value)} />
          </label>
        </>
      )}
      {error && <p className="error">{error}</p>}
    </div>
  )
}

/** Турнир: сетка; на телефоне ещё «играешь следующим», предложение вызова и ставки. */
export function TournamentView({ eventId, details, myId, tv }: { eventId: string; details: TournamentDetails; myId?: string; tv?: boolean }) {
  const [error, setError] = useState<string | null>(null)
  const next = details.matches.find((m) => m.id === details.myNextMatchId)
  const open = details.matches.filter(
    (m) => m.status === 'Pending' && m.playerA && m.playerB && !m.isBye && m.playerA.id !== myId && m.playerB.id !== myId,
  )
  const challengeMatch = details.matches.find((m) => m.round === 'Challenge')
  const final = details.matches.find((m) => m.round === 'Final')
  const champion = [final?.playerA, final?.playerB].find((p) => p && p.id === details.challenge.offeredToId)?.login

  async function decide(accept: boolean) {
    setError(null)
    try {
      await api(`/api/tournaments/${eventId}/challenge`, { method: 'POST', body: { accept } })
    } catch (e) {
      setError(errorText(e))
    }
  }

  return (
    <div className={tv ? 'tournament tv' : 'tournament'}>
      {tv && details.challenge.state === 'Offered' && champion && (
        <div className="challenge-banner">{champion} решает: драться с именинником или забрать ослики?</div>
      )}
      {tv && details.challenge.state === 'Declined' && champion && (
        <div className="challenge-banner">{champion} забирает ослики — вызов не брошен</div>
      )}
      {tv && challengeMatch && challengeMatch.status !== 'Done' && (
        <div className="challenge-banner">⚔️ {challengeMatch.playerA?.login} бросает вызов имениннику!</div>
      )}
      {!tv && next && next.playerA && next.playerB && (
        <div className="card next-match">🎮 Ты играешь следующим! {ROUND_TITLES[next.round]}</div>
      )}
      {!tv && details.challenge.offeredToMe && details.challenge.state === 'Offered' && (
        <div className="card challenge-offer">
          <b>🏆 Ты выиграл турнир!</b>
          <p>Забрать 100% осликов за 1-е место — или бросить вызов имениннику: победишь — ×2, проиграешь — ×0,5.</p>
          <div className="bet-sides">
            <button onClick={() => void decide(false)}>Забрать 100%</button>
            <button className="primary" onClick={() => void decide(true)}>
              ⚔️ Бросить вызов
            </button>
          </div>
          {error && <p className="error">{error}</p>}
        </div>
      )}
      <Bracket details={details} myId={myId} />
      {!tv && open.length > 0 && (
        <section>
          <h3>Тотализатор</h3>
          {open.map((m) => (
            <BetCard key={m.id} match={m} />
          ))}
        </section>
      )}
    </div>
  )
}
