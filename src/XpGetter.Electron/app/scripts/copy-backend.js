// Копирование backend в dist-electron не требуется — electron-builder берёт из backend/publish.
// Скрипт можно использовать для проверки наличия backend после build:backend.
const fs = require('fs');
const path = require('path');
const publishDir = path.join(__dirname, '..', 'backend', 'publish');
if (!fs.existsSync(publishDir)) {
  console.warn('backend/publish not found. Run: yarn build:backend');
} else {
  console.log('Backend publish found:', publishDir);
}
