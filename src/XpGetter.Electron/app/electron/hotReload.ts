import path from 'path'
import {
  startBackend,
  stopBackend,
  ensurePort5050Free,
  killStaleBackendProcesses,
  waitForPortFree,
} from './backend'
import { getMainWindow } from './window'

let backendRestarting = false
let cleanup: (() => void) | null = null

export function startHotReloadWatch(): void {
  const mainWindow = getMainWindow()
  if (!mainWindow) return

  const electronProjectDir = path.join(__dirname, '..', '..')
  const chokidar = require('chokidar') as typeof import('chokidar')
  let debounceTimer: NodeJS.Timeout | null = null
  const DEBOUNCE_MS = 500

  const watcher = chokidar.watch(
    [
      path.join(electronProjectDir, '**/*.cs'),
      path.join(electronProjectDir, '**/*.csproj'),
    ],
    { ignoreInitial: true, ignored: path.join(electronProjectDir, 'app', '**') }
  )

  watcher.on('change', () => {
    if (debounceTimer) clearTimeout(debounceTimer)
    debounceTimer = setTimeout(async () => {
      debounceTimer = null
      const win = getMainWindow()
      if (backendRestarting || !win || win.isDestroyed()) return
      backendRestarting = true
      if (win && !win.isDestroyed()) {
        win.webContents.send('backend-reload-start')
      }
      try {
        await stopBackend()
        await new Promise((r) => setTimeout(r, 1000))
        await ensurePort5050Free()
        killStaleBackendProcesses()
        await new Promise((r) => setTimeout(r, 1200))
        await ensurePort5050Free()
        await waitForPortFree()
        await new Promise((r) => setTimeout(r, 2500))
        await ensurePort5050Free()
        await startBackend()
        const w = getMainWindow()
        if (w && !w.isDestroyed()) {
          w.webContents.send('backend-reloaded')
        }
      } catch (e) {
        console.error('Backend hot reload failed:', e)
      } finally {
        backendRestarting = false
        const w = getMainWindow()
        if (w && !w.isDestroyed()) {
          w.webContents.send('backend-reload-end')
        }
      }
    }, DEBOUNCE_MS)
  })

  cleanup = () => {
    watcher.close()
    if (debounceTimer) clearTimeout(debounceTimer)
    cleanup = null
  }
}

export function cleanupHotReload(): void {
  if (cleanup) {
    cleanup()
  }
}
