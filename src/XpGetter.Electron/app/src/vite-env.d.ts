/// <reference types="vite/client" />

interface ElectronAPI {
  invoke: (path: string, body?: unknown) => Promise<unknown>
  onBackendReload: (callback: () => void) => () => void
  onBackendReloadStart: (callback: () => void) => () => void
  onBackendReloadEnd: (callback: () => void) => () => void
}

type CApi = (path: string, body?: unknown) => Promise<unknown>

declare global {
  interface Window {
    electronAPI: ElectronAPI
    cApi: CApi
  }
}

export {}
