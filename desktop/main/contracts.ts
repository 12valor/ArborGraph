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
