import { useApi } from './useApi'

function App() {
  const { data, error } = useApi<{ text: string }>('api/hello')

  return (
    <div style={{ padding: 24, fontFamily: 'system-ui' }}>
      <h1>CElectron</h1>
      <p>React + TypeScript + Electron + C# (без Electron.NET)</p>
      {error ? (
        <p style={{ color: 'red' }}>Ошибка: {error}</p>
      ) : (
        <p>Ответ от C# backend: <strong>{data?.text ?? 'Загрузка...'}</strong></p>
      )}
    </div>
  )
}

export default App