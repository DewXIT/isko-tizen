'use strict';
const assert = require('assert');
const { diagnose, render } = require('../tools/tv/doctor.cjs');
const { plan, nextSteps, BASE } = require('../tools/tv/setup-tools.cjs');

// Downloader plan: official host only, known sizes, a path inside the ignored folder, and no license acceptance.
for (const platform of ['darwin', 'linux', 'win32']) {
  const p = plan(platform, '/work/.local/downloads');
  assert(p.url.startsWith('https://download.tizen.org/sdk/Installer/'), platform + ': official https host');
  assert(p.bytes > 100e6, platform + ': a size to check against is known');
  assert(p.file.startsWith('/work/.local/downloads/'), platform + ': saved under the destination');
}
assert.throws(() => plan('freebsd'), /No installer/);
assert(BASE.startsWith('https://'), 'base is https');
const mac = nextSteps(plan('darwin', '/work/.local/downloads'), 'darwin').join('\n');
assert(mac.includes('--show-license') && mac.includes('--accept-license'), 'the steps let the user read and then accept the license');
assert(!mac.includes('/tmp'), 'the suggested install folder is permanent');

// Doctor with a scripted runner: a healthy setup, then each failure class.
const ok = (out = '') => ({ status: 0, out });
const bad = () => ({ status: 1, out: '' });
const healthy = (exe, args) => {
  const key = exe + ' ' + args.join(' ');
  if (key === 'dotnet --version') return ok('8.0.426\n');
  if (key === 'dotnet workload list') return ok('tizen  10.0.120/8.0.400  SDK 8.0.400\n');
  if (key === 'tz --version') return ok('tz 10.0.18\n');
  if (key === 'sdb version') return ok('Smart Development Bridge version 4.2.36\n');
  if (key === 'tz security-profiles list') return ok('MyProfile\nOther\n');
  if (key === 'sdb devices') return ok('List of devices attached\n192.0.2.7:26101\tdevice\tTV\n');
  return ok();
};
let r = diagnose({ signingProfile: 'MyProfile', deviceAddress: '192.0.2.7' }, healthy, () => true, '20.1.0');
assert(r.every(x => x.ok), 'a healthy setup passes every check: ' + JSON.stringify(r.filter(x => !x.ok)));
assert.strictEqual(render(r).failed, 0);

r = diagnose({ signingProfile: 'MyProfile' }, (e, a) => (e === 'tz' && a[0] === 'security-profiles' ? ok('Other\n') : healthy(e, a)), () => true, '20.1.0');
assert(r.find(x => x.name === 'Signing profile exists' && !x.ok), 'a profile missing from the store is reported');
assert(render(r).failed >= 1, 'and counts as a required failure');

r = diagnose({ signingProfile: 'YOUR_SAMSUNG_TV_PROFILE' }, healthy, () => true, '20.1.0');
assert(r.find(x => x.name === 'Signing profile configured' && !x.ok), 'the placeholder profile name is reported');

r = diagnose({ signingProfile: 'MyProfile', tz: '/tmp/tools/tz', sdb: '/tmp/tools/sdb' }, (e, a) => (e.startsWith('/tmp') ? ok(a[0] === 'security-profiles' ? 'MyProfile\n' : 'x\n') : healthy(e, a)), () => true, '20.1.0');
assert(r.filter(x => /location$/.test(x.name) && !x.ok).length === 2, 'tools in a temporary folder are flagged (not required)');
assert(render(r).failed === 0, 'a temporary location alone is a warning, not a failure');

r = diagnose({ signingProfile: 'MyProfile', deviceAddress: '192.0.2.7' }, (e, a) => (e === 'sdb' && a[0] === 'devices' ? ok('List of devices attached\n') : healthy(e, a)), () => true, '20.1.0');
const device = r.find(x => x.name === 'TV reachable over sdb');
assert(device && !device.ok && device.fix.includes('kill-server'), 'an unreachable TV points at the stale-connection fix');
assert(!JSON.stringify(r).includes('192.0.2.7'), 'the device address never appears in the report');

r = diagnose({ signingProfile: 'MyProfile' }, (e, a) => (e === 'dotnet' ? bad() : healthy(e, a)), () => true, '14.0.0');
assert(r.find(x => x.name === 'Node.js' && !x.ok) && r.find(x => x.name === '.NET SDK 8+' && !x.ok), 'old Node and a missing .NET SDK are reported');
console.log('PASS setup tools: official-only download plan with a size check and no license acceptance; doctor finds missing tools, profile, workload, volatile folders and a stale TV connection without printing the address');
