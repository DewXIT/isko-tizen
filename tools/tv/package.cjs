'use strict';
const fs = require('node:fs');
const path = require('node:path');
const { spawnSync } = require('node:child_process');
const root = path.resolve(__dirname, '../..');
const fields = ['dotnet', 'tz', 'sdb', 'sdkPath', 'signingProfile', 'deviceAddress', 'outputDirectory'];
function plan(config, install = false, startUrl = '', tfm = 'net6.0-tizen9.0') {
  if (!config || typeof config !== 'object' || Array.isArray(config)) throw Error('Configuration must be a JSON object.');
  for (const key of Object.keys(config)) {
    if (!fields.includes(key) || typeof config[key] !== 'string' || /[\r\n\0]/.test(config[key])) throw Error('Invalid configuration field.');
  }
  if (startUrl && !/^https?:\/\/[A-Za-z0-9.:\/_?=&-]{1,200}$/.test(startUrl)) throw Error('--start-url must be a plain http(s) address.');
  if (!/^net\d+\.\d-tizen\d+\.\d$/.test(tfm)) throw Error('--target-framework must look like net6.0-tizen9.0.');
  if (!config.signingProfile || config.signingProfile === 'YOUR_SAMSUNG_TV_PROFILE') throw Error('Configure your own Samsung TV signing profile.');
  if (install && (!config.deviceAddress || !/^[a-zA-Z0-9][a-zA-Z0-9.-]*(?::[0-9]{1,5})?$/.test(config.deviceAddress))) throw Error('Installation needs your TV hostname/address (optional sdb port).');
  const manifest = fs.readFileSync(path.join(root, 'src/Isko.Tv/tizen-manifest.xml'), 'utf8');
  const pkg = /<manifest\b[^>]*\bpackage="([^"]+)"/.exec(manifest)[1];
  const version = /<manifest\b[^>]*\bversion="([^"]+)"/.exec(manifest)[1];
  const output = path.resolve(root, config.outputDirectory || 'artifacts/tv');
  // Build output must never be written into source or tracked configuration directories.
  const relative = path.relative(path.join(root, 'artifacts'), output);
  if (relative.startsWith('..') || path.isAbsolute(relative)) throw Error('outputDirectory must be inside artifacts/.');
  const buildOutput = path.join(output, 'build') + path.sep;
  const input = path.join(buildOutput, 'Release', tfm, `${pkg}-${version}.tpk`);
  const signed = path.join(output, `${pkg}-${version}.tpk`);
  const jobs = [
    { executable: config.dotnet || 'dotnet', args: ['build', 'src/Isko.Tv/Isko.Tv.csproj', '-c', 'Release', '-p:BuildForTizen=true', `-p:IskoTizenTfm=${tfm}`, '-p:IskoRuntimeDiagnostics=false',
      '-p:DebugType=None', '-p:DebugSymbols=false', '-p:ContinuousIntegrationBuild=true', `-p:PathMap=${root}=/_/`, `-p:BaseOutputPath=${buildOutput}`, ...(startUrl ? [`-p:IskoStartUrl=${startUrl}`] : [])] },
    { executable: config.tz || 'tz', args: ['pack', '--type', 'tpk', '--sign-profile', config.signingProfile, '--base-pkg', input, '--out-path', signed] }
  ];
  if (install) {
    const device = config.deviceAddress.includes(':') ? config.deviceAddress : config.deviceAddress + ':26101';
    jobs.push({ executable: config.sdb || 'sdb', args: ['connect', device] },
      { executable: config.sdb || 'sdb', args: ['-s', device, 'install', signed] },
      { executable: config.tz || 'tz', args: ['run', '--package-id', pkg, '--serial', device] });
  }
  return { jobs, output, signed };
}
// A developer start address is the one intended exception to the LAN-address rule; every other rule still applies.
function audit(file, startUrl) {
  return require('../check-public-data.cjs').scanArchive(file).filter(f => !(startUrl && f.rule === 'private-lan-address'));
}
function main(args) {
  const allowed = new Set(['--config', '--install', '--dry-run', '--start-url', '--target-framework']);
  let configPath = '.local/isko-tv.json', install = false, dry = false, startUrl = '', tfm = 'net6.0-tizen9.0';
  for (let i = 0; i < args.length; i++) {
    if (!allowed.has(args[i])) throw Error('Usage: node tools/tv/package.cjs [--config FILE] [--dry-run] [--install] [--start-url http(s)://...] [--target-framework net6.0-tizen9.0]');
    if (args[i] === '--config') { configPath = args[++i]; if (!configPath) throw Error('Missing configuration path.'); }
    else if (args[i] === '--target-framework') { tfm = args[++i] || ''; if (!tfm) throw Error('Missing target framework.'); }
    else if (args[i] === '--start-url') { startUrl = args[++i] || ''; if (!startUrl) throw Error('Missing start address.'); }
    else if (args[i] === '--install') install = true; else dry = true;
  }
  const config = JSON.parse(fs.readFileSync(path.resolve(root, configPath), 'utf8'));
  const result = plan(config, install, startUrl, tfm);
  if (dry) { console.log(`Validated ${result.jobs.length} steps: build, sign${install ? ', connect, install, launch' : ''}. Local paths, profile and TV address are hidden. No commands executed.`); return; }
  fs.mkdirSync(result.output, { recursive: true });
  const env = { ...process.env };
  if (config.sdkPath) env.TIZEN_SDK_PATH = path.resolve(config.sdkPath);
  for (const job of result.jobs) {
    if (install && job === result.jobs[2]) {
      if (audit(result.signed, startUrl).length) throw Error('Package publication audit failed; installation stopped.');
    }
    // Argument arrays, no shell expansion; no password or private key is read by this script.
    const step = spawnSync(job.executable, job.args, { cwd: root, env, stdio: 'inherit', shell: false });
    if (step.error || step.status !== 0) throw Error('Packaging/install step failed. See local tool output.');
  }
  if (audit(result.signed, startUrl).length) throw Error('Package publication audit failed.');
  if (startUrl) console.log('Developer package: it embeds the start address. Keep it private; it is not publishable.');
  console.log('Signed package created in the configured artifacts directory.');
}
if (require.main === module) { try { main(process.argv.slice(2)); } catch (e) { console.error(e.code === 'ENOENT' ? 'Create your ignored local configuration and configure the SDK tools first.' : e.message); process.exitCode = 1; } }
module.exports = { plan };
