import { useEffect, useState } from 'react'
import type { DeveloperEcosystem } from '../../main/contracts'
import { bytes } from './format'

export function DeveloperPage() {
  const [ecosystems, setEcosystems] = useState<DeveloperEcosystem[] | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [revision, setRevision] = useState(0)
  useEffect(() => {
    let active = true
    setLoading(true); setError(null)
    void window.arbor.getIndexedDeveloperStorage()
      .then((data) => { if (active) setEcosystems(data.filter((item) => item.itemCount > 0)) })
      .catch((cause) => { if (active) setError(String(cause)) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [revision])

  return <>
    <div className="page-heading"><div><p className="eyebrow">PROJECT STORAGE</p><h1>Developer Storage</h1><p className="subhead">Project dependencies and build output identified within the current scan index.</p></div><button className="secondary-button" disabled={loading} onClick={() => setRevision((value) => value + 1)}>Refresh</button></div>
    {error && <div className="inline-error" role="alert">{error}</div>}{loading && <p role="status">Checking indexed project folders…</p>}
    {!loading && ecosystems && (ecosystems.length ? ecosystems.map((ecosystem) => <section className="panel developer-panel" key={ecosystem.name}><div className="section-heading"><div><h2>{ecosystem.name}</h2><p>{ecosystem.itemCount.toLocaleString()} identified folders · {bytes(ecosystem.logicalBytes)} indexed logical bytes</p></div></div><div className="table-scroll"><table className="data-table"><thead><tr><th>Folder</th><th>Project</th><th>Type</th><th>Files</th><th>Logical size</th></tr></thead><tbody>{ecosystem.items.map((item) => <tr key={item.path}><td><strong>{item.name}</strong><small title={item.path}>{item.path}</small></td><td>{item.projectName}</td><td title={item.reason}>{item.type}</td><td>{item.fileCount.toLocaleString()}</td><td>{bytes(item.logicalBytes)}</td></tr>)}</tbody></table></div>{ecosystem.itemsTruncated && <p className="map-note">Showing {ecosystem.items.length.toLocaleString()} of {ecosystem.itemCount.toLocaleString()} identified folders in this ecosystem.</p>}</section>) : <section className="panel"><p className="empty">No indexed developer folders matched the existing project context checks.</p></section>)}
    <p className="map-note">These are indexed directory totals, not measured reclaimable space. Global package caches are outside this view. No folders are changed.</p>
  </>
}
