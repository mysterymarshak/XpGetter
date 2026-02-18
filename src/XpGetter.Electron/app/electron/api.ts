import { ipcMain } from 'electron'
import http from 'http'
import { BACKEND_PORT } from './config'

export function proxyToBackend(port: number, pathStr: string, method: string, body: unknown): Promise<unknown> {
  return new Promise((resolve, reject) => {
    const data = body !== undefined ? JSON.stringify(body) : undefined
    const req = http.request(
      {
        hostname: '127.0.0.1',
        port,
        path: '/' + pathStr.replace(/^\/+/, ''),
        method: method || 'GET',
        headers: data ? { 'Content-Type': 'application/json', 'Content-Length': Buffer.byteLength(data) } : {},
      },
      (res) => {
        let raw = ''
        res.on('data', (chunk) => { raw += chunk })
        res.on('end', () => {
          try {
            resolve(raw ? JSON.parse(raw) : {})
          } catch {
            resolve(raw)
          }
        })
      }
    )
    req.on('error', reject)
    if (data) req.write(data)
    req.end()
  })
}

export function registerApiHandler(): void {
  ipcMain.handle('api', async (_event, pathStr: string, method: string, body?: unknown) => {
    try {
      return await proxyToBackend(BACKEND_PORT, pathStr, method || 'GET', body)
    } catch (e) {
      throw e instanceof Error ? e : new Error(String(e))
    }
  })
}
