import { chmod, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { tmpdir } from 'node:os';
import { basename, dirname, join, resolve } from 'node:path';
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';

export function localEnvironment(template, variables) {
  const host = new URL(variables.COFFEESHOP_COLLECTION_API_HOST ?? 'http://127.0.0.1:5232');
  if (!['http:', 'https:'].includes(host.protocol) || host.username || host.password ||
      host.pathname !== '/' || host.search || host.hash) {
    throw new Error('COFFEESHOP_COLLECTION_API_HOST must be an HTTP(S) origin without credentials.');
  }
  const overrides = { apiHost: host.origin, apiRoot: `${host.origin}/api/v1` };
  for (const [key, source] of Object.entries({
    managerUsername: 'COFFEESHOP_COLLECTION_MANAGER_USERNAME',
    managerPassword: 'COFFEESHOP_COLLECTION_MANAGER_PASSWORD',
    cashierUsername: 'COFFEESHOP_COLLECTION_CASHIER_USERNAME',
    cashierPassword: 'COFFEESHOP_COLLECTION_CASHIER_PASSWORD',
  })) {
    if (!variables[source]) throw new Error(`Set ${source} privately before running the collection.`);
    overrides[key] = variables[source];
  }
  return { ...template, values: template.values.map(value => ({ ...value, value: overrides[value.key] ?? value.value })) };
}

function requests(items) {
  return items.flatMap(item => item.item ? requests(item.item) : [item]);
}

// Deliberately exclude response bodies, headers, variables, error messages and
// raw CLI summaries: authentication responses contain session credentials.
export function evidence(summary, expectedRequests, runnerError = false) {
  const executions = summary?.run?.executions ?? [];
  const failures = summary?.run?.failures?.length ?? 0;
  const assertions = summary?.run?.stats?.assertions?.total ?? 0;
  return {
    passed: !runnerError && expectedRequests > 0 && failures === 0 && executions.length === expectedRequests && assertions > 0 &&
      executions.every(execution => execution.response && execution.assertions?.length > 0 &&
        execution.assertions.every(assertion => !assertion.error && !assertion.skipped)),
    expectedRequests,
    executedRequests: executions.length,
    assertions,
    failures,
    requests: executions.map(execution => ({
      name: execution.item.name,
      method: execution.request.method,
      path: new URL(typeof execution.request.url === 'string' ? execution.request.url : execution.request.url.raw ??
        `${execution.request.url.protocol}://${execution.request.url.host.join('.')}/${execution.request.url.path.join('/')}`).pathname,
      status: execution.response?.code ?? null,
      assertions: (execution.assertions ?? []).map(assertion => ({
        name: assertion.assertion, passed: !assertion.error,
      })),
    })),
  };
}

async function main() {
  const output = process.argv[2];
  if (output) await writeFile(output, `${JSON.stringify({ passed: false, state: 'not_complete' })}\n`);
  const collection = JSON.parse(await readFile(new URL('./coffee-shop-local.postman_collection.json', import.meta.url)));
  const template = JSON.parse(await readFile(new URL('./coffee-shop-local.postman_environment.json', import.meta.url)));
  const environment = localEnvironment(template, process.env);
  const privateDirectory = await mkdtemp(join(tmpdir(), 'coffeeshop-api-collection-'));
  await chmod(privateDirectory, 0o700);
  let result;
  try {
    const environmentPath = join(privateDirectory, 'environment.json');
    const rawReport = join(privateDirectory, 'raw.json');
    await writeFile(environmentPath, JSON.stringify(environment), { mode: 0o600 });
    const childEnvironment = { ...process.env };
    delete childEnvironment.POSTMAN_API_KEY;
    const cli = fileURLToPath(new URL('./node_modules/postman-cli/bin/postman.js', import.meta.url));
    let runnerError = false;
    try {
      await promisify(execFile)(process.execPath, [cli, 'collection', 'run',
        fileURLToPath(new URL('./coffee-shop-local.postman_collection.json', import.meta.url)),
        '--environment', environmentPath, '--report-events=false', '--reporters', 'json',
        '--reporter-json-export', rawReport, '--timeout-request', '10000', '--silent',
      ], { cwd: privateDirectory, env: childEnvironment, timeout: 180000, maxBuffer: 8 * 1024 * 1024 });
    } catch {
      runnerError = true;
    }
    const summary = JSON.parse(await readFile(rawReport, 'utf8'));
    result = evidence(summary, requests(collection.item).length, runnerError);
  } finally {
    if (dirname(resolve(privateDirectory)) !== resolve(tmpdir()) || !basename(privateDirectory).startsWith('coffeeshop-api-collection-')) {
      throw new Error('Unexpected temporary directory; cleanup stopped.');
    }
    await rm(privateDirectory, { recursive: true, force: true });
  }
  if (output) await writeFile(output, `${JSON.stringify(result, null, 2)}\n`);
  console.log(`${result.passed ? 'PASS' : 'FAIL'}: ${result.executedRequests}/${result.expectedRequests} requests, ${result.assertions} assertions, ${result.failures} failures.`);
  process.exitCode = result.passed ? 0 : 1;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch(() => {
    console.error('Collection runner failed. Check local configuration and API availability; no private response data was printed.');
    process.exitCode = 1;
  });
}
