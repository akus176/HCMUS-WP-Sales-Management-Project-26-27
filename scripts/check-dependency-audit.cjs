const fs = require('node:fs');
const assert = require('node:assert/strict');

function checkNuget(report) {
  if (!Array.isArray(report.projects) || !report.projects.length ||
      report.logs?.some(log => log.level !== 'information')) throw new Error('NuGet audit is incomplete.');
  for (const project of report.projects) {
    if (!project.path) throw new Error('NuGet project is missing its path.');
    // dotnet omits frameworks when the vulnerable-only query finds no packages.
    for (const framework of project.frameworks ?? []) {
      for (const pkg of [...(framework.topLevelPackages ?? []), ...(framework.transitivePackages ?? [])]) {
        if (pkg.vulnerabilities?.length) throw new Error(`Vulnerable NuGet dependency: ${pkg.id}`);
      }
    }
  }
}
function checkNpm(report) {
  if (report.error || !report.metadata?.vulnerabilities ||
      !Number.isInteger(report.metadata.vulnerabilities.total)) throw new Error('npm audit is incomplete.');
  for (const severity of ['high', 'critical']) {
    if (!Number.isInteger(report.metadata.vulnerabilities[severity])) throw new Error('npm audit is incomplete.');
    if (report.metadata.vulnerabilities[severity] !== 0) throw new Error(`npm audit found ${severity} vulnerabilities.`);
  }
}

if (process.argv[2] === '--self-test') {
  checkNuget({ projects: [{ path: 'fixture.csproj' }] });
  assert.throws(() => checkNuget({ projects: [] }));
  assert.throws(() => checkNuget({ projects: [{ path: 'fixture.csproj', frameworks: [{ transitivePackages: [{ id: 'fixture', vulnerabilities: [{}] }] }] }] }));
  assert.throws(() => checkNuget({ projects: [{}], logs: [{ level: 'error' }] }));
  checkNpm({ metadata: { vulnerabilities: { total: 0, high: 0, critical: 0 } } });
  checkNpm({ metadata: { vulnerabilities: { total: 2, moderate: 2, high: 0, critical: 0 } } });
  assert.throws(() => checkNpm({ metadata: { vulnerabilities: { total: 1, high: 1, critical: 0 } } }));
  assert.throws(() => checkNpm({ metadata: { vulnerabilities: { total: 1, high: 0, critical: 1 } } }));
  assert.throws(() => checkNpm({ error: { code: 'NETWORK_FAILURE' } }));
  assert.throws(() => checkNpm({}));
  console.log('Dependency gate: clean reports accepted; vulnerable and incomplete reports rejected.');
} else {
  const [nuget, ...npm] = process.argv.slice(2);
  if (!nuget || !npm.length) throw new Error('Supply NuGet JSON followed by npm audit JSON files.');
  checkNuget(JSON.parse(fs.readFileSync(nuget, 'utf8').replace(/^\uFEFF/, '')));
  for (const file of npm) checkNpm(JSON.parse(fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, '')));
  console.log('Dependency gate passed: no vulnerable NuGet packages or high/critical npm advisories. Review lower npm advisories in the reports.');
}
