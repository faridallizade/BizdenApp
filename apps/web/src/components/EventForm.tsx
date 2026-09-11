import { useState } from 'react'
import type { FormEvent } from 'react'
import { request } from '../lib/api'
import { blankEvent, toApiDate, toInputDate, type EventItem } from '../lib/eventTypes'
import { putFile } from '../lib/upload'

export function EventForm({ selected, onSaved, onCancel }: { selected: EventItem | null; onSaved: (event: EventItem) => void; onCancel: () => void }) {
  const [message, setMessage] = useState('')
  const [saving, setSaving] = useState(false)
  const value = selected ?? blankEvent()
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); const form = new FormData(event.currentTarget); setSaving(true); setMessage('')
    const body = { name: form.get('name'), description: form.get('description') || null, eventDate: toApiDate(String(form.get('eventDate'))), timeZone: form.get('timeZone'), uploadStartAt: toApiDate(String(form.get('uploadStartAt'))), uploadEndAt: toApiDate(String(form.get('uploadEndAt'))), status: form.get('status'), brandColor: form.get('brandColor') || null, customMessage: form.get('customMessage') || null }
    try {
      let saved = await request<EventItem>(selected ? `/api/host/events/${selected.id}` : '/api/host/events/', { method: selected ? 'PUT' : 'POST', body: JSON.stringify(body) })
      const cover = form.get('cover') as File | null
      if (cover?.size) {
        const prepared = await request<{ key: string; url: string }>(`/api/host/events/${saved.id}/cover/upload-url`, { method: 'POST', body: JSON.stringify({ fileName: cover.name, mimeType: cover.type, fileSize: cover.size }) })
        await putFile(prepared.url, cover, () => undefined)
        saved = await request<EventItem>(`/api/host/events/${saved.id}/cover/complete`, { method: 'POST', body: JSON.stringify({ key: prepared.key, mimeType: cover.type, fileSize: cover.size }) })
      }
      onSaved(saved)
    } catch (error) { setMessage(error instanceof Error ? error.message : 'Tədbir yadda saxlanmadı.') } finally { setSaving(false) }
  }
  return <section className="panel event-form"><div className="panel-heading"><div><p className="eyebrow">Tədbir</p><h2>{selected ? 'Tədbiri redaktə et' : 'Yeni tədbir'}</h2></div>{selected ? <button className="text-button" onClick={onCancel}>Yeni tədbirə keç</button> : null}</div><form onSubmit={submit}><label>Tədbirin adı<input name="name" defaultValue={value.name} required maxLength={160} /></label><label>Açıqlama<textarea name="description" defaultValue={value.description} maxLength={2000} rows={3} /></label><div className="form-grid"><label>Tədbir vaxtı<input name="eventDate" type="datetime-local" defaultValue={toInputDate(value.eventDate)} required /></label><label>Timezone<input name="timeZone" defaultValue={value.timeZone} required maxLength={64} /></label><label>Upload başlanğıcı<input name="uploadStartAt" type="datetime-local" defaultValue={toInputDate(value.uploadStartAt)} required /></label><label>Upload sonu<input name="uploadEndAt" type="datetime-local" defaultValue={toInputDate(value.uploadEndAt)} required /></label></div><div className="form-grid"><label>Brend rəngi<input name="brandColor" type="color" defaultValue={value.brandColor || '#805742'} /></label><label>Qonaqlar üçün mesaj<textarea name="customMessage" defaultValue={value.customMessage} maxLength={500} rows={2} placeholder="Xatirələrinizi bizimlə paylaşın." /></label></div><label>Cover şəkli (JPEG, PNG, WEBP; max 10 MB)<input name="cover" type="file" accept="image/jpeg,image/png,image/webp" /></label><label>Status<select name="status" defaultValue={value.status}><option value="Draft">Qaralama</option><option value="Active">Aktiv</option><option value="Completed">Tamamlanıb</option><option value="Archived">Arxivlənib</option></select></label>{message ? <p className="error" role="alert">{message}</p> : null}<button className="primary" disabled={saving}>{saving ? 'Yadda saxlanır...' : selected ? 'Dəyişiklikləri saxla' : 'Tədbir yarat'}</button></form></section>
}
