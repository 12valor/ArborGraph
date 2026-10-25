import type { DirectoryContents, Drive, FileAgeInsights, FilePage, FileQuery, IndexedFolder, Overview, PhotoshopInsights, ScanComparison, ScanEvent, ScanHistoryItem } from '../../main/contracts'

interface ArborApi {
  listDrives(): Promise<Drive[]>
  getOverview(): Promise<Overview>
  getFiles(query: FileQuery): Promise<FilePage>
  getLargestFolders(limit: number): Promise<IndexedFolder[]>
  getDirectoryContents(path: string | null, limit?: number): Promise<DirectoryContents>
  getScanHistory(limit: number): Promise<ScanHistoryItem[]>
  compareScans(olderId: number, newerId: number): Promise<ScanComparison>
  getFileAgeInsights(daysOld: number, limit: number): Promise<FileAgeInsights>
  getPhotoshopInsights(limit: number): Promise<PhotoshopInsights>
  chooseFolder(): Promise<string | null>
  startScan(roots: string[]): Promise<{ accepted: boolean; roots: string[] }>
  cancelScan(): Promise<{ cancelling: boolean }>
  onScanEvent(callback: (event: ScanEvent) => void): () => void
}

declare global {
  interface Window { arbor: ArborApi }
}
