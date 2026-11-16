import assert from 'node:assert/strict';
import test from 'node:test';
import { mkdtemp, readFile, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { evidence, localEnvironment } from './run-collection.mjs';

const variables = {
  COFFEESHOP_COLLECTION_API_HOST: 'http://127.0.0.1:56789',
  COFFEESHOP_COLLECTION_MANAGER_USERNAME: 'synthetic-manager',
  COFFEESHOP_COLLECTION_MANAGER_PASSWORD: 'private-manager-password',
  COFFEESHOP_COLLECTION_CASHIER_USERNAME: 'synthetic-cashier',
  COFFEESHOP_COLLECTION_CASHIER_PASSWORD: 'private-cashier-password',
};

test('One host override updates both health and business URLs without editing payloads', () => {
  const template = { values: ['apiHost', 'apiRoot', 'managerPassword', 'cashierPassword'].map(key => ({ key, value: '' })) };
  const values = Object.fromEntries(localEnvironment(template, variables).values.map(value => [value.key, value.value]));
  assert.equal(values.apiHost, 'http://127.0.0.1:56789');
  assert.equal(values.apiRoot, 'http://127.0.0.1:56789/api/v1');
  assert.equal(values.managerPassword, variables.COFFEESHOP_COLLECTION_MANAGER_PASSWORD);
  assert.equal(template.values[0].value, '');
  assert.throws(() => localEnvironment(template, { ...variables, COFFEESHOP_COLLECTION_MANAGER_PASSWORD: '' }));
  assert.throws(() => localEnvironment(template, { ...variables, COFFEESHOP_COLLECTION_API_HOST: 'http://user:secret@localhost' }));
});

test('Published evidence omits secrets even from failing responses and error messages', () => {
  const secret = 'never-export-this-session-secret';
  const summary = { environment: { password: secret }, run: {
    summary: { executedRequests: { executed: 1 }, tests: { executed: 1, failed: 1, passed: 0, skipped: 0 } },
    executions: [{ requestExecuted: { name: 'Manager login', method: 'POST',
      url: { path: ['api', 'v1', 'auth', 'login'], query: [{ key: 'private', value: secret }] }, headers: { Authorization: secret } },
      response: { code: 401, body: secret }, tests: [{ name: 'Session required', status: 'failed', error: { message: secret } }],
      errors: [{ message: secret }],
    }],
  } };
  const result = evidence(summary, 1);
  assert.equal(result.passed, false);
  assert.equal(result.requests[0].path, '/api/v1/auth/login');
  assert.equal(JSON.stringify(result).includes(secret), false);
});

test('Empty, incomplete and transport-failed runs cannot pass', () => {
  assert.equal(evidence(undefined, 9).passed, false);
  assert.equal(evidence({ run: { executions: [], failures: [], stats: { assertions: { total: 0 } } } }, 0).passed, false);
  assert.equal(evidence({ run: { executions: [], failures: [], stats: { assertions: { total: 1 } } } }, 1, true).passed, false);
});

test('A complete Postman CLI report passes, while skipped assertions cannot pass', () => {
  const summary = { run: { summary: {
    executedRequests: { executed: 1 }, tests: { executed: 1, failed: 0, passed: 1, skipped: 0 },
  }, executions: [{ requestExecuted: { name: 'Health', method: 'GET', url: { path: ['health'] } },
    response: { code: 200 }, tests: [{ name: 'Healthy', status: 'passed' }], errors: [],
  }] } };
  assert.equal(evidence(summary, 1).passed, true);
  assert.equal(evidence(summary, 2).passed, false);
  assert.equal(evidence(summary, 1, true).passed, false);
  summary.run.executions[0].errors.push({ source: 'request' });
  assert.equal(evidence(summary, 1).passed, false);
  summary.run.executions[0].errors = [];
  summary.run.summary.tests.skipped = 1;
  assert.equal(evidence(summary, 1).passed, false);
});

test('A configuration failure replaces a previous passing result before making requests', async () => {
  const directory = await mkdtemp(join(tmpdir(), 'collection-runner-'));
  const output = join(directory, 'result.json');
  await writeFile(output, JSON.stringify({ passed: true }));
  const result = spawnSync(process.execPath, [fileURLToPath(new URL('./run-collection.mjs', import.meta.url)), output], {
    env: { ...process.env, ...variables, COFFEESHOP_COLLECTION_MANAGER_PASSWORD: '' }, encoding: 'utf8',
  });
  assert.equal(result.status, 1);
  assert.equal(JSON.parse(await readFile(output, 'utf8')).passed, false);
  assert.equal(result.stderr.includes(variables.COFFEESHOP_COLLECTION_CASHIER_PASSWORD), false);
});
