'use strict';
const http = require('http');
const fs = require('fs');
const path = require('path');
const publicDir = path.join(__dirname, 'public');
const diagnosticsDir = path.resolve(__dirname, '../../src/Isko.Diagnostics');
const mediaPath = path.join(__dirname, 'media', 'sample.mp4');
const validRun = value => typeof value === 'string' && /^[a-zA-Z0-9_-]{1,48}$/.test(value);

function createFixtureServer(options = {}) {
  let crossOrigin = options.crossOrigin || process.env.ISKO_CROSS_ORIGIN || null;
  if (crossOrigin) {
    const parsed = new URL(crossOrigin);
    if (!['http:', 'https:'].includes(parsed.protocol) || parsed.username || parsed.password || parsed.search || parsed.hash || parsed.pathname !== '/') throw new Error('ISKO_CROSS_ORIGIN must be a plain HTTP(S) origin');
    crossOrigin = parsed.origin;
  }
  const counters = new Map();
  const servers = [];
  let ports = options.ports || [8765, 8766];
  const send = (res, status, body, type = 'application/json') => {
    res.writeHead(status, { 'Content-Type': type, 'Cache-Control': 'no-store', 'X-Content-Type-Options': 'nosniff' });
    res.end(type === 'application/json' ? JSON.stringify(body) : body);
  };
  function handler(originIndex) {
    return (req, res) => {
      let url;
      try { url = new URL(req.url, 'http://fixture.invalid'); } catch (_) { return send(res, 400, { error: 'bad request' }); }
      const run = url.searchParams.get('run');
      const needsRun = ['/ad-probe', '/api/counters', '/api/reset', '/arrived'].includes(url.pathname);
      if (needsRun && !validRun(run)) return send(res, 400, { error: 'run must be 1–48 ASCII letters, digits, _ or -' });
      if (url.pathname === '/api/reset') {
        if (req.method !== 'POST') return send(res, 405, { error: 'POST required' });
        if (!counters.has(run) && counters.size >= 500) return send(res, 429, { error: 'restart fixture server to clear run capacity' });
        counters.set(run, { adRequests: 0, arrivals: 0 });
        return send(res, 200, { run, adRequests: 0, arrivals: 0 });
      }
      if (url.pathname === '/api/counters') return send(res, 200, { run, ...(counters.get(run) || { adRequests: 0, arrivals: 0 }) });
      if (url.pathname === '/ad-probe' || url.pathname === '/arrived') {
        if (!counters.has(run) && counters.size >= 500) return send(res, 429, { error: 'restart fixture server to clear run capacity' });
        const count = counters.get(run) || { adRequests: 0, arrivals: 0 };
        count[url.pathname === '/ad-probe' ? 'adRequests' : 'arrivals']++;
        counters.set(run, count);
        // Only controlled run IDs, resource categories and numeric counts reach stdout.
        if (options.log !== false) process.stdout.write(JSON.stringify({ run, event: url.pathname === '/ad-probe' ? 'ad-request-received' : 'arrival', origin: originIndex, ...count }) + '\n');
        if (url.pathname === '/ad-probe') return send(res, 200, '<svg xmlns="http://www.w3.org/2000/svg" width="240" height="60"><rect width="240" height="60" fill="orange"/><text x="10" y="35">AD REQUEST REACHED SERVER</text></svg>', 'image/svg+xml');
        return send(res, 200, '<!doctype html><title>Arrival</title><h1>Navigation reached target</h1><button onclick="history.back()">Back</button>', 'text/html; charset=utf-8');
      }
      if (url.pathname === '/config.js') return send(res, 200, 'window.FIXTURE_PORTS=' + JSON.stringify(ports) + ';window.FIXTURE_CROSS_ORIGIN=' + JSON.stringify(crossOrigin) + ';', 'application/javascript');
      if (url.pathname === '/diagnostics') {
        res.writeHead(302, { Location: '/diagnostics/', 'Cache-Control': 'no-store' });
        return res.end();
      }
      const diagnosticFiles = { '/diagnostics/': 'index.html', '/diagnostics/probe.js': 'probe.js' };
      if (Object.prototype.hasOwnProperty.call(diagnosticFiles, url.pathname)) {
        return send(res, 200, fs.readFileSync(path.join(diagnosticsDir, diagnosticFiles[url.pathname])), url.pathname.endsWith('.js') ? 'application/javascript' : 'text/html; charset=utf-8');
      }
      if (url.pathname === '/media/sample.mp4') {
        if (!fs.existsSync(mediaPath)) return send(res, 404, { error: 'Run node generate-media.js first' });
        const size = fs.statSync(mediaPath).size;
        const range = req.headers.range;
        let start = 0, end = size - 1;
        if (range) {
          const match = /^bytes=(\d+)-(\d*)$/.exec(range);
          if (!match || Number(match[1]) >= size || (match[2] && Number(match[2]) < Number(match[1]))) {
            res.writeHead(416, { 'Content-Range': 'bytes */' + size }); return res.end();
          }
          start = Number(match[1]); end = match[2] ? Math.min(Number(match[2]), size - 1) : end;
        }
        res.writeHead(range ? 206 : 200, { 'Content-Type': 'video/mp4', 'Accept-Ranges': 'bytes', 'Content-Length': end - start + 1, ...(range ? { 'Content-Range': `bytes ${start}-${end}/${size}` } : {}) });
        const stream = fs.createReadStream(mediaPath, { start, end });
        stream.on('error', () => res.destroy()); res.on('close', () => stream.destroy()); stream.pipe(res); return;
      }
      const files = { '/': 'index.html', '/fixture.js': 'fixture.js', '/frame': 'frame.html' };
      if (files[url.pathname]) return send(res, 200, fs.readFileSync(path.join(publicDir, files[url.pathname])), url.pathname.endsWith('.js') ? 'application/javascript' : 'text/html; charset=utf-8');
      send(res, 404, { error: 'not found' });
    };
  }
  return {
    handler,
    async start() {
      for (let index = 0; index < 2; index++) {
        const server = http.createServer(handler(index)); servers.push(server);
        await new Promise((resolve, reject) => { server.once('error', reject); server.listen(ports[index], options.host || '127.0.0.1', resolve); });
        ports[index] = server.address().port;
      }
      return ports.slice();
    },
    async close() { await Promise.all(servers.map(server => new Promise(resolve => server.close(resolve)))); }
  };
}
if (require.main === module) {
  const app = createFixtureServer({ host: process.env.ISKO_FIXTURE_HOST || '127.0.0.1' });
  app.start().then(ports => console.log('Isko controlled fixture listening on ports ' + ports.join(', ') + '; loopback by default; LAN binding must be explicit')).catch(error => { console.error(error.message); process.exit(1); });
  process.on('SIGINT', () => app.close().then(() => process.exit(0)));
}
module.exports = { createFixtureServer };
