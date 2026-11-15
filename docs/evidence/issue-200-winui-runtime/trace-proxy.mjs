import http from 'node:http';
import fs from 'node:fs';

// Run only against the disposable issue-200 API. No bodies, headers or query
// strings are retained. Only the native WinUI client connects to port 52300.
const trace = process.argv[2];
if (!trace) throw new Error('Supply the output JSONL path.');
const server = http.createServer((req, res) => {
  const path = new URL(req.url, 'http://127.0.0.1').pathname;
  const upstream = http.request({
    host: '127.0.0.1', port: 52302, method: req.method, path: req.url,
    headers: { ...req.headers, host: '127.0.0.1:52302' },
  }, reply => {
    fs.appendFileSync(trace, JSON.stringify({
      at: new Date().toISOString(), method: req.method, path, status: reply.statusCode,
    }) + '\n');
    res.writeHead(reply.statusCode, reply.headers);
    reply.pipe(res);
  });
  upstream.on('error', () => { res.writeHead(502); res.end(); });
  req.pipe(upstream);
});
server.listen(52300, '127.0.0.1', () => process.stdout.write('Loopback trace ready.\n'));
