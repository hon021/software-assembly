import assert from 'node:assert/strict';
import test from 'node:test';
import { evaluateAudit, verifyRepository } from './ci-checks.mjs';

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