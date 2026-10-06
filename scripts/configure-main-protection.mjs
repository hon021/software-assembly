import { execFileSync } from 'node:child_process';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const repository = 'hon021/software-assembly';
const api = `https://api.github.com/repos/${repository}`;

export function createProtectionPolicy(appId) {
  if (!Number.isSafeInteger(appId) || appId <= 0) throw new Error('Invalid check provider.');
  return {
    required_status_checks: { strict: true, checks: [{ context: 'Motor CI', app_id: appId }] },
    enforce_admins: true,
    required_pull_request_reviews: {
      dismiss_stale_reviews: true,
      require_code_owner_reviews: false,
      required_approving_review_count: 0,
    },
    restrictions: null,
    allow_force_pushes: false,
    allow_deletions: false,
    required_conversation_resolution: true,
  };
}

export function verifyProtection(protection, appId) {
  const checks = protection.required_status_checks;
  if (!checks?.strict || !checks.checks?.some(check => check.context === 'Motor CI' && check.app_id === appId)
      || !protection.enforce_admins?.enabled || !protection.required_pull_request_reviews
      || protection.required_pull_request_reviews.required_approving_review_count !== 0
      || protection.allow_force_pushes?.enabled !== false || protection.allow_deletions?.enabled !== false
      || !protection.required_conversation_resolution?.enabled)
    throw new Error('Protection response does not match the authorized policy.');
}

async function configure() {
  if (process.argv[2] !== '--apply') throw new Error('Explicit --apply authorization is required.');
  const git = process.env.SOFTWARE_ASSEMBLY_GIT ?? (process.platform === 'win32' ? 'C:/Program Files/Git/cmd/git.exe' : 'git');
  const remote = execFileSync(git, ['remote', 'get-url', 'origin'], { cwd: root, encoding: 'utf8' }).trim();
  if (remote !== `https://github.com/${repository}.git`) throw new Error('Repository origin does not match the authorized destination.');

  let credentialText;
  try {
    credentialText = execFileSync(git, ['credential', 'fill'], {
      cwd: root, encoding: 'utf8',
      input: `protocol=https\nhost=github.com\npath=${repository}.git\n\n`,
      stdio: ['pipe', 'pipe', 'pipe'],
    });
  } catch {
    throw new Error('Existing Git authentication is unavailable. Authenticate directly in your authorized environment.');
  }
  const fields = new Map(credentialText.split(/\r?\n/).filter(line => line.includes('='))
    .map(line => [line.slice(0, line.indexOf('=')), line.slice(line.indexOf('=') + 1)]));
  credentialText = undefined;
  const token = fields.get('password');
  fields.clear();
  if (!token) throw new Error('Existing Git authentication did not provide an API credential.');

  const request = async (path, options = {}) => {
    const response = await fetch(`${api}${path}`, {
      ...options,
      headers: {
        Accept: 'application/vnd.github+json',
        Authorization: `Bearer ${token}`,
        'X-GitHub-Api-Version': '2022-11-28',
        'Content-Type': 'application/json',
      },
      signal: AbortSignal.timeout(30000),
    });
    if (!response.ok) throw new Error(`GitHub administration request rejected (HTTP ${response.status}). No credentials were logged.`);
    return response.json();
  };

  const metadata = await request('');
  if (metadata.permissions?.admin !== true) throw new Error('Existing identity does not have repository administration permission.');
  const branch = await request('/branches/main');
  if (branch.protected) throw new Error('Main already has protection. Existing rules will not be replaced automatically.');
  const result = await request('/commits/main/check-runs');
  const check = result.check_runs?.find(item => item.name === 'Motor CI' && item.app?.slug === 'github-actions'
    && item.status === 'completed' && item.conclusion === 'success');
  if (!check) throw new Error('Main has no completed successful Motor CI check from GitHub Actions.');

  const appId = check.app.id;
  await request('/branches/main/protection', { method: 'PUT', body: JSON.stringify(createProtectionPolicy(appId)) });
  verifyProtection(await request('/branches/main/protection'), appId);
  console.log('Main protection verified: pull request required, Motor CI required, approvals=0, admins enforced, force push and deletion disabled.');
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  configure().catch(error => {
    console.error(error.message);
    process.exitCode = 1;
  });
}