import assert from 'node:assert/strict';
import { Buffer } from 'node:buffer';
import { spawn } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { createWriteStream } from 'node:fs';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createInventoryForwarder } from './inventory-forwarder.mjs';
import { parseMobileEnvironment } from '../src/config/environment.ts';

const root = fileURLToPath(new URL('../', import.meta.url));
const required = (name) => {
  assert.ok(process.env[name], `Set ${name}.`);
  return process.env[name];
};
assert.equal(
  required('COFFEESHOP_INVENTORY_DISPOSABLE'),
  '1',
  'Use only a disposable database.',
);
const api = new URL(required('COFFEESHOP_INVENTORY_API_URL'));
assert.ok(
  api.protocol === 'http:' && ['localhost', '127.0.0.1'].includes(api.hostname),
);
assert.equal(api.pathname, '/api/v1');
const deviceUrl = new URL(required('EXPO_PUBLIC_API_BASE_URL'));
parseMobileEnvironment(required('EXPO_PUBLIC_APP_ENV'), deviceUrl.href);
assert.equal(
  process.env.EXPO_PUBLIC_APP_ENV,
  'device',
  'This focused runner uses the physical-device LAN profile.',
);
assert.equal(deviceUrl.pathname, '/api/v1');
assert.notEqual(
  deviceUrl.origin,
  api.origin,
  'Point Metro at the forwarding listener, not the upstream API.',
);
const bindHost = required('COFFEESHOP_INVENTORY_PROXY_HOST');
assert.equal(deviceUrl.hostname, bindHost);
const adb = process.env.ADB_PATH || 'adb';
const adbArgs = process.env.ANDROID_SERIAL
  ? ['-s', process.env.ANDROID_SERIAL]
  : [];
const java = join(
  required('JAVA_HOME'),
  'bin',
  process.platform === 'win32' ? 'java.exe' : 'java',
);
const maestroArgs = [
  '--enable-native-access=ALL-UNNAMED',
  '-Xmx1024m',
  '-cp',
  join(required('MAESTRO_HOME'), 'lib', '*'),
  'maestro.cli.AppKt',
];
const username = required('COFFEESHOP_INVENTORY_USERNAME');
assert.match(
  username,
  /^issue198[a-z0-9]+$/i,
  'Use a dedicated issue198 synthetic account.',
);
const password = required('COFFEESHOP_INVENTORY_PASSWORD');
const container = required('COFFEESHOP_INVENTORY_POSTGRES_CONTAINER');
const database = required('COFFEESHOP_INVENTORY_POSTGRES_DATABASE');
const dbUser = required('COFFEESHOP_INVENTORY_POSTGRES_USER');
for (const value of [container, database, dbUser])
  assert.match(value, /^[a-zA-Z0-9_-]+$/);
const runId = randomUUID().slice(0, 8);
const results = resolve(
  process.env.COFFEESHOP_INVENTORY_RESULTS ||
    join(root, '.maestro/results/inventory'),
  runId,
);
await mkdir(join(results, 'private'), { recursive: true });
const names = [
  'login',
  'browse',
  'refresh-before',
  'refresh-after',
  'adjustment',
  'invalid',
  'forbidden',
  'adjustment-conflict',
  'adjustment-lost',
  'adjustment-retry',
  'receipt-empty',
  'receipt-post',
  'receipt-conflict',
  'receipt-lost',
  'receipt-refresh',
  'receipt-retry-lost',
  'receipt-retry',
];
const summary = {
  issue: 198,
  runId,
  expected: names.length,
  passed: 0,
  failedOrMissing: names.length,
  serverVerificationPassed: false,
  cases: names.map((name) => ({ name, passed: false, status: 'not run' })),
};
const save = () =>
  writeFile(join(results, 'summary.json'), JSON.stringify(summary, null, 2));
await save(); // Fresh directory and red summary before any seed or device operation.
console.log(`Results: ${results}`);

