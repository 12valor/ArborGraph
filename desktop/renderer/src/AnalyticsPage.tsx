import { useEffect, useState } from 'react'
import type { FilePage, IndexedFolder, Overview } from '../../main/contracts'
import { bytes } from './format'

export function AnalyticsPage() {
  const [data, setData] = useState<{ overview: Overview; files: FilePage; folders: IndexedFolder[] } | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    let active = true
    void Promise.all([window.arbor.getOverview(), window.arbor.getFiles({ limit: 10, sortBy: 'size', sortDesc: true }), window.arbor.getLargestFolders(100)])
      .then(([overview, files, folders]) => { if (active) setData({ overview, files, folders }) })
      .catch((cause) => { if (active) setError(String(cause)) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [])

  const total = data?.overview.indexedLogicalBytes ?? 0
  const categories = [...(data?.overview.categories ?? [])].sort((a, b) => b.logicalBytes - a.logicalBytes)
  const folders = (data?.folders ?? []).filter((folder, _index, all) =>
    !all.some((child) => child.path !== folder.path && child.logicalBytes === folder.logicalBytes && child.path.toLowerCase().startsWith(`${folder.path.replace(/[\\/]+$/, '')}\\`.toLowerCase()))
  ).slice(0, 10)
  const largestFolder = Math.max(1, ...folders.map((item) => item.logicalBytes))

  return <>
    <div className="page-heading"><div><p className="eyebrow">INDEX ANALYSIS</p><h1>Analytics</h1><p className="subhead">Rankings come from the current scan index and use logical file sizes.</p></div></div>
    {error && <div className="inline-error" role="alert">{error}</div>}
    {loading && <p role="status">Loading indexed analysis…</p>}
    {!loading && data && <>
      <div className="metric-grid"><div className="metric accent"><span>Indexed logical bytes</span><strong>{bytes(total)}</strong><small>Not physical disk usage</small></div><div className="metric"><span>Indexed files</span><strong>{data.overview.indexedFiles.toLocaleString()}</strong><small>Current index</small></div><div className="metric"><span>Categories</span><strong>{categories.length}</strong><small>With indexed files</small></div><div className="metric"><span>Largest file</span><strong>{bytes(data.files.files[0]?.logicalBytes ?? 0)}</strong><small>Across current index</small></div></div>
      <div className="analytics-columns"><section className="panel"><div className="section-heading"><div><h2>Category breakdown</h2><p>Share of indexed logical file bytes.</p></div></div>{categories.length ? <div className="category-list">{categories.map((item) => <div className="category-row" key={item.category}><div><strong>{item.category}</strong><span>{bytes(item.logicalBytes)} · {total ? (item.logicalBytes / total * 100).toFixed(1) : '0.0'}%</span></div><div className="bar"><span style={{ width: `${total ? item.logicalBytes / total * 100 : 0}%` }} /></div><small>{item.fileCount.toLocaleString()} files</small></div>)}</div> : <p className="empty">No indexed categories yet.</p>}</section>
        <section className="panel"><div className="section-heading"><div><h2>Largest folders</h2><p>Recursive sizes; identical-size ancestors are hidden. Remaining rows can overlap.</p></div></div>{folders.length ? <div className="rank-list">{folders.map((item, index) => <div key={item.path} className="rank-row"><span>{index + 1}</span><div><strong title={item.path}>{item.name}</strong><small title={item.path}>{item.path}</small><div className="bar"><span style={{ width: `${item.logicalBytes / largestFolder * 100}%` }} /></div></div><b>{bytes(item.logicalBytes)}</b></div>)}</div> : <p className="empty">No indexed folders yet.</p>}</section></div>
      <section className="panel"><div className="section-heading"><div><h2>Largest files</h2><p>Individual files in the current index.</p></div></div>{data.files.files.length ? <div className="table-scroll"><table className="data-table"><thead><tr><th>Name</th><th>Category</th><th>Logical size</th></tr></thead><tbody>{data.files.files.map((file) => <tr key={file.path}><td><strong>{file.name}</strong><small title={file.path}>{file.path}</small></td><td>{file.category}</td><td>{bytes(file.logicalBytes)}</td></tr>)}</tbody></table></div> : <p className="empty">No indexed files yet.</p>}</section>
    </>}
  </>
}
