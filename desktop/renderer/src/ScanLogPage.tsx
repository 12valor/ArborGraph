import type { ScanEvent } from '../../main/contracts'

export interface DiagnosticEntry { timestamp: number; level: 'INFO' | 'WARN' | 'ERROR'; message: string }
export interface SkippedScanItem { category: 'Directory' | 'File'; path: string; reason: string }

export function ScanLogPage({ entries, skipped, scan, truncated, clear }: {
  entries: DiagnosticEntry[]
  skipped: SkippedScanItem[]
  scan: ScanEvent | null
  truncated: boolean
  clear: () => void
}) {
  return <>
    <div className="page-heading"><div><p className="eyebrow">SCAN DIAGNOSTICS</p><h1>Scan logs</h1><p className="subhead">Activity and skipped path reasons from this desktop session.</p></div><button className="secondary-button" onClick={clear} disabled={!entries.length && !skipped.length}>Clear session log</button></div>
    <section className="panel diagnostics-panel"><div className="section-heading"><div><h2>Latest scan counts</h2><p>Skipped items are omitted from indexed totals.</p></div></div><div className="diagnostic-counts"><div><span>Files skipped</span><strong>{(scan?.filesSkipped ?? 0).toLocaleString()}</strong></div><div><span>Directories skipped</span><strong>{(scan?.directoriesSkipped ?? 0).toLocaleString()}</strong></div></div></section>
    <section className="panel diagnostics-panel"><div className="section-heading"><div><h2>Activity</h2><p>Stored in memory until this window closes.</p></div></div>{entries.length ? <div className="table-scroll"><table className="data-table"><thead><tr><th>Time</th><th>Level</th><th>Message</th></tr></thead><tbody>{entries.map((item, index) => <tr key={`${item.timestamp}-${index}`}><td>{new Date(item.timestamp).toLocaleTimeString()}</td><td>{item.level}</td><td>{item.message}</td></tr>)}</tbody></table></div> : <p className="empty">No scan activity in this session yet.</p>}</section>
    <section className="panel diagnostics-panel"><div className="section-heading"><div><h2>Skipped paths</h2><p>Reported by the existing C# scanner after the latest scan.</p></div></div>{skipped.length ? <div className="table-scroll"><table className="data-table"><thead><tr><th>Type</th><th>Path</th><th>Reason</th></tr></thead><tbody>{skipped.map((item, index) => <tr key={`${item.category}-${item.path}-${index}`}><td>{item.category}</td><td title={item.path}>{item.path}</td><td>{item.reason}</td></tr>)}</tbody></table></div> : <p className="empty">No skipped path details reported for the latest scan.</p>}{truncated && <p className="map-note">Only the first 500 skipped files and first 500 skipped directories are shown.</p>}</section>
  </>
}
