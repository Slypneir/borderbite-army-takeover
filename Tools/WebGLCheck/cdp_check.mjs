// Headless browser check of the WebGL build (agent self-verification, TZ section 14/16).
// Usage: node Tools/WebGLCheck/cdp_check.mjs <url> <doneMarker> [timeoutSec] [browserPath]
// Opens the page in headless Chrome/Edge (software WebGL), streams the console, exits when a console line
// contains <doneMarker>. Prints every error/exception and the lines of the developer tools and the game flow ([DevBridge], [DevTools], [GameFlow]).
import { spawn } from 'node:child_process';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const [url, marker, timeoutArg, browserArg] = process.argv.slice(2);
const timeoutSec = Number(timeoutArg || 600);
const browser = browserArg || 'C:/Program Files/Google/Chrome/Application/chrome.exe';
const port = 9300 + Math.floor(Math.random() * 500);
const profile = mkdtempSync(join(tmpdir(), 'webglcheck-'));

const proc = spawn(browser, [
  '--headless=new', `--remote-debugging-port=${port}`, `--user-data-dir=${profile}`,
  '--enable-unsafe-swiftshader', '--use-angle=swiftshader', '--ignore-gpu-blocklist',
  '--autoplay-policy=no-user-gesture-required', '--window-size=1280,720', '--no-first-run', 'about:blank',
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
const send = (method, params = {}) => ws.send(JSON.stringify({ id: ++id, method, params }));
const errors = [];
let done = false;
let lines = 0;

ws.onmessage = (ev) => {
  const msg = JSON.parse(ev.data);
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
  if (/\[DevBridge\]|\[DevTools\]|\[GameFlow\]/.test(text)) console.log('LOG: ' + text.slice(0, 2000));
  if (marker && text.includes(marker)) done = true;
};

await new Promise((r) => { ws.onopen = r; if (ws.readyState === 1) r(); });
send('Runtime.enable');
send('Log.enable');
send('Page.enable');
send('Page.navigate', { url });

const t0 = Date.now();
while (!done && (Date.now() - t0) / 1000 < timeoutSec) await sleep(500);
console.log(`RESULT: ${done ? 'marker seen' : 'timeout'} after ${Math.round((Date.now() - t0) / 1000)} s, console lines ${lines}, errors ${errors.length}`);
ws.close();
proc.kill();
process.exit(done && errors.length === 0 ? 0 : 1);
