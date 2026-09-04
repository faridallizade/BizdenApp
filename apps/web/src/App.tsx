import { useCallback, useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { toDataURL } from 'qrcode'
import './App.css'

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:55080'
const blankEvent = () => ({ name: '', description: '', eventDate: '', timeZone: 'Asia/Baku', uploadStartAt: '', uploadEndAt: '', status: 'Draft' })
type Session = { id: string; name: string; email: string }
type EventItem = { id: string; name: string; description?: string; eventDate: string; timeZone: string; uploadStartAt: string; uploadEndAt: string; status: 'Draft' | 'Active' | 'Closed'; invitationCount: number }
type Invitation = { id: string; label?: string; uploadLimit: number; reservedUploads: number; completedUploads: number; isActive: boolean; expiresAt?: string; createdAt: string }
type InvitationToken = { invitation: Invitation; token: string }

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, { credentials: 'include', headers: { 'Content-Type': 'application/json', ...init?.headers }, ...init })
  const data = response.status === 204 ? null : await response.json()
  if (!response.ok) throw new Error(data?.message ?? 'Sorğu tamamlanmadı.')
  return data as T
}
function toApiDate(value: string) { return new Date(value).toISOString() }
function toInputDate(value: string) { return value ? new Date(value).toISOString().slice(0, 16) : '' }
function formatDate(value: string) { return new Intl.DateTimeFormat('az-AZ', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) }

function AuthScreen({ onAuthenticated }: { onAuthenticated: (session: Session) => void }) {
  const [isRegistering, setIsRegistering] = useState(false); const [isSubmitting, setIsSubmitting] = useState(false); const [message, setMessage] = useState('')
  async function submit(event: FormEvent<HTMLFormElement>) { event.preventDefault(); const form = new FormData(event.currentTarget); setIsSubmitting(true); setMessage(''); const body = isRegistering ? { name: form.get('name'), email: form.get('email'), password: form.get('password') } : { email: form.get('email'), password: form.get('password') }; try { onAuthenticated(await request<Session>(`/api/host/auth/${isRegistering ? 'register' : 'login'}`, { method: 'POST', body: JSON.stringify(body) })) } catch (error) { setMessage(error instanceof Error ? error.message : 'Giriş alınmadı.') } finally { setIsSubmitting(false) } }
  return <main className="app-shell"><section className="auth-card" aria-labelledby="page-title"><img className="brand-logo" src="/brand/bizden-logo.png" alt="Bizdən — Anılarınız, bizdən." /><p className="eyebrow">Host portal</p><h1 id="page-title">{isRegistering ? 'Hesab yaradın' : 'Xoş gördük'}</h1><p className="description">Tədbir xatirələrinizi idarə etmək üçün daxil olun.</p><form onSubmit={submit}>{isRegistering ? <label>Ad<input name="name" required maxLength={120} autoComplete="name" /></label> : null}<label>Email<input name="email" type="email" required maxLength={256} autoComplete="email" /></label><label>Şifrə<input name="password" type="password" required minLength={12} autoComplete={isRegistering ? 'new-password' : 'current-password'} /></label>{message ? <p className="error" role="alert">{message}</p> : null}<button className="primary" disabled={isSubmitting}>{isSubmitting ? 'Gözləyin...' : isRegistering ? 'Hesab yarat' : 'Daxil ol'}</button></form><button className="switch" type="button" onClick={() => { setIsRegistering(value => !value); setMessage('') }}>{isRegistering ? 'Artıq hesabınız var? Daxil olun' : 'Hesabınız yoxdur? Qeydiyyatdan keçin'}</button></section></main>
}

