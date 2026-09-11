import { useCallback, useEffect, useState } from 'react'
import { EventForm } from './EventForm'
import { GalleryManager } from './GalleryManager'
import { HostNavigation } from './HostNavigation'
import { QrManager } from './QrManager'
import { request } from '../lib/api'
import { formatDate, type EventItem } from '../lib/eventTypes'
import type { Session } from './AuthScreen'

export function Dashboard({ session, onLogout }: { session: Session; onLogout: () => void }) {
  const [events, setEvents] = useState<EventItem[]>([]); const [selectedId, setSelectedId] = useState<string | null>(null); const [loading, setLoading] = useState(true); const [message, setMessage] = useState('')
  const selected = events.find(item => item.id === selectedId) ?? null
  const load = useCallback(async () => { try { setEvents(await request<EventItem[]>('/api/host/events/')) } catch (error) { setMessage(error instanceof Error ? error.message : 'Tədbirlər yüklənmədi.') } finally { setLoading(false) } }, [])
  useEffect(() => { void load() }, [load])
  function saved(event: EventItem) { setEvents(previous => { const index = previous.findIndex(item => item.id === event.id); return index === -1 ? [event, ...previous] : previous.map(item => item.id === event.id ? event : item) }); setSelectedId(event.id) }
  async function logout() { await request('/api/host/auth/logout', { method: 'POST' }); onLogout() }
  return <><HostNavigation session={session} /><main className="dashboard-shell"><header className="dashboard-header"><img className="header-logo" src="/brand/bizden-logo.png" alt="Bizdən" /><div><strong>{session.name}</strong><span>{session.email}</span></div><button className="text-button" onClick={() => void logout()}>Çıxış</button></header><div className="dashboard-grid"><aside className="event-list panel"><div className="panel-heading"><div><p className="eyebrow">Tədbirlər</p><h2>Dashboard</h2></div><button className="text-button" onClick={() => setSelectedId(null)}>+ Yeni</button></div>{loading ? <p className="muted">Yüklənir...</p> : null}{message ? <p className="error">{message}</p> : null}{events.map(item => <button className={`event-row ${item.id === selectedId ? 'selected' : ''}`} key={item.id} onClick={() => setSelectedId(item.id)}><strong>{item.name}</strong><span>{formatDate(item.eventDate)} · {item.status}</span><small>{item.invitationCount} QR kod</small></button>)}{!loading && !events.length ? <p className="muted empty">İlk tədbirinizi yaradın.</p> : null}</aside><div className="workspace"><EventForm key={selected?.id ?? 'new'} selected={selected} onSaved={saved} onCancel={() => setSelectedId(null)} />{selected ? <><QrManager key={selected.id} event={selected} /><GalleryManager key={`gallery-${selected.id}`} event={selected} /></> : null}</div></div></main></>
}
