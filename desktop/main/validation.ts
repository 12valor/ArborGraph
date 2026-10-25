import path from 'node:path'
import type { FileQuery } from './contracts'

export function validateFileQuery(value: unknown): FileQuery {
  if (!value || typeof value !== 'object' || Array.isArray(value)) throw new Error('Invalid file query.')
  const query = value as Record<string, unknown>
  const allowed = new Set(['offset', 'limit', 'search', 'category', 'extension', 'locationPrefix', 'sortBy', 'sortDesc'])
  if (Object.keys(query).some((key) => !allowed.has(key))) throw new Error('Unsupported file query option.')
  const integer = (name: string, fallback: number, min: number, max: number): number => {
    const value = query[name] ?? fallback
    if (!Number.isInteger(value) || (value as number) < min || (value as number) > max) throw new Error(`Invalid ${name}.`)
    return value as number
  }
  const optional = (name: string, max: number): string | undefined => {
    const value = query[name]
    if (value === undefined || value === null || value === '') return undefined
    if (typeof value !== 'string' || value.length > max) throw new Error(`Invalid ${name}.`)
    return value
  }
  const sortBy = optional('sortBy', 32) ?? 'size'
  if (!['size', 'name', 'path', 'modified_time', 'category', 'extension'].includes(sortBy)) throw new Error('Invalid sortBy.')
  const sortDesc = query.sortDesc ?? true
  if (typeof sortDesc !== 'boolean') throw new Error('Invalid sortDesc.')
  return {
    offset: integer('offset', 0, 0, 10_000_000), limit: integer('limit', 50, 1, 200),
    search: optional('search', 200), category: optional('category', 80),
    extension: optional('extension', 32), locationPrefix: optional('locationPrefix', 32767),
    sortBy: sortBy as FileQuery['sortBy'], sortDesc,
  }
}

export function validateDirectoryQuery(value: unknown): { path: string | null; limit: number } {
  if (!value || typeof value !== 'object' || Array.isArray(value)) throw new Error('Invalid directory query.')
  const query = value as Record<string, unknown>
  if (Object.keys(query).some((key) => !['path', 'limit'].includes(key))) throw new Error('Unsupported directory query option.')
  const selected = query.path ?? null
  if (selected !== null && (typeof selected !== 'string' || selected.length > 32767 || !path.isAbsolute(selected))) {
    throw new Error('Choose an indexed absolute directory.')
  }
  const limit = query.limit ?? 100
  if (!Number.isInteger(limit) || (limit as number) < 1 || (limit as number) > 150) throw new Error('Invalid directory limit.')
  return { path: selected as string | null, limit: limit as number }
}

export function validateFolderLimit(value: unknown): number {
  if (!Number.isInteger(value) || (value as number) < 1 || (value as number) > 100) throw new Error('Invalid folder limit.')
  return value as number
}

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
