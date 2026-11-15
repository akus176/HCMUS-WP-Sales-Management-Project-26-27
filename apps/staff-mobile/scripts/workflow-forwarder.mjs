import assert from 'node:assert/strict';
import { Buffer } from 'node:buffer';
import { createServer, request as httpRequest } from 'node:http';

const workflowPath =
  /^\/api\/v1\/(preparation-tickets\/[\da-f-]+(?:\/(?:start|ready))?|discounts\/[\da-f-]+\/(?:approve|reject)|courier\/assignments\/[\da-f-]+(?:\/(?:start|complete|fail))?)$/;

// Test-only forwarding: callbacks change disposable fixtures, never fabricate HTTP
// business responses. Tokens, query strings and request bodies are not retained.
export function createWorkflowForwarder(origin) {
  const upstream = new URL(origin);
  assert.equal(upstream.protocol, 'http:');
  assert.ok(['127.0.0.1', 'localhost'].includes(upstream.hostname));
  let pending;
  const exchanges = [];
  const sockets = new Set();
  const server = createServer(async (req, res) => {
    const path = new URL(req.url, 'http://local').pathname;
    const entry = { method: req.method, path };
    try {
      const chunks = [];
      let size = 0;
      for await (const chunk of req) {
        size += chunk.length;
        assert.ok(size <= 1024 * 1024);
        chunks.push(chunk);
      }
      const fault =
        pending?.method === req.method && pending.path === path
          ? pending
          : undefined;
      if (fault) {
        pending = undefined;
        await fault.before();
        fault.beforeOriginal = await fault.observe();
      }
      const headers = { ...req.headers };
      for (const key of ['host', 'connection', 'content-length'])
        delete headers[key];
      const response = await fetch(new URL(req.url, upstream), {
        method: req.method,
        headers,
        body: ['GET', 'HEAD'].includes(req.method)
          ? undefined
          : Buffer.concat(chunks),
        redirect: 'manual',
        signal: AbortSignal.timeout(30000),
      });
      const bytes = Buffer.from(await response.arrayBuffer());
      entry.status = response.status;
      if (workflowPath.test(path)) exchanges.push(entry);
      if (fault) {
        fault.used = true;
        fault.status = response.status;
      }
      for (const [key, value] of response.headers)
        if (
          ![
            'content-encoding',
            'content-length',
            'transfer-encoding',
            'connection',
          ].includes(key)
        )
          res.setHeader(key, value);
      res.writeHead(response.status);
      res.end(bytes);
    } catch {
      entry.forwardingError = true;
      exchanges.push(entry);
      if (!res.destroyed) {
        res.writeHead(502);
        res.end();
      }
    }
  });
  server.on('connection', (socket) => {
    sockets.add(socket);
    socket.on('close', () => sockets.delete(socket));
  });
  server.on('upgrade', (req, client, head) => {
    const outgoing = httpRequest(new URL(req.url, upstream), {
      headers: { ...req.headers, host: upstream.host },
    });
    outgoing.on('upgrade', (response, socket, upstreamHead) => {
      sockets.add(socket);
      socket.on('close', () => sockets.delete(socket));
      client.write(`HTTP/1.1 ${response.statusCode} Switching Protocols\r\n`);
      for (const [key, value] of Object.entries(response.headers))
        client.write(`${key}: ${value}\r\n`);
      client.write('\r\n');
      socket.write(head);
      client.write(upstreamHead);
      socket.pipe(client).pipe(socket);
      client.on('error', () => socket.destroy());
      socket.on('error', () => client.destroy());
      client.on('close', () => socket.destroy());
    });
    outgoing.on('response', () => client.destroy());
    outgoing.on('error', () => client.destroy());
    outgoing.end();
  });
  return {
    server,
    exchanges,
    arm(fault) {
      assert.equal(
        pending,
        undefined,
        'Previous workflow fault was not exercised.',
      );
      assert.ok(['GET', 'POST'].includes(fault.method));
      assert.match(fault.path, workflowPath);
      assert.equal(typeof fault.before, 'function');
      assert.equal(typeof fault.observe, 'function');
      pending = fault;
      return fault;
    },
    async verify(fault, expectedStatus) {
      assert.ok(fault.used, 'Workflow fault was not exercised.');
      assert.equal(
        fault.status,
        expectedStatus,
        'The real API returned an unexpected status.',
      );
      const after = await fault.observe();
      assert.deepEqual(
        after,
        fault.beforeOriginal,
        'The rejected request changed authoritative state.',
      );
      return { status: fault.status, before: fault.beforeOriginal, after };
    },
    async close() {
      for (const socket of sockets) socket.destroy();
      if (server.listening)
        await new Promise((resolve) => server.close(resolve));
    },
  };
}