async function command(
  executable,
  args,
  { env = process.env, input, log, binary = false } = {},
) {
  return new Promise((accept, reject) => {
    const child = spawn(executable, args, {
      env,
      cwd: root,
      windowsHide: true,
      stdio: ['pipe', 'pipe', 'pipe'],
    });
    const timeout = setTimeout(() => child.kill(), 240000);
    const chunks = [];
    const file = log ? createWriteStream(log) : undefined;
    child.stdout.on('data', (chunk) =>
      file ? file.write(chunk) : chunks.push(chunk),
    );
    child.stderr.on('data', (chunk) => file?.write(chunk));
    child.on('error', (error) => {
      clearTimeout(timeout);
      reject(error);
    });
    child.on('close', (code) => {
      clearTimeout(timeout);
      file?.end();
      if (code !== 0)
        reject(
          new Error(
            `${executable.split(/[\\/]/).pop()} failed (${code}); see private runner output.`,
          ),
        );
      else
        accept(
          binary
            ? Buffer.concat(chunks)
            : Buffer.concat(chunks).toString().trim(),
        );
    });
    child.stdin.end(input);
  });
}
const android = (...args) => command(adb, [...adbArgs, ...args]);
async function request(path, { token, body, method = 'GET', expected } = {}) {
  const response = await fetch(`${api}${path}`, {
    method,
    headers: {
      'content-type': 'application/json',
      ...(token ? { authorization: `Bearer ${token}` } : {}),
    },
    body: body === undefined ? undefined : JSON.stringify(body),
    signal: AbortSignal.timeout(30000),
  });
  if (response.status === 401 && admin && token === admin.accessToken) {
    admin = await request('/auth/login', {
      method: 'POST',
      body: {
        username: required('COFFEESHOP_INVENTORY_ADMIN_USERNAME'),
        password: required('COFFEESHOP_INVENTORY_ADMIN_PASSWORD'),
      },
    });
    return request(path, { token: admin.accessToken, body, method, expected });
  }
  if (expected !== undefined)
    assert.equal(
      response.status,
      expected,
      `${method} ${path}: unexpected status.`,
    );
  else assert.ok(response.ok, `${method} ${path}: HTTP ${response.status}`);
  return response.status === 204 ? undefined : response.json();
}
let admin;
let inventory;
let roleRemoved = false;
let proxy;
const uuid = (value) => {
  assert.match(value, /^[0-9a-f-]{36}$/i);
  return value;
};
async function setInventoryRole(enabled) {
  const id = uuid(inventory.user.id);
  const sql = enabled
    ? `INSERT INTO identity.user_roles (user_id,role_id,assigned_at) SELECT '${id}',id,now() FROM identity.roles WHERE code='InventoryStaff' ON CONFLICT DO NOTHING;`
    : `DELETE FROM identity.user_roles WHERE user_id='${id}' AND role_id IN (SELECT id FROM identity.roles WHERE code='InventoryStaff');`;
  await command(
    'docker',
    [
      'exec',
      '-i',
      container,
      'psql',
      '-X',
      '-v',
      'ON_ERROR_STOP=1',
      '-U',
      dbUser,
      '-d',
      database,
    ],
    { input: sql },
  );
  roleRemoved = !enabled;
}
const get = (path) => request(path, { token: admin.accessToken });
const post = (path, body, token = admin.accessToken) =>
  request(path, { method: 'POST', body, token });
async function snapshot(ingredient, receipt) {
  const balance = await get(`/ingredients/${ingredient.ingredientId}`);
  const movements = await get(
    `/ingredients/${ingredient.ingredientId}/movements`,
  );
  const state = {
    balance: Number(balance.availableQuantity),
    movements: movements.sort((a, b) => a.id.localeCompare(b.id)),
  };
  if (receipt) {
    state.receiptStatus = (
      await get(`/purchase-receipts/${receipt.id}`)
    ).status;
    state.receiptId = receipt.id;
  }
  return state;
}
function oneEffect(state, quantity, type, actor) {
  assert.equal(state.balance, quantity);
  assert.equal(state.movements.length, 1);
  assert.equal(state.movements[0].type, type);
  assert.equal(state.movements[0].actorId, actor);
  if (type === 'receipt') {
    assert.equal(state.movements[0].purchaseReceiptId, state.receiptId);
    assert.equal(state.receiptStatus, 'posted');
  }
}
async function screenshot(name) {
  const bytes = await command(
    adb,
    [...adbArgs, 'exec-out', 'screencap', '-p'],
    { binary: true },
  );
  assert.equal(bytes.subarray(0, 8).toString('hex'), '89504e470d0a1a0a');
  await writeFile(join(results, `${name}.png`), bytes);
}
const flowEnv = {};
async function check(name, flow, variables = {}, verify = async () => ({})) {
  const item = summary.cases.find((candidate) => candidate.name === name);
  item.status = 'running';
  await save();
  console.log(`Running ${name}...`);
  const xmlPath = join(results, `${name}.xml`);
  try {
    await command(
      java,
      [
        ...maestroArgs,
        ...(process.env.ANDROID_SERIAL
          ? ['--device', process.env.ANDROID_SERIAL]
          : []),
        'test',
        '--format',
        'junit',
        '--output',
        xmlPath,
        '--debug-output',
        join(results, 'private', name),
        join(root, '.maestro', 'inventory-focused', `${flow}.yaml`),
      ],
      {
        env: {
          ...process.env,
          ...flowEnv,
          ...variables,
          MAESTRO_STAFF_USERNAME: username,
          MAESTRO_STAFF_PASSWORD: password,
          MAESTRO_CLI_NO_ANALYTICS: '1',
          MAESTRO_CLI_ANALYSIS_NOTIFICATION_DISABLED: 'true',
        },
        log: join(results, 'private', `${name}.log`),
      },
    );
    const xml = await readFile(xmlPath, 'utf8');
    assert.equal(
      (xml.match(/<testcase\b/g) || []).length,
      1,
      'Expected one fresh native case.',
    );
    assert.ok(
      !/<(?:failure|error|skipped)\b/.test(xml),
      'Native case failed or was skipped.',
    );
    item.state = await verify();
    await screenshot(name);
    item.passed = true;
    item.status = 'passed';
    summary.passed++;
    summary.failedOrMissing--;
    console.log(`PASS ${name}`);
  } catch (error) {
    item.status = 'failed';
    item.error = error.message;
    await screenshot(`${name}-failed`).catch(() => {});
    throw error;
  } finally {
    await save();
  }
}

