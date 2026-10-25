import { useEffect, useMemo, useState } from 'react'
import type { Drive, LegalStatus, Overview, ScanEvent } from '../../main/contracts'
import { bytes, duration } from './format'
import { FilesPage } from './FilesPage'
import { TreemapPage } from './TreemapPage'
import { AnalyticsPage } from './AnalyticsPage'
import { HistoryPage } from './HistoryPage'
import { FileTypesPage, OldFilesPage, PhotoshopPage } from './InsightsPages'
import { DuplicatesPage } from './DuplicatesPage'
import { LegalGate } from './LegalGate'

type Page = 'overview' | 'scanner' | 'files' | 'treemap' | 'analytics' | 'history' | 'types' | 'old' | 'photoshop' | 'duplicates'
type ScanStatus = 'Ready' | 'Scanning' | 'Stopping' | 'Completed' | 'Cancelled' | 'Failed'

function App() {
  const [legal, setLegal] = useState<LegalStatus | null>(null)
  const [startupError, setStartupError] = useState<string | null>(null)
  useEffect(() => {
    void window.arbor.getLegal().then(setLegal).catch((cause) => setStartupError(String(cause)))
  }, [])
  if (startupError) return <main className="legal-shell"><div className="legal-card" role="alert">{startupError}</div></main>
  if (!legal) return <main className="legal-shell" role="status">Loading ArborGraph…</main>
  if (!legal.accepted) return <LegalGate legal={legal} onAccepted={() => setLegal({ ...legal, accepted: true })} />
  return <Workspace />
}

