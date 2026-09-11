import type { Session } from './AuthScreen'

export function HostNavigation({ session }: { session: Session }) {
  return <nav className="host-navigation" aria-label="Host navigation"><a href="/">Dashboard</a><a href="/profile">Profilim</a>{session.isAdmin ? <a href="/admin">Admin panel</a> : null}</nav>
}
