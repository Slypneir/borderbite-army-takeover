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

// the block of LZ4 Unity uses inside its archives
const lz4 = (src, size) => {
  const out = Buffer.alloc(size);
  let s = 0; let d = 0;
  while (s < src.length) {
    const token = src[s++];
    let literals = token >> 4;
    if (literals === 15) { let b; do { b = src[s++]; literals += b; } while (b === 255); }
    src.copy(out, d, s, s + literals); s += literals; d += literals;
    if (s >= src.length) break;
    const offset = src[s] | (src[s + 1] << 8); s += 2;
    let match = token & 15;
    if (match === 15) { let b; do { b = src[s++]; match += b; } while (b === 255); }
    match += 4;
    for (let i = 0; i < match; i++, d++) out[d] = out[d - offset];
  }
  return out;
};

// UnityFS: the scenes and assets of a build, packed by blocks of LZ4
const unityFs = (bytes) => {
  let p = 8;
  const version = bytes.readUInt32BE(p); p += 4;
  p = bytes.indexOf(0, p) + 1;
  p = bytes.indexOf(0, p) + 1;
  p += 8;
  const packedInfo = bytes.readUInt32BE(p); p += 4;
  const plainInfo = bytes.readUInt32BE(p); p += 4;
  const flags = bytes.readUInt32BE(p); p += 4;
  if (version >= 7) p = (p + 15) & ~15;
  const infoAtEnd = (flags & 0x80) !== 0;
  const packed = infoAtEnd ? bytes.subarray(bytes.length - packedInfo) : bytes.subarray(p, p + packedInfo);
  if (!infoAtEnd) p += packedInfo;
  if (flags & 0x200) p = (p + 15) & ~15;
  const info = (flags & 0x3f) === 0 ? packed : lz4(packed, plainInfo);
  const count = info.readUInt32BE(16);
  const parts = [];
  for (let i = 0; i < count; i++) {
    const at = 20 + i * 10;
    const plain = info.readUInt32BE(at);
    const size = info.readUInt32BE(at + 4);
    const kind = info.readUInt16BE(at + 8) & 0x3f;
    const block = bytes.subarray(p, p + size); p += size;
    parts.push(kind === 0 ? block : lz4(block, plain));
  }
  return Buffer.concat(parts);
};

// UnityWebData: the files of the .data file, each opened if it is an archive
const unityWebData = (bytes) => {
  const end = bytes.readUInt32LE(16);
  const parts = [];
  for (let p = 20; p < end;) {
    const offset = bytes.readUInt32LE(p);
    const size = bytes.readUInt32LE(p + 4);
    const length = bytes.readUInt32LE(p + 8);
    p += 12 + length;
    const file = bytes.subarray(offset, offset + size);
    parts.push(file.subarray(0, 7).toString() === 'UnityFS' ? unityFs(file) : file);
  }
  return Buffer.concat(parts);
};

const open = (plain) => (plain.subarray(0, 15).toString() === 'UnityWebData1.0' ? unityWebData(plain) : plain);

const files = walk(dir).map((path) => {
  const bytes = readFileSync(path);
  const plain = open(unpack(path, bytes));
  // the only allowed hit: the list of search bots in the loader of Unity, not code of a platform
  const text = plain.toString('latin1').replaceAll('slurp|yandex', '');
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
