'use strict';
const assert = require('node:assert/strict');
const { plan } = require('../tools/tv/package.cjs');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { scanText, scanArchive } = require('../tools/check-public-data.cjs');
const config = { signingProfile: 'test-only-profile', deviceAddress: 'tv.example.invalid' };
const build = plan(config);
assert.equal(build.jobs.length, 2, 'Default packaging cannot connect/install/launch');
assert(build.jobs[0].args.includes('-p:IskoRuntimeDiagnostics=false'));
assert(build.jobs[0].args.includes('-p:DebugType=None'));
assert(build.jobs[0].args.some(x => x.startsWith('-p:PathMap=')));
assert.equal(plan(config, true).jobs.length, 5);
assert.throws(() => plan({ ...config, password: 'not-accepted' }));
assert(!plan(config).jobs[0].args.some(a => a.startsWith('-p:IskoStartUrl')), 'normal builds carry no start address');
assert(plan(config).jobs[0].args.includes('-p:IskoTizenTfm=net6.0-tizen9.0'), 'default Tizen target framework');
assert(plan(config, false, '', 'net8.0-tizen9.0').jobs[1].args.some(a => a.includes('net8.0-tizen9.0')), 'the TPK path follows the target framework');
for (const bad of ['net6.0', 'net6.0-tizen9.0;x', '../x']) assert.throws(() => plan(config, false, '', bad), bad + ' rejected');
assert(plan(config, false, 'http://192.0.2.1:8765/').jobs[0].args.includes('-p:IskoStartUrl=http://192.0.2.1:8765/'), 'developer start address is passed to the build');
for (const bad of ['javascript:alert(1)', 'http://x/ y', 'http://x/;rm', 'ftp://x/']) assert.throws(() => plan(config, false, bad), bad + ' rejected');
assert.throws(() => plan({ ...config, outputDirectory: 'src' }));
assert.throws(() => plan({ ...config, deviceAddress: '--help' }, true));
assert.throws(() => plan({ ...config, deviceAddress: 'tv.invalid;echo bad' }, true));
assert.throws(() => plan({ ...config, signingProfile: 'YOUR_SAMSUNG_TV_PROFILE' }));
const privateAddress = [192, 168, 42, 9].join('.');
assert(scanText(privateAddress).some(x => x.rule === 'private-lan-address'));
assert(scanText('gh' + 'p_' + 'a'.repeat(36)).some(x => x.rule === 'github-token'));
assert(scanText('/Us' + 'ers/private-person/build').some(x => x.rule === 'owner-workstation-path'));
assert.equal(scanText('http://YOUR_COMPUTER_LAN_IP:8765/').length, 0);
// A tiny stored ZIP fixture exercises the package gate, including UTF-16 assembly data.
function zip(name, data) {
  const filename = Buffer.from(name), local = Buffer.alloc(30), central = Buffer.alloc(46), end = Buffer.alloc(22);
  local.writeUInt32LE(0x04034b50); local.writeUInt32LE(data.length, 18); local.writeUInt32LE(data.length, 22); local.writeUInt16LE(filename.length, 26);
  central.writeUInt32LE(0x02014b50); central.writeUInt32LE(data.length, 20); central.writeUInt32LE(data.length, 24); central.writeUInt16LE(filename.length, 28);
  const start = local.length + filename.length + data.length;
  end.writeUInt32LE(0x06054b50); end.writeUInt16LE(1, 8); end.writeUInt16LE(1, 10); end.writeUInt32LE(central.length + filename.length, 12); end.writeUInt32LE(start, 16);
  return Buffer.concat([local, filename, data, central, filename, end]);
}
const temp = fs.mkdtempSync(path.join(os.tmpdir(), 'isko-package-test-'));
try {
  const pkg = path.join(temp, 'test.zip');
  fs.writeFileSync(pkg, zip('bin/app.dll', Buffer.from('safe metadata'))); assert.equal(scanArchive(pkg).length, 0);
  fs.writeFileSync(pkg, zip('bin/app.pdb', Buffer.from('debug'))); assert(scanArchive(pkg).some(x => x.rule === 'private-or-generated-file'));
  fs.writeFileSync(pkg, zip('bin/app.dll', Buffer.from(privateAddress, 'utf16le'))); assert(scanArchive(pkg).some(x => x.rule === 'private-lan-address'));
  fs.writeFileSync(pkg, zip('bin/app.dll', Buffer.from('Start' + 'InspectorServer'))); assert(scanArchive(pkg).some(x => x.rule === 'runtime-inspector-in-release'));
} finally { fs.rmSync(temp, { recursive: true }); }
console.log('PASS packaging plan and public-data rules: opt-in install, private configuration, no-shell device args, stripped debug paths, detection positives (no SDK/TV execution)');
