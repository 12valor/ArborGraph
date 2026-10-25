import { useEffect, useState } from 'react'
import type { ScanComparison, ScanHistoryItem } from '../../main/contracts'
import { bytes } from './format'

function signedBytes(value: number): string { return `${value > 0 ? '+' : value < 0 ? '−' : ''}${bytes(Math.abs(value))}` }
function signedCount(value: number): string { return `${value > 0 ? '+' : ''}${value.toLocaleString()}` }
function timestamp(value: number): string { return value > 0 ? new Date(value).toLocaleString() : 'Unknown' }

export function HistoryPage() {
  const [history, setHistory] = useState<ScanHistoryItem[]>([])
  const [olderId, setOlderId] = useState<number | null>(null)
  const [newerId, setNewerId] = useState<number | null>(null)
  const [comparison, setComparison] = useState<ScanComparison | null>(null)
  const [loading, setLoading] = useState(true)
  const [comparing, setComparing] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const refresh = async () => {
    setLoading(true)
    setError(null)
    try {
      const items = await window.arbor.getScanHistory(100)
      setHistory(items)
      setNewerId(items[0]?.id ?? null)
      setOlderId(items[1]?.id ?? null)
      setComparison(null)
    } catch (cause) { setError(String(cause)) }
    finally { setLoading(false) }
  }
  useEffect(() => { void refresh() }, [])

  const older = history.find((item) => item.id === olderId)
  const newer = history.find((item) => item.id === newerId)
  const comparable = !!older && !!newer && older.id < newer.id && older.state === 'Completed' && newer.state === 'Completed' && older.roots.toLowerCase() === newer.roots.toLowerCase()
  const compare = async () => {
    if (!comparable || !olderId || !newerId) return
    setComparing(true)
    setComparison(null)
    setError(null)
    try { setComparison(await window.arbor.compareScans(olderId, newerId)) }
    catch (cause) { setError(String(cause)) }
    finally { setComparing(false) }
  }

  return <>
    <div className="page-heading"><div><p className="eyebrow">STORED SCANS</p><h1>Scan history</h1><p className="subhead">Review completed and cancelled runs. Compare indexed totals only for matching targets.</p></div><button className="secondary-button" onClick={() => void refresh()} disabled={loading}>Refresh history</button></div>
    {error && <div className="inline-error" role="alert">{error}</div>}
    <section className="panel history-panel"><div className="section-heading"><div><h2>Recent scans</h2><p>Newest first · up to 100 stored records</p></div></div>
      {loading ? <p className="empty" role="status">Loading scan history…</p> : history.length ? <div className="table-scroll"><table className="data-table"><thead><tr><th>Started</th><th>Target</th><th>Status</th><th>Indexed files</th><th>Logical bytes</th><th>Duration</th><th>Skipped</th></tr></thead><tbody>{history.map((item) => <tr key={item.id}><td>{timestamp(item.startedAt)}</td><td title={item.roots}>{item.roots}</td><td>{item.state}</td><td>{item.filesIndexed.toLocaleString()}</td><td>{bytes(item.indexedLogicalBytes)}</td><td>{((item.finishedAt - item.startedAt) / 1000).toFixed(1)}s</td><td>{(item.filesSkipped + item.directoriesSkipped).toLocaleString()}</td></tr>)}</tbody></table></div> : <p className="empty">No stored scans yet. Run a scan to create history.</p>}
    </section>
    {history.length >= 2 && <section className="panel compare-panel"><div className="section-heading"><div><h2>Compare scans</h2><p>Both scans must be completed and use exactly the same targets.</p></div></div><div className="compare-controls"><label>Older scan<select value={olderId ?? ''} onChange={(event) => { setOlderId(Number(event.target.value)); setComparison(null) }}>{history.map((item) => <option key={item.id} value={item.id}>#{item.id} · {timestamp(item.startedAt)} · {item.state}</option>)}</select></label><label>Newer scan<select value={newerId ?? ''} onChange={(event) => { setNewerId(Number(event.target.value)); setComparison(null) }}>{history.map((item) => <option key={item.id} value={item.id}>#{item.id} · {timestamp(item.startedAt)} · {item.state}</option>)}</select></label><button className="primary-button" onClick={() => void compare()} disabled={!comparable || comparing}>{comparing ? 'Comparing…' : 'Compare'}</button></div>
      {!comparable && <p className="compare-hint">Choose an older and newer completed scan of the same target before comparing.</p>}
      {comparison && <div className="comparison-results"><p className="compare-hint">Changes in indexed totals for {comparison.roots}. These values do not establish which files were added or removed.</p>{comparison.coverageWarning && <p className="coverage-warning">Coverage differs or items were skipped. Treat these deltas as incomplete.</p>}<div className="compare-metrics"><div><span>Indexed logical bytes</span><strong>{signedBytes(comparison.deltaBytes)}</strong><small>{bytes(comparison.previousBytes)} → {bytes(comparison.currentBytes)}</small></div><div><span>Indexed files</span><strong>{signedCount(comparison.deltaFiles)}</strong><small>{comparison.previousFiles.toLocaleString()} → {comparison.currentFiles.toLocaleString()}</small></div></div><h3>Category changes</h3>{comparison.categories.length ? <div className="table-scroll"><table className="data-table"><thead><tr><th>Category</th><th>Earlier</th><th>Later</th><th>Byte change</th><th>File change</th></tr></thead><tbody>{comparison.categories.map((item) => <tr key={item.category}><td>{item.category}</td><td>{bytes(item.previousBytes)}</td><td>{bytes(item.currentBytes)}</td><td>{signedBytes(item.deltaBytes)}</td><td>{signedCount(item.deltaFiles)}</td></tr>)}</tbody></table></div> : <p className="empty">No category snapshots are stored for these scans.</p>}</div>}
    </section>}
  </>
}
