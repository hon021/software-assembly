import assert from 'node:assert/strict';
import test from 'node:test';
import { evaluateAudit, verifyRepository } from './ci-checks.mjs';
import { createProtectionPolicy, verifyProtection } from './configure-main-protection.mjs';
import { mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { materializeSmoke, templateDirectory, verifySmokeManifest } from './materialize-frontend-smoke.mjs';

const audit = (severity, transitive = false) => ({
  version: 1,
  projects: [{
    path: 'src/example.csproj',
    frameworks: [{ [transitive ? 'transitivePackages' : 'topLevelPackages']: [{
      id: 'example-package', vulnerabilities: [{ severity }],
    }] }],
  }],
});

test('repository allows only project files', () => {
  verifyRepository(['src/Core.cs', '.github/workflows/ci.yml', 'docs/overview.md']);
});

for (const filename of ['Base/example.cs', 'base/file', '.tools/node.exe', 'artifacts/report.json']) {
  test(`repository rejects ${filename}`, () => assert.throws(() => verifyRepository(['README.md', filename])));
}

test('repository fails on an empty or invalid file list', () => {
  assert.throws(() => verifyRepository([]));
  assert.throws(() => verifyRepository(null));
});

test('audit accepts a completed report with no advisories', () => {
  assert.deepEqual(evaluateAudit({ version: 1, projects: [{ path: 'src/Core.csproj' }] }), { blocking: [], warnings: [] });
});

for (const severity of ['High', 'Critical']) {
  for (const transitive of [false, true]) {
    test(`audit blocks ${severity} vulnerabilities; transitive=${transitive}`, () => {
      assert.equal(evaluateAudit(audit(severity, transitive)).blocking.length, 1);
    });
  }
}

for (const severity of ['Low', 'Moderate']) {
  test(`audit reports ${severity} as a warning`, () => {
    const result = evaluateAudit(audit(severity));
    assert.equal(result.blocking.length, 0);
    assert.equal(result.warnings.length, 1);
  });
}

test('audit fails closed on unknown severity', () => assert.throws(() => evaluateAudit(audit('Unknown'))));

for (const report of [null, {}, { version: 2, projects: [] }, { version: 1, projects: [] },
  { version: 1, projects: [{}] }, { version: 1, projects: [{ path: 'src/Core.csproj', frameworks: {} }] }]) {
  test(`audit rejects malformed input ${JSON.stringify(report)}`, () => assert.throws(() => evaluateAudit(report)));
}

test('audit refuses a report with collection problems', () => {
  const report = audit('Low');
  report.problems = [{ message: 'Source unavailable' }];
  assert.throws(() => evaluateAudit(report));
});

test('single-maintainer protection requires PR and CI without self-approval', () => {
  const policy = createProtectionPolicy(15368);
  assert.equal(policy.required_pull_request_reviews.required_approving_review_count, 0);
  assert.deepEqual(policy.required_status_checks, { strict: true, checks: [{ context: 'Motor CI', app_id: 15368 }] });
  assert.equal(policy.enforce_admins, true);
  assert.equal(policy.allow_force_pushes, false);
  assert.equal(policy.allow_deletions, false);
});

test('protection refuses an invalid provider', () => assert.throws(() => createProtectionPolicy(-1)));

test('protection verification rejects a missing or weakened rule', () => {
  const protection = {
    required_status_checks: createProtectionPolicy(15368).required_status_checks,
    required_pull_request_reviews: { required_approving_review_count: 0 },
    enforce_admins: { enabled: true }, allow_force_pushes: { enabled: false }, allow_deletions: { enabled: false },
    required_conversation_resolution: { enabled: true },
  };
  verifyProtection(protection, 15368);
  assert.throws(() => verifyProtection({}, 15368));
  assert.throws(() => verifyProtection(protection, 99));
  assert.throws(() => verifyProtection({ ...protection, allow_force_pushes: { enabled: true } }, 15368));
});

test('smoke materializes from tracked template and refuses overwrite', () => {
  const parent = mkdtempSync(join(tmpdir(), 'assembly-smoke-'));
  const target = join(parent, 'frontend');
  try {
    assert.equal(materializeSmoke(target), target);
    const manifest = JSON.parse(readFileSync(join(target, 'package.json'), 'utf8'));
    assert.equal(manifest.devDependencies.vitest, '4.1.11');
    assert.equal(JSON.parse(readFileSync(join(target, 'tsconfig.json'), 'utf8')).compilerOptions.rootDir, '.');
    assert.throws(() => materializeSmoke(target));
  } finally {
    rmSync(parent, { recursive: true, force: true });
  }
});

test('smoke rejects a floating dependency or missing security override', () => {
  const manifest = JSON.parse(readFileSync(join(templateDirectory, 'package.json'), 'utf8'));
  const profile = {
    toolchain: { pnpm: '10.11.0', angular: '21.2.25', typescript: '5.9.3', vitest: '4.1.11', playwright: '1.56.1',
      eslint: '10.12.0', eslintJavaScript: '10.0.1', eslintTypescript: '8.71.1' },
    dependencyOverrides: [{ parent: '@angular/cli', package: '@modelcontextprotocol/sdk', version: '1.31.0' }],
  };
  verifySmokeManifest(manifest, profile);
  manifest.devDependencies.vitest = '^4.1.11';
  assert.throws(() => verifySmokeManifest(manifest, profile));
  manifest.devDependencies.vitest = '4.1.11';
  delete manifest.pnpm.overrides['@angular/cli>@modelcontextprotocol/sdk'];
  assert.throws(() => verifySmokeManifest(manifest, profile));
});