import { AnimatePresence, motion } from 'framer-motion'
import { Avatar } from '../components/Avatar'
import { Donkeys } from '../components/DonkeyIcon'
import { stepView } from './ceremony'
import type { CeremonyDetails } from './details'

const CONFETTI = Array.from({ length: 40 }, (_, i) => i)

function Confetti() {
  return (
    <div className="confetti" aria-hidden>
      {CONFETTI.map((i) => (
        <span key={i} style={{ left: `${(i * 37) % 100}%`, animationDelay: `${(i % 10) * 0.3}s` }}>
          {['🎉', '🫏', '🧅', '✨'][i % 4]}
        </span>
      ))}
    </div>
  )
}

/** Церемония: на ТВ — шаги с анимацией, на телефоне — «Смотри на ТВ». */
export function CeremonyView({ details, tv }: { details: CeremonyDetails; tv?: boolean }) {
  if (!tv) {
    return <div className="card ceremony-phone">👀 Смотри на ТВ — идёт церемония награждения!</div>
  }

  const view = stepView(details.step, details.standings)
  return (
    <div className="ceremony">
      <AnimatePresence mode="wait">
        <motion.div key={details.step} initial={{ opacity: 0, scale: 0.9 }} animate={{ opacity: 1, scale: 1 }} exit={{ opacity: 0 }}>
          {view.kind === 'intro' && <div className="ceremony-title">🏆 Церемония награждения</div>}
          {view.kind === 'player' && (
            <div className="ceremony-player">
              <div className="ceremony-place">{view.standing.place}-е место</div>
              <Avatar url={view.standing.avatarUrl} login={view.standing.login} size={220} />
              <div className="ceremony-login nick">{view.standing.login}</div>
              <Donkeys value={view.standing.donkeys} />
              <div className="ceremony-achievement">🏅 {view.standing.achievement}</div>
            </div>
          )}
          {view.kind === 'podium' && (
            <div className="podium">
              {[2, 1, 3].map((place) =>
                view.top
                  .filter((s) => s.place === place)
                  .map((s) => (
                    <div key={s.userId} className={`podium-step p${place}`}>
                      <Avatar url={s.avatarUrl} login={s.login} size={place === 1 ? 160 : 120} />
                      <span className="nick">{s.login}</span>
                      <div className="podium-block">{place}</div>
                    </div>
                  )),
              )}
            </div>
          )}
          {view.kind === 'final' && (
            <div className="ceremony-final">
              <div className="ceremony-title">Спасибо, огры! 💚</div>
              <div className="ceremony-all">
                {details.standings.map((s) => (
                  <Avatar key={s.userId} url={s.avatarUrl} login={s.login} size={96} />
                ))}
              </div>
            </div>
          )}
        </motion.div>
      </AnimatePresence>
      {(view.kind === 'podium' || view.kind === 'final') && <Confetti />}
    </div>
  )
}
