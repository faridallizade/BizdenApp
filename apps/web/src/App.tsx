import './App.css'

export default function App() {
  return (
    <main className="app-shell">
      <section className="welcome-card" aria-labelledby="page-title">
        <img className="brand-logo" src="/brand/bizden-logo.png" alt="Bizdən — Anılarınız, bizdən." />
        <p className="eyebrow">Wedding memories platform</p>
        <h1 id="page-title">Bizdən hazırlanır</h1>
        <p className="description">Qonaqların xatirələrini bir QR kodla toplamağın sadə yolu.</p>
        <span className="status" role="status">Phase 1 · layihə skeleti hazırdır</span>
      </section>
    </main>
  )
}
