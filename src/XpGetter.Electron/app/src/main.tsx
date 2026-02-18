import React, { useState, useEffect } from 'react'
import ReactDOM from 'react-dom/client'
import App from './App'
import { BACKEND_RELOADED_EVENT } from './electron'
import './index.css'

function Root() {
  const [hotReloading, setHotReloading] = useState(false)

  // Глобальный cApi — в любом месте: cApi('api/hello').then((data) => ...)
  useEffect(() => {
    if (typeof window.electronAPI?.invoke === 'function') {
      window.cApi = (path: string, body?: unknown) => window.electronAPI.invoke(path, body)
    }
  }, [])

  // Модалка Hot Reload + после завершения шлём событие (подписчики перезапросят данные)
  useEffect(() => {
    const api = window.electronAPI
    if (!api?.onBackendReloadStart || !api?.onBackendReloadEnd) return
    const unsubStart = api.onBackendReloadStart(() => setHotReloading(true))
    const unsubEnd = api.onBackendReloadEnd(() => {
      setHotReloading(false)
      window.dispatchEvent(new CustomEvent(BACKEND_RELOADED_EVENT))
    })
    return () => {
      unsubStart()
      unsubEnd()
    }
  }, [])

  return (
    <>
      {hotReloading && (
        <div
          style={{
            position: 'fixed',
            inset: 0,
            background: 'rgba(0,0,0,0.6)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 9999,
          }}
        >
          <div
            style={{
              background: '#fff',
              padding: '24px 32px',
              borderRadius: 8,
              boxShadow: '0 4px 20px rgba(0,0,0,0.2)',
              textAlign: 'center',
            }}
          >
            <div style={{ fontSize: 18, fontWeight: 600, marginBottom: 8 }}>Cold Reload C#</div>
            <div style={{ color: '#666' }}>Подождите…</div>
          </div>
        </div>
      )}
      <App />
    </>
  )
}

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <Root />
  </React.StrictMode>
)
