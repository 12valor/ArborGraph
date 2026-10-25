import { contextBridge, ipcRenderer } from 'electron'
import type { Drive, Overview, ScanEvent } from './contracts'

const api = {
  listDrives: (): Promise<Drive[]> => ipcRenderer.invoke('arbor:list-drives'),
  getOverview: (): Promise<Overview> => ipcRenderer.invoke('arbor:get-overview'),
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
