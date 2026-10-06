import { execFileSync } from 'node:child_process';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

export function verifyRepository(files) {
  if (!Array.isArray(files) || files.length === 0) throw new Error('Repository file list is empty or invalid.');
  const forbidden = files.filter(filename => /^(?:Base|\.tools|artifacts)(?:\/|$)/i.test(filename));
  if (forbidden.length > 0) throw new Error(`Excluded paths are tracked: ${forbidden.join(', ')}`);
}

export function evaluateAudit(report) {
  if (report?.version !== 1 || !Array.isArray(report.projects) || report.projects.length === 0)
    throw new Error('Audit report is missing projects or has an unsupported format.');
  if (report.problems !== undefined && (!Array.isArray(report.problems) || report.problems.length > 0))
    throw new Error('Audit reported problems; a complete audit is required.');

  const blocking = [];
  const warnings = [];
  for (const project of report.projects) {
    if (!project || typeof project.path !== 'string' || project.path.trim() === '')
      throw new Error('Audit project has no path.');
    if (project.frameworks !== undefined && !Array.isArray(project.frameworks))
      throw new Error('Audit frameworks are invalid.');
    for (const framework of project.frameworks ?? []) {
      if (!framework || typeof framework !== 'object') throw new Error('Audit framework is invalid.');
      for (const kind of ['topLevelPackages', 'transitivePackages']) {
        if (framework[kind] !== undefined && !Array.isArray(framework[kind]))
          throw new Error('Audit package list is invalid.');
        for (const dependency of framework[kind] ?? []) {
          if (!dependency || typeof dependency.id !== 'string' || !Array.isArray(dependency.vulnerabilities))
            throw new Error('Audited package is invalid.');
          for (const vulnerability of dependency.vulnerabilities) {
            if (!['Low', 'Moderate', 'High', 'Critical'].includes(vulnerability?.severity))
              throw new Error('Audit vulnerability severity is unknown.');
            const finding = `${vulnerability.severity}: ${dependency.id}`;
            (['High', 'Critical'].includes(vulnerability.severity) ? blocking : warnings).push(finding);
          }
        }
      }
    }
  }
  return { blocking, warnings };
}

function main() {
  const [command, filename] = process.argv.slice(2);
  if (command === 'repository') {
    const git = process.env.SOFTWARE_ASSEMBLY_GIT ?? 'git';
    const files = execFileSync(git, ['ls-files', '-z'], { encoding: 'utf8' }).split('\0').filter(Boolean);
    verifyRepository(files);
    console.log(`Repository exclusions passed: ${files.length} tracked files.`);
  } else if (['audit', 'audit-local'].includes(command) && filename) {
    if (command === 'audit-local') {
      const dotnet = process.env.SOFTWARE_ASSEMBLY_DOTNET ?? 'dotnet';
      const output = execFileSync(dotnet, ['list', 'SoftwareAssembly.slnx', 'package', '--vulnerable',
        '--include-transitive', '--format', 'json'], { encoding: 'utf8', maxBuffer: 10 * 1024 * 1024 });
      mkdirSync(dirname(resolve(filename)), { recursive: true });
      writeFileSync(filename, output, 'utf8');
    }
    const result = evaluateAudit(JSON.parse(readFileSync(filename, 'utf8')));
    for (const warning of result.warnings) console.warn(warning);
    if (result.blocking.length > 0) throw new Error(`Dependency audit blocked: ${result.blocking.join('; ')}`);
    console.log(`Dependency audit passed; non-blocking findings: ${result.warnings.length}.`);
  } else {
    throw new Error('Usage: ci-checks.mjs repository|audit|audit-local <report.json>');
  }
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    main();
  } catch (error) {
    console.error(error.message);
    process.exitCode = 1;
  }
}