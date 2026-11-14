import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { test } from 'node:test';
import { createWorkflowForwarder } from './workflow-forwarder.mjs';

const ticket =
  '/api/v1/preparation-tickets/00000000-0000-4000-8000-000000000201';
async function setup(t) {
  let status = 'confirmed';
  let allowed = true;
  const upstream = createServer(async (req, res) => {
    for await (const chunk of req) void chunk;
    if (!allowed) {
      res.writeHead(403);
      res.end();
      return;
    }
    if (req.method === 'GET') {
      res.writeHead(status === 'ready' ? 404 : 200);
      res.end();
      return;
    }
    if (status !== 'confirmed') {
      res.writeHead(409);
      res.end();
      return;
    }
    status = 'preparing';
    res.writeHead(204);
    res.end();
  });
  await new Promise((resolve) => upstream.listen(0, '127.0.0.1', resolve));
  const proxy = createWorkflowForwarder(
    `http://127.0.0.1:${upstream.address().port}`,
  );
  await new Promise((resolve) => proxy.server.listen(0, '127.0.0.1', resolve));
  t.after(async () => {
    await proxy.close();
    await new Promise((resolve) => upstream.close(resolve));
  });
  return {
    proxy,
    observe: () => ({ status }),
    change: (value) => {
      status = value;
    },
    revoke: () => {
      allowed = false;
    },
    send: (method = 'POST') =>
      fetch(
        `http://127.0.0.1:${proxy.server.address().port}${ticket}${method === 'POST' ? '/start' : ''}?private=secret`,
        {
          method,
          headers: { authorization: 'Bearer secret' },
        },
      ),
  };
}
test('a competing commit yields a real conflict and the original adds no effect', async (t) => {
  const h = await setup(t);
  const fault = h.proxy.arm({
    method: 'POST',
    path: `${ticket}/start`,
    before: () => h.change('preparing'),
    observe: h.observe,
  });
  assert.equal((await h.send()).status, 409);
  await h.proxy.verify(fault, 409);
  assert.equal(h.observe().status, 'preparing');
  assert.ok(!JSON.stringify(h.proxy.exchanges).includes('secret'));
});
test('permission removal returns the upstream forbidden response with unchanged state', async (t) => {
  const h = await setup(t);
  const fault = h.proxy.arm({
    method: 'POST',
    path: `${ticket}/start`,
    before: h.revoke,
    observe: h.observe,
  });
  assert.equal((await h.send()).status, 403);
  await h.proxy.verify(fault, 403);
});
test('stale detail returns upstream 404; unexercised/wrong-status faults cannot pass', async (t) => {
  const h = await setup(t);
  const fault = h.proxy.arm({
    method: 'GET',
    path: ticket,
    before: () => h.change('ready'),
    observe: h.observe,
  });
  await assert.rejects(h.proxy.verify(fault, 404), /not exercised/);
  assert.throws(() => h.proxy.arm(fault), /not exercised/);
  assert.equal((await h.send('GET')).status, 404);
  await assert.rejects(h.proxy.verify(fault, 409), /unexpected status/);
  await h.proxy.verify(fault, 404);
});
