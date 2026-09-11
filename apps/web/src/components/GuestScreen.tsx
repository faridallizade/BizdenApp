import { useCallback, useEffect, useRef, useState } from 'react'
import { request } from '../lib/api'
import { newIdempotencyKey, putFile } from '../lib/upload'

type PublicQr = { state: string; eventName?: string; description?: string; remainingPhotos: number }
type GuestUpload = { id: string; file: File; reservationId?: string; progress: number; state: 'uploading' | 'failed' | 'completed'; attempts: number; error?: string }

export function GuestScreen({ token }: { token: string }) {
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
  async function addFiles(files: FileList | null) { const chosen = Array.from(files ?? []); if (!chosen.length) return; setMessage(''); const pending = chosen.map(file => ({ id: newIdempotencyKey(), file, progress: 0, state: 'uploading' as const, attempts: 0 })); setUploads(previous => [...pending, ...previous]); await Promise.all(pending.map(upload)) }
  async function retry(item: GuestUpload) { const next = { ...item, state: 'uploading' as const, error: undefined, attempts: item.attempts + 1 }; updateUpload(item.id, next); await upload(next) }
  if (!data) return <main className="guest-shell"><p className="muted">Yüklənir...</p></main>
  const copy: Record<string, string> = { NOT_FOUND: 'Bu QR kod tapılmadı.', INACTIVE: 'Bu QR kod deaktiv edilib.', EXPIRED: 'Bu QR kodun vaxtı bitib.', EVENT_UNAVAILABLE: 'Tədbir hazırda aktiv deyil.', NOT_OPEN: 'Foto yükləmə vaxtı hələ başlamayıb.', WINDOW_CLOSED: 'Foto yükləmə vaxtı bitib.', LIMIT_REACHED: 'Bu QR üçün foto limiti dolub.' }
  if (data.state !== 'READY') return <main className="guest-shell"><section className="guest-card"><img className="brand-logo" src="/brand/bizden-logo.png" alt="Bizdən" /><p className="eyebrow">Bizdən</p><h1>{copy[data.state] ?? 'Bu dəvət əlçatan deyil.'}</h1></section></main>
  const busy = uploads.some(item => item.state === 'uploading')
  return <main className="guest-shell"><section className="guest-card"><img className="brand-logo" src="/brand/bizden-logo.png" alt="Bizdən" /><p className="eyebrow">Xatirələri paylaşın</p><h1>{data.eventName}</h1><p className="description">{data.description ?? 'Bu xüsusi günün anlarını bizimlə paylaşın.'}</p><div className="guest-limit"><strong>{data.remainingPhotos}</strong><span>foto haqqı qalıb</span></div><input ref={cameraInput} className="visually-hidden" type="file" accept="image/jpeg,image/png,image/webp" capture="environment" disabled={busy} onChange={event => { void addFiles(event.target.files); event.target.value = '' }} /><input ref={galleryInput} className="visually-hidden" type="file" accept="image/jpeg,image/png,image/webp" multiple disabled={busy} onChange={event => { void addFiles(event.target.files); event.target.value = '' }} /><div className="guest-actions"><button className="primary" type="button" disabled={busy} onClick={() => cameraInput.current?.click()}>Foto çək</button><button className="secondary" type="button" disabled={busy} onClick={() => galleryInput.current?.click()}>Qalereyadan seç</button></div><div className="upload-list" aria-live="polite">{uploads.map(item => <article className="upload-item" key={item.id}><div><strong>{item.file.name}</strong><span>{item.state === 'completed' ? 'Yükləndi' : item.state === 'failed' ? 'Yükləmə alınmadı' : `${item.progress}% yüklənir`}</span></div><div className="progress-track"><span style={{ width: `${item.progress}%` }} /></div>{item.state === 'failed' ? <><p className="error">{item.error === 'NETWORK_ERROR' ? 'Bağlantı kəsildi.' : 'Yükləmə tamamlanmadı.'}</p><button className="text-button" type="button" onClick={() => void retry(item)}>Yenidən cəhd et</button></> : null}</article>)}</div>{message ? <p className="error" role="alert">{message}</p> : null}</section></main>
}
