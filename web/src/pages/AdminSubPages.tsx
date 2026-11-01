import type { ReactNode } from 'react'
import { Link } from 'react-router'
import { useSnapshot } from '../api/live'
import { AuctionLots } from '../admin/AuctionLots'
import { NeuroPhotos } from '../admin/NeuroPhotos'
import { Program } from '../admin/Program'
import { TvControls } from '../admin/TvControls'

/** Отдельная страница настроек админки: заголовок и путь назад. */
function AdminSubPage({ title, children }: { title: string; children: ReactNode }) {
  return (
    <main className="page">
      <header className="admin-header">
        <h1>{title}</h1>
        <Link to="/admin">← Админка</Link>
      </header>
      {children}
    </main>
  )
}

/** Программа вечера: порядок, добавление ивентов, «Начать» у следующего. */
export function AdminProgramPage() {
  const snapshot = useSnapshot()
  return (
    <AdminSubPage title="Программа">
      <Program version={snapshot?.serverTime} />
    </AdminSubPage>
  )
}

/** Мемы, подписи к фото матчей и скачались ли медиа на ТВ. */
export function AdminTvPage() {
  const snapshot = useSnapshot()
  return <AdminSubPage title="Экран ТВ">{snapshot === null ? <p className="muted">Подключаюсь…</p> : <TvControls preload={snapshot.tvPreload} />}</AdminSubPage>
}

/** Фото для нейрофото и кто на каждом. */
export function AdminNeuroPage() {
  const snapshot = useSnapshot()
  return <AdminSubPage title="Нейрофото">{snapshot === null ? <p className="muted">Подключаюсь…</p> : <NeuroPhotos snapshot={snapshot} />}</AdminSubPage>
}

/** Лоты аукциона. */
export function AdminAuctionPage() {
  return (
    <AdminSubPage title="Лоты аукциона">
      <AuctionLots />
    </AdminSubPage>
  )
}
