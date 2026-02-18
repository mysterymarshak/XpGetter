import { app } from 'electron'
import { isDev } from './config'
import { killStaleBackendProcesses, startBackend, killBackend } from './backend'
import { registerApiHandler } from './api'
import { createWindow } from './window'
import { startHotReloadWatch, cleanupHotReload } from './hotReload'

registerApiHandler()

app.whenReady().then(async () => {
  if (isDev) killStaleBackendProcesses()
  try {
    await startBackend()
  } catch (e) {
    console.error('Backend start failed:', e)
  }
  createWindow()
  if (isDev) {
    startHotReloadWatch()
  }
})

app.on('window-all-closed', () => {
  cleanupHotReload()
  killBackend()
  app.quit()
})
