// Headless browser check of a Web build (agent self-verification).
// Usage: node Tools/WebGLCheck/cdp_check.mjs <url> <doneMarker> [timeoutSec] [browserPath] [options]
// Options: --viewport=WxH            the size of the window
//          --screenshot=file.png     a picture after the marker
//          --report=file.json        requests, bytes, external hosts, the time to the marker lines, errors
//          --stub=file.js            a script run before the page code (a fake SDK), with --block
//          --block=pattern           URLs the browser must not load
//          --firstframe=text         the console line that marks the first frame
//          --settle=seconds          keep watching after the marker
// Opens the page in headless Chrome/Edge (software WebGL), streams the console, exits when a console line
// contains <doneMarker>. Prints every error/exception and the lines of the game with a [Prefix].
import { spawn } from 'node:child_process';
import { mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const positional = process.argv.slice(2).filter((a) => !a.startsWith('--'));
const option = (name) => process.argv.slice(2).find((a) => a.startsWith(`--${name}=`))?.split('=').slice(1).join('=');
const [url, marker, timeoutArg, browserArg] = positional;
const timeoutSec = Number(timeoutArg || 600);
const browser = browserArg || 'C:/Program Files/Google/Chrome/Application/chrome.exe';
const [viewW, viewH] = (option('viewport') || '1280x720').split('x').map(Number);
const firstFrameText = option('firstframe') || '[BootFlow] first frame';
const settleSec = Number(option('settle') || 0);
const port = 9300 + Math.floor(Math.random() * 500);
const profile = mkdtempSync(join(tmpdir(), 'webglcheck-'));

const proc = spawn(browser, [
  '--headless=new', `--remote-debugging-port=${port}`, `--user-data-dir=${profile}`,
  '--enable-unsafe-swiftshader', '--use-angle=swiftshader', '--ignore-gpu-blocklist',
  '--autoplay-policy=no-user-gesture-required', `--window-size=${viewW},${viewH}`, '--no-first-run', 'about:blank',
], { stdio: 'ignore' });

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
let ws;
for (let i = 0; i < 50; i++) {
  try {
    const list = await (await fetch(`http://127.0.0.1:${port}/json/list`)).json();
    const page = list.find((t) => t.type === 'page');
    if (page) { ws = new WebSocket(page.webSocketDebuggerUrl); break; }
  } catch { /* browser starting */ }
  await sleep(200);
}
if (!ws) { console.log('RESULT: browser did not start'); proc.kill(); process.exit(2); }

let id = 0;
const replies = new Map();
const send = (method, params = {}) => new Promise((resolve) => {
  const callId = ++id;
  replies.set(callId, resolve);
  ws.send(JSON.stringify({ id: callId, method, params }));
});
const errors = [];
const warnings = [];
const requests = new Map();
let done = false;
let lines = 0;
let t0 = Date.now();
let firstFrameMs = null;
let markerMs = null;

ws.onmessage = (ev) => {
  const msg = JSON.parse(ev.data);
  if (msg.id && replies.has(msg.id)) { replies.get(msg.id)(msg.result); replies.delete(msg.id); return; }
  if (msg.method === 'Network.requestWillBeSent') {
    requests.set(msg.params.requestId, { url: msg.params.request.url, bytes: 0, status: null, failed: null });
    return;
  }
  if (msg.method === 'Network.responseReceived') {
    const r = requests.get(msg.params.requestId);
    if (r) { r.status = msg.params.response.status; r.encoding = msg.params.response.headers['Content-Encoding'] || msg.params.response.headers['content-encoding'] || ''; }
    return;
  }
  if (msg.method === 'Network.loadingFinished') {
    const r = requests.get(msg.params.requestId);
    if (r) r.bytes = msg.params.encodedDataLength;
    return;
  }
  if (msg.method === 'Network.loadingFailed') {
    const r = requests.get(msg.params.requestId);
    if (r) r.failed = msg.params.errorText;
    return;
  }
  let text = null; let level = 'log';
  if (msg.method === 'Runtime.consoleAPICalled') {
    level = msg.params.type;
    text = msg.params.args.map((a) => a.value ?? a.description ?? '').join(' ');
  } else if (msg.method === 'Runtime.exceptionThrown') {
    level = 'error';
    text = 'EXCEPTION ' + (msg.params.exceptionDetails.exception?.description || msg.params.exceptionDetails.text);
  } else if (msg.method === 'Log.entryAdded') {
    level = msg.params.entry.level;
    text = msg.params.entry.text;
  }
  if (text == null) return;
  lines++;
  if (level === 'error' || /Exception|error CS/.test(text)) { errors.push(text); console.log('ERROR: ' + text.slice(0, 400)); }
  else if (level === 'warning') { warnings.push(text); console.log('WARN: ' + text.slice(0, 300)); }
  else if (/\[[A-Za-z]+\]/.test(text)) console.log('LOG: ' + text.slice(0, 2000));
  if (firstFrameMs == null && text.includes(firstFrameText)) firstFrameMs = Date.now() - t0;
  if (marker && text.includes(marker)) { if (markerMs == null) markerMs = Date.now() - t0; done = true; }
};

await new Promise((r) => { ws.onopen = r; if (ws.readyState === 1) r(); });
await send('Runtime.enable');
await send('Log.enable');
await send('Page.enable');
await send('Network.enable');
await send('Network.setCacheDisabled', { cacheDisabled: true });
await send('Emulation.setDeviceMetricsOverride', { width: viewW, height: viewH, deviceScaleFactor: 1, mobile: false });
if (option('block')) await send('Network.setBlockedURLs', { urls: [option('block')] });
if (option('stub')) await send('Page.addScriptToEvaluateOnNewDocument', { source: readFileSync(option('stub'), 'utf8') });
t0 = Date.now();
send('Page.navigate', { url });

while (!done && (Date.now() - t0) / 1000 < timeoutSec) await sleep(100);
if (done && settleSec > 0) await sleep(settleSec * 1000);

const scroll = await send('Runtime.evaluate', {
  expression: 'JSON.stringify({scrollW: document.documentElement.scrollWidth, scrollH: document.documentElement.scrollHeight, innerW: innerWidth, innerH: innerHeight, canvasW: document.querySelector("canvas")?.clientWidth, canvasH: document.querySelector("canvas")?.clientHeight})',
});
const page = JSON.parse(scroll?.result?.value || '{}');

if (option('screenshot')) {
  const shot = await send('Page.captureScreenshot', { format: 'png' });
  if (shot?.data) writeFileSync(option('screenshot'), Buffer.from(shot.data, 'base64'));
}

const origin = new URL(url).origin;
const all = [...requests.values()];
const external = all.filter((r) => /^https?:/.test(r.url) && !r.url.startsWith(origin));
const report = {
  url,
  viewport: `${viewW}x${viewH}`,
  markerSeen: done,
  firstFrameMs,
  markerMs,
  consoleLines: lines,
  errors,
  warnings,
  page,
  bytesTransferred: all.reduce((sum, r) => sum + (r.bytes || 0), 0),
  requests: all.map((r) => ({ url: r.url.replace(origin, ''), bytes: r.bytes, status: r.status, encoding: r.encoding, failed: r.failed })),
  externalRequests: external.map((r) => r.url),
};
if (option('report')) writeFileSync(option('report'), JSON.stringify(report, null, 2));
console.log(`PAGE: ${JSON.stringify(page)}`);
console.log(`NET: ${all.length} requests, ${(report.bytesTransferred / 1048576).toFixed(2)} MiB transferred, external: ${report.externalRequests.join(', ') || 'none'}`);
console.log(`TIME: first frame ${firstFrameMs} ms, marker ${markerMs} ms`);
console.log(`RESULT: ${done ? 'marker seen' : 'timeout'} after ${Math.round((Date.now() - t0) / 1000)} s, console lines ${lines}, errors ${errors.length}`);
ws.close();
proc.kill();
process.exit(done && errors.length === 0 ? 0 : 1);
