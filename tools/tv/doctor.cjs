'use strict';
// Diagnoses the local TV toolchain without changing anything: Node, .NET SDK and Samsung workload, tz, sdb, the private
// configuration, the signing profile (by name only: keys and passwords are never read) and the TV connection.
// Usage: node tools/tv/doctor.cjs [--config FILE]
const fs = require('node:fs');
const path = require('node:path');
const { spawnSync } = require('node:child_process');
const root = path.resolve(__dirname, '../..');

function defaultRun(executable, args) {
  const r = spawnSync(executable, args, { cwd: root, encoding: 'utf8', timeout: 20000, shell: false });
  return { status: r.status, out: (r.stdout || '') + (r.stderr || ''), error: r.error };
}

function diagnose(config, run = defaultRun, exists = fs.existsSync, nodeVersion = process.versions.node) {
  const results = [];
  const add = (name, ok, detail, fix = '', required = true) => results.push({ name, ok, detail, fix, required });
  const volatile = value => /^\/(private\/)?tmp\//.test(value || '');
  const major = v => parseInt(String(v).split('.')[0], 10);

  add('Node.js', major(nodeVersion) >= 16, 'v' + nodeVersion, 'Install Node 16 or newer.');

  const dotnet = config.dotnet || 'dotnet';
  const dv = run(dotnet, ['--version']);
  const dvOk = dv.status === 0 && major(dv.out.trim()) >= 8;
  add('.NET SDK 8+', dvOk, dvOk ? dv.out.trim() : 'not found or too old', 'Install the .NET 8 SDK (https://dotnet.microsoft.com/download/dotnet/8.0) and point "dotnet" in .local/isko-tv.json at it.');
  if (dvOk) {
    const wl = run(dotnet, ['workload', 'list']);
    add('Samsung Tizen workload', wl.status === 0 && /\btizen\b/i.test(wl.out), wl.status === 0 && /\btizen\b/i.test(wl.out) ? 'installed' : 'missing',
      'Install it with the script from https://github.com/Samsung/Tizen.NET/wiki/Installing-Tizen-.NET-Workload (read the script before running it).');
  }

  const tz = config.tz || 'tz';
  const tzv = run(tz, ['--version']);
  add('tz (Tizen Core CLI)', tzv.status === 0, tzv.status === 0 ? tzv.out.trim().split('\n')[0] : 'not found',
    'Run: node tools/tv/setup-tools.cjs, then follow its steps; set "tz" in .local/isko-tv.json to <install>/tools/tizen-core/tz.');
  const sdb = config.sdb || 'sdb';
  const sv = run(sdb, ['version']);
  add('sdb', sv.status === 0, sv.status === 0 ? sv.out.trim().split('\n')[0] : 'not found', 'Set "sdb" in .local/isko-tv.json to <install>/tools/sdb.');
  for (const [label, value] of [['tz', tz], ['sdb', sdb], ['dotnet', dotnet]]) {
    if (volatile(value)) add(`${label} location`, false, 'inside a temporary folder', 'Temporary folders are cleared by the system: install the tools in a permanent folder and update the path.', false);
  }

  const profile = config.signingProfile;
  const profileSet = !!profile && profile !== 'YOUR_SAMSUNG_TV_PROFILE';
  add('Signing profile configured', profileSet, profileSet ? 'name set' : 'not set', 'Create a Samsung TV certificate profile (docs/install.md) and put its name in .local/isko-tv.json.');
  if (profileSet && tzv.status === 0) {
    const list = run(tz, ['security-profiles', 'list']);
    const found = list.status === 0 && list.out.split('\n').some(line => line.includes(profile));
    add('Signing profile exists', found, found ? 'listed by tz' : 'not listed by tz',
      'The profile store may have been removed or moved. Recreate the profile in Certificate Manager, or import your backed-up author.p12, and run `tz security-profiles add`.');
  }

  if (config.deviceAddress) {
    const device = config.deviceAddress.includes(':') ? config.deviceAddress : config.deviceAddress + ':26101';
    if (sv.status === 0) {
      run(sdb, ['connect', device]);
      const devices = run(sdb, ['devices']);
      const present = devices.status === 0 && devices.out.includes(device);
      add('TV reachable over sdb', present, present ? 'device listed' : 'not listed',
        'Turn the TV fully on; check Apps > 12345 > Developer mode ON with this computer\'s IP; then run `sdb disconnect`, `sdb kill-server`, `sdb start-server`, `sdb connect <address>` (a restart leaves a stale connection).', false);
    }
  } else {
    add('TV address', false, 'not set (fine for packaging only)', 'Set "deviceAddress" in .local/isko-tv.json to install on your TV.', false);
  }
  return results;
}

function render(results) {
  const lines = results.map(r => `${r.ok ? '[ok]' : r.required ? '[!!]' : '[--]'} ${r.name}: ${r.detail}` + (!r.ok && r.fix ? `\n      fix: ${r.fix}` : ''));
  const failed = results.filter(r => !r.ok && r.required).length;
  return { text: lines.join('\n'), failed };
}

function main(args) {
  let configPath = path.join(root, '.local', 'isko-tv.json');
  const i = args.indexOf('--config');
  if (i >= 0) configPath = path.resolve(root, args[i + 1] || '');
  let config = {};
  if (fs.existsSync(configPath)) config = JSON.parse(fs.readFileSync(configPath, 'utf8'));
  else console.log('No private configuration yet: copy tools/tv/config.example.json to .local/isko-tv.json (it is ignored by Git).\n');
  const { text, failed } = render(diagnose(config));
  console.log(text);
  console.log(failed ? `\n${failed} required check(s) need attention.` : '\nRequired checks pass.');
  process.exitCode = failed ? 1 : 0;
}

if (require.main === module) main(process.argv.slice(2));
module.exports = { diagnose, render };
