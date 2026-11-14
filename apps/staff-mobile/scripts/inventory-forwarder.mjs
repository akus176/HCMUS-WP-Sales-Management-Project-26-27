import assert from 'node:assert/strict';
import { Buffer } from 'node:buffer';
import { createHash } from 'node:crypto';
import { createServer, request as httpRequest } from 'node:http';

// Test harness only: every business response comes from the real upstream API.
// No control routes, credentials, request bodies, or token query strings are logged.
export function createInventoryForwarder(origin) {
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
        assert.ok(size <= 1024 * 1024, 'Request exceeded test proxy limit.');
        chunks.push(chunk);
      }
      const body = Buffer.concat(chunks);
      const headers = { ...req.headers };
      for (const key of ['host', 'connection', 'content-length'])
        delete headers[key];
      const send = async (payload = body) => {
        const response = await fetch(new URL(req.url, upstream), {
          method: req.method,
          headers,
          body: ['GET', 'HEAD'].includes(req.method) ? undefined : payload,
          redirect: 'manual',
          signal: AbortSignal.timeout(30000),
        });
        return { response, bytes: Buffer.from(await response.arrayBuffer()) };
      };
      const fault =
        pending?.method === req.method && pending.path === path
          ? pending
          : undefined;
      // Android may replay an empty POST after socket loss. Keep a receipt loss
      // window armed until the native unconfirmed-result message is observed.
      if (fault && fault.mode !== 'drop-window') pending = undefined;
      if (path.endsWith('/adjustments')) {
        entry.requestId = JSON.parse(body).requestId;
        entry.bodyHash = createHash('sha256').update(body).digest('hex');
      }
      if (fault?.mode === 'compete') {
        const competingBody = path.endsWith('/adjustments')
          ? Buffer.from(
              JSON.stringify({
                ...JSON.parse(body),
                quantityDelta: 1,
                reason: 'Competing fixture',
              }),
            )
          : body;
        const first = await send(competingBody);
        assert.ok(first.response.ok, 'Competing fixture request failed.');
        entry.competingStatus = first.response.status;
      }
      await fault?.before?.();
      if (fault && fault.beforeRejectedOrLost === undefined)
        fault.beforeRejectedOrLost = await fault.observe();
      const { response, bytes } = await send();
      entry.status = response.status;
      if (fault) {
        fault.used = true;
        fault.status ??= response.status;
        if (fault.mode === 'drop' || fault.mode === 'drop-window') {
          assert.ok(
            response.ok ||
              (fault.mode === 'drop-window' &&
                fault.afterCommit &&
                response.status === 409),
            'Loss requires a committed success or a replay conflict after that commit.',
          );
          // The upstream commit and response have finished. Do not abort its request.
          const committed = await fault.observe();
          if (fault.afterCommit) assert.deepEqual(committed, fault.afterCommit);
          else fault.afterCommit = committed;
          entry.responseDroppedAfterCommit = true;
          exchanges.push(entry);
          res.destroy();
          return;
        }
      }
      if (
        req.method === 'POST' &&
        (path.endsWith('/adjustments') || path.endsWith('/post'))
      )
        exchanges.push(entry);
      for (const [key, value] of response.headers) {
        if (
          ![
            'content-encoding',
            'content-length',
            'transfer-encoding',
            'connection',
          ].includes(key)
        )
          res.setHeader(key, value);
      }
      res.writeHead(response.status);
      res.end(bytes);
    } catch {
      entry.proxyError = true;
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
        'Previous test fault was not exercised.',
      );
      assert.ok(
        ['drop', 'drop-window', 'compete', 'before'].includes(fault.mode),
      );
      assert.equal(fault.method, 'POST');
      assert.match(
        fault.path,
        /^\/api\/v1\/(ingredients\/[\da-f-]+\/adjustments|purchase-receipts\/[\da-f-]+\/post)$/,
      );
      pending = fault;
      return fault;
    },
    disarm(fault) {
      assert.equal(
        pending,
        fault,
        'Only the active loss window may be closed.',
      );
      assert.ok(fault.used, 'The receipt loss window was not exercised.');
      pending = undefined;
    },
    async close() {
      for (const socket of sockets) socket.destroy();
      if (server.listening)
        await new Promise((resolve) => server.close(resolve));
    },
  };
}