function Workspace() {
  const [page, setPage] = useState<Page>('overview')
  const [drives, setDrives] = useState<Drive[]>([])
  const [overview, setOverview] = useState<Overview | null>(null)
  const [selected, setSelected] = useState<string[]>([])
  const [folder, setFolder] = useState<string | null>(null)
  const [scan, setScan] = useState<ScanEvent | null>(null)
  const [status, setStatus] = useState<ScanStatus>('Ready')
  const [error, setError] = useState<string | null>(null)
  const [engineConnected, setEngineConnected] = useState(true)
  const [activity, setActivity] = useState<string[]>([])

  const refresh = async () => {
    const [newDrives, newOverview] = await Promise.all([window.arbor.listDrives(), window.arbor.getOverview()])
    setDrives(newDrives)
    setOverview(newOverview)
    setSelected((old) => old.length ? old : newDrives.filter((d) => d.path.toUpperCase().startsWith('C:')).map((d) => d.path))
  }

  useEffect(() => {
    let live = true
    const load = async () => {
      try {
        const [newDrives, newOverview] = await Promise.all([window.arbor.listDrives(), window.arbor.getOverview()])
        if (!live) return
        setDrives(newDrives)
        setOverview(newOverview)
        setSelected(newDrives.filter((d) => d.path.toUpperCase().startsWith('C:')).map((d) => d.path))
      } catch (cause) {
        if (live) setError(cause instanceof Error ? cause.message : String(cause))
      }
    }
    void load()
    const unsubscribe = window.arbor.onScanEvent((event) => {
      if (!live) return
      if (event.eventName.startsWith('duplicate')) return
      setScan((previous) => ({ ...previous, ...event }))
      if (event.currentDirectory) setActivity((old) => [event.currentDirectory!, ...old].slice(0, 8))
      if (event.eventName === 'scanFinished') {
        setStatus(event.state as ScanStatus)
        void window.arbor.getOverview().then(setOverview).catch((cause) => setError(String(cause)))
      } else if (event.eventName === 'scanFailed' || event.eventName === 'engineExited') {
        setStatus('Failed')
        setError(event.message ?? 'The scan stopped unexpectedly.')
        if (event.eventName === 'engineExited') setEngineConnected(false)
      }
    })
    return () => { live = false; unsubscribe() }
  }, [])

  const selectedDrives = useMemo(() => drives.filter((d) => selected.includes(d.path)), [drives, selected])
  const selectedCapacity = selectedDrives.reduce((sum, d) => sum + d.totalBytes, 0)
  const selectedUsed = selectedDrives.reduce((sum, d) => sum + d.usedBytes, 0)
  const selectedFree = selectedDrives.reduce((sum, d) => sum + d.freeBytes, 0)
  const categories = [...(overview?.categories ?? [])].sort((a, b) => b.logicalBytes - a.logicalBytes).slice(0, 6)

  const chooseFolder = async () => {
    try { setFolder(await window.arbor.chooseFolder()) }
    catch (cause) { setError(cause instanceof Error ? cause.message : String(cause)) }
  }

  const startScan = async () => {
    const roots = folder ? [folder] : selected
    if (roots.length === 0) { setError('Choose a drive or folder first.'); return }
    setError(null)
    setStatus('Scanning')
    setScan(null)
    setActivity([])
    setPage('scanner')
    try {
      await window.arbor.startScan(roots)
    } catch (cause) { setStatus('Failed'); setError(cause instanceof Error ? cause.message : String(cause)) }
  }

  const stopScan = async () => {
    try { setStatus('Stopping'); await window.arbor.cancelScan() }
    catch (cause) { setStatus('Failed'); setError(cause instanceof Error ? cause.message : String(cause)) }
  }

  return <div className="app-shell">
    <aside className="sidebar">
      <div className="brand"><div className="brand-mark">A</div><div><strong>ArborGraph</strong><small>Storage intelligence</small></div></div>
      <div className="side-label">WORKSPACE</div>
      <nav aria-label="Workspace">
        <button className={page === 'overview' ? 'nav-item active' : 'nav-item'} onClick={() => setPage('overview')}><span>▦</span> Overview</button>
        <button className={page === 'scanner' ? 'nav-item active' : 'nav-item'} onClick={() => setPage('scanner')}><span>◎</span> Scanner</button>
        <button className={page === 'files' ? 'nav-item active' : 'nav-item'} onClick={() => setPage('files')}><span>▤</span> Files</button>
        <button className={page === 'treemap' ? 'nav-item active' : 'nav-item'} onClick={() => setPage('treemap')}><span>▧</span> Treemap</button>
        <button className={page === 'analytics' ? 'nav-item active' : 'nav-item'} onClick={() => setPage('analytics')}><span>▥</span> Analytics</button>
        <button className={page === 'history' ? 'nav-item active' : 'nav-item'} onClick={() => setPage('history')}><span>◷</span> History</button>
        <button className={page === 'types' ? 'nav-item active' : 'nav-item'} onClick={() => setPage('types')}><span>▦</span> File types</button>
        <button className={page === 'old' ? 'nav-item active' : 'nav-item'} onClick={() => setPage('old')}><span>◴</span> Old files</button>
        <button className={page === 'photoshop' ? 'nav-item active' : 'nav-item'} onClick={() => setPage('photoshop')}><span>▧</span> Photoshop</button>
        <button className={page === 'duplicates' ? 'nav-item active' : 'nav-item'} onClick={() => setPage('duplicates')}><span>▣</span> Duplicates</button>
      </nav>
      <div className="sidebar-bottom"><span className="status-dot" /> {engineConnected ? 'Engine connected through local process' : 'Engine disconnected'}</div>
    </aside>
    <main className="main-area">
      <header className="topbar"><span>ArborGraph Desktop <em>Preview</em></span><div className="topbar-right"><span className="status-dot" /> Local analysis</div></header>
      <div className="content">
        {error && <div className="error-banner" role="alert"><span>{error}</span><button onClick={() => setError(null)} aria-label="Dismiss error">×</button></div>}
        {page === 'overview' ? <>
          <div className="page-heading"><div><p className="eyebrow">STORAGE OVERVIEW</p><h1>Understand your disk space</h1><p className="subhead">Physical volume capacity and indexed file totals are shown separately.</p></div><button className="secondary-button" onClick={() => void refresh()}>Refresh data</button></div>
          <section className="panel drive-panel"><div className="section-heading"><div><h2>Drives</h2><p>Select a volume to inspect or scan.</p></div><span className="muted">{drives.length} available</span></div>
            <div className="drive-list">{drives.length ? drives.map((drive) => <label className="drive-row" key={drive.path}><input type="checkbox" checked={selected.includes(drive.path)} onChange={(event) => setSelected((old) => event.target.checked ? [...old, drive.path] : old.filter((x) => x !== drive.path))} /><span className="drive-icon">▣</span><span className="drive-name"><strong>{drive.label || 'Local disk'} <span>{drive.path}</span></strong><small>{drive.fileSystem} · {bytes(drive.freeBytes)} available</small></span><span className="drive-meter"><span style={{ width: `${Math.min(100, drive.totalBytes ? drive.usedBytes / drive.totalBytes * 100 : 0)}%` }} /></span><b>{bytes(drive.totalBytes)}</b></label>) : <p className="empty">No accessible drives were returned by Windows.</p>}</div>
          </section>
          <div className="metric-grid">
            <div className="metric"><span>Selected capacity</span><strong>{bytes(selectedCapacity)}</strong><small>Physical volume size</small></div>
            <div className="metric"><span>Used on selected drives</span><strong>{bytes(selectedUsed)}</strong><small>{selectedCapacity ? `${(selectedUsed / selectedCapacity * 100).toFixed(1)}% of capacity` : 'Select a drive'}</small></div>
            <div className="metric"><span>Available on selected drives</span><strong>{bytes(selectedFree)}</strong><small>Reported by Windows</small></div>
            <div className="metric accent"><span>Indexed logical files</span><strong>{bytes(overview?.indexedLogicalBytes ?? 0)}</strong><small>{(overview?.indexedFiles ?? 0).toLocaleString()} files in current index</small></div>
          </div>
          <div className="overview-grid">
            <section className="panel"><div className="section-heading"><div><h2>Largest categories</h2><p>Share of indexed logical file bytes.</p></div></div>{categories.length ? <div className="category-list">{categories.map((category) => <div className="category-row" key={category.category}><div><strong>{category.category || 'Other'}</strong><span>{bytes(category.logicalBytes)}</span></div><div className="bar"><span style={{ width: `${overview?.indexedLogicalBytes ? Math.min(100, category.logicalBytes / overview.indexedLogicalBytes * 100) : 0}%` }} /></div><small>{category.fileCount.toLocaleString()} files</small></div>)}</div> : <div className="empty">No indexed files yet. Run a scan to see category sizes.</div>}</section>
            <section className="panel next-panel"><div className="section-heading"><div><h2>Run a scan</h2><p>Index selected drives with the existing C# scanner.</p></div></div><p>Scanning records logical file sizes and reports skipped items. It does not measure physical allocation.</p><button className="primary-button" onClick={() => void startScan()} disabled={status === 'Scanning' || status === 'Stopping'}>Scan selected drives <span>→</span></button></section>
          </div>
        </> : page === 'scanner' ? <>
          <div className="page-heading"><div><p className="eyebrow">FILESYSTEM SCANNER</p><h1>Scan your storage</h1><p className="subhead">Index a drive or a chosen folder. The analysis runs outside the interface.</p></div><span className={`state-pill ${status.toLowerCase()}`}>{status}</span></div>
          <section className="panel scan-targets"><div className="section-heading"><div><h2>Scan target</h2><p>Choose drives or use a specific folder.</p></div></div><div className="target-options">{drives.map((drive) => <label key={drive.path}><input type="checkbox" checked={selected.includes(drive.path)} disabled={!!folder || status === 'Scanning' || status === 'Stopping'} onChange={(event) => setSelected((old) => event.target.checked ? [...old, drive.path] : old.filter((x) => x !== drive.path))} />{drive.label || 'Local disk'} <span>{drive.path}</span></label>)}</div><div className="folder-row"><button className="secondary-button" onClick={() => void chooseFolder()} disabled={status === 'Scanning' || status === 'Stopping'}>Choose folder</button><span title={folder ?? ''}>{folder ?? 'No custom folder selected'}</span>{folder && <button className="text-button" onClick={() => setFolder(null)}>Clear</button>}</div><div className="scan-actions"><button className="primary-button" onClick={() => void startScan()} disabled={status === 'Scanning' || status === 'Stopping'}>Start scan</button><button className="secondary-button" onClick={() => void stopScan()} disabled={status !== 'Scanning'}>Stop scan</button></div></section>
          <div className="scan-grid"><div className="metric"><span>Files discovered</span><strong>{(scan?.filesDiscovered ?? 0).toLocaleString()}</strong></div><div className="metric"><span>Files indexed</span><strong>{(scan?.filesIndexed ?? 0).toLocaleString()}</strong></div><div className="metric"><span>Folders processed</span><strong>{(scan?.directoriesProcessed ?? 0).toLocaleString()}</strong></div><div className="metric"><span>Elapsed</span><strong>{duration(scan?.elapsedMilliseconds ?? 0)}</strong></div></div>
          <section className="panel progress-panel"><div className="section-heading"><div><h2>Scan activity</h2><p>{status === 'Scanning' ? 'Indexing files and building directory totals…' : status === 'Stopping' ? 'Finishing database work and cancelling…' : 'Start a scan to see live progress.'}</p></div></div><div className="scan-details"><div><span>Indexed logical bytes</span><strong>{bytes(scan?.indexedLogicalBytes ?? 0)}</strong></div><div><span>Skipped folders</span><strong>{(scan?.directoriesSkipped ?? 0).toLocaleString()}</strong></div><div><span>Skipped files</span><strong>{(scan?.filesSkipped ?? 0).toLocaleString()}</strong></div></div><div className="current-path"><small>CURRENT DIRECTORY</small><span>{scan?.currentDirectory || 'Waiting for scan activity'}</span></div><div className="activity-list">{activity.map((item, index) => <div key={`${item}-${index}`}>{item}</div>)}</div></section>
        </> : page === 'files' ? <FilesPage overview={overview} /> : page === 'treemap' ? <TreemapPage /> : page === 'analytics' ? <AnalyticsPage /> : page === 'history' ? <HistoryPage /> : page === 'types' ? <FileTypesPage overview={overview} /> : page === 'old' ? <OldFilesPage /> : page === 'photoshop' ? <PhotoshopPage /> : <DuplicatesPage />}
      </div>
    </main>
  </div>
}

export default App
