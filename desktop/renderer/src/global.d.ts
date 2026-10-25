import type { DesktopSettings, DirectoryContents, Drive, DuplicateOverview, DuplicateResults, DuplicateStatus, FileAgeInsights, FilePage, FileQuery, IndexedFolder, LegalStatus, Overview, PhotoshopInsights, ScanComparison, ScanEvent, ScanHistoryItem, ScanSettings } from '../../main/contracts'

interface ArborApi {
  getLegal(): Promise<LegalStatus>
  acceptEula(accepted: boolean): Promise<{ accepted: boolean; version: string }>
  declineEula(): Promise<void>
  getSettings(): Promise<DesktopSettings>
  setScanSettings(value: ScanSettings): Promise<DesktopSettings>
  addExclusion(): Promise<DesktopSettings | null>
  removeExclusion(path: string): Promise<DesktopSettings>
  listDrives(): Promise<Drive[]>
  getOverview(): Promise<Overview>
  getFiles(query: FileQuery): Promise<FilePage>
  getLargestFolders(limit: number): Promise<IndexedFolder[]>
  getDirectoryContents(path: string | null, limit?: number): Promise<DirectoryContents>
  getScanHistory(limit: number): Promise<ScanHistoryItem[]>
  compareScans(olderId: number, newerId: number): Promise<ScanComparison>
  getFileAgeInsights(daysOld: number, limit: number): Promise<FileAgeInsights>
  getPhotoshopInsights(limit: number): Promise<PhotoshopInsights>
  getDuplicateOverview(): Promise<DuplicateOverview>
  getDuplicateStatus(): Promise<DuplicateStatus>
  startDuplicateAnalysis(): Promise<{ accepted: boolean }>
  cancelDuplicateAnalysis(): Promise<{ cancelling: boolean }>
  getDuplicateResults(offset: number, limit: number): Promise<DuplicateResults>
  chooseFolder(): Promise<string | null>
  startScan(roots: string[]): Promise<{ accepted: boolean; roots: string[] }>
  cancelScan(): Promise<{ cancelling: boolean }>
  onScanEvent(callback: (event: ScanEvent) => void): () => void
}

declare global {
  interface Window { arbor: ArborApi }
}
