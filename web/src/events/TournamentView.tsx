import { useLayoutEffect, useRef, useState } from 'react'
import { api } from '../api/http'
import { errorText } from '../admin/errors'
import { Avatar } from '../components/Avatar'
import type { MatchDto, TournamentDetails, TournamentPlayer } from './details'
import { bracketColumns, bracketLines, currentMatchId, type MatchBox, myBets, oddsText, ROUND_TITLES } from './tournament'
import { Emoji } from '../components/Emoji'
import { CameraButton } from '../admin/CameraButton'

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

/** Сетка: колонки по раундам, на телефоне — горизонтальный скролл; именинник — с мигающей рамкой; текущий матч — жёлтый. */
export function Bracket({ details, myId }: { details: TournamentDetails; myId?: string }) {
  const current = currentMatchId(details)
  const root = useRef<HTMLDivElement>(null)
  const svg = useRef<SVGSVGElement>(null)

  // Линии между раундами — по настоящему положению карточек; пересчёт при новых данных и при смене размера экрана.
  useLayoutEffect(() => {
    const el = root.current
    const layer = svg.current
    if (!el || !layer) {
      return
    }

    const draw = () => {
      const origin = el.getBoundingClientRect()
      const boxes = new Map<number, MatchBox>()
      el.querySelectorAll<HTMLElement>('[data-index]').forEach((card) => {
        const r = card.getBoundingClientRect()
        const dx = el.scrollLeft - origin.left
        const dy = el.scrollTop - origin.top
        boxes.set(Number(card.dataset.index), { left: r.left + dx, top: r.top + dy, right: r.right + dx, bottom: r.bottom + dy })
      })
      layer.setAttribute('width', String(el.scrollWidth))
      layer.setAttribute('height', String(el.scrollHeight))
      layer.innerHTML = bracketLines(details.matches, boxes)
        .map((l) => `<path d="${l.d}" />`)
        .join('')
    }

    draw()
    const observer = new ResizeObserver(draw)
    observer.observe(el)
    return () => observer.disconnect()
  }, [details.matches])

  return (
    <div className="bracket" ref={root}>
      <svg className="bracket-lines" ref={svg} aria-hidden />
      {bracketColumns(details.matches).map((column) => (
        <div key={column.title} className="bracket-column">
          <div className="bracket-title">{column.title}</div>
          <div className="bracket-matches">
            {column.matches.map((m) => (
              <div
                key={m.id}
                data-index={m.index}
                className={['bracket-match', m.status === 'Live' && 'live', m.id === current && 'current', m.id === details.myNextMatchId && 'mine']
                  .filter(Boolean)
                  .join(' ')}
              >
                {m.round === 'ThirdPlace' && <small className="muted">За 3-е место</small>}
                <PlayerRow player={m.playerA} match={m} />
                <PlayerRow player={m.playerB} match={m} isBye={m.isBye} />
                {m.score && <small className="bracket-score">{m.score}</small>}
                {m.pool > 0 && <small className="muted">банк {m.pool} <Emoji e="🫏" /></small>}
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
        {ROUND_TITLES[match.round]} · банк {match.pool} <Emoji e="🫏" />
      </small>
      {match.myBet ? (
        <p>
          Ставка {match.myBet.amount} <Emoji e="🫏" /> на {sides.find(([p]) => p.id === match.myBet!.onPlayerId)?.[0].login}
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

/** Идущий матч на телефоне: кто играет и кнопка «сфоткать» — фото уходят в карусель ТВ. */
function LiveMatch({ match }: { match: MatchDto }) {
  const [state, setState] = useState<'idle' | 'busy' | 'done'>('idle')
  const [error, setError] = useState<string | null>(null)

  async function send(form: FormData) {
    setError(null)
    setState('busy')
    try {
      await api(`/api/matches/${match.id}/photos`, { method: 'POST', body: form })
      setState('done')
    } catch (e) {
      setError(errorText(e))
      setState('idle')
    }
  }

  return (
    <section className="card tournament-block live-match">
      <h3>Сейчас играют</h3>
      <p>
        {match.playerA?.login} — {match.playerB?.login}
      </p>
      <CameraButton disabled={state === 'busy'} onPhoto={(form) => void send(form)}>
        <Emoji e="📸" /> {state === 'busy' ? 'Отправляю…' : state === 'done' ? 'Ещё фото' : 'Фото матча'}
      </CameraButton>
      {state === 'done' && <small className="muted">Фото ушло на ТВ</small>}
      {error && <p className="error">{error}</p>}
    </section>
  )
}

/** Турнир: сетка; на телефоне ещё «играешь следующим», предложение вызова и ставки. */
export function TournamentView({ eventId, details, myId, tv }: { eventId: string; details: TournamentDetails; myId?: string; tv?: boolean }) {
  const [error, setError] = useState<string | null>(null)
  const next = details.matches.find((m) => m.id === details.myNextMatchId)
  // Тотализатор открыт только на текущий матч; на свой матч ставить нельзя.
  const betMatch = details.matches.find((m) => m.id === details.betMatchId) ?? null
  const myMatch = betMatch !== null && (betMatch.playerA?.id === myId || betMatch.playerB?.id === myId)
  const bets = myBets(details)
  const live = details.matches.find((m) => m.status === 'Live') ?? null
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
        <div className="challenge-banner"><Emoji e="⚔️" /> {challengeMatch.playerA?.login} бросает вызов имениннику!</div>
      )}
      {!tv && next && next.playerA && next.playerB && (
        <div className="card next-match"><Emoji e="🎮" /> Ты играешь следующим! {ROUND_TITLES[next.round]}</div>
      )}
      {!tv && details.challenge.offeredToMe && details.challenge.state === 'Offered' && (
        <div className="card challenge-offer">
          <b><Emoji e="🏆" /> Ты выиграл турнир!</b>
          <p>Забрать 100% осликов за 1-е место — или бросить вызов имениннику: победишь — ×2, проиграешь — ×0,5.</p>
          <div className="bet-sides">
            <button onClick={() => void decide(false)}>Забрать 100%</button>
            <button className="primary" onClick={() => void decide(true)}>
              <Emoji e="⚔️" /> Бросить вызов
            </button>
          </div>
          {error && <p className="error">{error}</p>}
        </div>
      )}
      {!tv && live && <LiveMatch key={live.id} match={live} />}
      {!tv && (
        <section className="card tournament-block">
          <h3>Тотализатор</h3>
          {!betMatch ? (
            <p className="muted">Ставки откроются, когда станет известна следующая пара</p>
          ) : myMatch ? (
            <p className="muted">Сейчас твой матч — на него ставить нельзя. Удачи!</p>
          ) : (
            <BetCard key={betMatch.id} match={betMatch} />
          )}
        </section>
      )}
      {!tv && bets.length > 0 && (
        <section className="card tournament-block">
          <h3>Мои ставки</h3>
          <ul className="my-bets">
            {bets.map((b) => (
              <li key={b.matchId} className={b.result}>
                <span>
                  {ROUND_TITLES[b.round]}: на {b.on}
                </span>
                <b>
                  {b.amount} <Emoji e="🫏" /> {b.result === 'won' ? '— выиграла' : b.result === 'lost' ? '— проиграла' : ''}
                </b>
              </li>
            ))}
          </ul>
        </section>
      )}
      {tv ? (
        <Bracket details={details} myId={myId} />
      ) : (
        <section className="card tournament-block">
          <h3>Сетка</h3>
          <Bracket details={details} myId={myId} />
        </section>
      )}
    </div>
  )
}