function EventForm({ selected, onSaved, onCancel }: { selected: EventItem | null; onSaved: (event: EventItem) => void; onCancel: () => void }) {
  const [message, setMessage] = useState(''); const [saving, setSaving] = useState(false); const value = selected ?? blankEvent()
  async function submit(event: FormEvent<HTMLFormElement>) { event.preventDefault(); const form = new FormData(event.currentTarget); setSaving(true); setMessage(''); const body = { name: form.get('name'), description: form.get('description') || null, eventDate: toApiDate(String(form.get('eventDate'))), timeZone: form.get('timeZone'), uploadStartAt: toApiDate(String(form.get('uploadStartAt'))), uploadEndAt: toApiDate(String(form.get('uploadEndAt'))), status: form.get('status') }; try { onSaved(await request<EventItem>(selected ? `/api/host/events/${selected.id}` : '/api/host/events/', { method: selected ? 'PUT' : 'POST', body: JSON.stringify(body) })) } catch (error) { setMessage(error instanceof Error ? error.message : 'Tədbir yadda saxlanmadı.') } finally { setSaving(false) } }
  return <section className="panel event-form"><div className="panel-heading"><div><p className="eyebrow">Phase 4</p><h2>{selected ? 'Tədbiri redaktə et' : 'Yeni tədbir'}</h2></div>{selected ? <button className="text-button" onClick={onCancel}>Yeni tədbirə keç</button> : null}</div><form onSubmit={submit}><label>Tədbirin adı<input name="name" defaultValue={value.name} required maxLength={160} /></label><label>Açıqlama<textarea name="description" defaultValue={value.description} maxLength={2000} rows={3} /></label><div className="form-grid"><label>Tədbir vaxtı<input name="eventDate" type="datetime-local" defaultValue={toInputDate(value.eventDate)} required /></label><label>Timezone<input name="timeZone" defaultValue={value.timeZone} required maxLength={64} /></label><label>Upload başlanğıcı<input name="uploadStartAt" type="datetime-local" defaultValue={toInputDate(value.uploadStartAt)} required /></label><label>Upload sonu<input name="uploadEndAt" type="datetime-local" defaultValue={toInputDate(value.uploadEndAt)} required /></label></div><label>Status<select name="status" defaultValue={value.status}><option value="Draft">Draft</option><option value="Active">Aktiv</option><option value="Closed">Bağlı</option></select></label>{message ? <p className="error" role="alert">{message}</p> : null}<button className="primary" disabled={saving}>{saving ? 'Yadda saxlanır...' : selected ? 'Dəyişiklikləri saxla' : 'Tədbir yarat'}</button></form></section>
}

function QrPreview({ item }: { item: InvitationToken }) {
  const [source, setSource] = useState('')
  const link = `${window.location.origin}/q/${item.token}`
  useEffect(() => { void toDataURL(link, { width: 360, margin: 2, color: { dark: '#4e3b2f', light: '#fffdfa' } }).then(setSource) }, [link])
  function download() { if (!source) return; const anchor = document.createElement('a'); anchor.href = source; anchor.download = `bizden-${item.invitation.label ?? 'qr'}.png`; anchor.click() }
  return <article className="qr-preview"><img src={source} alt={`${item.invitation.label ?? 'Bizdən'} QR kodu`} /><div><strong>{item.invitation.label ?? 'Yeni QR'}</strong><code>{link}</code><button className="text-button" type="button" onClick={download} disabled={!source}>PNG endir</button></div></article>
}

function QrManager({ event }: { event: EventItem }) {
  const [invitations, setInvitations] = useState<Invitation[]>([]); const [tokens, setTokens] = useState<InvitationToken[]>([]); const [message, setMessage] = useState(''); const [busy, setBusy] = useState(false)
  const load = useCallback(async () => { try { setInvitations(await request<Invitation[]>(`/api/host/events/${event.id}/invitations`)) } catch (error) { setMessage(error instanceof Error ? error.message : 'QR-lər yüklənmədi.') } }, [event.id])
  useEffect(() => { void load() }, [load])
  async function create(eventData: FormEvent<HTMLFormElement>) { eventData.preventDefault(); const form = new FormData(eventData.currentTarget); setBusy(true); setMessage(''); try { const result = await request<{ invitations: InvitationToken[] }>(`/api/host/events/${event.id}/invitations`, { method: 'POST', body: JSON.stringify({ label: form.get('label') || null, uploadLimit: Number(form.get('uploadLimit')), count: Number(form.get('count')) }) }); setTokens(result.invitations); await load(); eventData.currentTarget.reset() } catch (error) { setMessage(error instanceof Error ? error.message : 'QR yaradıla bilmədi.') } finally { setBusy(false) } }
  async function regenerate(id: string) { if (!window.confirm('Köhnə QR dərhal deaktiv olacaq. Davam edək?')) return; setBusy(true); setMessage(''); try { const result = await request<InvitationToken>(`/api/host/events/${event.id}/invitations/${id}/regenerate`, { method: 'POST' }); setTokens([result]); await load() } catch (error) { setMessage(error instanceof Error ? error.message : 'QR yenilənmədi.') } finally { setBusy(false) } }
  async function toggle(item: Invitation) { setBusy(true); setMessage(''); try { await request(`/api/host/events/${event.id}/invitations/${item.id}`, { method: 'PATCH', body: JSON.stringify({ label: item.label ?? null, uploadLimit: item.uploadLimit, expiresAt: item.expiresAt ?? null, isActive: !item.isActive }) }); await load() } catch (error) { setMessage(error instanceof Error ? error.message : 'QR statusu dəyişmədi.') } finally { setBusy(false) } }
  return <section className="panel qr-panel"><div className="panel-heading"><div><p className="eyebrow">Phase 5</p><h2>QR kodlar</h2><p className="muted">{event.name} · {invitations.length} QR</p></div></div><form className="qr-create" onSubmit={create}><label>QR etiketi<input name="label" placeholder="Məsələn: Ana masa" maxLength={120} /></label><label>Foto limiti<input name="uploadLimit" type="number" min="1" max="10000" defaultValue="15" required /></label><label>Sayı<input name="count" type="number" min="1" max="50" defaultValue="1" required /></label><button className="primary" disabled={busy}>QR yarat</button></form>{message ? <p className="error" role="alert">{message}</p> : null}{tokens.length ? <div className="token-box"><strong>Yeni QR-lər — indi endirin və ya kopyalayın.</strong><p>Raw token bazada saxlanmır; səhifə yenilənəndə QR-lər yenidən görünməyəcək.</p><div className="qr-preview-list">{tokens.map(item => <QrPreview key={item.invitation.id} item={item} />)}</div></div> : null}<div className="invitation-list">{invitations.map(item => <article className="invitation" key={item.id}><div><strong>{item.label ?? 'Adsız QR'}</strong><p>{item.completedUploads}/{item.uploadLimit} foto · {item.isActive ? 'Aktiv' : 'Deaktiv'}</p></div><div className="invitation-actions"><button className="text-button" disabled={busy} onClick={() => void toggle(item)}>{item.isActive ? 'Deaktiv et' : 'Aktiv et'}</button><button className="text-button" disabled={busy} onClick={() => void regenerate(item.id)}>Yenilə</button></div></article>)}{!invitations.length ? <p className="muted empty">Hələ QR kod yoxdur.</p> : null}</div></section>
}

