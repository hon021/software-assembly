import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const git = process.env.SOFTWARE_ASSEMBLY_GIT ?? 'C:/Program Files/Git/cmd/git.exe';
const remote = 'https://github.com/hon021/software-assembly.git';
const excluded = /^(Base|\.tools|artifacts)(\/|$)/i;
const options = { cwd: root, encoding: 'utf8', maxBuffer: 10 * 1024 * 1024 };
const run = (...argumentsList) => execFileSync(git, argumentsList, options).trim();
const trackedFiles = () => run('ls-files', '-z').split('\0').filter(Boolean);

function verifyFiles(files) {
  if (files.length === 0) throw new Error('No files were selected.');
  for (const filename of files) {
    if (excluded.test(filename)) throw new Error(`Excluded path selected: ${filename}`);
    const content = run('show', `:${filename}`);
    if (/-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----/.test(content))
      throw new Error(`Private key material detected: ${filename}`);
  }
}

const command = process.argv[2];
if (!['prepare', 'verify', 'commit', 'push'].includes(command))
  throw new Error('Usage: prepare-publication.mjs prepare|verify|commit|push');

if (command === 'prepare') {
  const ignore = readFileSync(resolve(root, '.gitignore'), 'utf8');
  for (const rule of ['/Base/', '/.tools/', '/artifacts/']) {
    if (!ignore.split(/\r?\n/).includes(rule)) throw new Error(`Missing exclusion: ${rule}`);
  }
  try {
    const existingRoot = run('rev-parse', '--show-toplevel');
    if (resolve(existingRoot) !== root) throw new Error('Workspace belongs to another repository.');
  } catch (error) {
    if (error.status !== 128) throw error;
    console.log(run('init', '--initial-branch=main'));
  }
  if (run('branch', '--show-current') !== 'main') throw new Error('Expected main; no branch will be renamed.');
  const remotes = run('remote').split(/\r?\n/);
  if (remotes.includes('origin')) {
    if (run('remote', 'get-url', 'origin') !== remote) throw new Error('Existing origin differs from requested destination.');
  } else {
    run('remote', 'add', 'origin', remote);
  }
  if (trackedFiles().some(filename => excluded.test(filename)))
    throw new Error('Excluded files are already tracked; no files will be removed automatically.');
  run('check-ignore', 'Base/', '.tools/', 'artifacts/');
  const paths = ['.gitignore', '.github', '.vscode', 'README.md', 'global.json', 'SoftwareAssembly.slnx',
    'azure-pipelines.yml', 'applications', 'docs', 'domains', 'policies', 'profiles', 'schemas', 'scripts', 'src', 'tests'];
  run('add', '--', ...paths);
  const files = trackedFiles();
  verifyFiles(files);
  console.log(`Prepared ${files.length} files. Excluded directories: not tracked.`);
  console.log(files.join('\n'));
}

if (command === 'verify') {
  const files = trackedFiles();
  verifyFiles(files);
  console.log(`Verified ${files.length} tracked files. Excluded directories: not tracked.`);
  console.log(run('status', '--short', '--branch'));
}

if (command === 'commit') {
  verifyFiles(trackedFiles());
  console.log(run('commit', '-m', process.argv[3] ?? 'Initialize domain-independent software assembly foundation'));
  const files = run('ls-tree', '-r', '--name-only', 'HEAD').split(/\r?\n/).filter(Boolean);
  if (files.some(filename => excluded.test(filename))) throw new Error('Excluded directory found in committed tree.');
  console.log(`Commit tree verified: ${files.length} files; excluded directories absent.`);
}

if (command === 'push') {
  if (run('remote', 'get-url', 'origin') !== remote) throw new Error('Remote destination changed.');
  const files = run('ls-tree', '-r', '--name-only', 'HEAD').split(/\r?\n/).filter(Boolean);
  if (files.some(filename => excluded.test(filename))) throw new Error('Excluded directory found in committed tree.');
  execFileSync(git, ['push', '-u', 'origin', 'main'], { cwd: root, stdio: 'inherit' });
  console.log(`Published commit ${run('rev-parse', '--short', 'HEAD')} to origin/main.`);
}