import { useCallback, useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { toDataURL } from 'qrcode'
import { downloadBrandedQrPdf } from './lib/qrPdf'
import { AuthScreen, type Session } from './components/AuthScreen'
import { ProfileScreen } from './components/ProfileScreen'
import { GalleryManager } from './components/GalleryManager'
import { LanguageSwitcher } from './components/LanguageSwitcher'
import { AdminScreen } from './components/AdminScreen'
import { HostNavigation } from './components/HostNavigation'
import './App.css'

// Nginx proxies /api to the API container. Keeping requests same-origin means
// the app also works from a phone or another computer, not only localhost.
const apiBaseUrl = ''
const blankEvent = () => ({ name: '', description: '', eventDate: '', timeZone: 'Asia/Baku', uploadStartAt: '', uploadEndAt: '', status: 'Draft', brandColor: '#805742', customMessage: '' })
type EventItem = { id: string; name: string; description?: string; eventDate: string; timeZone: string; uploadStartAt: string; uploadEndAt: string; status: 'Draft' | 'Active' | 'Completed' | 'Archived'; invitationCount: number; brandColor?: string; customMessage?: string }
type Invitation = { id: string; label?: string; uploadLimit: number; reservedUploads: number; completedUploads: number; isActive: boolean; expiresAt?: string; createdAt: string }
type InvitationToken = { invitation: Invitation; token: string }

let csrfToken: Promise<string> | null = null
function getCsrfToken(refresh = false) {
  if (refresh) csrfToken = null
  csrfToken ??= fetch('/api/host/antiforgery', { credentials: 'include' }).then(async response => {
    if (!response.ok) throw new Error('Təhlükəsizlik tokeni alına bilmədi.')
    return (await response.json() as { token: string }).token
  })
  return csrfToken
}
async function request<T>(path: string, init?: RequestInit, retryCsrf = true): Promise<T> {
  const method = init?.method?.toUpperCase() ?? 'GET'
  const csrf = path.startsWith('/api/host/') && !path.endsWith('/antiforgery') && !['GET', 'HEAD', 'OPTIONS'].includes(method) ? await getCsrfToken() : undefined
  const response = await fetch(`${apiBaseUrl}${path}`, { credentials: 'include', headers: { 'Content-Type': 'application/json', ...(csrf ? { 'X-CSRF-TOKEN': csrf } : {}), ...init?.headers }, ...init })
  const data = response.status === 204 ? null : await response.json()
  if (!response.ok) {
    if (csrf && retryCsrf && data?.code === 'INVALID_CSRF') {
      await getCsrfToken(true)
      return request<T>(path, init, false)
    }
    throw new Error(data?.message ?? 'Sorğu tamamlanmadı.')
  }
  return data as T
}
function toApiDate(value: string) { return new Date(value).toISOString() }
function toInputDate(value: string) { return value ? new Date(value).toISOString().slice(0, 16) : '' }
function formatDate(value: string) { return new Intl.DateTimeFormat('az-AZ', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) }
function newIdempotencyKey() {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') return crypto.randomUUID()
  return `${Date.now()}-${Math.random().toString(36).slice(2)}-${Math.random().toString(36).slice(2)}`
}

export function LegacyAuthScreen({ onAuthenticated }: { onAuthenticated: (session: Session) => void }) {
  const [isRegistering, setIsRegistering] = useState(false)
  const [verificationEmail, setVerificationEmail] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [showPassword, setShowPassword] = useState(false)
  const [message, setMessage] = useState('')

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    setIsSubmitting(true); setMessage('')
    try {
      if (verificationEmail) {
        onAuthenticated(await request<Session>('/api/host/auth/verify-email', { method: 'POST', body: JSON.stringify({ email: verificationEmail, code: form.get('code') }) }))
        return
      }
      const email = String(form.get('email') ?? '')
      const body = isRegistering ? { name: form.get('name'), email, password: form.get('password') } : { email, password: form.get('password') }
      const result = await request<Session | { requiresEmailVerification: true }>(`/api/host/auth/${isRegistering ? 'register' : 'login'}`, { method: 'POST', body: JSON.stringify(body) })
      if ('requiresEmailVerification' in result) { setVerificationEmail(email); return }
      onAuthenticated(result)
    } catch (error) { setMessage(error instanceof Error ? error.message : 'Giriş alınmadı.') } finally { setIsSubmitting(false) }
  }

  async function resend() {
    setIsSubmitting(true); setMessage('')
    try { await request('/api/host/auth/resend-verification', { method: 'POST', body: JSON.stringify({ email: verificationEmail }) }); setMessage('Yeni kod göndərildi.') }
    catch (error) { setMessage(error instanceof Error ? error.message : 'Kod yenidən göndərilə bilmədi.') }
    finally { setIsSubmitting(false) }
  }

  if (verificationEmail) return <main className="app-shell"><section className="auth-card" aria-labelledby="page-title"><img className="brand-logo" src="/brand/bizden-logo.png" alt="Bizdən — Anılarınız, bizdən." /><p className="eyebrow">Email təsdiqi</p><h1 id="page-title">Kodu daxil edin</h1><p className="description">{verificationEmail} ünvanına göndərilən 6 rəqəmli kodu yazın.</p><form onSubmit={submit}><label>Təsdiq kodu<input name="code" inputMode="numeric" autoComplete="one-time-code" pattern="[0-9]{6}" minLength={6} maxLength={6} required /></label>{message ? <p className="error" role="alert">{message}</p> : null}<button className="primary" disabled={isSubmitting}>{isSubmitting ? 'Gözləyin...' : 'Təsdiqlə'}</button></form><button className="switch" type="button" disabled={isSubmitting} onClick={() => void resend()}>Kodu yenidən göndər</button><button className="switch" type="button" onClick={() => { setVerificationEmail(''); setMessage('') }}>Başqa email istifadə et</button></section></main>

  return <main className="app-shell"><section className="auth-card" aria-labelledby="page-title"><img className="brand-logo" src="/brand/bizden-logo.png" alt="Bizdən — Anılarınız, bizdən." /><p className="eyebrow">Host portal</p><h1 id="page-title">{isRegistering ? 'Hesab yaradın' : 'Xoş gördük'}</h1><p className="description">Tədbir xatirələrinizi idarə etmək üçün daxil olun.</p><form onSubmit={submit}>{isRegistering ? <label>Ad<input name="name" required maxLength={120} autoComplete="name" /></label> : null}<label>Email<input name="email" type="email" required maxLength={256} autoComplete="email" /></label><label>Şifrə<span className="password-field"><input name="password" type={showPassword ? 'text' : 'password'} required minLength={isRegistering ? 8 : undefined} pattern={isRegistering ? '.*[0-9].*' : undefined} title={isRegistering ? 'Minimum 8 simvol və ən az 1 rəqəm daxil edin.' : undefined} autoComplete={isRegistering ? 'new-password' : 'current-password'} /><button type="button" className="password-toggle" onClick={() => setShowPassword(value => !value)} aria-label={showPassword ? 'Şifrəni gizlət' : 'Şifrəni göstər'}>{showPassword ? 'Gizlət' : 'Göstər'}</button></span>{isRegistering ? <small>Minimum 8 simvol və ən az 1 rəqəm.</small> : null}</label>{message ? <p className="error" role="alert">{message}</p> : null}<button className="primary" disabled={isSubmitting}>{isSubmitting ? 'Gözləyin...' : isRegistering ? 'Hesab yarat' : 'Daxil ol'}</button></form><button className="switch" type="button" onClick={() => { setIsRegistering(value => !value); setMessage('') }}>{isRegistering ? 'Artıq hesabınız var? Daxil olun' : 'Hesabınız yoxdur? Qeydiyyatdan keçin'}</button></section></main>
}

