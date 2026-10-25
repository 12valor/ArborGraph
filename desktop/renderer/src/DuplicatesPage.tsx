import { useEffect, useState } from 'react'
import type { DuplicateOverview, DuplicateResults } from '../../main/contracts'
import { bytes, date } from './format'

export function DuplicatesPage() {
  const [overview, setOverview] = useState<DuplicateOverview | null>(null)
  const [results, setResults] = useState<DuplicateResults | null>(null)
  const [running, setRunning] = useState(false)
  const [hasResults, setHasResults] = useState(false)
  const [message, setMessage] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [offset, setOffset] = useState(0)
  const [revision, setRevision] = useState(0)

  useEffect(() => {
    let active = true
    void Promise.all([window.arbor.getDuplicateOverview(), window.arbor.getDuplicateStatus()])
      .then(([summary, status]) => {
        if (!active) return
        setOverview(summary)
        setRunning(status.running)
        setHasResults(status.hasResults)
        if (status.running) setMessage('Duplicate analysis is running…')
        else if (status.hasResults) setMessage('Analysis complete.')
        if (status.hasResults) setRevision((value) => value + 1)
      })
      .catch((cause) => { if (active) setError(String(cause)) })
    const unsubscribe = window.arbor.onScanEvent((event) => {
      if (!active) return
      if (event.eventName === 'duplicateProgress') setMessage(event.message ?? 'Checking candidate files…')
      if (event.eventName === 'duplicateFinished') {
        setRunning(false)
        setHasResults(event.state === 'Completed')
        setMessage(event.state === 'Completed' ? 'Analysis complete.' : 'Analysis cancelled. Run it again for complete results.')
        if (event.state === 'Completed') { setOffset(0); setRevision((value) => value + 1) }
      }
      if (event.eventName === 'duplicateFailed') {
        setRunning(false)
        setError(event.message ?? 'Duplicate analysis failed.')
      }
    })
    return () => { active = false; unsubscribe() }
  }, [])

  useEffect(() => {
    if (!hasResults) return
    let active = true
    void window.arbor.getDuplicateResults(offset, 25)
      .then((data) => { if (active) setResults(data) })
      .catch((cause) => { if (active) setError(String(cause)) })
    return () => { active = false }
  }, [hasResults, offset, revision])

  const start = async () => {
    setError(null)
    setResults(null)
    setHasResults(false)
    setRunning(true)
    setMessage('Starting three stage duplicate verification…')
    try { await window.arbor.startDuplicateAnalysis() }
    catch (cause) { setRunning(false); setError(String(cause)) }
  }

  const cancel = async () => {
    try { await window.arbor.cancelDuplicateAnalysis(); setMessage('Cancelling analysis…') }
    catch (cause) { setError(String(cause)) }
  }

  const redundantBytes = results?.groups.reduce((sum, group) => sum + group.potentialRedundantBytes, 0) ?? 0
  return <>
    <div className="page-heading"><div><p className="eyebrow">DUPLICATE ANALYSIS</p><h1>Duplicate files</h1><p className="subhead">Size collisions are candidates. Verification reads file content and compares SHA-256 hashes.</p></div></div>
    {error && <div className="inline-error" role="alert">{error}</div>}
    <div className="metric-grid"><div className="metric"><span>Candidate size groups</span><strong>{overview?.candidateGroups.toLocaleString() ?? '—'}</strong><small>Unverified index matches</small></div><div className="metric"><span>Candidate files</span><strong>{overview?.candidateFiles.toLocaleString() ?? '—'}</strong><small>At least 1 KB each</small></div><div className="metric"><span>Possible redundant bytes</span><strong>{bytes(overview?.potentialRedundantBytes ?? 0)}</strong><small>Upper bound before content checks</small></div><div className="metric accent"><span>Confirmed groups</span><strong>{hasResults ? (results?.total.toLocaleString() ?? '…') : '—'}</strong><small>From current analysis session</small></div></div>
    <section className="panel duplicate-controls"><div className="section-heading"><div><h2>Verify indexed candidates</h2><p>The existing C# analyzer checks file sizes, partial signatures, then full SHA-256. Files are read without modification.</p></div></div><div className="duplicate-actions"><button className="primary-button" disabled={running} onClick={() => void start()}>Start analysis</button><button className="secondary-button" disabled={!running} onClick={() => void cancel()}>Cancel</button><span role="status">{message || 'Run analysis to confirm matching content.'}</span></div></section>
    {hasResults && <section className="panel duplicate-results"><div className="section-heading"><div><h2>Confirmed duplicate groups</h2><p>{results?.total.toLocaleString() ?? '…'} {results?.total === 1 ? 'group' : 'groups'} · {bytes(redundantBytes)} potential redundant bytes on this page. No space has been reclaimed.</p></div></div>{results?.groups.length ? <div className="duplicate-groups">{results.groups.map((group) => <div className="duplicate-group" key={`${group.exactSize}-${group.sha256}`}><div className="duplicate-group-heading"><strong>{group.fileCount.toLocaleString()} matching files · {bytes(group.exactSize)} each</strong><span>{bytes(group.potentialRedundantBytes)} potential redundancy</span></div><small>SHA-256 {group.sha256}</small><ul>{group.files.map((file) => <li key={file.path}><strong>{file.name}</strong><span title={file.path}>{file.path}</span><time>{date(file.modifiedTime)}</time></li>)}</ul>{group.filesTruncated && <p>Showing {group.files.length.toLocaleString()} of {group.fileCount.toLocaleString()} files in this group.</p>}</div>)}</div> : <p className="empty">No content-matched duplicate groups were found within the 250 largest size collision groups.</p>}<div className="table-footer"><span>Showing {results && results.total ? `${results.offset + 1}–${Math.min(results.offset + results.groups.length, results.total)}` : '0'} of {results?.total ?? 0} groups</span><div><button className="secondary-button" disabled={offset === 0} onClick={() => setOffset(Math.max(0, offset - 25))}>Previous</button><button className="secondary-button" disabled={!results || offset + 25 >= results.total} onClick={() => setOffset(offset + 25)}>Next</button></div></div></section>}
    <p className="map-note">Results reflect files available when analysis ran. Candidate and confirmed redundant bytes are estimates; no files are selected or deleted.</p>
  </>
}
