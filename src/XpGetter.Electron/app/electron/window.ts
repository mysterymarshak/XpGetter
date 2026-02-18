import { BrowserWindow } from 'electron'
import path from 'path'
import { isDev } from './config'

let mainWindow: BrowserWindow | null = null

export function createWindow(): void {
  mainWindow = new BrowserWindow({
    width: 900,
    height: 700,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
    },
  })
  if (isDev) {
    mainWindow.loadURL('http://localhost:5173')
    mainWindow.webContents.openDevTools()
  } else {
    mainWindow.loadFile(path.join(__dirname, '..', 'dist', 'index.html'))
  }
  mainWindow.on('closed', () => { mainWindow = null })
}

export function getMainWindow(): BrowserWindow | null {
  return mainWindow
}
