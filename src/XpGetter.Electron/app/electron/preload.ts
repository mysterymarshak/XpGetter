import { contextBridge, ipcRenderer } from 'electron'

contextBridge.exposeInMainWorld('electronAPI', {
  invoke: (path: string, body?: unknown) =>
    ipcRenderer.invoke('api', path, 'GET', body),
  onBackendReload: (callback: () => void) => {
    const fn = () => callback()
    ipcRenderer.on('backend-reloaded', fn)
    return () => ipcRenderer.removeListener('backend-reloaded', fn)
  },
  onBackendReloadStart: (callback: () => void) => {
    const fn = () => callback()
    ipcRenderer.on('backend-reload-start', fn)
    return () => ipcRenderer.removeListener('backend-reload-start', fn)
  },
  onBackendReloadEnd: (callback: () => void) => {
    const fn = () => callback()
    ipcRenderer.on('backend-reload-end', fn)
    return () => ipcRenderer.removeListener('backend-reload-end', fn)
  },
})
