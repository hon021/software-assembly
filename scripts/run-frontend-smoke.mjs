import { execFileSync } from 'node:child_process';
import { dirname, delimiter, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { readFileSync } from 'node:fs';
import { materializeSmoke } from './materialize-frontend-smoke.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const target = join(root, 'artifacts/repro-frontend');
const profile = JSON.parse(readFileSync(join(root, 'profiles/dotnet-angular/2.0.2/profile.json'), 'utf8'));
const stages = {
  install: ['install', '--frozen-lockfile', '--strict-peer-dependencies', '--reporter=append-only'],
  lint: ['run', 'lint'], build: ['run', 'build'], unit: ['run', 'test'],
  browser: ['exec', 'playwright', 'install', '--no-shell', 'chromium'],
  e2e: ['run', 'e2e'], audit: ['audit', '--audit-level=high'],
};

try {
  const stage = process.argv[2];
  if (process.versions.node !== profile.toolchain.node) throw new Error('Portable Node version differs from profile.');
  if (stage === 'create') {
    console.log(`Fresh smoke: ${materializeSmoke(target)}`);
  } else {
    if (!Object.hasOwn(stages, stage)) throw new Error('Usage: run-frontend-smoke.mjs create|install|lint|build|unit|browser|e2e|audit');
    const pnpm = process.env.SOFTWARE_ASSEMBLY_PNPM ?? join(root, '.tools/pnpm/node_modules/pnpm/bin/pnpm.cjs');
    const env = {
      ...process.env,
      PATH: `${dirname(process.execPath)}${delimiter}${process.env.PATH ?? ''}`,
      PLAYWRIGHT_BROWSERS_PATH: process.env.PLAYWRIGHT_BROWSERS_PATH ?? join(root, '.tools/playwright-browsers'),
    };
    const version = execFileSync(process.execPath, [pnpm, '--version'], { env, encoding: 'utf8' }).trim();
    if (version !== profile.toolchain.pnpm) throw new Error('Portable pnpm version differs from profile.');
    execFileSync(process.execPath, [pnpm, '--dir', target, ...stages[stage]], {
      cwd: root, env, stdio: 'inherit', timeout: stage === 'e2e' ? 600000 : 300000,
    });
    console.log(`Frontend smoke ${stage}: passed.`);
  }
} catch (error) {
  console.error(error.message);
  process.exitCode = 1;
}