function EventForm({ selected, onSaved, onCancel }: { selected: EventItem | null; onSaved: (event: EventItem) => void; onCancel: () => void }) {
  const [message, setMessage] = useState(''); const [saving, setSaving] = useState(false); const value = selected ?? blankEvent()
  async function submit(event: FormEvent<HTMLFormElement>) { event.preventDefault(); const form = new FormData(event.currentTarget); setSaving(true); setMessage(''); const body = { name: form.get('name'), description: form.get('description') || null, eventDate: toApiDate(String(form.get('eventDate'))), timeZone: form.get('timeZone'), uploadStartAt: toApiDate(String(form.get('uploadStartAt'))), uploadEndAt: toApiDate(String(form.get('uploadEndAt'))), status: form.get('status'), brandColor: form.get('brandColor') || null, customMessage: form.get('customMessage') || null }; try { let saved = await request<EventItem>(selected ? `/api/host/events/${selected.id}` : '/api/host/events/', { method: selected ? 'PUT' : 'POST', body: JSON.stringify(body) }); const cover = form.get('cover') as File | null; if (cover?.size) { const prepared = await request<{ key: string; url: string }>(`/api/host/events/${saved.id}/cover/upload-url`, { method: 'POST', body: JSON.stringify({ fileName: cover.name, mimeType: cover.type, fileSize: cover.size }) }); await putFile(prepared.url, cover, () => undefined); saved = await request<EventItem>(`/api/host/events/${saved.id}/cover/complete`, { method: 'POST', body: JSON.stringify({ key: prepared.key, mimeType: cover.type, fileSize: cover.size }) }) } onSaved(saved) } catch (error) { setMessage(error instanceof Error ? error.message : 'Tədbir yadda saxlanmadı.') } finally { setSaving(false) } }
  return <section className="panel event-form"><div className="panel-heading"><div><p className="eyebrow">Tədbir</p><h2>{selected ? 'Tədbiri redaktə et' : 'Yeni tədbir'}</h2></div>{selected ? <button className="text-button" onClick={onCancel}>Yeni tədbirə keç</button> : null}</div><form onSubmit={submit}><label>Tədbirin adı<input name="name" defaultValue={value.name} required maxLength={160} /></label><label>Açıqlama<textarea name="description" defaultValue={value.description} maxLength={2000} rows={3} /></label><div className="form-grid"><label>Tədbir vaxtı<input name="eventDate" type="datetime-local" defaultValue={toInputDate(value.eventDate)} required /></label><label>Timezone<input name="timeZone" defaultValue={value.timeZone} required maxLength={64} /></label><label>Upload başlanğıcı<input name="uploadStartAt" type="datetime-local" defaultValue={toInputDate(value.uploadStartAt)} required /></label><label>Upload sonu<input name="uploadEndAt" type="datetime-local" defaultValue={toInputDate(value.uploadEndAt)} required /></label></div><div className="form-grid"><label>Brend rəngi<input name="brandColor" type="color" defaultValue={value.brandColor || '#805742'} /></label><label>Qonaqlar üçün mesaj<textarea name="customMessage" defaultValue={value.customMessage} maxLength={500} rows={2} placeholder="Xatirələrinizi bizimlə paylaşın." /></label></div><label>Cover şəkli (JPEG, PNG, WEBP; max 10 MB)<input name="cover" type="file" accept="image/jpeg,image/png,image/webp" /></label><label>Status<select name="status" defaultValue={value.status}><option value="Draft">Qaralama</option><option value="Active">Aktiv</option><option value="Completed">Tamamlanıb</option><option value="Archived">Arxivlənib</option></select></label>{message ? <p className="error" role="alert">{message}</p> : null}<button className="primary" disabled={saving}>{saving ? 'Yadda saxlanır...' : selected ? 'Dəyişiklikləri saxla' : 'Tədbir yarat'}</button></form></section>
}