type PublicQr = { state: string; eventName?: string; description?: string; eventDate?: string; timeZone?: string; remainingPhotos: number; uploadLimit?: number; uploadEndAt?: string }
function GuestScreen({ token }: { token: string }) {
  const [data, setData] = useState<PublicQr | null>(null); const [message, setMessage] = useState(''); const [reserving, setReserving] = useState(false); const [held, setHeld] = useState(0)
  const load = useCallback(async () => { try { setData(await request<PublicQr>(`/api/public/qr/${token}`)) } catch { setMessage('QR kod oxuna bilmədi.') } }, [token])
  useEffect(() => { void load() }, [load])
  async function selectFiles(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); const input = event.currentTarget.elements.namedItem('photos') as HTMLInputElement; const files = Array.from(input.files ?? []); if (!files.length) return; setReserving(true); setMessage('')
    try {
      const results = await Promise.all(files.map(async file => {
        const reserved = await request<{ state: string; reservationId?: string }>(`/api/public/qr/${token}/reservations`, { method: 'POST', body: JSON.stringify({ fileName: file.name, mimeType: file.type, fileSize: file.size, idempotencyKey: crypto.randomUUID() }) })
        if (reserved.state !== 'RESERVED' || !reserved.reservationId) return reserved.state
        const signed = await request<{ state: string; url?: string }>(`/api/public/qr/${token}/reservations/${reserved.reservationId}/upload-url`, { method: 'POST' })
        if (signed.state !== 'READY' || !signed.url) return signed.state
        const put = await fetch(signed.url, { method: 'PUT', headers: { 'Content-Type': file.type }, body: file }); if (!put.ok) return 'UPLOAD_FAILED'
        return (await request<{ state: string }>(`/api/public/qr/${token}/reservations/${reserved.reservationId}/complete`, { method: 'POST' })).state
      }))
      const count = results.filter(item => item === 'COMPLETED').length; setHeld(previous => previous + count); setMessage(count === files.length ? `${count} foto uğurla yükləndi.` : `${count}/${files.length} foto yükləndi. R2 konfiqurasiyasını yoxlayın.`); await load()
    } catch { setMessage('Yükləmə mümkün olmadı. İnterneti yoxlayıb yenidən cəhd edin.') } finally { setReserving(false) }
  }
  if (!data) return <main className="guest-shell"><p className="muted">Yüklənir...</p></main>
  const copy: Record<string, string> = { NOT_FOUND: 'Bu QR kod tapılmadı.', INACTIVE: 'Bu QR kod deaktiv edilib.', EXPIRED: 'Bu QR kodun vaxtı bitib.', EVENT_UNAVAILABLE: 'Tədbir hazırda aktiv deyil.', NOT_OPEN: 'Foto yükləmə vaxtı hələ başlamayıb.', WINDOW_CLOSED: 'Foto yükləmə vaxtı bitib.', LIMIT_REACHED: 'Bu QR üçün foto limiti dolub.' }
  if (data.state !== 'READY') return <main className="guest-shell"><section className="guest-card"><img className="brand-logo" src="/brand/bizden-logo.png" alt="Bizdən" /><p className="eyebrow">Bizdən</p><h1>{copy[data.state] ?? 'Bu dəvət əlçatan deyil.'}</h1></section></main>
  return <main className="guest-shell"><section className="guest-card"><img className="brand-logo" src="/brand/bizden-logo.png" alt="Bizdən" /><p className="eyebrow">Xatirələri paylaşın</p><h1>{data.eventName}</h1><p className="description">{data.description ?? 'Bu xüsusi günün anlarını bizimlə paylaşın.'}</p><div className="guest-limit"><strong>{data.remainingPhotos}</strong><span>foto haqqı qalıb</span></div><form onSubmit={selectFiles}><label className="file-picker">Foto seçin<input name="photos" type="file" accept="image/jpeg,image/png,image/webp,image/heic" multiple disabled={reserving} /></label><button className="primary" disabled={reserving}>{reserving ? 'Yüklənir...' : 'Fotoları yüklə'}</button></form>{held ? <p className="success">{held} foto uğurla yükləndi.</p> : null}{message ? <p className="error" role="alert">{message}</p> : null}</section></main>
}

