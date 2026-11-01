import type { ReactNode } from 'react'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router'
import { homeOf, MeProvider, useMe } from './api/me'
import type { UserRole } from './api/types'
import { AdminPage } from './pages/AdminPage'
import { EventPage } from './pages/EventPage'
import { HomePage } from './pages/HomePage'
import { LoginPage } from './pages/LoginPage'
import { RegisterPage } from './pages/RegisterPage'
import { TvPage } from './pages/TvPage'
import { QuizEditorPage } from './quiz/QuizEditorPage'
import { AdminAuctionPage, AdminNeuroPage, AdminProgramPage, AdminTvPage } from './pages/AdminSubPages'

/** Пускает только указанную роль (или гостя, если role === null); остальных — на их домашнюю страницу. */
function Only({ role, children }: { role: UserRole | null; children: ReactNode }) {
  const { me } = useMe()
  if (me === undefined) {
    return <main className="page center muted">Загрузка…</main>
  }

  const actual = me?.role ?? null
  return actual === role ? children : <Navigate to={homeOf(me?.role)} replace />
}

export function App() {
  return (
    <MeProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/" element={<Only role="Player"><HomePage /></Only>} />
          <Route path="/event/:id" element={<Only role="Player"><EventPage /></Only>} />
          <Route path="/register" element={<Only role={null}><RegisterPage /></Only>} />
          <Route path="/login" element={<Only role={null}><LoginPage /></Only>} />
          <Route path="/tv" element={<Only role="Viewer"><TvPage /></Only>} />
          <Route path="/admin" element={<Only role="Admin"><AdminPage /></Only>} />
          <Route path="/admin/quiz" element={<Only role="Admin"><QuizEditorPage /></Only>} />
          <Route path="/admin/program" element={<Only role="Admin"><AdminProgramPage /></Only>} />
          <Route path="/admin/tv" element={<Only role="Admin"><AdminTvPage /></Only>} />
          <Route path="/admin/neuro" element={<Only role="Admin"><AdminNeuroPage /></Only>} />
          <Route path="/admin/auction" element={<Only role="Admin"><AdminAuctionPage /></Only>} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </BrowserRouter>
    </MeProvider>
  )
}
