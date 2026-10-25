export interface Drive {
  path: string
  label: string
  totalBytes: number
  freeBytes: number
  usedBytes: number
  fileSystem: string
}

export interface Category {
  category: string
  fileCount: number
  logicalBytes: number
}

export interface Overview {
  indexedFiles: number
  indexedLogicalBytes: number
  categories: Category[]
}

export interface FileQuery {
  offset?: number
  limit?: number
  search?: string
  category?: string
  extension?: string
  locationPrefix?: string
  sortBy?: 'size' | 'name' | 'path' | 'modified_time' | 'category' | 'extension'
  sortDesc?: boolean
}

export interface IndexedFile {
  path: string
  name: string
  parent: string
  logicalBytes: number
  modifiedTime: number
  extension: string
  category: string
  accessible: boolean
}

export interface FilePage {
  total: number
  offset: number
  limit: number
  files: IndexedFile[]
}

export interface IndexedFolder {
  path: string
  name: string
  parent: string
  logicalBytes: number
  fileCount: number
  subfolderCount: number
}

export interface DirectoryItem {
  path: string
  name: string
  logicalBytes: number
  isDirectory: boolean
  fileCount: number
  category: string
}

export interface DirectoryContents {
  path: string | null
  logicalBytes: number
  totalItems: number
  items: DirectoryItem[]
}

export interface ScanHistoryItem {
  id: number
  startedAt: number
  finishedAt: number
  roots: string
  state: string
  filesDiscovered: number
  filesIndexed: number
  filesSkipped: number
  directoriesProcessed: number
  directoriesSkipped: number
  indexedLogicalBytes: number
}

export interface CategoryComparison {
  category: string
  previousBytes: number
  currentBytes: number
  deltaBytes: number
  previousFiles: number
  currentFiles: number
  deltaFiles: number
}

export interface ScanComparison {
  olderId: number
  newerId: number
  roots: string
  coverageWarning: boolean
  previousBytes: number
  currentBytes: number
  deltaBytes: number
  previousFiles: number
  currentFiles: number
  deltaFiles: number
  categories: CategoryComparison[]
}

export interface ScanEvent {
  eventName: 'scanProgress' | 'scanFinished' | 'scanFailed' | 'engineExited'
  sequence?: number
  state?: string
  filesDiscovered?: number
  filesIndexed?: number
  directoriesProcessed?: number
  directoriesSkipped?: number
  filesSkipped?: number
  indexedLogicalBytes?: number
  elapsedMilliseconds?: number
  currentDirectory?: string
  message?: string
}
