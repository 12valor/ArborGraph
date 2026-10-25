import { contextBridge, ipcRenderer } from 'electron'
import type { DesktopSettings, DeveloperEcosystem, DirectoryContents, Drive, DuplicateOverview, DuplicateResults, DuplicateStatus, FileAgeInsights, FilePage, FileQuery, IndexedFolder, LegalStatus, Overview, PhotoshopInsights, ScanComparison, ScanEvent, ScanHistoryItem, ScanSettings } from './contracts'

const api = {
  getLegal: (): Promise<LegalStatus> => ipcRenderer.invoke('arbor:get-legal'),
  acceptEula: (accepted: boolean): Promise<{ accepted: boolean; version: string }> => ipcRenderer.invoke('arbor:accept-eula', accepted),
  declineEula: (): Promise<void> => ipcRenderer.invoke('arbor:decline-eula'),
  getSettings: (): Promise<DesktopSettings> => ipcRenderer.invoke('arbor:get-settings'),
  setScanSettings: (value: ScanSettings): Promise<DesktopSettings> => ipcRenderer.invoke('arbor:set-scan-settings', value),
  addExclusion: (): Promise<DesktopSettings | null> => ipcRenderer.invoke('arbor:add-exclusion'),
  removeExclusion: (path: string): Promise<DesktopSettings> => ipcRenderer.invoke('arbor:remove-exclusion', path),
  listDrives: (): Promise<Drive[]> => ipcRenderer.invoke('arbor:list-drives'),
  getOverview: (): Promise<Overview> => ipcRenderer.invoke('arbor:get-overview'),
  getFiles: (query: FileQuery): Promise<FilePage> => ipcRenderer.invoke('arbor:get-files', query),
  getLargestFolders: (limit: number): Promise<IndexedFolder[]> => ipcRenderer.invoke('arbor:get-largest-folders', limit),
  getDirectoryContents: (path: string | null, limit = 100): Promise<DirectoryContents> => ipcRenderer.invoke('arbor:get-directory-contents', { path, limit }),
  getScanHistory: (limit: number): Promise<ScanHistoryItem[]> => ipcRenderer.invoke('arbor:get-scan-history', limit),
  compareScans: (olderId: number, newerId: number): Promise<ScanComparison> => ipcRenderer.invoke('arbor:compare-scans', olderId, newerId),
  getFileAgeInsights: (daysOld: number, limit: number): Promise<FileAgeInsights> => ipcRenderer.invoke('arbor:get-file-age-insights', daysOld, limit),
  getPhotoshopInsights: (limit: number): Promise<PhotoshopInsights> => ipcRenderer.invoke('arbor:get-photoshop-insights', limit),
  getDuplicateOverview: (): Promise<DuplicateOverview> => ipcRenderer.invoke('arbor:get-duplicate-overview'),
  getDuplicateStatus: (): Promise<DuplicateStatus> => ipcRenderer.invoke('arbor:get-duplicate-status'),
  startDuplicateAnalysis: (): Promise<{ accepted: boolean }> => ipcRenderer.invoke('arbor:start-duplicate-analysis'),
  cancelDuplicateAnalysis: (): Promise<{ cancelling: boolean }> => ipcRenderer.invoke('arbor:cancel-duplicate-analysis'),
  getDuplicateResults: (offset: number, limit: number): Promise<DuplicateResults> => ipcRenderer.invoke('arbor:get-duplicate-results', offset, limit),
  getIndexedDeveloperStorage: (): Promise<DeveloperEcosystem[]> => ipcRenderer.invoke('arbor:get-indexed-developer-storage'),
  chooseFolder: (): Promise<string | null> => ipcRenderer.invoke('arbor:choose-folder'),
  startScan: (roots: string[]): Promise<{ accepted: boolean; roots: string[] }> => ipcRenderer.invoke('arbor:start-scan', roots),
  cancelScan: (): Promise<{ cancelling: boolean }> => ipcRenderer.invoke('arbor:cancel-scan'),
  onScanEvent: (callback: (event: ScanEvent) => void): (() => void) => {
    const listener = (_event: Electron.IpcRendererEvent, payload: ScanEvent) => callback(payload)
    ipcRenderer.on('arbor:scan-event', listener)
    return () => ipcRenderer.removeListener('arbor:scan-event', listener)
  },
}

contextBridge.exposeInMainWorld('arbor', api)
