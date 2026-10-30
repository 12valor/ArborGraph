import { useEffect, useState } from 'react'
import type { DesktopSettings, ScanSettings } from '../../main/contracts'

export function SettingsPage() {
  const [settings, setSettings] = useState<DesktopSettings | null>(null)
  const [legalText, setLegalText] = useState('')
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState('')
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    void Promise.all([window.arbor.getSettings(), window.arbor.getLegal()])
      .then(([data, legal]) => { if (active) { setSettings(data); setLegalText(legal.text) } })
      .catch((cause) => { if (active) setError(String(cause)) })
    return () => { active = false }
  }, [])

  const update = (values: Partial<ScanSettings>) => setSettings((old) => old ? { ...old, ...values } : old)
  const save = async () => {
    if (!settings) return
    setBusy(true); setError(null); setMessage('')
    try {
      const result = await window.arbor.setScanSettings({ workerCount: settings.workerCount,
        followJunctions: settings.followJunctions, includeHiddenFiles: settings.includeHiddenFiles,
        includeSystemFiles: settings.includeSystemFiles })
      setSettings(result); setMessage('Scan settings saved. They apply to the next scan.')
    } catch (cause) { setError(String(cause)) }
    finally { setBusy(false) }
  }
  const addExclusion = async () => {
    setError(null)
    try { const result = await window.arbor.addExclusion(); if (result) { setSettings(result); setMessage('Folder excluded from future scans.') } }
    catch (cause) { setError(String(cause)) }
  }
  const removeExclusion = async (path: string) => {
    setError(null)
    try { setSettings(await window.arbor.removeExclusion(path)); setMessage('Exclusion removed.') }
    catch (cause) { setError(String(cause)) }
  }

  return <>
    <div className="page-heading"><div><p className="eyebrow">PREFERENCES</p><h1>Settings</h1><p className="subhead">Scanner options use the existing ArborGraph settings file and apply to future scans.</p></div></div>
    {error && <div className="inline-error" role="alert">{error}</div>}{message && <p className="settings-message" role="status">{message}</p>}
    {!settings ? <p role="status">Loading settings…</p> : <>
      <section className="panel settings-panel"><div className="section-heading"><div><h2>Scanning</h2><p>Directory junctions and symbolic links are skipped.</p></div></div><div className="settings-fields">
        <label><span>Worker count</span><select value={settings.workerCount} onChange={(event) => update({ workerCount: Number(event.target.value) })}><option value={0}>Automatic</option>{[1, 2, 4, 8, 16, 32, 64].map((count) => <option key={count} value={count}>{count}</option>)}</select></label>
        <label><input type="checkbox" checked={settings.includeHiddenFiles} onChange={(event) => update({ includeHiddenFiles: event.target.checked })} /> Include hidden files</label>
        <label><input type="checkbox" checked={settings.includeSystemFiles} onChange={(event) => update({ includeSystemFiles: event.target.checked })} /> Include system files</label>
        <button className="primary-button" disabled={busy} onClick={() => void save()}>{busy ? 'Saving…' : 'Save scan settings'}</button>
      </div></section>
      <section className="panel settings-panel"><div className="section-heading"><div><h2>Excluded folders</h2><p>These directories and their descendants are skipped by the scanner.</p></div><button className="secondary-button" onClick={() => void addExclusion()}>Choose folder</button></div><div className="exclusion-list">{settings.excludedPaths.length ? settings.excludedPaths.map((path) => <div key={path}><span title={path}>{path}</span>{settings.protectedExclusions.some((item) => item.toLowerCase() === path.toLowerCase()) ? <small>Built-in</small> : <button className="text-button" onClick={() => void removeExclusion(path)}>Remove</button>}</div>) : <p className="empty">No folders excluded.</p>}</div></section>
      <section className="panel settings-panel"><div className="section-heading"><div><h2>Safety and history</h2><p>Current values from the shared settings file. Cleanup actions are not yet available in this preview.</p></div></div><dl className="settings-summary"><dt>Recycle Bin by default</dt><dd>{settings.defaultToRecycleBin ? 'Yes' : 'No'}</dd><dt>Confirm Recycle Bin actions</dt><dd>{settings.requireConfirmationForRecycleBin ? 'Yes' : 'No'}</dd><dt>Confirm permanent deletion</dt><dd>{settings.requireConfirmationForPermanent ? 'Yes' : 'No'}</dd><dt>History retention</dt><dd>{settings.retentionDays} days · {settings.maxScanSessionsToKeep} sessions</dd><dt>Log level</dt><dd>{settings.logLevel}</dd></dl></section>
      <details className="panel settings-panel legal-review"><summary>Review MIT License</summary><pre>{legalText}</pre></details>
    </>}
  </>
}
