import { cpSync, existsSync, lstatSync, readdirSync, readFileSync } from 'node:fs';
import { dirname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
export const templateDirectory = join(root, 'tests/fixtures/frontend-smoke');

export function verifySmokeManifest(manifest, profile) {
  const dependencies = { ...manifest.dependencies, ...manifest.devDependencies };
  const mapping = {
    angular: ['@angular/common', '@angular/compiler', '@angular/core', '@angular/platform-browser', '@angular/build', '@angular/cli', '@angular/compiler-cli'],
    typescript: ['typescript'], vitest: ['vitest'], playwright: ['@playwright/test'],
    eslint: ['eslint'], eslintJavaScript: ['@eslint/js'], eslintTypescript: ['typescript-eslint'],
  };
  if (manifest.packageManager !== `pnpm@${profile.toolchain.pnpm}`) throw new Error('Smoke package manager differs from profile.');
  for (const [tool, packages] of Object.entries(mapping)) {
    for (const name of packages) {
      if (dependencies[name] !== profile.toolchain[tool]) throw new Error(`Smoke package differs from profile: ${name}`);
    }
  }
  for (const item of profile.dependencyOverrides ?? []) {
    if (manifest.pnpm?.overrides?.[`${item.parent}>${item.package}`] !== item.version)
      throw new Error('Smoke dependency override differs from profile.');
  }
}

function verifyTemplate(directory) {
  for (const entry of readdirSync(directory)) {
    const path = join(directory, entry);
    const stat = lstatSync(path);
    if (stat.isSymbolicLink() || ['node_modules', 'dist', '.angular', 'test-results'].includes(entry))
      throw new Error('Smoke template contains generated files or symbolic links.');
    if (stat.isDirectory()) verifyTemplate(path);
  }
}

export function materializeSmoke(targetDirectory) {
  const target = resolve(targetDirectory);
  if (existsSync(target)) throw new Error('Smoke destination already exists; no files were overwritten.');
  const relativeTarget = relative(templateDirectory, target);
  if (!relativeTarget.startsWith('..')) throw new Error('Smoke destination cannot be inside its template.');
  const profile = JSON.parse(readFileSync(join(root, 'profiles/dotnet-angular/2.0.2/profile.json'), 'utf8'));
  if (!existsSync(join(templateDirectory, 'pnpm-lock.yaml'))) throw new Error('Versioned smoke lockfile is missing.');
  verifySmokeManifest(JSON.parse(readFileSync(join(templateDirectory, 'package.json'), 'utf8')), profile);
  verifyTemplate(templateDirectory);
  cpSync(templateDirectory, target, { recursive: true, errorOnExist: true, force: false });
  return target;
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    console.log(`Fresh frontend smoke created: ${materializeSmoke(process.argv[2] ?? join(root, 'artifacts/repro-frontend'))}`);
  } catch (error) {
    console.error(error.message);
    process.exitCode = 1;
  }
}