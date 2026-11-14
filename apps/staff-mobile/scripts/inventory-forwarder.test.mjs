import assert from 'node:assert/strict';
import { Buffer } from 'node:buffer';
import { createServer } from 'node:http';
import { test } from 'node:test';
import { createInventoryForwarder } from './inventory-forwarder.mjs';

const path =
  '/api/v1/ingredients/00000000-0000-4000-8000-000000000198/adjustments';
const body = {
  requestId: '00000000-0000-4000-8000-000000000199',
  quantityDelta: 2,
  reason: 'Count',
};

async function setup(t) {
  let balance = 1;
  const committed = new Map();
  const upstream = createServer(async (req, res) => {
    const chunks = [];
    for await (const chunk of req) chunks.push(chunk);
    const payload = JSON.parse(Buffer.concat(chunks));
    const previous = committed.get(payload.requestId);
    if (previous && JSON.stringify(previous) !== JSON.stringify(payload)) {
      res.writeHead(409);
      res.end();
      return;
    }
    if (!previous) {
      balance += payload.quantityDelta;
      committed.set(payload.requestId, payload);
    }
    res.setHeader('content-type', 'application/json');
    res.end(JSON.stringify({ availableQuantity: balance }));
  });
  await new Promise((resolve) => upstream.listen(0, '127.0.0.1', resolve));
  const proxy = createInventoryForwarder(
    `http://127.0.0.1:${upstream.address().port}`,
  );
  await new Promise((resolve) => proxy.server.listen(0, '127.0.0.1', resolve));
  t.after(async () => {
    await proxy.close();
    await new Promise((resolve) => upstream.close(resolve));
  });
  const send = () =>
    fetch(`http://127.0.0.1:${proxy.server.address().port}${path}`, {
      method: 'POST',
      body: JSON.stringify(body),
      headers: {
        'content-type': 'application/json',
        authorization: 'Bearer synthetic-secret',
      },
    });
  return {
    proxy,
    send,
    snapshot: () => ({ balance, movements: committed.size }),
  };
}

test('drops only the completed upstream response; deliberate retry commits once', async (t) => {
  const { proxy, send, snapshot } = await setup(t);
  const fault = proxy.arm({
    method: 'POST',
    path,
    mode: 'drop',
    observe: snapshot,
  });
  await assert.rejects(send());
  assert.deepEqual(fault.afterCommit, { balance: 3, movements: 1 });
  const retry = await send();
  assert.equal(retry.status, 200);
  assert.deepEqual(await retry.json(), { availableQuantity: 3 });
  assert.equal(proxy.exchanges.length, 2);
  assert.equal(proxy.exchanges[0].bodyHash, proxy.exchanges[1].bodyHash);
  assert.ok(!JSON.stringify(proxy.exchanges).includes('synthetic-secret'));
});

test('conflict comes from a committed competing request, not a fabricated response', async (t) => {
  const { proxy, send, snapshot } = await setup(t);
  const fault = proxy.arm({
    method: 'POST',
    path,
    mode: 'compete',
    observe: snapshot,
  });
  assert.equal((await send()).status, 409);
  assert.deepEqual(fault.beforeRejectedOrLost, { balance: 2, movements: 1 });
  assert.deepEqual(snapshot(), fault.beforeRejectedOrLost);
  assert.equal(proxy.exchanges[0].competingStatus, 200);
});

test('an unexercised fault cannot silently be replaced by a later case', async (t) => {
  const { proxy, snapshot } = await setup(t);
  const fault = { method: 'POST', path, mode: 'drop', observe: snapshot };
  proxy.arm(fault);
  assert.throws(() => proxy.arm(fault), /not exercised/);
});

test('receipt response-loss window covers transport replay before deliberate recovery', async (t) => {
  let posts = 0;
  const upstream = createServer(async (req, res) => {
    for await (const chunk of req) void chunk;
    posts++;
    res.writeHead(posts === 1 ? 204 : 409);
    res.end();
  });
  await new Promise((resolve) => upstream.listen(0, '127.0.0.1', resolve));
  const proxy = createInventoryForwarder(
    `http://127.0.0.1:${upstream.address().port}`,
  );
  await new Promise((resolve) => proxy.server.listen(0, '127.0.0.1', resolve));
  t.after(async () => {
    await proxy.close();
    await new Promise((resolve) => upstream.close(resolve));
  });
  const receiptPath =
    '/api/v1/purchase-receipts/00000000-0000-4000-8000-000000000198/post';
  const send = () =>
    fetch(`http://127.0.0.1:${proxy.server.address().port}${receiptPath}`, {
      method: 'POST',
    });
  const fault = proxy.arm({
    mode: 'drop-window',
    method: 'POST',
    path: receiptPath,
    observe: () => ({ movements: posts ? 1 : 0 }),
  });
  await assert.rejects(send());
  await assert.rejects(send());
  assert.deepEqual(fault.afterCommit, { movements: 1 });
  assert.equal(fault.status, 204);
  proxy.disarm(fault);
  assert.equal((await send()).status, 409);
  assert.deepEqual(
    proxy.exchanges.map((entry) => entry.status),
    [204, 409, 409],
  );
  assert.ok(
    proxy.exchanges
      .slice(0, 2)
      .every((entry) => entry.responseDroppedAfterCommit),
  );
  assert.equal(proxy.exchanges[2].responseDroppedAfterCommit, undefined);
});