try {
  assert.equal(await android('get-state'), 'device');
  summary.device = {};
  for (const [key, property] of Object.entries({
    model: 'ro.product.model',
    android: 'ro.build.version.release',
    apiLevel: 'ro.build.version.sdk',
    abi: 'ro.product.cpu.abi',
  }))
    summary.device[key] = await android('shell', 'getprop', property);
  summary.appCommit =
    process.env.COFFEESHOP_INVENTORY_APP_COMMIT || 'unspecified';
  summary.apiCommit = required('COFFEESHOP_INVENTORY_API_COMMIT');
  summary.harnessCommit = await command('git', ['rev-parse', 'HEAD']);
  summary.environment =
    'Disposable PostgreSQL 18; shared ASP.NET Core API; local HTTP forwarding listener; synthetic accounts';
  const installedApk = (
    await android('shell', 'pm', 'path', 'com.coffeeshop.staffmobile')
  )
    .split('\n')[0]
    .replace(/^package:/, '')
    .trim();
  assert.ok(
    installedApk.startsWith('/data/app/') && installedApk.endsWith('.apk'),
  );
  summary.apkSha256 = (await android('shell', 'sha256sum', installedApk)).split(
    /\s+/,
  )[0];
  summary.device.app = 'com.coffeeshop.staffmobile';
  summary.device.build = await android(
    'shell',
    'dumpsys',
    'package',
    summary.device.app,
  );
  summary.device.build = summary.device.build
    .split('\n')
    .filter((line) => /versionCode=|versionName=/.test(line))
    .map((line) => line.trim());
  assert.ok(summary.device.build.length, 'Install the native app first.');
  const readiness = await fetch(`${api.origin}/health/ready`);
  assert.equal(readiness.status, 200);
  admin = await request('/auth/login', {
    method: 'POST',
    body: {
      username: required('COFFEESHOP_INVENTORY_ADMIN_USERNAME'),
      password: required('COFFEESHOP_INVENTORY_ADMIN_PASSWORD'),
    },
  });
  inventory = await request('/auth/login', {
    method: 'POST',
    body: { username, password },
  });
  assert.deepEqual(inventory.user.roles, ['InventoryStaff']);
  const prefix = `I198 ${runId}`;
  const ingredient = async (
    label,
    availableQuantity = 1,
    reorderThreshold = 3,
  ) =>
    post('/ingredients', {
      name: `${prefix} ${label}`,
      baseUnit: 'l',
      availableQuantity,
      reorderThreshold,
    });
  const milk = await ingredient('milk');
  const boundary = await ingredient('boundary', 3);
  const normal = await ingredient('normal', 10);
  const refresh = await ingredient('refresh');
  const conflict = await ingredient('conflict', 5);
  const forbidden = await ingredient('forbidden');
  const lost = await ingredient('lost');
  const supplier = await post('/suppliers', {
    name: `${prefix} supplier`,
    phone: '0123456789',
    email: 'inventory198@example.test',
    address: 'Synthetic test address',
  });
  async function receipt(label, empty = false) {
    const stock = await ingredient(label, 0);
    const record = await post('/purchase-receipts', {
      supplierId: supplier.id,
      receiptNumber: `I198-${runId}-${label}`,
    });
    if (!empty)
      await post(`/purchase-receipts/${record.id}/lines`, {
        ingredientId: stock.ingredientId,
        quantity: 4,
        unitCost: 2,
      });
    return { stock, record };
  }
  const empty = await receipt('empty', true);
  const standard = await receipt('standard');
  const competing = await receipt('competing');
  const recovery = await receipt('recovery');
  const retry = await receipt('retry');
  summary.seed = {
    prefix,
    milk,
    boundary,
    normal,
    refresh,
    conflict,
    forbidden,
    lost,
    receipts: { empty, standard, competing, recovery, retry },
  };
  await save();
  proxy = createInventoryForwarder(api.origin);
  await new Promise((accept, reject) => {
    proxy.server.once('error', reject);
    proxy.server.listen(Number(deviceUrl.port), bindHost, accept);
  });
  Object.assign(flowEnv, {
    MAESTRO_INGREDIENT: milk.name,
    MAESTRO_BOUNDARY: boundary.name,
    MAESTRO_NORMAL: normal.name,
    MAESTRO_DELTA: '2',
    MAESTRO_REASON: 'Issue198 count',
  });
  await check('login', 'login');
  await check('browse', 'browse');
  await check('refresh-before', 'refresh-before', {
    MAESTRO_INGREDIENT: refresh.name,
  });
  await post(`/ingredients/${refresh.ingredientId}/adjustments`, {
    requestId: randomUUID(),
    quantityDelta: 1,
    reason: 'Refresh fixture',
  });
  await check('refresh-after', 'refresh-after', {}, async () => {
    const state = await snapshot(refresh);
    oneEffect(state, 2, 'adjustment', admin.user.id);
    return state;
  });
  await check('adjustment', 'adjustment-success', {}, async () => {
    const state = await snapshot(milk);
    oneEffect(state, 3, 'adjustment', inventory.user.id);
    assert.equal(state.movements[0].reason, 'Issue198 count');
    return state;
  });
  const beforeInvalid = await snapshot(milk);
  await check('invalid', 'invalid', { MAESTRO_DELTA: '-9999' }, async () => {
    const state = await snapshot(milk);
    assert.deepEqual(state, beforeInvalid);
    assert.equal(proxy.exchanges.at(-1).status, 400);
    return state;
  });
  const denial = proxy.arm({
    mode: 'before',
    method: 'POST',
    path: `/api/v1/ingredients/${forbidden.ingredientId}/adjustments`,
    before: () => setInventoryRole(false),
    observe: () => snapshot(forbidden),
  });
  try {
    await check(
      'forbidden',
      'adjustment-error',
      {
        MAESTRO_INGREDIENT: forbidden.name,
        MAESTRO_RESULT: 'The server denied this adjustment.*',
      },
      async () => {
        assert.ok(denial.used);
        assert.equal(denial.status, 403);
        const state = await snapshot(forbidden);
        assert.deepEqual(state, denial.beforeRejectedOrLost);
        return state;
      },
    );
  } finally {
    if (roleRemoved) await setInventoryRole(true);
  }
  const collision = proxy.arm({
    mode: 'compete',
    method: 'POST',
    path: `/api/v1/ingredients/${conflict.ingredientId}/adjustments`,
    observe: () => snapshot(conflict),
  });
  await check(
    'adjustment-conflict',
    'adjustment-error',
    {
      MAESTRO_INGREDIENT: conflict.name,
      MAESTRO_RESULT: 'The server rejected this adjustment because.*',
    },
    async () => {
      assert.ok(collision.used);
      assert.equal(collision.status, 409);
      const state = await snapshot(conflict);
      assert.deepEqual(state, collision.beforeRejectedOrLost);
      oneEffect(state, 6, 'adjustment', inventory.user.id);
      return state;
    },
  );
  const lostAdjustment = proxy.arm({
    mode: 'drop',
    method: 'POST',
    path: `/api/v1/ingredients/${lost.ingredientId}/adjustments`,
    observe: () => snapshot(lost),
  });
  await check(
    'adjustment-lost',
    'adjustment-error',
    {
      MAESTRO_INGREDIENT: lost.name,
      MAESTRO_RESULT: 'The adjustment could not be confirmed.*',
    },
    async () => {
      assert.ok(lostAdjustment.used);
      oneEffect(lostAdjustment.afterCommit, 3, 'adjustment', inventory.user.id);
      return lostAdjustment.afterCommit;
    },
  );
  await check('adjustment-retry', 'adjustment-retry', {}, async () => {
    const state = await snapshot(lost);
    assert.deepEqual(state, lostAdjustment.afterCommit);
    const requests = proxy.exchanges.filter(
      (entry) => entry.path === lostAdjustment.path,
    );
    assert.equal(requests.length, 2);
    assert.equal(requests[0].requestId, requests[1].requestId);
    assert.equal(requests[0].bodyHash, requests[1].bodyHash);
    return state;
  });
  await check(
    'receipt-empty',
    'receipt-empty',
    { MAESTRO_RECEIPT: empty.record.receiptNumber },
    async () => {
      const state = await snapshot(empty.stock, empty.record);
      assert.equal(state.receiptStatus, 'draft');
      assert.equal(state.movements.length, 0);
      return state;
    },
  );
  await check(
    'receipt-post',
    'receipt-post',
    { MAESTRO_RECEIPT: standard.record.receiptNumber },
    async () => {
      const state = await snapshot(standard.stock, standard.record);
      oneEffect(state, 4, 'receipt', inventory.user.id);
      assert.equal(state.receiptStatus, 'posted');
      return state;
    },
  );
  const receiptCollision = proxy.arm({
    mode: 'compete',
    method: 'POST',
    path: `/api/v1/purchase-receipts/${competing.record.id}/post`,
    observe: () => snapshot(competing.stock, competing.record),
  });
  await check(
    'receipt-conflict',
    'receipt-error',
    {
      MAESTRO_RECEIPT: competing.record.receiptNumber,
      MAESTRO_RESULT: 'The receipt changed or was already posted.*',
    },
    async () => {
      assert.ok(receiptCollision.used);
      assert.equal(receiptCollision.status, 409);
      const state = await snapshot(competing.stock, competing.record);
      assert.deepEqual(state, receiptCollision.beforeRejectedOrLost);
      oneEffect(state, 4, 'receipt', inventory.user.id);
      return state;
    },
  );
  async function loseReceipt(name, fixture) {
    const fault = proxy.arm({
      mode: 'drop',
      method: 'POST',
      path: `/api/v1/purchase-receipts/${fixture.record.id}/post`,
      observe: () => snapshot(fixture.stock, fixture.record),
    });
    await check(
      name,
      'receipt-error',
      {
        MAESTRO_RECEIPT: fixture.record.receiptNumber,
        MAESTRO_RESULT: 'The receipt post could not be confirmed.*',
      },
      async () => {
        assert.ok(fault.used);
        assert.equal(fault.status, 204);
        oneEffect(fault.afterCommit, 4, 'receipt', inventory.user.id);
        return fault.afterCommit;
      },
    );
    return fault;
  }
  const lostReceipt = await loseReceipt('receipt-lost', recovery);
  await check(
    'receipt-refresh',
    'receipt-refresh',
    { MAESTRO_RECEIPT: recovery.record.receiptNumber },
    async () => {
      const state = await snapshot(recovery.stock, recovery.record);
      assert.deepEqual(state, lostReceipt.afterCommit);
      return state;
    },
  );
  const retriedReceipt = await loseReceipt('receipt-retry-lost', retry);
  await check(
    'receipt-retry',
    'receipt-retry',
    { MAESTRO_RECEIPT: retry.record.receiptNumber },
    async () => {
      const state = await snapshot(retry.stock, retry.record);
      assert.deepEqual(state, retriedReceipt.afterCommit);
      const attempts = proxy.exchanges.filter(
        (entry) => entry.path === retriedReceipt.path,
      );
      assert.deepEqual(
        attempts.map((entry) => entry.status),
        [204, 409],
      );
      return state;
    },
  );
  assert.ok(
    !proxy.exchanges.some((entry) => entry.proxyError),
    'A proxy failure occurred.',
  );
  summary.serverVerificationPassed = true;
  console.log(
    `PASS ${summary.passed}/${summary.expected} focused native cases and independent API verification.`,
  );
} catch (error) {
  summary.error = error.message;
  process.exitCode = 1;
  console.error(`FAILED: ${error.message}`);
} finally {
  if (roleRemoved) {
    try {
      await setInventoryRole(true);
    } catch {
      summary.permissionRestoreFailed = true;
      process.exitCode = 1;
    }
  }
  if (proxy) {
    summary.exchanges = proxy.exchanges;
    await proxy.close();
  }
  await save();
}
