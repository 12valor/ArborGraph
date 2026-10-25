import { contextBridge, ipcRenderer } from 'electron'
import type { DirectoryContents, Drive, FileAgeInsights, FilePage, FileQuery, IndexedFolder, Overview, PhotoshopInsights, ScanComparison, ScanEvent, ScanHistoryItem } from './contracts'

const api = {
  listDrives: (): Promise<Drive[]> => ipcRenderer.invoke('arbor:list-drives'),
  getOverview: (): Promise<Overview> => ipcRenderer.invoke('arbor:get-overview'),
  getFiles: (query: FileQuery): Promise<FilePage> => ipcRenderer.invoke('arbor:get-files', query),
  getLargestFolders: (limit: number): Promise<IndexedFolder[]> => ipcRenderer.invoke('arbor:get-largest-folders', limit),
  getDirectoryContents: (path: string | null, limit = 100): Promise<DirectoryContents> => ipcRenderer.invoke('arbor:get-directory-contents', { path, limit }),
  getScanHistory: (limit: number): Promise<ScanHistoryItem[]> => ipcRenderer.invoke('arbor:get-scan-history', limit),
  compareScans: (olderId: number, newerId: number): Promise<ScanComparison> => ipcRenderer.invoke('arbor:compare-scans', olderId, newerId),
  getFileAgeInsights: (daysOld: number, limit: number): Promise<FileAgeInsights> => ipcRenderer.invoke('arbor:get-file-age-insights', daysOld, limit),
  getPhotoshopInsights: (limit: number): Promise<PhotoshopInsights> => ipcRenderer.invoke('arbor:get-photoshop-insights', limit),
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
