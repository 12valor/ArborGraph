import { app, BrowserWindow, dialog, ipcMain, Menu, session } from 'electron'
import path from 'node:path'
import { EngineClient, findEngineExecutable } from './engine-client'
import { validateDirectoryQuery, validateDuplicatePage, validateFileInsightsQuery, validateFileQuery, validateFolderLimit, validateHistoryLimit, validateScanIds, validateScanRoots } from './validation'
import type { Drive, ScanEvent } from './contracts'

let window: BrowserWindow | null = null
let engine: EngineClient | null = null
let isQuitting = false
const allowedRoots = new Set<string>()

function requireWindowSender(senderId: number): void {
  if (!window || senderId !== window.webContents.id || window.webContents.isDestroyed()) {
    throw new Error('Untrusted IPC sender.')
  }
}

function engineRequest(command: string, args: Record<string, unknown> = {}, timeoutMs?: number): Promise<unknown> {
  if (!engine) throw new Error('Engine is unavailable.')
  return engine.request(command, args, timeoutMs)
}

function registerIpc(): void {
  ipcMain.handle('arbor:get-legal', (event) => {
    requireWindowSender(event.sender.id)
    return engineRequest('getLegal')
  })
  ipcMain.handle('arbor:accept-eula', (event, accepted: unknown) => {
    requireWindowSender(event.sender.id)
    if (accepted !== true) throw new Error('Check the license agreement before continuing.')
    return engineRequest('acceptEula', { accepted: true })
  })
  ipcMain.handle('arbor:decline-eula', (event) => {
    requireWindowSender(event.sender.id)
    app.quit()
  })
  ipcMain.handle('arbor:list-drives', async (event) => {
    requireWindowSender(event.sender.id)
    const result = await engineRequest('listDrives') as Drive[]
    for (const drive of result) allowedRoots.add(path.resolve(drive.path).toLowerCase())
    return result
  })
  ipcMain.handle('arbor:get-overview', (event) => {
    requireWindowSender(event.sender.id)
    return engineRequest('getOverview')
  })
  ipcMain.handle('arbor:get-files', (event, query: unknown) => {
    requireWindowSender(event.sender.id)
    return engineRequest('getFiles', { ...validateFileQuery(query) })
  })
  ipcMain.handle('arbor:get-largest-folders', (event, limit: unknown) => {
    requireWindowSender(event.sender.id)
    return engineRequest('getLargestFolders', { limit: validateFolderLimit(limit) })
  })
  ipcMain.handle('arbor:get-directory-contents', (event, query: unknown) => {
    requireWindowSender(event.sender.id)
    return engineRequest('getDirectoryContents', validateDirectoryQuery(query))
  })
  ipcMain.handle('arbor:get-scan-history', (event, limit: unknown) => {
    requireWindowSender(event.sender.id)
    return engineRequest('getScanHistory', { limit: validateHistoryLimit(limit) })
  })
  ipcMain.handle('arbor:compare-scans', (event, olderId: unknown, newerId: unknown) => {
    requireWindowSender(event.sender.id)
    return engineRequest('compareScans', { ...validateScanIds(olderId, newerId) })
  })
  ipcMain.handle('arbor:get-file-age-insights', (event, daysOld: unknown, limit: unknown) => {
    requireWindowSender(event.sender.id)
    return engineRequest('getFileAgeInsights', { ...validateFileInsightsQuery(daysOld, limit) })
  })
  ipcMain.handle('arbor:get-photoshop-insights', (event, limit: unknown) => {
    requireWindowSender(event.sender.id)
    return engineRequest('getPhotoshopInsights', { limit: validateFileInsightsQuery(180, limit).limit }, 60_000)
  })
  ipcMain.handle('arbor:get-duplicate-overview', (event) => {
    requireWindowSender(event.sender.id)
    return engineRequest('getDuplicateOverview')
  })
  ipcMain.handle('arbor:get-duplicate-status', (event) => {
    requireWindowSender(event.sender.id)
    return engineRequest('getDuplicateStatus')
  })
  ipcMain.handle('arbor:start-duplicate-analysis', (event) => {
    requireWindowSender(event.sender.id)
    return engineRequest('startDuplicateAnalysis')
  })
  ipcMain.handle('arbor:cancel-duplicate-analysis', (event) => {
    requireWindowSender(event.sender.id)
    return engineRequest('cancelDuplicateAnalysis')
  })
  ipcMain.handle('arbor:get-duplicate-results', (event, offset: unknown, limit: unknown) => {
    requireWindowSender(event.sender.id)
    return engineRequest('getDuplicateResults', validateDuplicatePage(offset, limit))
  })
  ipcMain.handle('arbor:choose-folder', async (event) => {
    requireWindowSender(event.sender.id)
    const result = await dialog.showOpenDialog(window!, { properties: ['openDirectory'], title: 'Choose a folder to scan' })
    if (result.canceled || result.filePaths.length !== 1) return null
    const selected = path.resolve(result.filePaths[0])
    allowedRoots.add(selected.toLowerCase())
    return selected
  })
  ipcMain.handle('arbor:start-scan', (event, roots: unknown) => {
    requireWindowSender(event.sender.id)
    return engineRequest('startScan', { roots: validateScanRoots(roots, allowedRoots) })
  })
  ipcMain.handle('arbor:cancel-scan', (event) => {
    requireWindowSender(event.sender.id)
    return engineRequest('cancelScan')
  })
}

async function createWindow(): Promise<void> {
  window = new BrowserWindow({
    width: 1360, height: 900, minWidth: 960, minHeight: 680,
    show: false,
    backgroundColor: '#f6f7f9',
    title: 'ArborGraph',
    icon: path.resolve(app.getAppPath(), '..', 'Resources', 'ArborGraph.ico'),
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
      webviewTag: false,
    },
  })
  window.webContents.setWindowOpenHandler(() => ({ action: 'deny' }))
  window.webContents.on('will-navigate', (event) => event.preventDefault())
  window.once('ready-to-show', () => window?.show())
  await window.loadFile(path.join(__dirname, '..', 'renderer', 'index.html'))
}

if (!app.requestSingleInstanceLock()) {
  app.quit()
} else {
  app.on('second-instance', () => { window?.restore(); window?.focus() })
  app.whenReady().then(async () => {
    Menu.setApplicationMenu(null)
    session.defaultSession.setPermissionRequestHandler((_contents, _permission, callback) => callback(false))
    session.defaultSession.setPermissionCheckHandler(() => false)
    registerIpc()
    const testDataDir = !app.isPackaged ? process.env.ARBORGRAPH_DEV_DATA_DIR : undefined
    engine = new EngineClient(
      findEngineExecutable(app.getAppPath(), process.resourcesPath, app.isPackaged),
      testDataDir ? ['--data-dir', testDataDir] : [],
    )
    engine.on('event', (message: ScanEvent) => {
      if (window && !window.isDestroyed()) window.webContents.send('arbor:scan-event', message)
    })
    await engine.start()
    await createWindow()
  }).catch((error) => { dialog.showErrorBox('ArborGraph startup failed', String(error)); app.quit() })
}

app.on('before-quit', (event) => {
  if (isQuitting || !engine) return
  event.preventDefault()
  isQuitting = true
  void engine.stop().finally(() => app.quit())
})
app.on('window-all-closed', () => app.quit())
