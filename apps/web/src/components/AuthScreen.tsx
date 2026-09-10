import { useState } from 'react'
import type { FormEvent } from 'react'
import { request } from '../lib/api'

export type Session = { id: string; name: string; email: string; isAdmin?: boolean }
type AuthMode = 'login' | 'register' | 'verify' | 'reset-request' | 'reset-confirm'

export function AuthScreen({ onAuthenticated }: { onAuthenticated: (session: Session) => void }) {
  const [mode, setMode] = useState<AuthMode>('login')
  const [email, setEmail] = useState('')
  const [message, setMessage] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [showPassword, setShowPassword] = useState(false)
  const reset = (next: AuthMode) => { setMode(next); setMessage('') }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    const submittedEmail = String(form.get('email') ?? email)
    setIsSubmitting(true); setMessage('')
    try {
      if (mode === 'verify') {
        onAuthenticated(await request<Session>('/api/host/auth/verify-email', { method: 'POST', body: JSON.stringify({ email, code: form.get('code') }) }))
      } else if (mode === 'reset-request') {
        await request('/api/host/auth/password-reset/request', { method: 'POST', body: JSON.stringify({ email: submittedEmail }) })
        setEmail(submittedEmail); reset('reset-confirm'); setMessage('Kod göndərildisə, aşağıdakı formda yeni şifrə təyin edin.')
      } else if (mode === 'reset-confirm') {
        await request('/api/host/auth/password-reset/confirm', { method: 'POST', body: JSON.stringify({ email, code: form.get('code'), password: form.get('password') }) })
        reset('login'); setMessage('Şifrəniz yeniləndi. İndi daxil ola bilərsiniz.')
      } else {
        const isRegistering = mode === 'register'
        const body = isRegistering ? { name: form.get('name'), email: submittedEmail, password: form.get('password') } : { email: submittedEmail, password: form.get('password') }
        const result = await request<Session | { requiresEmailVerification: true }>(`/api/host/auth/${isRegistering ? 'register' : 'login'}`, { method: 'POST', body: JSON.stringify(body) })
        if ('requiresEmailVerification' in result) { setEmail(submittedEmail); reset('verify') } else onAuthenticated(result)
      }
    } catch (error) { setMessage(error instanceof Error ? error.message : 'Sorğu tamamlanmadı.') } finally { setIsSubmitting(false) }
  }

  async function resend() {
    setIsSubmitting(true); setMessage('')
    try { await request('/api/host/auth/resend-verification', { method: 'POST', body: JSON.stringify({ email }) }); setMessage('Yeni kod göndərildi.') }
    catch (error) { setMessage(error instanceof Error ? error.message : 'Kod yenidən göndərilə bilmədi.') }
    finally { setIsSubmitting(false) }
  }

  const isRegistering = mode === 'register'
  const isReset = mode === 'reset-request' || mode === 'reset-confirm'
  const title = mode === 'verify' ? 'Kodu daxil edin' : mode === 'reset-request' ? 'Şifrəni sıfırla' : mode === 'reset-confirm' ? 'Yeni şifrə' : isRegistering ? 'Hesab yaradın' : 'Xoş gördük'
  return <main className="app-shell"><section className="auth-card" aria-labelledby="page-title"><img className="brand-logo" src="/brand/bizden-logo.png" alt="Bizdən — Anılarınız, bizdən." /><p className="eyebrow">{mode === 'verify' ? 'Email təsdiqi' : isReset ? 'Hesab bərpası' : 'Host portal'}</p><h1 id="page-title">{title}</h1><p className="description">{mode === 'verify' ? `${email} ünvanına göndərilən 6 rəqəmli kodu yazın.` : isReset ? 'Email ünvanınıza gələn kod ilə yeni şifrə təyin edin.' : 'Tədbir xatirələrinizi idarə etmək üçün daxil olun.'}</p><form onSubmit={submit}>{mode !== 'verify' && mode !== 'reset-confirm' ? <label>Email<input name="email" type="email" defaultValue={email} required maxLength={256} autoComplete="email" /></label> : null}{isRegistering ? <label>Ad<input name="name" required maxLength={120} autoComplete="name" /></label> : null}{mode === 'verify' || mode === 'reset-confirm' ? <label>Təsdiq kodu<input name="code" inputMode="numeric" autoComplete="one-time-code" pattern="[0-9]{6}" minLength={6} maxLength={6} required /></label> : null}{mode !== 'verify' && mode !== 'reset-request' ? <label>Şifrə<span className="password-field"><input name="password" type={showPassword ? 'text' : 'password'} required minLength={isRegistering || mode === 'reset-confirm' ? 8 : undefined} pattern={isRegistering || mode === 'reset-confirm' ? '.*[0-9].*' : undefined} title={isRegistering || mode === 'reset-confirm' ? 'Minimum 8 simvol və ən az 1 rəqəm daxil edin.' : undefined} autoComplete={isRegistering || mode === 'reset-confirm' ? 'new-password' : 'current-password'} /><button type="button" className="password-toggle" onClick={() => setShowPassword(value => !value)} aria-label={showPassword ? 'Şifrəni gizlət' : 'Şifrəni göstər'}>{showPassword ? 'Gizlət' : 'Göstər'}</button></span>{isRegistering || mode === 'reset-confirm' ? <small>Minimum 8 simvol və ən az 1 rəqəm.</small> : null}</label> : null}{message ? <p className="error" role="alert">{message}</p> : null}<button className="primary" disabled={isSubmitting}>{isSubmitting ? 'Gözləyin...' : mode === 'verify' ? 'Təsdiqlə' : mode === 'reset-request' ? 'Kod göndər' : mode === 'reset-confirm' ? 'Şifrəni yenilə' : isRegistering ? 'Hesab yarat' : 'Daxil ol'}</button></form>{mode === 'verify' ? <><button className="switch" type="button" disabled={isSubmitting} onClick={() => void resend()}>Kodu yenidən göndər</button><button className="switch" type="button" onClick={() => reset('register')}>Başqa email istifadə et</button></> : mode === 'login' ? <><button className="switch" type="button" onClick={() => reset('reset-request')}>Şifrəni unutmusunuz?</button><button className="switch" type="button" onClick={() => reset('register')}>Hesabınız yoxdur? Qeydiyyatdan keçin</button></> : <button className="switch" type="button" onClick={() => reset('login')}>Daxil olmağa qayıt</button>}</section></main>
}
