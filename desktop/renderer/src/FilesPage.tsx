import { useEffect, useState, type FormEvent } from 'react'
import type { FilePage, IndexedFile, Overview } from '../../main/contracts'
import { bytes, date } from './format'

const PAGE_SIZE = 50

export function FilesPage({ overview }: { overview: Overview | null }) {
  const [draft, setDraft] = useState('')
  const [search, setSearch] = useState('')
  const [category, setCategory] = useState('')
  const [extensionDraft, setExtensionDraft] = useState('')
  const [extension, setExtension] = useState('')
  const [locationDraft, setLocationDraft] = useState('')
  const [location, setLocation] = useState('')
  const [sortBy, setSortBy] = useState<'size' | 'name' | 'modified_time'>('size')
  const [sortDesc, setSortDesc] = useState(true)
  const [offset, setOffset] = useState(0)
  const [result, setResult] = useState<FilePage | null>(null)
  const [selected, setSelected] = useState<IndexedFile | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    setLoading(true)
    setError(null)
    setResult(null)
    setSelected(null)
    void window.arbor.getFiles({ offset, limit: PAGE_SIZE, search, category, extension, locationPrefix: location, sortBy, sortDesc })
      .then((data) => { if (active) { setResult(data); setSelected(null) } })
      .catch((cause) => { if (active) setError(String(cause)) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [offset, search, category, extension, location, sortBy, sortDesc])

  const submit = (event: FormEvent) => { event.preventDefault(); setOffset(0); setSearch(draft.trim()); setExtension(extensionDraft.trim()); setLocation(locationDraft.trim()) }
  const changeCategory = (value: string) => { setCategory(value); setOffset(0) }
  const changeSort = (value: 'size' | 'name' | 'modified_time') => { setSortBy(value); setOffset(0) }

  return <>
    <div className="page-heading"><div><p className="eyebrow">INDEXED FILES</p><h1>Explore files</h1><p className="subhead">Search and sort the current scan index. Sizes are logical file bytes.</p></div><span className="muted">{(result?.total ?? 0).toLocaleString()} {result?.total === 1 ? 'match' : 'matches'}</span></div>
    <section className="panel explorer-panel">
      <form className="explorer-filters" onSubmit={submit}>
        <label>Search name or path<input value={draft} onChange={(event) => setDraft(event.target.value)} maxLength={200} placeholder="Search indexed files" /></label>
        <button className="secondary-button" type="submit">Search</button>
        <label>Category<select value={category} onChange={(event) => changeCategory(event.target.value)}><option value="">All categories</option>{(overview?.categories ?? []).map((item) => <option key={item.category} value={item.category}>{item.category}</option>)}</select></label>
        <label>Sort by<select value={sortBy} onChange={(event) => changeSort(event.target.value as typeof sortBy)}><option value="size">Size</option><option value="name">Name</option><option value="modified_time">Modified</option></select></label>
        <button className="secondary-button" type="button" onClick={() => setSortDesc((value) => !value)} aria-label={sortDesc ? 'Sort ascending' : 'Sort descending'}>{sortDesc ? 'Descending' : 'Ascending'}</button>
        <label>Extension<input value={extensionDraft} onChange={(event) => setExtensionDraft(event.target.value)} maxLength={32} placeholder="e.g. .psd" /></label>
        <label>Location prefix<input value={locationDraft} onChange={(event) => setLocationDraft(event.target.value)} maxLength={32767} placeholder="e.g. C:\\Users\\" /></label>
      </form>
      {error && <div className="inline-error" role="alert">{error}</div>}
      <div className="table-scroll"><table className="data-table"><thead><tr><th>Name</th><th>Category</th><th>Size</th><th>Modified</th></tr></thead><tbody>
        {(result?.files ?? []).map((file) => <tr key={file.path} tabIndex={0} aria-label={`Show details for ${file.name}`} className={selected?.path === file.path ? 'selected' : ''} onClick={() => setSelected(file)} onKeyDown={(event) => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); setSelected(file) } }}><td><strong>{file.name}</strong><small title={file.path}>{file.parent}</small></td><td>{file.category}</td><td>{bytes(file.logicalBytes)}</td><td>{date(file.modifiedTime)}</td></tr>)}
      </tbody></table></div>
      {loading && <p className="empty" role="status">Loading indexed files…</p>}
      {!loading && result?.files.length === 0 && <p className="empty">No indexed files match these filters.</p>}
      <div className="table-footer"><span>{result?.total ? `${offset + 1}–${Math.min(offset + PAGE_SIZE, result.total)} of ${result.total.toLocaleString()}` : 'No rows'}</span><div><button className="secondary-button" onClick={() => setOffset((value) => Math.max(0, value - PAGE_SIZE))} disabled={offset === 0 || loading}>Previous</button><button className="secondary-button" onClick={() => setOffset((value) => value + PAGE_SIZE)} disabled={loading || !result || offset + PAGE_SIZE >= result.total}>Next</button></div></div>
    </section>
    {selected && <section className="panel detail-panel"><h2>File details</h2><dl><dt>Path</dt><dd>{selected.path}</dd><dt>Logical size</dt><dd>{bytes(selected.logicalBytes)}</dd><dt>Category</dt><dd>{selected.category}</dd><dt>Modified</dt><dd>{date(selected.modifiedTime)}</dd><dt>Accessible during scan</dt><dd>{selected.accessible ? 'Yes' : 'No'}</dd></dl></section>}
  </>
}
