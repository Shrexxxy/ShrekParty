import type { PetDto, PetsOverviewDto } from '../api/types'
import { CLASS_NAMES } from '../pets/pets'
import { useAdminAction } from './useAdminAction'

function PetRow({ pet }: { pet: PetDto }) {
  const { run, error, busy } = useAdminAction()

  function rename() {
    const name = prompt(`Новая кличка для питомца ${pet.login}`, pet.name)
    if (name !== null) {
      void run(`/api/admin/pets/${pet.userId}/name`, { method: 'PUT', body: { name } })
    }
  }

  return (
    <div className="control-row">
      <span>
        <b>{pet.login}</b> · {pet.name}{' '}
        <button className="link" disabled={busy} onClick={rename} aria-label="Переименовать">
          ✏️
        </button>
        {error && <small className="error"> {error}</small>}
      </span>
      <span>
        {CLASS_NAMES[pet.class]} · {pet.level} ур. {pet.hatched ? '' : '🥚'}
      </span>
    </div>
  )
}

/** Драконоослики: кто какой класс и уровень, кто ещё не вылупил; кличку можно поправить. */
export function Pets({ overview }: { overview: PetsOverviewDto | null | undefined }) {
  if (!overview || overview.total === 0) {
    return null
  }

  return (
    <section className="card admin-block">
      <details>
        <summary>
          🐉 Драконоослики · вылупилось {overview.hatched} из {overview.total}
        </summary>
        {overview.pets.map((p) => (
          <PetRow key={p.userId} pet={p} />
        ))}
      </details>
    </section>
  )
}
