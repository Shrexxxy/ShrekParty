import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react'
import { ApiError, api } from './http'
import { reconnectLive } from './live'
import type { MeDto, UserRole } from './types'

interface MeContextValue {
  /** undefined — ещё грузится, null — гость. */
  me: MeDto | null | undefined
  /** Обновить после входа/регистрации/аватарки. */
  setMe: (me: MeDto | null) => void
  logout: () => Promise<void>
}

const MeContext = createContext<MeContextValue | null>(null)

/** Домашняя страница роли. */
export function homeOf(role: UserRole | undefined): string {
  switch (role) {
    case 'Admin':
      return '/admin'
    case 'Viewer':
      return '/tv'
    case 'Player':
      return '/'
    default:
      return '/register'
  }
}

export function MeProvider({ children }: { children: ReactNode }) {
  const [me, setMeState] = useState<MeDto | null | undefined>(undefined)

  useEffect(() => {
    api<MeDto>('/api/auth/me')
      .then(setMeState)
      .catch((e) => {
        if (e instanceof ApiError && e.status === 401) {
          setMeState(null)
        } else {
          throw e
        }
      })
  }, [])

  const setMe = useCallback((next: MeDto | null) => {
    setMeState((prev) => {
      if (prev?.id !== next?.id) {
        void reconnectLive()
      }
      return next
    })
  }, [])

  const logout = useCallback(async () => {
    await api('/api/auth/logout', { method: 'POST' })
    setMe(null)
  }, [setMe])

  return <MeContext.Provider value={{ me, setMe, logout }}>{children}</MeContext.Provider>
}

export function useMe(): MeContextValue {
  const value = useContext(MeContext)
  if (value === null) {
    throw new Error('useMe вне MeProvider')
  }
  return value
}
