import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { request } from '../lib/api'
import type { Session } from './AuthScreen'

export function ProfileScreen({ session, onSessionChanged, onBack }: { session: Session; onSessionChanged: (session: Session) => void; onBack: () => void }) {
  const [message, setMessage] = useState('')
  const [busy, setBusy] = useState(false)
  const [pendingEmail, setPendingEmail] = useState(false)
  const [current, setCurrent] = useState(session)
  useEffect(() => { void request<Session>('/api/host/auth/me').then(setCurrent).catch(() => undefined) }, [])

  async function updateName(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true); setMessage('')
    try { const next = await request<Session>('/api/host/auth/profile', { method: 'PUT', body: JSON.stringify({ name: new FormData(event.currentTarget).get('name') }) }); setCurrent(next); onSessionChanged(next); setMessage('Profil yeniləndi.') }
    catch (error) { setMessage(error instanceof Error ? error.message : 'Profil yenilənmədi.') } finally { setBusy(false) }
  }

  async function requestEmailChange(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true); setMessage('')
    try {
      const result = await request<Session | { requiresEmailVerification: true }>('/api/host/auth/email-change/request', { method: 'POST', body: JSON.stringify({ email: new FormData(event.currentTarget).get('email') }) })
      if ('id' in result) { setCurrent(result); onSessionChanged(result); setMessage('Email dəyişdirildi.') } else { setPendingEmail(true); setMessage('Yeni email ünvanına 6 rəqəmli kod göndərildi.') }
    } catch (error) { setMessage(error instanceof Error ? error.message : 'Email dəyişikliyi başladılmadı.') } finally { setBusy(false) }
  }

  async function confirmEmailChange(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true); setMessage('')
    try { const next = await request<Session>('/api/host/auth/email-change/confirm', { method: 'POST', body: JSON.stringify({ code: new FormData(event.currentTarget).get('code') }) }); setCurrent(next); onSessionChanged(next); setPendingEmail(false); setMessage('Email ünvanı yeniləndi.') }
    catch (error) { setMessage(error instanceof Error ? error.message : 'Kod etibarsızdır.') } finally { setBusy(false) }
  }

  return <main className="app-shell"><section className="auth-card profile-card"><button type="button" className="text-button" onClick={onBack}>← Dashboard</button><p className="eyebrow">Profilim</p><h1>Hesab ayarları</h1><p className="description">{current.email}</p><form onSubmit={updateName}><label>Ad<input name="name" defaultValue={current.name} maxLength={120} required /></label><button className="primary" disabled={busy}>Adı yenilə</button></form><hr /><form onSubmit={requestEmailChange}><label>Yeni email<input name="email" type="email" maxLength={256} required /></label><button className="secondary" disabled={busy}>Emaili dəyiş</button></form>{pendingEmail ? <form onSubmit={confirmEmailChange}><label>Email təsdiq kodu<input name="code" inputMode="numeric" pattern="[0-9]{6}" minLength={6} maxLength={6} required /></label><button className="primary" disabled={busy}>Emaili təsdiqlə</button></form> : null}{message ? <p className="error" role="alert">{message}</p> : null}</section></main>
}
