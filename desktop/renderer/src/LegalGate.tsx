import { useState } from 'react'
import type { LegalStatus } from '../../main/contracts'

export function LegalGate({ legal, onAccepted }: { legal: LegalStatus; onAccepted: () => void }) {
  const [agreed, setAgreed] = useState(false)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const accept = async () => {
    if (!agreed) return
    setSaving(true)
    setError(null)
    try { await window.arbor.acceptEula(true); onAccepted() }
    catch (cause) { setError(String(cause)); setSaving(false) }
  }
  return <main className="legal-shell"><div className="legal-card">
    <div className="legal-heading"><div className="brand-mark">A</div><div><p className="eyebrow">ARBORGRAPH · FIRST RUN</p><h1>End User License Agreement</h1><p>Read and accept version {legal.version} to use the desktop preview.</p></div></div>
    <pre className="legal-text" tabIndex={0}>{legal.text}</pre>
    <label className="legal-consent"><input type="checkbox" checked={agreed} onChange={(event) => setAgreed(event.target.checked)} /> I have read and accept the terms of this agreement.</label>
    {error && <div className="inline-error" role="alert">{error}</div>}
    <div className="legal-actions"><button className="secondary-button" onClick={() => void window.arbor.declineEula()}>Decline and exit</button><button className="primary-button" disabled={!agreed || saving} onClick={() => void accept()}>{saving ? 'Saving…' : 'Accept and continue'}</button></div>
  </div></main>
}
