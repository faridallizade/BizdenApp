import { useEffect, useState } from 'react'
import { AdminScreen } from './components/AdminScreen'
import { AuthScreen, type Session } from './components/AuthScreen'
import { Dashboard } from './components/Dashboard'
import { GuestScreen } from './components/GuestScreen'
import { LanguageSwitcher } from './components/LanguageSwitcher'
import { ProfileScreen } from './components/ProfileScreen'
import { PublicGalleryScreen } from './components/PublicGalleryScreen'
import { request } from './lib/api'
import './App.css'

function HostApp() {
  const [session, setSession] = useState<Session | null>(null)
  const [checked, setChecked] = useState(false)
  useEffect(() => { void request<Session>('/api/host/auth/me').then(setSession).catch(() => null).finally(() => setChecked(true)) }, [])
  if (!checked) return <main className="app-shell"><p className="muted">Yüklənir...</p></main>
  if (!session) return <AuthScreen onAuthenticated={setSession} />
  if (window.location.pathname === '/profile') return <ProfileScreen session={session} onSessionChanged={setSession} onBack={() => window.location.assign('/')} />
  if (window.location.pathname === '/admin') return <AdminScreen session={session} onBack={() => window.location.assign('/')} />
  return <Dashboard session={session} onLogout={() => setSession(null)} />
}

export default function App() {
  const token = window.location.pathname.match(/^\/q\/([^/]+)$/)?.[1]
  const publicId = window.location.pathname.match(/^\/g\/([0-9a-f-]{36})$/i)?.[1]
  return <>{token ? <GuestScreen token={token} /> : publicId ? <PublicGalleryScreen publicId={publicId} /> : <HostApp />}<LanguageSwitcher /></>
}
