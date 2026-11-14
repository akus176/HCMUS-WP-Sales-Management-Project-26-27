import assert from 'node:assert/strict';
import { Buffer } from 'node:buffer';
import { spawn } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { createWriteStream } from 'node:fs';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseMobileEnvironment } from '../src/config/environment.ts';
import { createWorkflowForwarder } from './workflow-forwarder.mjs';

const root = fileURLToPath(new URL('../', import.meta.url));
const required = (name) => {
  assert.ok(process.env[name], `Set ${name}.`);
  return process.env[name];
};
assert.equal(required('COFFEESHOP_WORKFLOW_DISPOSABLE'), '1');
const api = new URL(
  parseMobileEnvironment('test', required('COFFEESHOP_WORKFLOW_API_URL'))
    .apiBaseUrl,
);
assert.ok(
  api.protocol === 'http:' && ['127.0.0.1', 'localhost'].includes(api.hostname),
);
const device = new URL(
  parseMobileEnvironment(
    required('EXPO_PUBLIC_APP_ENV'),
    required('EXPO_PUBLIC_API_BASE_URL'),
  ).apiBaseUrl,
);
assert.ok(['device', 'emulator'].includes(process.env.EXPO_PUBLIC_APP_ENV));
const host = required('COFFEESHOP_WORKFLOW_PROXY_HOST');
assert.equal(
  device.hostname,
  process.env.EXPO_PUBLIC_APP_ENV === 'emulator' ? '10.0.2.2' : host,
);
assert.notEqual(
  device.port,
  api.port,
  'Point Metro at the forwarding listener.',
);
const adb = process.env.ADB_PATH || 'adb';
const adbArgs = process.env.ANDROID_SERIAL
  ? ['-s', process.env.ANDROID_SERIAL]
  : [];
const java = join(
  required('JAVA_HOME'),
  'bin',
  process.platform === 'win32' ? 'java.exe' : 'java',
);
const maestro = [
  '--enable-native-access=ALL-UNNAMED',
  '-Xmx1024m',
  '-cp',
  join(required('MAESTRO_HOME'), 'lib', '*'),
  'maestro.cli.AppKt',
];
const container = required('COFFEESHOP_WORKFLOW_POSTGRES_CONTAINER');
const database = required('COFFEESHOP_WORKFLOW_POSTGRES_DATABASE');
const dbUser = required('COFFEESHOP_WORKFLOW_POSTGRES_USER');
for (const value of [container, database, dbUser])
  assert.match(value, /^[a-zA-Z0-9_-]+$/);
assert.match(database, /^issue201_/);
const roles = ['Admin', 'Manager', 'Barista', 'DeliveryStaff', 'OtherCourier'];
const accounts = Object.fromEntries(
  roles.map((role) => {
    const username = required(
      `COFFEESHOP_WORKFLOW_${role.toUpperCase()}_USERNAME`,
    );
    assert.match(
      username,
      /^issue201[a-z0-9]+$/i,
      'Use dedicated issue201 accounts.',
    );
    return [
      role,
      { username, password: required('COFFEESHOP_WORKFLOW_PASSWORD') },
    ];
  }),
);
const names = [
  'barista-progression',
  'barista-conflict',
  'barista-stale',
  'barista-forbidden',
  'barista-offline',
  'discount-approve',
  'discount-reject',
  'discount-conflict',
  'discount-stale',
  'discount-forbidden',
  'discount-offline',
  'courier-own-list',
  'courier-other-detail',
  'courier-delivered',
  'courier-failed',
  'courier-conflict',
  'courier-stale',
  'courier-forbidden',
  'courier-offline',
];
const runId = randomUUID().slice(0, 8);
const results = resolve(
  process.env.COFFEESHOP_WORKFLOW_RESULTS ||
    join(root, '.maestro/results/workflows'),
  runId,
);
await mkdir(join(results, 'private'), { recursive: true });
const summary = {
  issue: 201,
  runId,
  expected: names.length,
  passed: 0,
  failedOrMissing: names.length,
  serverVerificationPassed: false,
  cases: names.map((name) => ({
    name,
    passed: false,
    status: 'not run',
    steps: [],
  })),
};
const save = () =>
  writeFile(join(results, 'summary.json'), JSON.stringify(summary, null, 2));
