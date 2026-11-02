// What a Web build weighs and what it holds. Usage: node Tools/WebGLCheck/build_report.mjs <dir> [forbidden,words] [report.json]
// Sizes on disk, after gzip (what the servers of Playables send) and after Brotli; the words are looked for in the unpacked content.
import { readdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import { join, relative } from 'node:path';
import { brotliCompressSync, brotliDecompressSync, constants, gunzipSync, gzipSync } from 'node:zlib';

const [dir, wordsArg, reportPath] = process.argv.slice(2);
const words = (wordsArg || '').split(',').filter(Boolean);
const external = /https?:\/\/[a-zA-Z0-9.\-]+\.[a-z]{2,}[^\s"'`)<>\\]*/g;

const walk = (folder) => readdirSync(folder).flatMap((name) => {
  const path = join(folder, name);
  return statSync(path).isDirectory() ? walk(path) : [path];
});

const unpack = (path, bytes) => {
  try {
    if (path.endsWith('.br')) return brotliDecompressSync(bytes);
    if (path.endsWith('.gz')) return gunzipSync(bytes);
    if (path.endsWith('.unityweb')) {
      try { return brotliDecompressSync(bytes); } catch { return gunzipSync(bytes); }
    }
  } catch { /* not compressed after all */ }
  return bytes;
};

const files = walk(dir).map((path) => {
  const bytes = readFileSync(path);
  const plain = unpack(path, bytes);
  const text = plain.toString('latin1');
  const found = words.filter((word) => text.includes(word));
  const urls = [...new Set(text.match(external) || [])];
  return {
    file: relative(dir, path).replaceAll('\\', '/'),
    bytes: bytes.length,
    gzip: gzipSync(bytes, { level: 6 }).length,
    brotli: brotliCompressSync(bytes, { params: { [constants.BROTLI_PARAM_QUALITY]: 9 } }).length,
    unpacked: plain.length,
    found,
    urls,
  };
}).sort((a, b) => b.bytes - a.bytes);

const mib = (n) => (n / 1048576).toFixed(2).padStart(7) + ' MiB';
const sum = (key) => files.reduce((total, f) => total + f[key], 0);
console.log('file'.padEnd(46), 'on disk'.padStart(11), 'gzip'.padStart(11), 'brotli'.padStart(11));
for (const f of files) console.log(f.file.padEnd(46), mib(f.bytes), mib(f.gzip), mib(f.brotli));
console.log('TOTAL'.padEnd(46), mib(sum('bytes')), mib(sum('gzip')), mib(sum('brotli')));
console.log(`largest: ${files[0].file} ${mib(files[0].bytes)}; files: ${files.length}`);
const hits = files.filter((f) => f.found.length > 0);
console.log(hits.length ? `FORBIDDEN: ${hits.map((f) => `${f.file}: ${f.found.join(', ')}`).join('; ')}` : `forbidden words: none of ${words.length}`);
console.log('addresses in the build:');
for (const f of files) for (const u of f.urls) console.log(`  ${f.file}: ${u.slice(0, 110)}`);
if (reportPath) writeFileSync(reportPath, JSON.stringify({ files, totals: { bytes: sum('bytes'), gzip: sum('gzip'), brotli: sum('brotli') } }, null, 2));
process.exit(hits.length ? 1 : 0);
