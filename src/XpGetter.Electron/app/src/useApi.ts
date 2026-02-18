import { useState, useEffect, useCallback } from 'react'
import { BACKEND_RELOADED_EVENT } from './electron'

/**
 * Один вызов — запрос к API и авто-перезапрос после Hot Reload C#.
 * Пример: const { data, error } = useApi<{ text: string }>('api/hello')
 */
export function useApi<T = unknown>(path: string) {
  const [data, setData] = useState<T | null>(null)
  const [error, setError] = useState<string | null>(null)

  const fetch = useCallback(() => {
    if (typeof window.cApi !== 'function') return
    window.cApi(path).then((res) => { setError(null); setData(res as T) }).catch((e: Error) => setError(e.message))
  }, [path])

  useEffect(() => {
    setError(null)
    fetch()
  }, [fetch])

  useEffect(() => {
    const fn = () => fetch()
    window.addEventListener(BACKEND_RELOADED_EVENT, fn)
    return () => window.removeEventListener(BACKEND_RELOADED_EVENT, fn)
  }, [fetch])

  return { data, error, refetch: fetch }
}
