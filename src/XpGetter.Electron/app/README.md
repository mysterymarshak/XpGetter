# CElectron — Electron + React + TypeScript + C# (без Electron.NET)

Приложение собирает в одном дистрибутиве:
- **Electron** — окно и UI (React + TypeScript, Vite)
- **C# backend** — отдельный процесс (ASP.NET Core Minimal API), запускается из main process и общается по HTTP

Сборка единая: `yarn build` собирает и фронт, и бэкенд; `yarn dist` создаёт установщик с обоими частями.

## Требования

- **Node.js** 18+
- **Yarn**
- **.NET 10 SDK** (для C# backend)

## Установка

```bash
yarn
```

## Разработка

1. Запуск в режиме разработки — **онлайн-принятие изменений** и в C#, и в интерфейсе:

```bash
yarn start:dev
```

   - **Интерфейс (React):** Vite даёт HMR — правки в `src/` сразу отображаются в окне.
   - **C# backend:** запускается через `dotnet watch run` — при сохранении файлов в `backend/` приложение перезапускается автоматически.

2. Либо в двух терминалах:

```bash
yarn dev
```

```bash
yarn start
```

(Перед первым `yarn start` без dev нужна сборка: `yarn build`.)

## Сборка всего вместе

```bash
yarn build
```

Последовательно выполняется:
- `yarn build:backend` — `dotnet publish` в `backend/publish`
- `yarn build:react` — Vite собирает React в `dist`
- `yarn build:electron` — компиляция main/preload в `dist-electron`

## Запуск собранного приложения

```bash
yarn start
```

(Используются `dist/`, `dist-electron/` и `backend/publish/`.)

## Дистрибутив (установщик)

```bash
yarn dist
```

В папке `release/` появятся установщик и портативная сборка. C# backend лежит в `resources/backend/` и запускается при старте приложения.

## Структура

- `src/` — React + TypeScript (renderer)
- `electron/` — main process и preload (TypeScript)
- `backend/` — C# ASP.NET Core Minimal API
- Вызовы C# из React идут через IPC в main process, main проксирует HTTP на `localhost:5050`

Можно добавлять маршруты в `backend/Program.cs` и вызывать их через `window.electronAPI.invoke('api/...')`.
