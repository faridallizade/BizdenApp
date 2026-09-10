import { useCallback, useEffect, useMemo, useState } from 'react'
import { request } from '../lib/api'

type EventItem = { id: string; name: string }
type Invitation = { id: string; label?: string }
type HostPhoto = { id: string; invitationId: string; invitationLabel?: string; originalFileName: string; uploadedAt: string; thumbnailUrl?: string }
type PhotoPage = { items: HostPhoto[]; page: number; pageSize: number; totalCount: number }
type ExportJob = { id: string; status: 'Queued' | 'Processing' | 'Ready' | 'Failed' | 'Expired'; fileName?: string; downloadUrl?: string; error?: string; createdAt: string }
type SharedGallery = { id: string; publicId: string; name: string; photoCount: number }

export function GalleryManager({ event }: { event: EventItem }) {
  const [page, setPage] = useState<PhotoPage | null>(null)
  const [invitations, setInvitations] = useState<Invitation[]>([])
  const [filter, setFilter] = useState('')
  const [selectedIds, setSelectedIds] = useState<Set<string>>(() => new Set())
  const [allMatching, setAllMatching] = useState(false)
  const [exports, setExports] = useState<ExportJob[]>([])
  const [galleries, setGalleries] = useState<SharedGallery[]>([])
  const [message, setMessage] = useState('')
  const [busy, setBusy] = useState(false)

  const load = useCallback(async (targetPage = 1) => {
    try {
      const query = new URLSearchParams({ page: String(targetPage), pageSize: '24' }); if (filter) query.set('invitationId', filter)
      const [photos, qrItems, jobs, links] = await Promise.all([
        request<PhotoPage>(`/api/host/events/${event.id}/photos?${query}`),
        request<Invitation[]>(`/api/host/events/${event.id}/invitations`),
        request<ExportJob[]>(`/api/host/events/${event.id}/photos/exports`),
        request<SharedGallery[]>(`/api/host/events/${event.id}/galleries`)
      ])
      setPage(photos); setInvitations(qrItems); setExports(jobs); setGalleries(links)
    } catch (error) { setMessage(error instanceof Error ? error.message : 'Qalereya yüklənmədi.') }
  }, [event.id, filter])
  useEffect(() => { setSelectedIds(new Set()); setAllMatching(false); void load() }, [load])
  useEffect(() => { const timer = window.setInterval(() => { if (exports.some(job => job.status === 'Queued' || job.status === 'Processing')) void load(page?.page ?? 1) }, 5000); return () => window.clearInterval(timer) }, [exports, load, page?.page])

  const selectedCount = allMatching ? page?.totalCount ?? 0 : selectedIds.size
  const currentPageSelected = useMemo(() => page?.items.length ? page.items.every(photo => selectedIds.has(photo.id)) : false, [page, selectedIds])
  const togglePage = () => setSelectedIds(previous => { const next = new Set(previous); page?.items.forEach(photo => currentPageSelected ? next.delete(photo.id) : next.add(photo.id)); return next })
  const togglePhoto = (id: string) => setSelectedIds(previous => { const next = new Set(previous); next.has(id) ? next.delete(id) : next.add(id); return next })
  const clearSelection = () => { setSelectedIds(new Set()); setAllMatching(false) }

  async function removeSelected() {
    if (!selectedCount || !window.confirm(`${selectedCount} foto silinsin? Bu əməliyyat soft-delete edir; storage təmizlənməsi worker ilə aparılır.`)) return
    setBusy(true); setMessage('')
    try { await request('/api/host/photos/bulk-delete', { method: 'POST', body: JSON.stringify({ eventId: event.id, photoIds: [...selectedIds], invitationId: filter || null, allMatching }) }); clearSelection(); await load(page?.page ?? 1) }
    catch (error) { setMessage(error instanceof Error ? error.message : 'Fotolar silinmədi.') } finally { setBusy(false) }
  }

  async function createGallery(form: HTMLFormElement) {
    const formData = new FormData(form); if (!selectedCount) { setMessage('Əvvəl paylaşılacaq fotoları seçin.'); return }
    setBusy(true); setMessage('')
    try { await request(`/api/host/events/${event.id}/galleries`, { method: 'POST', body: JSON.stringify({ name: formData.get('name'), pin: formData.get('pin'), photoIds: [...selectedIds], invitationId: filter || null, allMatching }) }); form.reset(); clearSelection(); await load(page?.page ?? 1); setMessage('Yeni public gallery linki yaradıldı.') }
    catch (error) { setMessage(error instanceof Error ? error.message : 'Public gallery yaradıla bilmədi.') } finally { setBusy(false) }
  }

  async function queueExport() {
    setBusy(true); setMessage('')
    try { await request(`/api/host/events/${event.id}/photos/export`, { method: 'POST' }); await load(page?.page ?? 1); setMessage('ZIP export növbəyə əlavə edildi. Hazır olduqda email gələcək.') }
    catch (error) { setMessage(error instanceof Error ? error.message : 'Export başladılmadı.') } finally { setBusy(false) }
  }

  async function copy(publicId: string) { try { await navigator.clipboard.writeText(`${window.location.origin}/g/${publicId}`); setMessage('Link kopyalandı.') } catch { setMessage('Linki manual kopyalayın.') } }
  const totalPages = page ? Math.max(1, Math.ceil(page.totalCount / page.pageSize)) : 1
  return <section className="panel gallery-panel"><div className="panel-heading"><div><p className="eyebrow">Foto qalereyası</p><h2>{page?.totalCount ?? 0} foto</h2><p className="muted">{selectedCount ? `${selectedCount} seçilib` : 'Seçim edin və public gallery yaradın.'}</p></div><div className="gallery-tools"><button className="text-button" type="button" disabled={busy} onClick={() => void queueExport()}>ZIP hazırla</button><label className="gallery-filter">QR filtri<select value={filter} onChange={item => setFilter(item.target.value)}><option value="">Bütün QR-lər</option>{invitations.map(item => <option key={item.id} value={item.id}>{item.label ?? 'Adsız QR'}</option>)}</select></label></div></div><div className="bulk-actions"><button className="text-button" type="button" onClick={togglePage}>{currentPageSelected ? 'Səhifə seçimini ləğv et' : 'Səhifəni seç'}</button><button className="text-button" type="button" onClick={() => { setAllMatching(value => !value); setSelectedIds(new Set()) }}>{allMatching ? 'Bütün nəticələr seçimini ləğv et' : 'Bu filtr üzrə hamısını seç'}</button>{selectedCount ? <><button className="text-button" type="button" onClick={clearSelection}>Seçimi təmizlə</button><button className="danger-button" type="button" disabled={busy} onClick={() => void removeSelected}>Seçilənləri sil</button></> : null}</div><form className="share-form" onSubmit={formEvent => { formEvent.preventDefault(); void createGallery(formEvent.currentTarget) }}><label>Public gallery adı<input name="name" maxLength={120} required placeholder="Məsələn: Ailə fotoları" /></label><label>PIN (4–12 rəqəm)<input name="pin" inputMode="numeric" pattern="[0-9]{4,12}" minLength={4} maxLength={12} required /></label><button className="primary" disabled={busy || !selectedCount}>Seçilənlərdən link yarat</button></form>{message ? <p className="error" role="alert">{message}</p> : null}<div className="photo-grid">{page?.items.map(photo => <article className="photo-card" key={photo.id}><label className="photo-select"><input type="checkbox" checked={allMatching || selectedIds.has(photo.id)} onChange={() => togglePhoto(photo.id)} disabled={allMatching} /><span>Seç</span></label><div className="photo-preview">{photo.thumbnailUrl ? <img src={photo.thumbnailUrl} alt={`${photo.originalFileName} önizləmə`} /> : <span>Önizləmə hazırlanır...</span>}</div><div><strong title={photo.originalFileName}>{photo.originalFileName}</strong><span>{photo.invitationLabel ?? 'Adsız QR'}</span></div></article>)}</div>{page && page.totalCount > page.pageSize ? <div className="pagination"><button className="text-button" disabled={page.page <= 1} onClick={() => void load(page.page - 1)}>← Əvvəlki</button><span>{page.page} / {totalPages}</span><button className="text-button" disabled={page.page >= totalPages} onClick={() => void load(page.page + 1)}>Növbəti →</button></div> : null}<div className="share-links">{galleries.map(gallery => <article className="share-link" key={gallery.id}><strong>{gallery.name}</strong><span>{gallery.photoCount} foto</span><code>{`${window.location.origin}/g/${gallery.publicId}`}</code><button className="text-button" type="button" onClick={() => void copy(gallery.publicId)}>Linki kopyala</button></article>)}</div><div className="export-list">{exports.map(job => <p className="muted" key={job.id}>ZIP: {job.status}{job.downloadUrl ? <a href={job.downloadUrl}> — endir</a> : null}{job.error ? ` — ${job.error}` : null}</p>)}</div></section>
}
