'use strict';
// Narrow, deterministic publication gate; findings name rules/lines, never secret values.
const fs = require('node:fs');
const path = require('node:path');
const zlib = require('node:zlib');
const { execFileSync } = require('node:child_process');
const root = path.resolve(__dirname, '..');
const rules = [
  ['private-key', /-----BEGIN (?:[A-Z]+ )?PRIVATE KEY-----/g],
  ['github-token', /\b(?:gh[pousr]_[A-Za-z0-9]{36,255}|github_pat_[A-Za-z0-9_]{60,255})\b/g],
  ['aws-access-id', /\b(?:AKIA|ASIA)[A-Z0-9]{16}\b/g],
  ['slack-token', /\bxox[baprs]-[A-Za-z0-9-]{20,}\b/g],
  ['private-lan-address', /\b(?:192\.168\.\d{1,3}\.\d{1,3}|10\.\d{1,3}\.\d{1,3}\.\d{1,3}|172\.(?:1[6-9]|2\d|3[01])\.\d{1,3}\.\d{1,3})\b/g],
  ['owner-workstation-path', /(?:\/Users\/|\/Volumes\/|[A-Za-z]:\\Users\\)[^\s"'<>]+/g]
];
function scanText(text) {
  const found = [];
  for (const [rule, pattern] of rules) {
    pattern.lastIndex = 0;
    for (const match of text.matchAll(pattern)) found.push({ rule, line: text.slice(0, match.index).split('\n').length });
  }
  return found;
}
const privateFile = name => /(?:^|\/)(?:profiles\.xml|isko\.local\.json|\.local\/)|\.(?:p12|pfx|pem|key|cer|crt|pdb|tpk|wgt|log)$/i.test(name);
function scanFiles(names) {
  const findings = [];
  for (const name of names) {
    if (privateFile(name)) { findings.push({ file: name, rule: 'private-or-generated-file' }); continue; }
    const absolute = path.join(root, name);
    if (!fs.existsSync(absolute)) continue;
    const bytes = fs.readFileSync(absolute);
    for (const finding of scanText(bytes.toString('utf8'))) findings.push({ file: name, ...finding });
  }
  return findings;
}
function scanArchive(filename) {
  const bytes = fs.readFileSync(filename), findings = [];
  let end = -1;
  for (let i = bytes.length - 22; i >= Math.max(0, bytes.length - 65557); i--) {
    if (bytes.readUInt32LE(i) === 0x06054b50) { end = i; break; }
  }
  if (end < 0) throw Error('Unsupported package: ZIP directory missing.');
  const count = bytes.readUInt16LE(end + 10); let offset = bytes.readUInt32LE(end + 16), total = 0;
  for (let i = 0; i < count; i++) {
    if (bytes.readUInt32LE(offset) !== 0x02014b50) throw Error('Unsupported ZIP directory.');
    const method = bytes.readUInt16LE(offset + 10), compressed = bytes.readUInt32LE(offset + 20), size = bytes.readUInt32LE(offset + 24);
    const length = bytes.readUInt16LE(offset + 28), extra = bytes.readUInt16LE(offset + 30), comment = bytes.readUInt16LE(offset + 32);
    const name = bytes.subarray(offset + 46, offset + 46 + length).toString('utf8'), local = bytes.readUInt32LE(offset + 42);
    offset += 46 + length + extra + comment;
    if (name.endsWith('/')) continue;
    if (privateFile(name) || name.startsWith('/') || name.split('/').includes('..')) findings.push({ file: name, rule: 'private-or-generated-file' });
    total += size; if (size > 16 * 1024 * 1024 || total > 64 * 1024 * 1024) throw Error('Package exceeds audit bounds.');
    const start = local + 30 + bytes.readUInt16LE(local + 26) + bytes.readUInt16LE(local + 28);
    const raw = bytes.subarray(start, start + compressed);
    const data = method === 0 ? raw : method === 8 ? zlib.inflateRawSync(raw, { maxOutputLength: 16 * 1024 * 1024 }) : null;
    if (!data || data.length !== size) throw Error('Unsupported package compression/size.');
    if (data.includes(Buffer.from('StartInspectorServer'))) findings.push({ file: name, rule: 'runtime-inspector-in-release' });
    for (const encoding of ['utf8', 'utf16le'])
      for (const finding of scanText(data.toString(encoding))) findings.push({ file: name, ...finding });
  }
  return findings;
}
function main(args) {
  let findings;
  if (args[0] === '--archive' && args.length === 2) findings = scanArchive(args[1]);
  else if (args.length === 0) {
    const names = execFileSync('git', ['ls-files', '-z', '--cached', '--others', '--exclude-standard'], { cwd: root }).toString().split('\0').filter(Boolean);
    findings = scanFiles(names);
  } else throw Error('Usage: node tools/check-public-data.cjs [--archive PACKAGE.tpk]');
  for (const finding of findings) console.error(JSON.stringify(finding));
  if (findings.length) process.exitCode = 1;
  else console.log('PASS public-data gate: no matched credentials, private LAN literals, owner paths or private/generated files in the inspected scope.');
}
if (require.main === module) { try { main(process.argv.slice(2)); } catch { console.error('Publication audit could not complete.'); process.exitCode = 1; } }
module.exports = { scanText, scanFiles, scanArchive };
