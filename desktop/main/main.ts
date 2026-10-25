import { app, BrowserWindow, dialog, ipcMain, Menu, session } from 'electron'
import path from 'node:path'
import { EngineClient, findEngineExecutable } from './engine-client'
import { validateDirectoryQuery, validateFileQuery, validateFolderLimit, validateScanRoots } from './validation'
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

function engineRequest(command: string, args: Record<string, unknown> = {}): Promise<unknown> {
  if (!engine) throw new Error('Engine is unavailable.')
  return engine.request(command, args)
}

function registerIpc(): void {
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
