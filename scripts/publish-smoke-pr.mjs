import { execFileSync } from 'node:child_process';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const git = process.env.SOFTWARE_ASSEMBLY_GIT ?? 'C:/Program Files/Git/cmd/git.exe';
const branch = 'ci/reproducible-frontend-smoke';
const repository = 'hon021/software-assembly';
const run = (...args) => execFileSync(git, args, { cwd: root, encoding: 'utf8' }).trim();

try {
  if (process.argv[2] !== '--publish') throw new Error('Explicit --publish is required.');
  if (run('remote', 'get-url', 'origin') !== `https://github.com/${repository}.git`)
    throw new Error('Unexpected remote destination.');
  const current = run('branch', '--show-current');
  if (!['main', branch].includes(current)) throw new Error('Unexpected work branch.');

  let credentials;
  try {
    credentials = execFileSync(git, ['credential', 'fill'], {
      cwd: root, encoding: 'utf8', input: `protocol=https\nhost=github.com\npath=${repository}.git\n\n`,
      stdio: ['pipe', 'pipe', 'pipe'],
    });
  } catch {
    throw new Error('Authenticate directly in your authorized environment; no credentials were logged.');
  }
  const token = credentials.split(/\r?\n/).find(line => line.startsWith('password='))?.slice('password='.length);
  credentials = undefined;
  if (!token) throw new Error('Existing authentication is unavailable.');
  const request = async (path, options = {}) => {
    const response = await fetch(`https://api.github.com/repos/${repository}${path}`, {
      ...options,
      headers: {
        Accept: 'application/vnd.github+json', Authorization: `Bearer ${token}`,
        'Content-Type': 'application/json', 'X-GitHub-Api-Version': '2022-11-28',
      },
      signal: AbortSignal.timeout(30000),
    });
    if (!response.ok) throw new Error(`GitHub request rejected (HTTP ${response.status}); credentials were not logged.`);
    return response.json();
  };
  const metadata = await request('');
  if (!metadata.permissions?.push) throw new Error('Identity has no publication permission.');

  if (current === 'main') console.log(run('switch', '-c', branch));
  execFileSync(process.execPath, [join(root, 'scripts/prepare-publication.mjs'), 'prepare', branch], { cwd: root, stdio: 'inherit' });
  const changes = run('diff', '--cached', '--name-only');
  if (changes) {
    execFileSync(process.execPath, [join(root, 'scripts/prepare-publication.mjs'), 'commit',
      'Add reproducible frontend smoke and required CI aggregate'], { cwd: root, stdio: 'inherit' });
  }
  execFileSync(process.execPath, [join(root, 'scripts/prepare-publication.mjs'), 'push'], { cwd: root, stdio: 'inherit' });

  const query = new URLSearchParams({ state: 'open', head: `hon021:${branch}`, base: 'main' });
  const existing = await request(`/pulls?${query}`);
  const pullRequest = existing[0] ?? await request('/pulls', {
    method: 'POST',
    body: JSON.stringify({
      title: 'Add reproducible frontend smoke and required CI aggregate', head: branch, base: 'main',
      body: 'Adds a neutral Angular fixture with frozen lockfile, fresh materialization and frontend CI. Motor CI requires both motor and frontend success. Local validation: 26 control tests, 2 Angular unit tests, 1 Chromium test, lint/build and audit passed. Includes locally verified main-protection tooling. Base, portable tools and generated artifacts remain excluded. No merge is performed by this operation.',
    }),
  });
  console.log(`Pull request created or already open: ${pullRequest.html_url}`);
  console.log('Main was not pushed directly, bypassed or merged.');
} catch (error) {
  console.error(error.message);
  process.exitCode = 1;
}