'use strict';
const http = require('http');
const fs = require('fs');
const path = require('path');
const publicDir = path.join(__dirname, 'public');
const diagnosticsDir = path.resolve(__dirname, '../../src/Isko.Diagnostics');
const mediaFiles = { '/media/sample.mp4': path.join(__dirname, 'media', 'sample.mp4'), '/media/sample-frag.mp4': path.join(__dirname, 'media', 'sample-frag.mp4'), '/media/sample.m4a': path.join(__dirname, 'media', 'sample.m4a'), '/media/sample-vp9.webm': path.join(__dirname, 'media', 'sample-vp9.webm') };
const validRun = value => typeof value === 'string' && /^[a-zA-Z0-9_-]{1,48}$/.test(value);

function createFixtureServer(options = {}) {
  let crossOrigin = options.crossOrigin || process.env.ISKO_CROSS_ORIGIN || null;
  if (crossOrigin) {
    const parsed = new URL(crossOrigin);
    if (!['http:', 'https:'].includes(parsed.protocol) || parsed.username || parsed.password || parsed.search || parsed.hash || parsed.pathname !== '/') throw new Error('ISKO_CROSS_ORIGIN must be a plain HTTP(S) origin');
    crossOrigin = parsed.origin;
  }
  const counters = new Map();
  const blank = () => ({ adRequests: 0, arrivals: 0, downloads: 0 });
  // Page-side events for unattended device runs: bounded, run-scoped, names and detail strictly sanitised.
  const events = new Map();
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
      const needsRun = ['/ad-probe', '/api/counters', '/api/reset', '/arrived', '/download/sample.zip', '/api/event', '/api/events'].includes(url.pathname);
      if (needsRun && !validRun(run)) return send(res, 400, { error: 'run must be 1–48 ASCII letters, digits, _ or -' });
      if (url.pathname === '/api/reset') {
        if (req.method !== 'POST') return send(res, 405, { error: 'POST required' });
        if (!counters.has(run) && counters.size >= 500) return send(res, 429, { error: 'restart fixture server to clear run capacity' });
        counters.set(run, blank()); events.delete(run);
        return send(res, 200, { run, ...blank() });
      }
      if (url.pathname === '/api/counters') return send(res, 200, { run, ...(counters.get(run) || blank()) });
      if (url.pathname === '/api/event') {
        const name = url.searchParams.get('name') || '', detail = (url.searchParams.get('d') || '').slice(0, 200);
        if (!/^[a-z0-9-]{1,40}$/.test(name) || /[^\x20-\x7e]/.test(detail)) return send(res, 400, { error: 'bad event' });
        if (!events.has(run) && events.size >= 500) return send(res, 429, { error: 'restart fixture server to clear run capacity' });
        const list = events.get(run) || []; if (list.length >= 300) return send(res, 429, { error: 'event limit' });
        list.push({ n: list.length + 1, name, d: detail }); events.set(run, list);
        res.writeHead(204, { 'Cache-Control': 'no-store' }); return res.end();
      }
      if (url.pathname === '/api/events') return send(res, 200, { run, events: events.get(run) || [] });
      if (url.pathname === '/download/sample.zip') {
        // Counts requests that reach the server. A refused download may still be requested; the app's own
        // downloadsBlocked counter, not this one, is the evidence that nothing was saved.
        if (!counters.has(run) && counters.size >= 500) return send(res, 429, { error: 'restart fixture server to clear run capacity' });
        const count = counters.get(run) || blank();
        count.downloads++; counters.set(run, count);
        if (options.log !== false) process.stdout.write(JSON.stringify({ run, event: 'download-request-received', origin: originIndex, ...count }) + '\n');
        res.writeHead(200, { 'Content-Type': 'application/zip', 'Content-Disposition': 'attachment; filename="fixture.zip"', 'Cache-Control': 'no-store', 'X-Content-Type-Options': 'nosniff' });
        // An empty but valid ZIP archive: end-of-central-directory record only.
        return res.end(Buffer.concat([Buffer.from([0x50, 0x4b, 0x05, 0x06]), Buffer.alloc(18)]));
      }
      if (url.pathname === '/ad-probe' || url.pathname === '/arrived') {
        if (!counters.has(run) && counters.size >= 500) return send(res, 429, { error: 'restart fixture server to clear run capacity' });
        const count = counters.get(run) || blank();
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
      if (Object.prototype.hasOwnProperty.call(mediaFiles, url.pathname)) {
        const mediaPath = mediaFiles[url.pathname];
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
        res.writeHead(range ? 206 : 200, { 'Content-Type': url.pathname.endsWith('.m4a') ? 'audio/mp4' : url.pathname.endsWith('.webm') ? 'video/webm' : 'video/mp4', 'Accept-Ranges': 'bytes', 'Content-Length': end - start + 1, ...(range ? { 'Content-Range': `bytes ${start}-${end}/${size}` } : {}) });
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
