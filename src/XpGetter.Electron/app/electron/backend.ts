import { app } from 'electron'
import path from 'path'
import { spawn, ChildProcess, execSync } from 'child_process'
import http from 'http'
import { platform } from 'os'
import { BACKEND_PORT, isDev } from './config'

let backendProcess: ChildProcess | null = null

const isWindows = platform() === 'win32'
const backendBin = isWindows ? 'Backend.exe' : 'Backend'

export function getBackendPath(): string {
  if (app.isPackaged) {
    return path.join(process.resourcesPath, 'backend', backendBin)
  }
  const appDir = path.join(__dirname, '..')
  return path.join(appDir, 'backend', 'publish', backendBin)
}

/** Убивает все зависшие процессы бэкенда. Только Windows, только в dev. */
export function killStaleBackendProcesses(): void {
  if (!isDev || !isWindows) return
  try {
    execSync(`taskkill /IM ${backendBin} /F`, { stdio: 'ignore', windowsHide: true })
  } catch {
    // Нет процесса — нормально
  }
}

/** Освобождает порт 5050 на Windows. */
export function releasePort5050(): void {
  if (!isWindows) return
  try {
    const out = execSync('netstat -ano', { encoding: 'utf8', windowsHide: true })
    const lines = out.split('\n').filter((l) => l.includes(':5050') && (l.includes('LISTENING') || l.includes('ПРОСМОТР')))
    const pids = new Set<string>()
    for (const line of lines) {
      const m = line.trim().split(/\s+/)
      const pid = m[m.length - 1]
      if (pid && /^\d+$/.test(pid)) pids.add(pid)
    }
    for (const pid of pids) {
      try {
        execSync(`taskkill /PID ${pid} /F`, { stdio: 'ignore', windowsHide: true })
      } catch { /* ignore */ }
    }
  } catch { /* ignore */ }
}

/** Ждёт освобождения порта 5050 (ECONNREFUSED + 800ms), макс ~8 сек. */
export function waitForPortFree(): Promise<void> {
  return new Promise((resolve) => {
    const start = Date.now()
    const maxMs = 1000
    const check = () => {
      if (Date.now() - start > maxMs) {
        resolve()
        return
      }
      const req = http.get(`http://127.0.0.1:${BACKEND_PORT}/`, (res) => {
        res.destroy()
        setTimeout(check, 250)
      })
      req.on('error', (err: NodeJS.ErrnoException) => {
        if (err.code === 'ECONNREFUSED') {
          setTimeout(() => resolve(), 800)
        } else {
          setTimeout(check, 250)
        }
      })
      req.setTimeout(300, () => { req.destroy(); setTimeout(check, 250) })
    }
    setTimeout(check, 400)
  })
}

export function stopBackend(): Promise<void> {
  return new Promise((resolve) => {
    if (!backendProcess) {
      resolve()
      return
    }
    const p = backendProcess
    backendProcess = null
    p.once('exit', () => resolve())
    if (isWindows && p.pid) {
      try {
        execSync(`taskkill /PID ${p.pid} /F /T`, { stdio: 'ignore', windowsHide: true })
      } catch {
        p.kill('SIGTERM')
      }
    } else {
      p.kill('SIGTERM')
    }
    setTimeout(() => resolve(), 5000)
  })
}

/** В dev: несколько раз освобождаем порт с паузами. */
export async function ensurePort5050Free(): Promise<void> {
  if (!isWindows) return
  for (let i = 0; i < 5; i++) {
    releasePort5050()
    await new Promise((r) => setTimeout(r, 600))
  }
}

/** В dev: сборка XpGetter.Electron (родитель app/) в app/backend/run. */
export function buildBackendDev(): void {
  const appDir = path.join(__dirname, '..')
  const electronProjectDir = path.join(appDir, '..')
  const csproj = path.join(electronProjectDir, 'XpGetter.Electron.csproj')
  const outDir = path.join(appDir, 'backend', 'run')
  const spawnOpts: { cwd: string; stdio: 'inherit'; windowsHide?: boolean } = {
    cwd: electronProjectDir,
    stdio: 'inherit',
  }
  if (isWindows) spawnOpts.windowsHide = true
  execSync(`dotnet build "${csproj}" -c Debug -o "${outDir}"`, spawnOpts)
}

export function startBackend(): Promise<number> {
  return new Promise((resolve, reject) => {
    const appDir = path.join(__dirname, '..')

    if (isDev) {
      buildBackendDev()
      const exe = path.join(appDir, 'backend', 'run', backendBin)
      const spawnOpts: { cwd: string; stdio: ('ignore' | 'pipe')[]; env: NodeJS.ProcessEnv; windowsHide?: boolean } = {
        cwd: appDir,
        stdio: ['ignore', 'pipe', 'pipe'],
        env: { ...process.env, ASPNETCORE_URLS: `http://localhost:${BACKEND_PORT}` },
      }
      if (isWindows) spawnOpts.windowsHide = true
      backendProcess = spawn(exe, [], spawnOpts)
    } else {
      const exe = getBackendPath()
      backendProcess = spawn(exe, [], {
        stdio: ['ignore', 'pipe', 'pipe'],
        env: { ...process.env, ASPNETCORE_URLS: `http://localhost:${BACKEND_PORT}` },
      })
    }

    let resolved = false
    const onReady = () => {
      if (!resolved) {
        resolved = true
        resolve(BACKEND_PORT)
      }
    }
    const onData = (chunk: Buffer) => {
      const text = chunk.toString()
      if (text.includes('Now listening') || text.includes('Application started')) onReady()
      if (isDev) process.stdout.write(text)
    }
    backendProcess.stdout?.on('data', onData)
    backendProcess.stderr?.on('data', onData)
    backendProcess.on('error', (err) => {
      if (!resolved) {
        resolved = true
        reject(err)
      }
    })
    const readyTimeout = isDev ? 20000 : 3000
    setTimeout(() => onReady(), readyTimeout)
  })
}

/** Завершить процесс бэкенда (при выходе из приложения). */
export function killBackend(): void {
  if (backendProcess) {
    backendProcess.kill()
    backendProcess = null
  }
}
