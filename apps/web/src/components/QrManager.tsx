import { useCallback, useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { toDataURL } from 'qrcode'
import { request } from '../lib/api'
import { type EventItem, type Invitation } from '../lib/eventTypes'
import { downloadBrandedQrPdf, downloadBrandedQrPdfBatch } from '../lib/qrPdf'

type InvitationToken = { invitation: Invitation; token: string }

function QrPreview({ item, event }: { item: InvitationToken; event: EventItem }) {
  const [source, setSource] = useState('')
  const link = `${window.location.origin}/q/${item.token}`
  useEffect(() => { void toDataURL(link, { width: 360, margin: 2, color: { dark: '#4e3b2f', light: '#fffdfa' } }).then(setSource) }, [link])
  function download() { if (!source) return; const anchor = document.createElement('a'); anchor.href = source; anchor.download = `bizden-${item.invitation.label ?? 'qr'}.png`; anchor.click() }
  function downloadPdf() { if (!source) return; downloadBrandedQrPdf({ eventName: event.name, eventDate: event.eventDate, label: item.invitation.label, uploadLimit: item.invitation.uploadLimit, source, link, brandColor: event.brandColor }) }
  return <article className="qr-preview"><img src={source} alt={`${item.invitation.label ?? 'Bizdən'} QR kodu`} /><div><strong>{item.invitation.label ?? 'Yeni QR'}</strong><code>{link}</code><button className="text-button" type="button" onClick={download} disabled={!source}>PNG endir</button><button className="text-button" type="button" onClick={downloadPdf} disabled={!source}>Brendli PDF endir</button></div></article>
}

export function QrManager({ event }: { event: EventItem }) {
  const [invitations, setInvitations] = useState<Invitation[]>([]); const [tokens, setTokens] = useState<InvitationToken[]>([]); const [message, setMessage] = useState(''); const [busy, setBusy] = useState(false)
  const load = useCallback(async () => { try { setInvitations(await request<Invitation[]>(`/api/host/events/${event.id}/invitations`)) } catch (error) { setMessage(error instanceof Error ? error.message : 'QR-lər yüklənmədi.') } }, [event.id])
  useEffect(() => { void load() }, [load])
  async function create(eventData: FormEvent<HTMLFormElement>) { eventData.preventDefault(); const form = new FormData(eventData.currentTarget); setBusy(true); setMessage(''); try { const result = await request<{ invitations: InvitationToken[] }>(`/api/host/events/${event.id}/invitations`, { method: 'POST', body: JSON.stringify({ label: form.get('label') || null, uploadLimit: Number(form.get('uploadLimit')), count: Number(form.get('count')) }) }); setTokens(result.invitations); await load(); eventData.currentTarget.reset() } catch (error) { setMessage(error instanceof Error ? error.message : 'QR yaradıla bilmədi.') } finally { setBusy(false) } }
  async function regenerate(id: string) { if (!window.confirm('Köhnə QR dərhal deaktiv olacaq. Davam edək?')) return; setBusy(true); setMessage(''); try { const result = await request<InvitationToken>(`/api/host/events/${event.id}/invitations/${id}/regenerate`, { method: 'POST' }); setTokens([result]); await load() } catch (error) { setMessage(error instanceof Error ? error.message : 'QR yenilənmədi.') } finally { setBusy(false) } }
  async function downloadBatchPdf() {
    if (!tokens.length) return
    setBusy(true); setMessage('')
    try {
      const items = await Promise.all(tokens.map(async item => ({ eventName: event.name, eventDate: event.eventDate, label: item.invitation.label, uploadLimit: item.invitation.uploadLimit, source: await toDataURL(`${window.location.origin}/q/${item.token}`, { width: 360, margin: 2, color: { dark: '#4e3b2f', light: '#fffdfa' } }), link: `${window.location.origin}/q/${item.token}`, brandColor: event.brandColor })))
      downloadBrandedQrPdfBatch(items)
    } catch (error) { setMessage(error instanceof Error ? error.message : 'Batch PDF yaradıla bilmədi.') } finally { setBusy(false) }
  }
  async function toggle(item: Invitation) { setBusy(true); setMessage(''); try { await request(`/api/host/events/${event.id}/invitations/${item.id}`, { method: 'PATCH', body: JSON.stringify({ label: item.label ?? null, uploadLimit: item.uploadLimit, expiresAt: item.expiresAt ?? null, isActive: !item.isActive }) }); await load() } catch (error) { setMessage(error instanceof Error ? error.message : 'QR statusu dəyişmədi.') } finally { setBusy(false) } }
  return <section className="panel qr-panel"><div className="panel-heading"><div><p className="eyebrow">Qonaq dəvətləri</p><h2>QR kodlar</h2><p className="muted">{event.name} · {invitations.length} QR</p></div></div><form className="qr-create" onSubmit={create}><label>QR etiketi<input name="label" placeholder="Məsələn: Ana masa" maxLength={120} /></label><label>Foto limiti<input name="uploadLimit" type="number" min="1" max="10000" defaultValue="15" required /></label><label>Sayı<input name="count" type="number" min="1" max="50" defaultValue="1" required /></label><button className="primary" disabled={busy}>QR yarat</button></form>{message ? <p className="error" role="alert">{message}</p> : null}{tokens.length ? <div className="token-box"><strong>Yeni QR-lər — indi endirin və ya kopyalayın.</strong><p>Raw token bazada saxlanmır; səhifə yenilənəndə QR-lər yenidən görünməyəcək.</p><button className="text-button" type="button" disabled={busy} onClick={() => void downloadBatchPdf}>Bütün yeniləri bir PDF-də endir</button><div className="qr-preview-list">{tokens.map(item => <QrPreview key={item.invitation.id} item={item} event={event} />)}</div></div> : null}<div className="invitation-list">{invitations.map(item => <article className="invitation" key={item.id}><div><strong>{item.label ?? 'Adsız QR'}</strong><p>{item.completedUploads}/{item.uploadLimit} foto · {item.isActive ? 'Aktiv' : 'Deaktiv'}</p></div><div className="invitation-actions"><button className="text-button" disabled={busy} onClick={() => void toggle(item)}>{item.isActive ? 'Deaktiv et' : 'Aktiv et'}</button><button className="text-button" disabled={busy} onClick={() => void regenerate(item.id)}>Yenilə</button></div></article>)}{!invitations.length ? <p className="muted empty">Hələ QR kod yoxdur.</p> : null}</div></section>
}