function Dashboard({ session, onLogout }: { session: Session; onLogout: () => void }) {
  const [events, setEvents] = useState<EventItem[]>([]); const [selectedId, setSelectedId] = useState<string | null>(null); const [loading, setLoading] = useState(true); const [message, setMessage] = useState(''); const selected = events.find(item => item.id === selectedId) ?? null
  const load = useCallback(async () => { try { const result = await request<EventItem[]>('/api/host/events/'); setEvents(result) } catch (error) { setMessage(error instanceof Error ? error.message : 'Tədbirlər yüklənmədi.') } finally { setLoading(false) } }, [])
  useEffect(() => { void load() }, [load])
  function saved(event: EventItem) { setEvents(previous => { const index = previous.findIndex(item => item.id === event.id); return index === -1 ? [event, ...previous] : previous.map(item => item.id === event.id ? event : item) }); setSelectedId(event.id) }
  async function logout() { await request('/api/host/auth/logout', { method: 'POST' }); onLogout() }
  return <main className="dashboard-shell"><header className="dashboard-header"><img className="header-logo" src="/brand/bizden-logo.png" alt="Bizdən" /><div><strong>{session.name}</strong><span>{session.email}</span></div><button className="text-button" onClick={() => void logout()}>Çıxış</button></header><div className="dashboard-grid"><aside className="event-list panel"><div className="panel-heading"><div><p className="eyebrow">Tədbirlər</p><h2>Dashboard</h2></div><button className="text-button" onClick={() => setSelectedId(null)}>+ Yeni</button></div>{loading ? <p className="muted">Yüklənir...</p> : null}{message ? <p className="error">{message}</p> : null}{events.map(item => <button className={`event-row ${item.id === selectedId ? 'selected' : ''}`} key={item.id} onClick={() => setSelectedId(item.id)}><strong>{item.name}</strong><span>{formatDate(item.eventDate)} · {item.status}</span><small>{item.invitationCount} QR kod</small></button>)}{!loading && !events.length ? <p className="muted empty">İlk tədbirinizi yaradın.</p> : null}</aside><div className="workspace"><EventForm key={selected?.id ?? 'new'} selected={selected} onSaved={saved} onCancel={() => setSelectedId(null)} />{selected ? <QrManager key={selected.id} event={selected} /> : null}</div></div></main>
}

function HostApp() {
  const [session, setSession] = useState<Session | null>(null); const [checked, setChecked] = useState(false)
  useEffect(() => { void request<Session>('/api/host/auth/me').then(setSession).catch(() => null).finally(() => setChecked(true)) }, [])
  if (!checked) return <main className="app-shell"><p className="muted">Yüklənir...</p></main>
  return session ? <Dashboard session={session} onLogout={() => setSession(null)} /> : <AuthScreen onAuthenticated={setSession} />
}

export default function App() {
  const token = window.location.pathname.match(/^\/q\/([^/]+)$/)?.[1]
  return token ? <GuestScreen token={token} /> : <HostApp />
}
