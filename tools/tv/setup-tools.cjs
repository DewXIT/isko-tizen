'use strict';
// Downloads Samsung's official Tizen SDK command-line installer (sdb, the tz CLI and the package manager) into
// .local/downloads. It does not install anything and does not accept any license: the next step is printed and
// belongs to you. Only https://download.tizen.org is contacted. The size is checked against the published listing.
const https = require('node:https');
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../..');
const BASE = 'https://download.tizen.org/sdk/Installer/tizen-sdk_10.0/';
const FILES = {
  darwin: { name: 'web-cli_Tizen_SDK_10.0_macos-64.bin', bytes: 326076958 },
  linux: { name: 'web-cli_Tizen_SDK_10.0_ubuntu-64.bin', bytes: 401290863 },
  win32: { name: 'web-cli_Tizen_SDK_10.0_windows-64.exe', bytes: 373556224 },
};

function plan(platform = process.platform, destination = path.join(root, '.local', 'downloads')) {
  const file = FILES[platform];
  if (!file) throw Error('No installer is listed for this platform: ' + platform);
  return { url: BASE + file.name, file: path.join(destination, file.name), bytes: file.bytes, name: file.name };
}

function nextSteps(p, platform = process.platform) {
  if (platform === 'win32') {
    return ['Run the downloaded installer: ' + p.file, 'Read and accept the license in its window, then add its tools folder to PATH.'];
  }
  const install = '$HOME/tizen-sdk'; // a permanent folder: never /tmp, which the system may clear
  return [
    'Read the license first:   "' + p.file + '" --show-license',
    'Install (this accepts it): "' + p.file + '" --accept-license ' + install,
    'Then add to PATH or to .local/isko-tv.json: ' + install + '/tools (sdb) and ' + install + '/tools/tizen-core (tz).',
    'Run: node tools/tv/doctor.cjs   to check the whole setup.',
  ];
}

function download(p) {
  return new Promise((resolve, reject) => {
    fs.mkdirSync(path.dirname(p.file), { recursive: true });
    if (fs.existsSync(p.file) && fs.statSync(p.file).size === p.bytes) return resolve('already downloaded');
    const part = p.file + '.part';
    const request = (url, hops) => https.get(url, response => {
      if ([301, 302, 307, 308].includes(response.statusCode) && response.headers.location && hops < 3) {
        const next = new URL(response.headers.location, url);
        response.resume();
        if (next.protocol !== 'https:' || next.hostname !== 'download.tizen.org') return reject(Error('Unexpected redirect target.'));
        return request(next.href, hops + 1);
      }
      if (response.statusCode !== 200) { response.resume(); return reject(Error('HTTP ' + response.statusCode)); }
      const out = fs.createWriteStream(part);
      let seen = 0, shown = 0;
      response.on('data', chunk => { seen += chunk.length; if (seen - shown > 20e6) { shown = seen; process.stdout.write('.'); } });
      response.pipe(out);
      out.on('finish', () => {
        if (seen !== p.bytes) { fs.rmSync(part, { force: true }); return reject(Error(`Size mismatch: got ${seen}, expected ${p.bytes}.`)); }
        fs.renameSync(part, p.file); fs.chmodSync(p.file, 0o755); resolve('downloaded');
      });
      response.on('error', reject); out.on('error', reject);
    }).on('error', reject);
    request(p.url, 0);
  });
}

async function main(args) {
  const dry = args.includes('--dry-run');
  const p = plan();
  console.log(`Official installer: ${p.url}\nSaved as: ${path.relative(root, p.file)} (${(p.bytes / 1e6).toFixed(0)} MB, size-checked)`);
  if (dry) { console.log('Dry run: nothing downloaded.'); } else { console.log(await download(p)); }
  console.log('\nNext steps (nothing has been installed or accepted):\n' + nextSteps(p).map(l => '  ' + l).join('\n'));
}

if (require.main === module) main(process.argv.slice(2)).catch(e => { console.error(e.message); process.exitCode = 1; });
module.exports = { plan, nextSteps, BASE, FILES };
