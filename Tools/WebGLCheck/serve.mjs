// A local server of a Web build. Usage: node Tools/WebGLCheck/serve.mjs <dir> <port> [gzip|plain|unity]
// gzip  — gzip on the fly like the servers of YouTube Playables, which support neither Brotli nor extra headers;
// plain — no compression at all;
// unity — the *.br, *.gz files of Unity with their Content-Encoding headers.
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { extname, join, normalize } from 'node:path';
import { gzipSync } from 'node:zlib';

const [dir, portArg, mode = 'gzip'] = process.argv.slice(2);
const types = { '.html': 'text/html; charset=utf-8', '.js': 'application/javascript', '.wasm': 'application/wasm', '.json': 'application/json', '.data': 'application/octet-stream', '.css': 'text/css', '.png': 'image/png', '.ico': 'image/x-icon' };

createServer(async (req, res) => {
  const path = normalize(decodeURIComponent(new URL(req.url, 'http://x').pathname)).replace(/^([\\/]\.\.)+/, '');
  const file = join(dir, path.endsWith('/') || path.endsWith('\\') ? 'index.html' : path);
  let body;
  try { body = await readFile(file); } catch { res.writeHead(404); res.end(); return; }
  const headers = { 'Content-Type': types[extname(file)] || 'application/octet-stream', 'Cache-Control': 'no-store' };
  if (mode === 'gzip') {
    body = gzipSync(body, { level: 6 });
    headers['Content-Encoding'] = 'gzip';
  } else if (mode === 'unity') {
    const inner = file.replace(/\.(br|gz)$/, '');
    if (inner !== file) {
      headers['Content-Encoding'] = file.endsWith('.br') ? 'br' : 'gzip';
      headers['Content-Type'] = types[extname(inner)] || 'application/octet-stream';
    }
  }
  headers['Content-Length'] = body.length;
  res.writeHead(200, headers);
  res.end(body);
}).listen(Number(portArg), '127.0.0.1', () => console.log(`serving ${dir} on http://127.0.0.1:${portArg}/ (${mode})`));
