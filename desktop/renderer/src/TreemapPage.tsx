import { useEffect, useMemo, useState } from 'react'
import type { DirectoryContents, DirectoryItem } from '../../main/contracts'
import { bytes } from './format'

type Tile = { item: DirectoryItem; x: number; y: number; w: number; h: number }

function layout(items: DirectoryItem[], x = 0, y = 0, w = 100, h = 100): Tile[] {
  if (items.length === 0) return []
  if (items.length === 1) return [{ item: items[0], x, y, w, h }]
  const total = items.reduce((sum, item) => sum + item.logicalBytes, 0)
  let first = 0
  let split = 0
  while (split < items.length - 1 && first < total / 2) first += items[split++].logicalBytes
  const fraction = total ? first / total : .5
  if (w * 9.6 >= h * 5) {
    const left = w * fraction
    return [...layout(items.slice(0, split), x, y, left, h), ...layout(items.slice(split), x + left, y, w - left, h)]
  }
  const top = h * fraction
  return [...layout(items.slice(0, split), x, y, w, top), ...layout(items.slice(split), x, y + top, w, h - top)]
}

export function TreemapPage() {
  const [trail, setTrail] = useState<string[]>([])
  const [contents, setContents] = useState<DirectoryContents | null>(null)
  const [selected, setSelected] = useState<DirectoryItem | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const current = trail.at(-1) ?? null

  useEffect(() => {
    let active = true
    setLoading(true)
    setError(null)
    setSelected(null)
    setContents(null)
    void window.arbor.getDirectoryContents(current, 100)
      .then((value) => { if (active) setContents(value) })
      .catch((cause) => { if (active) setError(String(cause)) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [current])

  const items = useMemo(() => {
    if (!contents) return []
    const listed = [...contents.items]
    const remainder = Math.max(0, contents.logicalBytes - listed.reduce((sum, item) => sum + item.logicalBytes, 0))
    if (remainder > 0 && contents.totalItems > listed.length) listed.push({ path: '', name: `${(contents.totalItems - listed.length).toLocaleString()} more items`, logicalBytes: remainder, isDirectory: false, fileCount: contents.totalItems - listed.length, category: 'Other' })
    return listed.sort((a, b) => b.logicalBytes - a.logicalBytes)
  }, [contents])
  const tiles = useMemo(() => layout(items.filter((item) => item.logicalBytes > 0)), [items])
  const open = (item: DirectoryItem) => { if (item.isDirectory) setTrail((old) => [...old, item.path]); else setSelected(item) }

  return <>
    <div className="page-heading"><div><p className="eyebrow">DIRECTORY MAP</p><h1>Treemap</h1><p className="subhead">Each rectangle represents indexed logical bytes at this directory level.</p></div><span className="muted">{bytes(contents?.logicalBytes ?? 0)} at this level</span></div>
    <div className="breadcrumbs"><button onClick={() => setTrail([])}>Indexed drives</button>{trail.map((path, index) => <span key={path}> / <button onClick={() => setTrail((old) => old.slice(0, index + 1))}>{path.split('\\').filter(Boolean).at(-1) ?? path}</button></span>)}</div>
    {error && <div className="inline-error" role="alert">{error}</div>}
    {loading && <p role="status">Loading directory map…</p>}
    {!loading && contents && <>
      <section className="panel map-panel">
        <div className="section-heading"><div><h2>{current ?? 'Indexed drives'}</h2><p>{contents.totalItems.toLocaleString()} direct items · top {contents.items.length.toLocaleString()} shown</p></div>{trail.length > 0 && <button className="secondary-button" onClick={() => setTrail((old) => old.slice(0, -1))}>Up one level</button>}</div>
        {tiles.length > 0 ? <div className="treemap-canvas" role="group" aria-label="Directory size treemap">{tiles.map((tile) => <button key={tile.item.path || 'other'} className={`treemap-tile ${tile.item.isDirectory ? 'folder-tile' : 'file-tile'}`} style={{ left: `${tile.x}%`, top: `${tile.y}%`, width: `${tile.w}%`, height: `${tile.h}%` }} onClick={() => open(tile.item)} title={`${tile.item.name} — ${bytes(tile.item.logicalBytes)}`}><strong>{tile.w > 10 && tile.h > 8 ? tile.item.name : ''}</strong>{tile.w > 13 && tile.h > 14 && <small>{bytes(tile.item.logicalBytes)}</small>}</button>)}</div> : <div className="empty">No indexed file bytes are available at this level.</div>}
        {contents.totalItems > contents.items.length && <p className="map-note">Showing the largest {contents.items.length} items. Any remaining nonzero bytes appear as one grouped tile.</p>}
      </section>
      <div className="map-lower"><section className="panel"><div className="section-heading"><div><h2>Contents</h2><p>Open a folder to inspect its immediate children.</p></div></div><div className="directory-list">{contents.items.map((item) => <button key={item.path} onClick={() => { setSelected(item); if (item.isDirectory) open(item) }}><span>{item.isDirectory ? '▣' : '▤'} {item.name}</span><strong>{bytes(item.logicalBytes)}</strong></button>)}</div></section><section className="panel detail-panel"><h2>Selected item</h2>{selected ? <dl><dt>Name</dt><dd>{selected.name}</dd><dt>Path</dt><dd>{selected.path || 'Grouped remainder'}</dd><dt>Logical size</dt><dd>{bytes(selected.logicalBytes)}</dd><dt>Type</dt><dd>{selected.isDirectory ? 'Folder' : selected.category}</dd>{selected.isDirectory && <><dt>Files below</dt><dd>{selected.fileCount.toLocaleString()}</dd></>}</dl> : <p className="empty">Choose a file tile for details. Select a folder to open it.</p>}</section></div>
    </>}
  </>
}