function QrPreview({ item, event }: { item: InvitationToken; event?: EventItem }) {
  const [source, setSource] = useState('')
  const link = `${window.location.origin}/q/${item.token}`
  useEffect(() => { void toDataURL(link, { width: 360, margin: 2, color: { dark: '#4e3b2f', light: '#fffdfa' } }).then(setSource) }, [link])
  function download() { if (!source) return; const anchor = document.createElement('a'); anchor.href = source; anchor.download = `bizden-${item.invitation.label ?? 'qr'}.png`; anchor.click() }
  function downloadPdf() { if (!source) return; downloadBrandedQrPdf({ eventName: event?.name ?? 'Bizdən tədbiri', eventDate: event?.eventDate ?? new Date().toISOString(), label: item.invitation.label, uploadLimit: item.invitation.uploadLimit, source, link, brandColor: event?.brandColor }) }
  return <article className="qr-preview"><img src={source} alt={`${item.invitation.label ?? 'Bizdən'} QR kodu`} /><div><strong>{item.invitation.label ?? 'Yeni QR'}</strong><code>{link}</code><button className="text-button" type="button" onClick={download} disabled={!source}>PNG endir</button><button className="text-button" type="button" onClick={downloadPdf} disabled={!source}>Brendli PDF endir</button></div></article>
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
type GuestUpload = { id: string; file: File; reservationId?: string; progress: number; state: 'uploading' | 'failed' | 'completed'; attempts: number; error?: string }
function putFile(url: string, file: File, onProgress: (progress: number) => void) {
  return new Promise<void>((resolve, reject) => { const xhr = new XMLHttpRequest(); xhr.open('PUT', url); xhr.setRequestHeader('Content-Type', file.type); xhr.upload.onprogress = event => { if (event.lengthComputable) onProgress(Math.round(event.loaded / event.total * 100)) }; xhr.onerror = () => reject(new Error('NETWORK_ERROR')); xhr.onload = () => xhr.status >= 200 && xhr.status < 300 ? resolve() : reject(new Error(`UPLOAD_${xhr.status}`)); xhr.send(file) })
}
function GuestScreen({ token }: { token: string }) {
  const [data, setData] = useState<PublicQr | null>(null); const [message, setMessage] = useState(''); const [uploads, setUploads] = useState<GuestUpload[]>([])
  const cameraInput = useRef<HTMLInputElement>(null); const galleryInput = useRef<HTMLInputElement>(null)
  const load = useCallback(async () => { try { setData(await request<PublicQr>(`/api/public/qr/${token}`)) } catch { setMessage('QR kod oxuna bilmədi.') } }, [token])
  useEffect(() => { void load() }, [load])
  const updateUpload = useCallback((id: string, change: Partial<GuestUpload>) => setUploads(previous => previous.map(item => item.id === id ? { ...item, ...change } : item)), [])
  const upload = useCallback(async function upload(item: GuestUpload) {
    try {
      let reservationId = item.reservationId
      if (!reservationId) {
        const reserved = await request<{ state: string; reservationId?: string }>(`/api/public/qr/${token}/reservations`, { method: 'POST', body: JSON.stringify({ fileName: item.file.name, mimeType: item.file.type, fileSize: item.file.size, idempotencyKey: newIdempotencyKey() }) })
        if (reserved.state !== 'RESERVED' || !reserved.reservationId) throw new Error(reserved.state)
        reservationId = reserved.reservationId; item = { ...item, reservationId }; updateUpload(item.id, { reservationId })
      }
      const signed = await request<{ state: string; url?: string }>(`/api/public/qr/${token}/reservations/${reservationId}/upload-url`, { method: 'POST' })
      if (signed.state !== 'READY' || !signed.url) throw new Error(signed.state)
      await putFile(signed.url, item.file, progress => updateUpload(item.id, { progress }))
      const completed = await request<{ state: string }>(`/api/public/qr/${token}/reservations/${reservationId}/complete`, { method: 'POST' })
      if (completed.state !== 'COMPLETED') throw new Error(completed.state)
      updateUpload(item.id, { state: 'completed', progress: 100, error: undefined }); await load()
    } catch (error) {
      const reason = error instanceof Error ? error.message : 'Yükləmə mümkün olmadı.'
      if (reason === 'NETWORK_ERROR' && item.attempts < 1) { await upload({ ...item, attempts: item.attempts + 1 }); return }
      updateUpload(item.id, { state: 'failed', error: reason })
    }
  }, [load, token, updateUpload])
  async function addFiles(files: FileList | null) {
    const chosen = Array.from(files ?? []); if (!chosen.length) return; setMessage('')
    const pending = chosen.map(file => ({ id: newIdempotencyKey(), file, progress: 0, state: 'uploading' as const, attempts: 0 }))
    setUploads(previous => [...pending, ...previous]); await Promise.all(pending.map(upload))
  }
  async function retry(item: GuestUpload) { const next = { ...item, state: 'uploading' as const, error: undefined, attempts: item.attempts + 1 }; updateUpload(item.id, next); await upload(next) }
  if (!data) return <main className="guest-shell"><p className="muted">Yüklənir...</p></main>
  const copy: Record<string, string> = { NOT_FOUND: 'Bu QR kod tapılmadı.', INACTIVE: 'Bu QR kod deaktiv edilib.', EXPIRED: 'Bu QR kodun vaxtı bitib.', EVENT_UNAVAILABLE: 'Tədbir hazırda aktiv deyil.', NOT_OPEN: 'Foto yükləmə vaxtı hələ başlamayıb.', WINDOW_CLOSED: 'Foto yükləmə vaxtı bitib.', LIMIT_REACHED: 'Bu QR üçün foto limiti dolub.' }
  if (data.state !== 'READY') return <main className="guest-shell"><section className="guest-card"><img className="brand-logo" src="/brand/bizden-logo.png" alt="Bizdən" /><p className="eyebrow">Bizdən</p><h1>{copy[data.state] ?? 'Bu dəvət əlçatan deyil.'}</h1></section></main>
  const busy = uploads.some(item => item.state === 'uploading')
  return <main className="guest-shell"><section className="guest-card"><img className="brand-logo" src="/brand/bizden-logo.png" alt="Bizdən" /><p className="eyebrow">Xatirələri paylaşın</p><h1>{data.eventName}</h1><p className="description">{data.description ?? 'Bu xüsusi günün anlarını bizimlə paylaşın.'}</p><div className="guest-limit"><strong>{data.remainingPhotos}</strong><span>foto haqqı qalıb</span></div><input ref={cameraInput} className="visually-hidden" type="file" accept="image/jpeg,image/png,image/webp" capture="environment" disabled={busy} onChange={event => { void addFiles(event.target.files); event.target.value = '' }} /><input ref={galleryInput} className="visually-hidden" type="file" accept="image/jpeg,image/png,image/webp" multiple disabled={busy} onChange={event => { void addFiles(event.target.files); event.target.value = '' }} /><div className="guest-actions"><button className="primary" type="button" disabled={busy} onClick={() => cameraInput.current?.click()}>Foto çək</button><button className="secondary" type="button" disabled={busy} onClick={() => galleryInput.current?.click()}>Qalereyadan seç</button></div><div className="upload-list" aria-live="polite">{uploads.map(item => <article className="upload-item" key={item.id}><div><strong>{item.file.name}</strong><span>{item.state === 'completed' ? 'Yükləndi' : item.state === 'failed' ? 'Yükləmə alınmadı' : `${item.progress}% yüklənir`}</span></div><div className="progress-track"><span style={{ width: `${item.progress}%` }} /></div>{item.state === 'failed' ? <><p className="error">{item.error === 'NETWORK_ERROR' ? 'Bağlantı kəsildi.' : 'Yükləmə tamamlanmadı.'}</p><button className="text-button" type="button" onClick={() => void retry(item)}>Yenidən cəhd et</button></> : null}</article>)}</div>{message ? <p className="error" role="alert">{message}</p> : null}</section></main>
}

type GalleryShare = { publicId: string; enabledAt: string }
function GalleryShareManager({ event }: { event: EventItem }) {
  const [share, setShare] = useState<GalleryShare | null>(null); const [message, setMessage] = useState(''); const [busy, setBusy] = useState(false)
  const link = share ? `${window.location.origin}/g/${share.publicId}` : ''
  useEffect(() => { void request<GalleryShare | null>(`/api/host/events/${event.id}/gallery-share`).then(setShare).catch(() => setMessage('Paylaşım statusu yüklənmədi.')) }, [event.id])
  async function enable(formEvent: FormEvent<HTMLFormElement>) { formEvent.preventDefault(); const pin = String(new FormData(formEvent.currentTarget).get('pin') ?? ''); setBusy(true); setMessage(''); try { setShare(await request<GalleryShare>(`/api/host/events/${event.id}/gallery-share`, { method: 'POST', body: JSON.stringify({ pin }) })); formEvent.currentTarget.reset() } catch (error) { setMessage(error instanceof Error ? error.message : 'Paylaşım aktiv edilmədi.') } finally { setBusy(false) } }
  async function disable() { if (!window.confirm('Bu link dərhal bağlanacaq. Davam edək?')) return; setBusy(true); try { await request(`/api/host/events/${event.id}/gallery-share`, { method: 'DELETE' }); setShare(null); setMessage('Paylaşım bağlandı.') } catch { setMessage('Paylaşım bağlanmadı.') } finally { setBusy(false) } }
  async function copy() { try { await navigator.clipboard.writeText(link); setMessage('Link kopyalandı.') } catch { setMessage('Linki manual kopyalayın.') } }
  return <section className="panel share-panel"><div className="panel-heading"><div><p className="eyebrow">Paylaşılan qalereya</p><h2>PIN-li link</h2><p className="muted">Qonaq hesab yaratmadan yalnız PIN ilə fotolara baxır.</p></div></div>{share ? <div className="share-link"><code>{link}</code><div className="photo-actions"><button className="text-button" onClick={() => void copy()}>Linki kopyala</button><button className="danger-button" disabled={busy} onClick={() => void disable()}>Paylaşımı bağla</button></div></div> : <form className="share-form" onSubmit={enable}><label>PIN (4–12 rəqəm)<input name="pin" inputMode="numeric" pattern="[0-9]{4,12}" minLength={4} maxLength={12} required /></label><button className="primary" disabled={busy}>{busy ? 'Hazırlanır...' : 'Paylaşımı aktiv et'}</button></form>}{message ? <p className="error" role="alert">{message}</p> : null}</section>
}

type PublicGallery = { eventName: string; description?: string; eventDate: string; brandColor?: string; customMessage?: string; coverUrl?: string; photos: { id: string; originalFileName: string; uploadedAt: string; thumbnailUrl?: string; previewUrl?: string }[] }
function PublicGalleryScreen({ publicId }: { publicId: string }) {
  const [info, setInfo] = useState<PublicGallery | null>(null); const [gallery, setGallery] = useState<PublicGallery | null>(null); const [message, setMessage] = useState(''); const [selected, setSelected] = useState<PublicGallery['photos'][number] | null>(null)
  useEffect(() => { void request<PublicGallery>(`/api/public/galleries/${publicId}`).then(setInfo).catch(() => setMessage('Bu qalereya əlçatan deyil.')) }, [publicId])
  async function unlock(formEvent: FormEvent<HTMLFormElement>) { formEvent.preventDefault(); setMessage(''); try { setGallery(await request<PublicGallery>(`/api/public/galleries/${publicId}/unlock`, { method: 'POST', body: JSON.stringify({ pin: new FormData(formEvent.currentTarget).get('pin') }) })) } catch { setMessage('PIN düzgün deyil və ya qalereya əlçatan deyil.') } }
  if (!info) return <main className="guest-shell"><section className="guest-card"><p className="error">{message || 'Yüklənir...'}</p></section></main>
  if (!gallery) return <main className="guest-shell"><section className="guest-card" style={{ borderColor: info.brandColor }}><img className="brand-logo" src="/brand/bizden-logo.png" alt="Bizdən" />{info.coverUrl ? <img className="gallery-cover" src={info.coverUrl} alt="" /> : null}<p className="eyebrow">Paylaşılan qalereya</p><h1>{info.eventName}</h1><p className="description">{info.customMessage || info.description || 'Fotolara baxmaq üçün hostun paylaşdığı PIN-i daxil edin.'}</p><form onSubmit={unlock}><label>PIN<input name="pin" inputMode="numeric" pattern="[0-9]{4,12}" minLength={4} maxLength={12} required autoFocus /></label><button className="primary" style={{ backgroundColor: info.brandColor }}>Qalereyanı aç</button></form>{message ? <p className="error" role="alert">{message}</p> : null}</section></main>
  return <main className="public-gallery-shell"><header className="public-gallery-head"><img className="header-logo" src="/brand/bizden-logo.png" alt="Bizdən" />{gallery.coverUrl ? <img className="gallery-cover" src={gallery.coverUrl} alt="" /> : null}<div><p className="eyebrow" style={{ color: gallery.brandColor }}>Paylaşılan xatirələr</p><h1>{gallery.eventName}</h1><p className="description">{gallery.customMessage || gallery.description}</p></div></header><div className="public-photo-grid">{gallery.photos.map(photo => <button className="public-photo" key={photo.id} onClick={() => setSelected(photo)}>{photo.thumbnailUrl ? <img src={photo.thumbnailUrl} alt={photo.originalFileName} /> : <span>Önizləmə hazırlanır...</span>}</button>)}</div>{!gallery.photos.length ? <p className="muted">Hələ paylaşılmış foto yoxdur.</p> : null}{selected ? <div className="modal-backdrop" onClick={() => setSelected(null)}><section className="photo-modal" role="dialog" aria-modal="true" onClick={e => e.stopPropagation()}>{selected.previewUrl ? <img src={selected.previewUrl} alt={selected.originalFileName} /> : null}<button className="text-button" onClick={() => setSelected(null)}>Bağla</button></section></div> : null}</main>
}

type HostPhoto = { id: string; invitationId: string; invitationLabel?: string; originalFileName: string; mimeType: string; fileSize: number; uploadedAt: string; thumbnailUrl?: string; previewUrl?: string }
type PhotoPage = { items: HostPhoto[]; page: number; pageSize: number; totalCount: number }
export function LegacyGalleryManager({ event }: { event: EventItem }) {
  const [page, setPage] = useState<PhotoPage | null>(null); const [filter, setFilter] = useState(''); const [invitations, setInvitations] = useState<Invitation[]>([]); const [selected, setSelected] = useState<HostPhoto | null>(null); const [message, setMessage] = useState(''); const [loading, setLoading] = useState(true)
  const load = useCallback(async (targetPage = 1) => { setLoading(true); try { const query = new URLSearchParams({ page: String(targetPage), pageSize: '24' }); if (filter) query.set('invitationId', filter); const [photos, qrItems] = await Promise.all([request<PhotoPage>(`/api/host/events/${event.id}/photos?${query}`), request<Invitation[]>(`/api/host/events/${event.id}/invitations`)]); setPage(photos); setInvitations(qrItems); } catch (error) { setMessage(error instanceof Error ? error.message : 'Fotolar yüklənmədi.') } finally { setLoading(false) } }, [event.id, filter])
  useEffect(() => { void load() }, [load])
  async function download(photo: HostPhoto) { try { const item = await request<{ url: string }>(`/api/host/photos/${photo.id}/download`); window.open(item.url, '_blank', 'noopener,noreferrer') } catch { setMessage('Foto endirmə linki yaradıla bilmədi.') } }
  async function exportZip() { try { const response = await fetch(`/api/host/events/${event.id}/photos/export`, { credentials: 'include' }); if (!response.ok) throw new Error(); const blob = await response.blob(); const url = URL.createObjectURL(blob); const anchor = document.createElement('a'); anchor.href = url; anchor.download = `${event.name}.zip`; anchor.click(); URL.revokeObjectURL(url) } catch { setMessage('ZIP export hazırlana bilmədi.') } }
  async function remove(photo: HostPhoto) { if (!window.confirm(`“${photo.originalFileName}” silinsin? Bu əməliyyat geri qaytarılmır.`)) return; try { await request(`/api/host/photos/${photo.id}`, { method: 'DELETE' }); setSelected(null); await load(page?.page ?? 1) } catch { setMessage('Foto silinə bilmədi.') } }
  const totalPages = page ? Math.max(1, Math.ceil(page.totalCount / page.pageSize)) : 1
  return <section className="panel gallery-panel"><div className="panel-heading"><div><p className="eyebrow">Foto qalereyası</p><h2>Foto qalereyası</h2><p className="muted">{page?.totalCount ?? 0} foto</p></div><div className="gallery-tools"><button className="text-button" onClick={() => void exportZip()}>ZIP endir</button><label className="gallery-filter">QR filtri<select value={filter} onChange={e => { setFilter(e.target.value); setSelected(null) }}><option value="">Bütün QR-lər</option>{invitations.map(item => <option key={item.id} value={item.id}>{item.label ?? 'Adsız QR'}</option>)}</select></label></div></div>{message ? <p className="error" role="alert">{message}</p> : null}{loading ? <p className="muted">Fotolar yüklənir...</p> : null}{!loading && !page?.items.length ? <p className="muted empty">Bu filtr üzrə foto yoxdur.</p> : null}<div className="photo-grid">{page?.items.map(photo => <article className="photo-card" key={photo.id}><button className="photo-preview" type="button" onClick={() => setSelected(photo)}>{photo.thumbnailUrl ? <img src={photo.thumbnailUrl} alt={`${photo.originalFileName} önizləmə`} /> : <span>Önizləmə hazırlanır...</span>}</button><div><strong title={photo.originalFileName}>{photo.originalFileName}</strong><span>{photo.invitationLabel ?? 'Adsız QR'} · {formatDate(photo.uploadedAt)}</span><div className="photo-actions"><button className="text-button" onClick={() => void download(photo)}>Endir</button><button className="danger-button" onClick={() => void remove(photo)}>Sil</button></div></div></article>)}</div>{page && page.totalCount > page.pageSize ? <div className="pagination"><button className="text-button" disabled={page.page <= 1} onClick={() => void load(page.page - 1)}>← Əvvəlki</button><span>{page.page} / {totalPages}</span><button className="text-button" disabled={page.page >= totalPages} onClick={() => void load(page.page + 1)}>Növbəti →</button></div> : null}{selected ? <div className="modal-backdrop" role="presentation" onClick={() => setSelected(null)}><section className="photo-modal" role="dialog" aria-modal="true" aria-label={selected.originalFileName} onClick={e => e.stopPropagation()}>{selected.previewUrl ? <img src={selected.previewUrl} alt={selected.originalFileName} /> : null}<div><strong>{selected.originalFileName}</strong><div className="photo-actions"><button className="text-button" onClick={() => void download(selected)}>Orijinalı endir</button><button className="danger-button" onClick={() => void remove(selected)}>Sil</button><button className="text-button" onClick={() => setSelected(null)}>Bağla</button></div></div></section></div> : null}</section>
}

function Dashboard({ session, onLogout }: { session: Session; onLogout: () => void }) {
  const [events, setEvents] = useState<EventItem[]>([]); const [selectedId, setSelectedId] = useState<string | null>(null); const [loading, setLoading] = useState(true); const [message, setMessage] = useState(''); const selected = events.find(item => item.id === selectedId) ?? null
  const load = useCallback(async () => { try { const result = await request<EventItem[]>('/api/host/events/'); setEvents(result) } catch (error) { setMessage(error instanceof Error ? error.message : 'Tədbirlər yüklənmədi.') } finally { setLoading(false) } }, [])
  useEffect(() => { void load() }, [load])
  function saved(event: EventItem) { setEvents(previous => { const index = previous.findIndex(item => item.id === event.id); return index === -1 ? [event, ...previous] : previous.map(item => item.id === event.id ? event : item) }); setSelectedId(event.id) }
  async function logout() { await request('/api/host/auth/logout', { method: 'POST' }); onLogout() }
  return <main className="dashboard-shell"><header className="dashboard-header"><img className="header-logo" src="/brand/bizden-logo.png" alt="Bizdən" /><div><strong>{session.name}</strong><span>{session.email}</span></div><button className="text-button" onClick={() => void logout()}>Çıxış</button></header><div className="dashboard-grid"><aside className="event-list panel"><div className="panel-heading"><div><p className="eyebrow">Tədbirlər</p><h2>Dashboard</h2></div><button className="text-button" onClick={() => setSelectedId(null)}>+ Yeni</button></div>{loading ? <p className="muted">Yüklənir...</p> : null}{message ? <p className="error">{message}</p> : null}{events.map(item => <button className={`event-row ${item.id === selectedId ? 'selected' : ''}`} key={item.id} onClick={() => setSelectedId(item.id)}><strong>{item.name}</strong><span>{formatDate(item.eventDate)} · {item.status}</span><small>{item.invitationCount} QR kod</small></button>)}{!loading && !events.length ? <p className="muted empty">İlk tədbirinizi yaradın.</p> : null}</aside><div className="workspace"><EventForm key={selected?.id ?? 'new'} selected={selected} onSaved={saved} onCancel={() => setSelectedId(null)} />{selected ? <><QrManager key={selected.id} event={selected} /><GalleryManager key={`gallery-${selected.id}`} event={selected} /><GalleryShareManager key={`share-${selected.id}`} event={selected} /></> : null}</div></div></main>
}

function HostApp() {
  const [session, setSession] = useState<Session | null>(null); const [checked, setChecked] = useState(false)
  useEffect(() => { void request<Session>('/api/host/auth/me').then(setSession).catch(() => null).finally(() => setChecked(true)) }, [])
  if (!checked) return <main className="app-shell"><p className="muted">Yüklənir...</p></main>
  if (session && window.location.pathname === '/profile') return <ProfileScreen session={session} onSessionChanged={setSession} onBack={() => { window.location.assign('/') }} />
  if (session && window.location.pathname === '/admin') return <AdminScreen session={session} onBack={() => { window.location.assign('/') }} />
  return session ? <><HostNavigation session={session} /><Dashboard session={session} onLogout={() => setSession(null)} /></> : <AuthScreen onAuthenticated={setSession} />
}

export default function App() {
  const token = window.location.pathname.match(/^\/q\/([^/]+)$/)?.[1]
  const publicId = window.location.pathname.match(/^\/g\/([0-9a-f-]{36})$/i)?.[1]
  return <>{token ? <GuestScreen token={token} /> : publicId ? <PublicGalleryScreen publicId={publicId} /> : <HostApp />}<LanguageSwitcher /></>
}