await save();
console.log(`Results: ${results}`);
async function command(
  executable,
  args,
  { input, log, env = process.env, binary = false } = {},
) {
  return new Promise((accept, reject) => {
    const child = spawn(executable, args, {
      env,
      cwd: root,
      windowsHide: true,
      stdio: ['pipe', 'pipe', 'pipe'],
    });
    const timer = setTimeout(() => child.kill(), 240000);
    const chunks = [];
    const file = log ? createWriteStream(log) : undefined;
    child.stdout.on('data', (chunk) =>
      file ? file.write(chunk) : chunks.push(chunk),
    );
    child.stderr.on('data', (chunk) => file?.write(chunk));
    child.on('error', (error) => {
      clearTimeout(timer);
      reject(error);
    });
    child.on('close', (code) => {
      clearTimeout(timer);
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
const sql = (input) =>
  command(
    'docker',
    [
      'exec',
      '-i',
      container,
      'psql',
      '-X',
      '-A',
      '-t',
      '-v',
      'ON_ERROR_STOP=1',
      '-U',
      dbUser,
      '-d',
      database,
    ],
    { input },
  );
const uuid = (value) => {
  assert.match(value, /^[0-9a-f-]{36}$/i);
  return value;
};
const sessions = {};
async function request(
  path,
  { role = 'Admin', method = 'GET', body, expected, renewal = false } = {},
) {
  const response = await fetch(`${api}${path}`, {
    method,
    headers: {
      'content-type': 'application/json',
      ...(sessions[role]
        ? { authorization: `Bearer ${sessions[role].accessToken}` }
        : {}),
    },
    body: body === undefined ? undefined : JSON.stringify(body),
    signal: AbortSignal.timeout(30000),
  });
  if (response.status === 401 && !renewal && path !== '/auth/login') {
    sessions[role] = await request('/auth/login', {
      role,
      method: 'POST',
      body: accounts[role],
      renewal: true,
    });
    return request(path, { role, method, body, expected, renewal: true });
  }
  if (expected !== undefined)
    assert.equal(
      response.status,
      expected,
      `${method} ${path}: unexpected status`,
    );
  else assert.ok(response.ok, `${method} ${path}: HTTP ${response.status}`);
  if (!response.ok || response.status === 204) return undefined;
  return response.json();
}
const post = (path, body, role = 'Admin') =>
  request(path, { method: 'POST', body, role });
const removedRoles = new Set();
async function setRole(role, enabled) {
  const id = uuid(sessions[role].user.id);
  const code = role === 'OtherCourier' ? 'DeliveryStaff' : role;
  assert.ok(roles.includes(code));
  if (!enabled) removedRoles.add(role); // Restore even after an uncertain command result.
  await sql(
    enabled
      ? `INSERT INTO identity.user_roles (user_id,role_id,assigned_at) SELECT '${id}',id,now() FROM identity.roles WHERE code='${code}' ON CONFLICT DO NOTHING;`
      : `DELETE FROM identity.user_roles WHERE user_id='${id}' AND role_id IN (SELECT id FROM identity.roles WHERE code='${code}');`,
  );
  if (enabled) removedRoles.delete(role);
}
async function snapshot(order) {
  const state = await request(`/orders/${order.id}`);
  const history = JSON.parse(
    await sql(
      `SELECT coalesce(json_agg(t ORDER BY t.id),'[]') FROM (SELECT id,from_status,to_status,changed_by FROM sales.order_status_history WHERE order_id='${uuid(order.id)}') t;`,
    ),
  );
  const value = {
    status: state.status,
    total: state.total,
    discounts: state.discounts,
    history,
  };
  if (order.delivery) {
    value.delivery = JSON.parse(
      await sql(
        `SELECT row_to_json(t) FROM (SELECT status,assigned_staff_id,failure_reason FROM sales.deliveries WHERE order_id='${uuid(order.id)}') t;`,
      ),
    );
    value.deliveryHistory = JSON.parse(
      await sql(
        `SELECT coalesce(json_agg(t ORDER BY t.id),'[]') FROM (SELECT h.id,h.from_status,h.to_status,h.actor_id FROM sales.delivery_status_history h JOIN sales.deliveries d ON d.id=h.delivery_id WHERE d.order_id='${uuid(order.id)}') t;`,
      ),
    );
  }
  return value;
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
let current;
let stepNumber;
let currentRole;
async function native(flow, variables = {}) {
  const name = `${current.name}-${++stepNumber}-${flow}`;
  const xml = join(results, `${name}.xml`);
  await command(
    java,
    [
      ...maestro,
      ...(adbArgs.length ? ['--device', process.env.ANDROID_SERIAL] : []),
      'test',
      '--format',
      'junit',
      '--output',
      xml,
      '--debug-output',
      join(results, 'private', name),
      join(root, '.maestro/workflows-focused', `${flow}.yaml`),
    ],
    {
      env: {
        ...process.env,
        ...Object.fromEntries(
          Object.entries(variables).map(([key, value]) => [
            `MAESTRO_${key}`,
            value,
          ]),
        ),
        MAESTRO_STAFF_USERNAME: accounts[currentRole].username,
        MAESTRO_STAFF_PASSWORD: accounts[currentRole].password,
        MAESTRO_CLI_NO_ANALYTICS: '1',
        MAESTRO_CLI_ANALYSIS_NOTIFICATION_DISABLED: 'true',
      },
      log: join(results, 'private', `${name}.log`),
    },
  );
  const report = await readFile(xml, 'utf8');
  assert.equal(
    (report.match(/<testcase\b/g) || []).length,
    1,
    'Expected one fresh native case per step.',
  );
  assert.ok(
    !/<(?:failure|error|skipped)\b/.test(report),
    'Failed/skipped native step.',
  );
  current.steps.push({ name, report: `${name}.xml`, passed: true });
}
async function check(name, role, run) {
  current = summary.cases.find((value) => value.name === name);
  stepNumber = 0;
  current.status = 'running';
  await save();
  console.log(`Running ${name}...`);
  try {
    if (currentRole !== role) {
      currentRole = role;
      await native('login');
    }
    current.state = await run();
    await screenshot(name);
    current.passed = true;
    current.status = 'passed';
    summary.passed++;
    summary.failedOrMissing--;
    console.log(`PASS ${name}`);
  } catch (error) {
    current.status = 'failed';
    current.error = error.message;
    await screenshot(`${name}-failed`).catch(() => {});
    throw error;
  } finally {
    await save();
  }
}
const open = (kind, order, expected) =>
  native('open', {
    KIND: kind,
    RECORD_ID: kind === 'approval' ? order.discountId : order.id,
    EXPECTED: expected,
  });
let proxy;
let originalNetwork;
async function restoreNetwork() {
  if (!originalNetwork) return;
  await android(
    'shell',
    'cmd',
    'connectivity',
    'airplane-mode',
    originalNetwork.airplane === '1' ? 'enable' : 'disable',
  );
  await android(
    'shell',
    'svc',
    'wifi',
    originalNetwork.wifi === '1' ? 'enable' : 'disable',
  );
  await android(
    'shell',
    'svc',
    'data',
    originalNetwork.data === '1' ? 'enable' : 'disable',
  );
}
async function offline(order, kind, action, expected) {
  await open(kind, order, action);
  const before = await snapshot(order);
  const exchangeCount = proxy.exchanges.filter(
    (entry) => entry.method === 'POST',
  ).length;
  try {
    await android('shell', 'cmd', 'connectivity', 'airplane-mode', 'enable');
    await android('shell', 'svc', 'wifi', 'disable');
    await android('shell', 'svc', 'data', 'disable');
    await native('offline', { ACTION: action });
    await screenshot(`${current.name}-disabled`);
  } finally {
    await restoreNetwork();
  }
  await native('reconnected', {
    EXPECTED: expected,
    SUCCESS_LABEL: '.*Confirmed by the service.*|.*Approved by the server.*',
  });
  assert.deepEqual(await snapshot(order), before);
  assert.equal(
    proxy.exchanges.filter((entry) => entry.method === 'POST').length,
    exchangeCount,
    'Offline action was replayed.',
  );
  return { before, after: await snapshot(order), businessWrites: 0 };
}
try {
  assert.equal(await android('get-state'), 'device');
  originalNetwork = {
    airplane: await android(
      'shell',
      'settings',
      'get',
      'global',
      'airplane_mode_on',
    ),
    wifi: await android('shell', 'settings', 'get', 'global', 'wifi_on'),
    data: await android('shell', 'settings', 'get', 'global', 'mobile_data'),
  };
  assert.equal(originalNetwork.airplane, '0', 'Start with Android online.');
  summary.device = {};
  for (const [key, prop] of Object.entries({
    model: 'ro.product.model',
    android: 'ro.build.version.release',
    apiLevel: 'ro.build.version.sdk',
    abi: 'ro.product.cpu.abi',
  }))
    summary.device[key] = await android('shell', 'getprop', prop);
  summary.appCommit = required('COFFEESHOP_WORKFLOW_APP_COMMIT');
  summary.apiCommit = required('COFFEESHOP_WORKFLOW_API_COMMIT');
  summary.harnessCommit = await command('git', ['rev-parse', 'HEAD']);
  summary.environment =
    'Separate disposable issue201 PostgreSQL database; shared ASP.NET Core API; synthetic staff; real API forwarding on port ' +
    device.port;
  const apk = (
    await android('shell', 'pm', 'path', 'com.coffeeshop.staffmobile')
  )
    .split('\n')[0]
    .replace(/^package:/, '')
    .trim();
  assert.ok(apk.startsWith('/data/app/') && apk.endsWith('.apk'));
  summary.apkSha256 = (await android('shell', 'sha256sum', apk)).split(
    /\s+/,
  )[0];
  summary.device.build = (
    await android('shell', 'dumpsys', 'package', 'com.coffeeshop.staffmobile')
  )
    .split('\n')
    .filter((line) => /versionCode=|versionName=/.test(line))
    .map((line) => line.trim());
  assert.ok(summary.device.build.length);
  assert.equal((await fetch(`${api.origin}/health/ready`)).status, 200);
  for (const role of roles)
    sessions[role] = await request('/auth/login', {
      role,
      method: 'POST',
      body: accounts[role],
    });
  for (const role of roles)
    assert.deepEqual(sessions[role].user.roles, [
      role === 'OtherCourier' ? 'DeliveryStaff' : role,
    ]);
  const category = await post('/categories', {
    name: `I201 ${runId}`,
    displayOrder: 0,
  });
  const item = await post('/menu-items', {
    categoryId: category.id,
    name: `I201 latte ${runId}`,
    initialVariants: [{ sizeName: 'Regular', price: 10 }],
  });
  async function order(
    label,
    { discount = false, delivery = false, other = false } = {},
  ) {
    const id = randomUUID();
    const record = await post('/orders', {
      orderId: id,
      channel: delivery ? 'delivery' : 'takeaway',
      ...(delivery
        ? {
            customer: {
              recipientName: `Synthetic ${label}`,
              contactNumber: '0123456789',
              addressLine: `Synthetic I201 ${runId} address`,
            },
          }
        : {}),
      lines: [
        {
          menuItemId: item.id,
          sizeVariantId: item.sizeVariants[0].id,
          quantity: 1,
          addonIds: [],
        },
      ],
    });
    const result = { id, orderNumber: record.orderNumber, delivery, label };
    if (discount) {
      result.discountId = randomUUID();
      await post(`/orders/${id}/discounts`, {
        discountId: result.discountId,
        type: 'fixed',
        value: 1,
        reason: `I201 ${runId} ${label}`,
      });
    } else {
      await post(`/orders/${id}/confirm`, { reviewedTotal: 10 });
      if (delivery) {
        await post(`/orders/${id}/preparing`);
        await post(`/orders/${id}/ready`);
        await post(`/orders/${id}/assign-delivery`, {
          staffId: sessions[other ? 'OtherCourier' : 'DeliveryStaff'].user.id,
        });
      }
    }
    return result;
  }
  const seed = {};
  for (const name of [
    'progression',
    'conflict',
    'stale',
    'forbidden',
    'offline',
  ])
    seed[`barista-${name}`] = await order(`barista-${name}`);
  for (const name of [
    'approve',
    'reject',
    'conflict',
    'stale',
    'forbidden',
    'offline',
  ])
    seed[`discount-${name}`] = await order(`discount-${name}`, {
      discount: true,
    });
  for (const name of [
    'delivered',
    'failed',
    'conflict',
    'stale',
    'forbidden',
    'offline',
    'other',
  ])
    seed[`courier-${name}`] = await order(`courier-${name}`, {
      delivery: true,
      other: name === 'other',
    });
  summary.seed = seed;
  summary.actors = Object.fromEntries(
    roles.map((role) => [role, sessions[role].user.id]),
  );
  await save();
  proxy = createWorkflowForwarder(api.origin);
  await new Promise((accept, reject) => {
    proxy.server.once('error', reject);
    proxy.server.listen(Number(device.port), host, accept);
  });
  await android('reverse', 'tcp:8081', 'tcp:8081');
  const fault = (method, path, before, order) =>
    proxy.arm({ method, path, before, observe: () => snapshot(order) });
  const baristaStart = (expected) =>
    native('barista-start', { EXPECTED: expected });
  await check('barista-progression', 'Barista', async () => {
    const o = seed['barista-progression'];
    await open('urgent-order', o, 'Start preparing');
    await baristaStart('Preparation started. Confirmed by the service.');
    assert.equal((await snapshot(o)).status, 'preparing');
    await native('barista-ready');
    const state = await snapshot(o);
    assert.equal(state.status, 'ready');
    assert.deepEqual(
      state.history
        .filter((h) => ['preparing', 'ready'].includes(h.to_status))
        .map((h) => h.changed_by),
      [sessions.Barista.user.id, sessions.Barista.user.id],
    );
    return state;
  });
  await check('barista-conflict', 'Barista', async () => {
    const o = seed['barista-conflict'];
    await open('urgent-order', o, 'Start preparing');
    const f = fault(
      'POST',
      `/api/v1/preparation-tickets/${o.id}/start`,
      () => post(`/orders/${o.id}/preparing`),
      o,
    );
    await baristaStart('Ticket changed elsewhere.*');
    return proxy.verify(f, 409);
  });
  await check('barista-stale', 'Barista', async () => {
    const o = seed['barista-stale'];
    const f = fault(
      'GET',
      `/api/v1/preparation-tickets/${o.id}`,
      async () => {
        await post(`/orders/${o.id}/preparing`);
        await post(`/orders/${o.id}/ready`);
      },
      o,
    );
    await native('unavailable', {
      KIND: 'urgent-order',
      RECORD_ID: o.id,
      EXPECTED: 'Ticket is no longer in the preparation queue.*',
      FORBIDDEN_ACTION: 'Start preparing',
      PRIVATE_LABEL: `Ticket ${o.orderNumber}`,
    });
    return proxy.verify(f, 404);
  });
  await check('barista-forbidden', 'Barista', async () => {
    const o = seed['barista-forbidden'];
    await open('urgent-order', o, 'Start preparing');
    const f = fault(
      'POST',
      `/api/v1/preparation-tickets/${o.id}/start`,
      () => setRole('Barista', false),
      o,
    );
    try {
      await baristaStart('Access unavailable.*');
      return await proxy.verify(f, 403);
    } finally {
      await setRole('Barista', true);
    }
  });
  await check('barista-offline', 'Barista', () =>
    offline(
      seed['barista-offline'],
      'urgent-order',
      'Start preparing',
      'Status: Confirmed',
    ),
  );
  for (const action of ['approve', 'reject'])
    await check(`discount-${action}`, 'Manager', async () => {
      const o = seed[`discount-${action}`];
      await open('approval', o, 'Approve discount');
      await native('discount-decision', {
        ACTION: action === 'approve' ? 'Approve discount' : 'Reject discount',
        EXPECTED: `${action === 'approve' ? 'Approved' : 'Rejected'} by the server.*`,
      });
      const state = await snapshot(o);
      assert.equal(state.total, action === 'approve' ? 9 : 10);
      assert.equal(
        state.discounts.find((d) => d.id === o.discountId).status,
        action === 'approve' ? 'approved' : 'rejected',
      );
      return state;
    });
  await check('discount-conflict', 'Manager', async () => {
    const o = seed['discount-conflict'];
    await open('approval', o, 'Approve discount');
    const f = fault(
      'POST',
      `/api/v1/discounts/${o.discountId}/approve`,
      () => post(`/discounts/${o.discountId}/reject`),
      o,
    );
    await native('discount-decision', {
      ACTION: 'Approve discount',
      EXPECTED: 'This discount changed before your decision.*',
    });
    return proxy.verify(f, 409);
  });
  await check('discount-stale', 'Manager', async () => {
    const o = seed['discount-stale'];
    await post(`/discounts/${o.discountId}/approve`);
    await native('unavailable', {
      KIND: 'approval',
      RECORD_ID: o.discountId,
      EXPECTED: 'This discount is no longer pending.*',
      FORBIDDEN_ACTION: 'Approve discount',
      PRIVATE_LABEL: `Order ${o.orderNumber}`,
    });
    const state = await snapshot(o);
    assert.equal(state.total, 9);
    return state;
  });
  await check('discount-forbidden', 'Manager', async () => {
    const o = seed['discount-forbidden'];
    await open('approval', o, 'Approve discount');
    const f = fault(
      'POST',
      `/api/v1/discounts/${o.discountId}/approve`,
      () => setRole('Manager', false),
      o,
    );
    try {
      await native('discount-denied', {
        PRIVATE_LABEL: `Order ${o.orderNumber}`,
      });
      return await proxy.verify(f, 403);
    } finally {
      await setRole('Manager', true);
    }
  });
  await check('discount-offline', 'Manager', () =>
    offline(
      seed['discount-offline'],
      'approval',
      'Approve discount',
      'Approve discount',
    ),
  );
  const privateLabel = `Address: Synthetic I201 ${runId} address`;
  await check('courier-own-list', 'DeliveryStaff', async () => {
    const list = await request('/courier/assignments?pageSize=100', {
      role: 'DeliveryStaff',
    });
    const index = list.items.findIndex(
      (o) => o.orderId === seed['courier-delivered'].id,
    );
    assert.ok(index >= 0);
    await native('delivery-list', {
      PAGE_SKIP: String(Math.floor(index / 10)),
      OWN_ORDER: seed['courier-delivered'].orderNumber,
      OTHER_ORDER: seed['courier-other'].orderNumber,
      PRIVATE_LABEL: privateLabel,
    });
    assert.ok(
      list.items.some((o) => o.orderId === seed['courier-delivered'].id),
    );
    assert.ok(!list.items.some((o) => o.orderId === seed['courier-other'].id));
    return list;
  });
  await check('courier-other-detail', 'DeliveryStaff', async () => {
    const o = seed['courier-other'];
    const before = await snapshot(o);
    await native('unavailable', {
      KIND: 'assignment',
      RECORD_ID: o.id,
      EXPECTED: 'Delivery is unavailable or no longer assigned to you.',
      FORBIDDEN_ACTION: 'Start delivery',
      PRIVATE_LABEL: privateLabel,
    });
    await request(`/courier/assignments/${o.id}/start`, {
      role: 'DeliveryStaff',
      method: 'POST',
      expected: 404,
    });
    assert.deepEqual(await snapshot(o), before);
    return { before, after: await snapshot(o), status: 404 };
  });
  const start = () =>
    native('delivery-start', {
      EXPECTED: 'Delivery is out for delivery. Confirmed by the service.',
    });
  for (const outcome of ['delivered', 'failed'])
    await check(`courier-${outcome}`, 'DeliveryStaff', async () => {
      const o = seed[`courier-${outcome}`];
      await open('assignment', o, 'Start delivery');
      await start();
      await native(
        outcome === 'delivered' ? 'delivery-complete' : 'delivery-fail',
        { PRIVATE_LABEL: privateLabel },
      );
      const state = await snapshot(o);
      assert.equal(state.delivery.status, outcome);
      assert.equal(
        state.delivery.assigned_staff_id,
        sessions.DeliveryStaff.user.id,
      );
      assert.equal(
        state.delivery.failure_reason,
        outcome === 'failed' ? 'Customer unavailable' : null,
      );
      assert.equal(
        state.deliveryHistory.filter(
          (h) =>
            h.to_status === outcome &&
            h.actor_id === sessions.DeliveryStaff.user.id,
        ).length,
        1,
      );
      await request(`/courier/assignments/${o.id}`, {
        role: 'DeliveryStaff',
        expected: 404,
      });
      return state;
    });
  await check('courier-conflict', 'DeliveryStaff', async () => {
    const o = seed['courier-conflict'];
    await open('assignment', o, 'Start delivery');
    const f = fault(
      'POST',
      `/api/v1/courier/assignments/${o.id}/start`,
      () =>
        post(`/courier/assignments/${o.id}/start`, undefined, 'DeliveryStaff'),
      o,
    );
    await native('delivery-start', {
      EXPECTED: 'Delivery changed elsewhere.*',
    });
    return proxy.verify(f, 409);
  });
  await check('courier-stale', 'DeliveryStaff', async () => {
    const o = seed['courier-stale'];
    const f = fault(
      'GET',
      `/api/v1/courier/assignments/${o.id}`,
      async () => {
        await post(
          `/courier/assignments/${o.id}/start`,
          undefined,
          'DeliveryStaff',
        );
        await post(
          `/courier/assignments/${o.id}/complete`,
          undefined,
          'DeliveryStaff',
        );
      },
      o,
    );
    await native('unavailable', {
      KIND: 'assignment',
      RECORD_ID: o.id,
      EXPECTED: 'Delivery is unavailable or no longer assigned to you.',
      FORBIDDEN_ACTION: 'Start delivery',
      PRIVATE_LABEL: privateLabel,
    });
    return proxy.verify(f, 404);
  });
  await check('courier-forbidden', 'DeliveryStaff', async () => {
    const o = seed['courier-forbidden'];
    await open('assignment', o, 'Start delivery');
    const f = fault(
      'POST',
      `/api/v1/courier/assignments/${o.id}/start`,
      () => setRole('DeliveryStaff', false),
      o,
    );
    try {
      await native('delivery-start', {
        EXPECTED: 'Delivery is unavailable or no longer assigned to you.',
      });
      return await proxy.verify(f, 403);
    } finally {
      await setRole('DeliveryStaff', true);
    }
  });
  await check('courier-offline', 'DeliveryStaff', () =>
    offline(
      seed['courier-offline'],
      'assignment',
      'Start delivery',
      'Status: assigned',
    ),
  );
  summary.serverVerificationPassed = true;
} finally {
  summary.cleanupErrors = [];
  for (const role of removedRoles)
    await setRole(role, true).catch(() =>
      summary.cleanupErrors.push(
        `Restore synthetic ${role} role before reusing this database.`,
      ),
    );
  await restoreNetwork().catch(() =>
    summary.cleanupErrors.push('Restore original Android radio settings.'),
  );
  summary.exchanges = proxy?.exchanges || [];
  await proxy?.close();
  if (summary.cleanupErrors.length) summary.serverVerificationPassed = false;
  await save();
}
assert.equal(summary.passed, summary.expected);
assert.ok(summary.serverVerificationPassed);
console.log(
  `PASS ${summary.passed}/${summary.expected} focused Android cases with independent server verification.`,
);
