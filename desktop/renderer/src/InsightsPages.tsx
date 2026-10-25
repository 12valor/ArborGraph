import { useEffect, useState } from 'react'
import type { FileAgeInsights, FilePage, Overview, PhotoshopInsights } from '../../main/contracts'
import { bytes, date } from './format'

function useFileAgeInsights(daysOld: number) {
  const [data, setData] = useState<FileAgeInsights | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  useEffect(() => {
    let active = true
    setData(null)
    setLoading(true)
    setError(null)
    void window.arbor.getFileAgeInsights(daysOld, 200)
      .then((result) => { if (active) setData(result) })
      .catch((cause) => { if (active) setError(String(cause)) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [daysOld])
  return { data, loading, error }
}

function usePhotoshopInsights() {
  const [data, setData] = useState<PhotoshopInsights | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  useEffect(() => {
    let active = true
    void window.arbor.getPhotoshopInsights(200)
      .then((result) => { if (active) setData(result) })
      .catch((cause) => { if (active) setError(String(cause)) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [])
  return { data, loading, error }
}

export function FileTypesPage({ overview }: { overview: Overview | null }) {
  const [selected, setSelected] = useState<string | null>(null)
  const [page, setPage] = useState<FilePage | null>(null)
  const [error, setError] = useState<string | null>(null)
  const categories = [...(overview?.categories ?? [])].sort((a, b) => b.logicalBytes - a.logicalBytes)
  useEffect(() => {
    if (!selected) { setPage(null); return }
    let active = true
    setPage(null)
    setError(null)
    void window.arbor.getFiles({ category: selected, limit: 25, sortBy: 'size', sortDesc: true })
      .then((data) => { if (active) setPage(data) })
      .catch((cause) => { if (active) setError(String(cause)) })
    return () => { active = false }
  }, [selected])
  return <>
    <div className="page-heading"><div><p className="eyebrow">FILE CATEGORIES</p><h1>File types</h1><p className="subhead">Categories reflect the existing scanner classification of indexed files.</p></div></div>
    <section className="panel"><div className="section-heading"><div><h2>Category storage</h2><p>Logical bytes and file counts; select a category to see its largest files.</p></div></div>{categories.length ? <div className="category-choice-list">{categories.map((item) => <button key={item.category} onClick={() => setSelected(item.category)} className={selected === item.category ? 'selected' : ''}><span><strong>{item.category}</strong><small>{item.fileCount.toLocaleString()} files · {overview?.indexedLogicalBytes ? (item.logicalBytes / overview.indexedLogicalBytes * 100).toFixed(1) : '0.0'}% of index</small></span><b>{bytes(item.logicalBytes)}</b><div className="bar"><span style={{ width: `${overview?.indexedLogicalBytes ? item.logicalBytes / overview.indexedLogicalBytes * 100 : 0}%` }} /></div></button>)}</div> : <p className="empty">No indexed categories yet.</p>}</section>
    {error && <div className="inline-error" role="alert">{error}</div>}
    {selected && <section className="panel insight-table"><div className="section-heading"><div><h2>Largest {selected} files</h2><p>Showing up to 25 of {(page?.total ?? 0).toLocaleString()} indexed files.</p></div></div>{page ? <div className="table-scroll"><table className="data-table"><thead><tr><th>Name</th><th>Logical size</th></tr></thead><tbody>{page.files.map((file) => <tr key={file.path}><td><strong>{file.name}</strong><small title={file.path}>{file.path}</small></td><td>{bytes(file.logicalBytes)}</td></tr>)}</tbody></table></div> : <p className="empty" role="status">Loading category files…</p>}</section>}
  </>
}

export function OldFilesPage() {
  const [daysOld, setDaysOld] = useState(180)
  const { data, loading, error } = useFileAgeInsights(daysOld)
  const total = data?.ageBuckets.reduce((sum, item) => sum + item.logicalBytes, 0) ?? 0
  return <>
    <div className="page-heading"><div><p className="eyebrow">FILE AGE</p><h1>Old files</h1><p className="subhead">Modification age helps review storage. Age alone does not mean a file is unnecessary.</p></div><label className="heading-select">Older than <select value={daysOld} onChange={(event) => setDaysOld(Number(event.target.value))}><option value={30}>30 days</option><option value={90}>90 days</option><option value={180}>180 days</option><option value={365}>1 year</option><option value={730}>2 years</option></select></label></div>
    {error && <div className="inline-error" role="alert">{error}</div>}{loading && <p role="status">Loading file ages…</p>}
    {data && <><section className="panel insight-table"><div className="section-heading"><div><h2>Age distribution</h2><p>Share of indexed logical file bytes by modification date.</p></div></div><div className="category-list">{data.ageBuckets.map((bucket) => <div className="category-row" key={bucket.name}><div><strong>{bucket.name}</strong><span>{bytes(bucket.logicalBytes)}</span></div><div className="bar"><span style={{ width: `${total ? bucket.logicalBytes / total * 100 : 0}%` }} /></div><small>{bucket.fileCount.toLocaleString()} files · {bucket.description}</small></div>)}</div></section><section className="panel"><div className="section-heading"><div><h2>Largest files older than {daysOld} days</h2><p>Up to 200 indexed results, sorted by logical size. No files are changed.</p></div></div>{data.oldFiles.length ? <div className="table-scroll"><table className="data-table"><thead><tr><th>Name</th><th>Category</th><th>Logical size</th><th>Modified</th></tr></thead><tbody>{data.oldFiles.map((file) => <tr key={file.path}><td><strong>{file.name}</strong><small title={file.path}>{file.path}</small></td><td>{file.category}</td><td>{bytes(file.logicalBytes)}</td><td>{date(file.modifiedTime)}</td></tr>)}</tbody></table></div> : <p className="empty">No indexed files meet this age threshold.</p>}</section></>}
  </>
}

export function PhotoshopPage() {
  const { data, loading, error } = usePhotoshopInsights()
  const [filter, setFilter] = useState('All')
  const types = [...new Set(data?.photoshop.map((item) => item.type) ?? [])]
  const items = data?.photoshop.filter((item) => filter === 'All' || item.type === filter) ?? []
  return <>
    <div className="page-heading"><div><p className="eyebrow">ADOBE STORAGE</p><h1>Photoshop Inspector</h1><p className="subhead">Identified files in the current index. This does not measure live scratch activity or Photoshop performance.</p></div></div>
    {error && <div className="inline-error" role="alert">{error}</div>}{loading && <p role="status">Loading Photoshop-associated files…</p>}
    {data && <><div className="metric-grid"><div className="metric accent"><span>Identified logical bytes</span><strong>{bytes(data.photoshopSummary.identifiedLogicalBytes)}</strong><small>Indexed Photoshop-associated files</small></div><div className="metric"><span>PSD documents</span><strong>{data.photoshopSummary.psdCount.toLocaleString()}</strong><small>{bytes(data.photoshopSummary.psdBytes)}</small></div><div className="metric"><span>PSB documents</span><strong>{data.photoshopSummary.psbCount.toLocaleString()}</strong><small>{bytes(data.photoshopSummary.psbBytes)}</small></div><div className="metric"><span>Other associated files</span><strong>{data.photoshopSummary.otherCount.toLocaleString()}</strong><small>Based on path and extension</small></div></div><section className="panel"><div className="section-heading"><div><h2>Associated files</h2><p>Up to 200 largest indexed matches. Documents and presets should be treated as user data.</p></div><label className="heading-select">Type <select value={filter} onChange={(event) => setFilter(event.target.value)}><option>All</option>{types.map((type) => <option key={type}>{type}</option>)}</select></label></div>{items.length ? <div className="table-scroll"><table className="data-table"><thead><tr><th>Name</th><th>Classification</th><th>Logical size</th><th>Modified</th></tr></thead><tbody>{items.map((item) => <tr key={item.path}><td><strong>{item.name}</strong><small title={item.path}>{item.path}</small></td><td>{item.type}</td><td>{bytes(item.logicalBytes)}</td><td>{date(item.modifiedTime)}</td></tr>)}</tbody></table></div> : <p className="empty">No indexed Photoshop-associated files match this type.</p>}</section></>}
  </>
}
