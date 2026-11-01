import { useCallback, useEffect, useState, type ChangeEvent, type FormEvent } from 'react'
import { api } from '../api/http'
import { photoForm } from '../components/imageUpload'
import { parsePrize, PRIZE_OPTIONS, prizeKey } from '../events/boss'
import type { AuctionLot } from '../events/details'
import { useAdminAction } from './useAdminAction'

const MAX_SIDE = 1600

/** Один лот: название и стартовая цена сохраняются при уходе с поля, фото — загрузкой. */
function LotRow({ lot, run, reload, busy }: { lot: AuctionLot; run: ReturnType<typeof useAdminAction>['run']; reload: () => void; busy: boolean }) {
  const [name, setName] = useState(lot.name)
  const [price, setPrice] = useState(String(lot.startPrice))
  const prize = prizeKey(lot.boss ?? null, lot.medal ?? null)

  async function save() {
    if (name !== lot.name || Number(price) !== lot.startPrice) {
      await run(`/api/admin/auction/lots/${lot.id}`, { method: 'PUT', body: { name, startPrice: Number(price) } })
      reload()
    }
  }

  async function setPrize(key: string) {
    await run(`/api/admin/auction/lots/${lot.id}/prize`, { method: 'PUT', body: parsePrize(key) })
    reload()
  }

  async function upload(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    e.target.value = ''
    if (file) {
      await run(`/api/admin/auction/lots/${lot.id}/photo`, { method: 'POST', body: await photoForm(file, MAX_SIDE) })
      reload()
    }
  }

  return (
    <div className="auction-lot-row">
      {lot.photoUrl ? <img src={lot.photoUrl} alt="" /> : <span className="auction-lot-nophoto">🎁</span>}
      <input value={name} onChange={(e) => setName(e.target.value)} onBlur={() => void save()} />
      <input type="number" inputMode="numeric" min={1} value={price} onChange={(e) => setPrice(e.target.value)} onBlur={() => void save()} />
      <select value={prize} disabled={busy} onChange={(e) => void setPrize(e.target.value)} aria-label="Куда">
        {PRIZE_OPTIONS.map((o) => (
          <option key={o.key} value={o.key}>
            {o.label}
          </option>
        ))}
      </select>
      <label className="button-like">
        📷
        <input type="file" accept="image/*" hidden disabled={busy} onChange={(e) => void upload(e)} />
      </label>
      <button disabled={busy} onClick={async () => confirm(`Удалить «${lot.name}»?`) && (await run(`/api/admin/auction/lots/${lot.id}`, { method: 'DELETE' })) && reload()}>
        🗑
      </button>
    </div>
  )
}

/** Лоты аукциона: заводятся заранее, порядок показа — порядок добавления. */
export function AuctionLots() {
  const { run, error, busy } = useAdminAction()
  const [lots, setLots] = useState<AuctionLot[]>([])
  const [name, setName] = useState('')
  const [price, setPrice] = useState('50')

  const load = useCallback(
    () =>
      api<AuctionLot[]>('/api/admin/auction/lots')
        .then(setLots)
        .catch(() => undefined),
    [],
  )

  useEffect(() => {
    void load()
  }, [load])

  async function add(e: FormEvent) {
    e.preventDefault()
    if (await run('/api/admin/auction/lots', { method: 'POST', body: { name, startPrice: Number(price) } })) {
      setName('')
      void load()
    }
  }

  return (
    <section className="card admin-block">
      <h2>Лоты аукциона</h2>
      {lots.map((lot) => (
        <LotRow key={lot.id} lot={lot} run={run} reload={() => void load()} busy={busy} />
      ))}
      <form className="auction-lot-row" onSubmit={add}>
        <span className="auction-lot-nophoto">＋</span>
        <input value={name} onChange={(e) => setName(e.target.value)} placeholder="Новый лот" />
        <input type="number" inputMode="numeric" min={1} value={price} onChange={(e) => setPrice(e.target.value)} />
        <button disabled={busy || !name.trim()}>Добавить</button>
      </form>
      {error && <p className="error">{error}</p>}
    </section>
  )
}
