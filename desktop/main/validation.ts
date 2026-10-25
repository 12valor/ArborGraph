import path from 'node:path'

export function validateScanRoots(value: unknown, allowedRoots: ReadonlySet<string>): string[] {
  if (!Array.isArray(value) || value.length < 1 || value.length > 16) {
    throw new Error('Select between 1 and 16 scan targets.')
  }
  const roots: string[] = []
  const unique = new Set<string>()
  for (const item of value) {
    if (typeof item !== 'string' || item.length === 0 || item.length > 32767 || !path.isAbsolute(item)) {
      throw new Error('Scan targets must be selected absolute paths.')
    }
    const canonical = path.resolve(item).toLowerCase()
    if (!allowedRoots.has(canonical) || unique.has(canonical)) {
      throw new Error('Choose distinct targets from the drive list or folder picker.')
    }
    unique.add(canonical)
    roots.push(item)
  }
  return roots
}
