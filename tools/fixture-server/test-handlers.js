'use strict';
const assert = require('assert');
const { createFixtureServer } = require('./server');
const app = createFixtureServer({ log: false });
function request(url, method = 'GET', origin = 0) {
  let status, headers, body;
  app.handler(origin)({ url, method, headers: {} }, {
    writeHead(code, value) { status = code; headers = value; },
    end(value) { body = Buffer.isBuffer(value) ? value.toString() : value; }
  });
  return { status, headers, body, json() { return JSON.parse(body); } };
}
assert.strictEqual(request('/').status, 200);
assert(request('/').body.includes('fixture.js'));
assert.strictEqual(request('/diagnostics').headers.Location, '/diagnostics/');
assert(request('/diagnostics/').body.includes('probe.js'));
assert.strictEqual(request('/diagnostics/probe.js').headers['Content-Type'], 'application/javascript');
assert.strictEqual(request('/diagnostics/config.xml').status, 404);
assert.strictEqual(request('/frame', 'GET', 1).status, 200);
assert.strictEqual(request('/api/reset?run=test').status, 405);
assert.strictEqual(request('/api/reset?run=test', 'POST').status, 200);
assert.strictEqual(request('/api/counters?run=test').json().adRequests, 0);
assert.strictEqual(request('/ad-probe?run=test&token=SECRET').status, 200);
request('/ad-probe?run=test', 'GET', 1);
assert.strictEqual(request('/api/counters?run=test').json().adRequests, 2);
assert.strictEqual(request('/api/counters?run=other').json().adRequests, 0);
request('/arrived?run=test');
assert.strictEqual(request('/api/counters?run=test').json().arrivals, 1);
request('/api/reset?run=test', 'POST');
assert.strictEqual(request('/api/counters?run=test').json().adRequests, 0);
assert.strictEqual(request('/ad-probe?run=bad%20run').status, 400);
const download = request('/download/sample.zip?run=test');
assert.strictEqual(download.status, 200);
assert.strictEqual(download.headers['Content-Type'], 'application/zip');
assert(/^attachment/.test(download.headers['Content-Disposition']), 'archive is served as an attachment');
assert(download.body.startsWith('PK'), 'a valid (empty) ZIP end record');
assert.strictEqual(request('/api/counters?run=test').json().downloads, 1);
assert.strictEqual(request('/download/sample.zip?run=bad%20run').status, 400);
assert.strictEqual(request('/api/reset?run=test', 'POST').json().downloads, 0);
// Browser-side fixtures must at least parse; their behavior is exercised in a real browser or on the device.
const fs = require('fs'), path = require('path'), vm = require('vm');
new vm.Script(fs.readFileSync(path.join(__dirname, 'public/fixture.js'), 'utf8'));
const frameScript = /<script>([\s\S]*)<\/script>/.exec(fs.readFileSync(path.join(__dirname, 'public/frame.html'), 'utf8'));
assert(frameScript, 'frame fixture has a script'); new vm.Script(frameScript[1]);
for (const id of ['arm-interstitial', 'popup-cross', 'popup-delayed', 'state', 'dl-attr', 'dl-plain', 'dl-script', 'bait-start', 'arm-trap', 'trap-play'])
  assert(request('/').body.includes('id="' + id + '"'), 'index fixture has #' + id);
assert(fs.readFileSync(path.join(__dirname, 'public/frame.html'), 'utf8').includes('id="popup-x"'), 'frame fixture has the cross-site popup button');
assert.strictEqual(request('/api/event?run=test&name=player-click&d=%7B%7D').status, 204);
assert.strictEqual(request('/api/event?run=test&name=Bad%20Name').status, 400);
assert.strictEqual(request('/api/event?run=test&name=ok&d=%C3%A9').status, 400, 'non-ASCII detail refused');
assert.deepStrictEqual(request('/api/events?run=test').json().events.map(e => e.name), ['player-click']);
request('/api/reset?run=test', 'POST'); assert.strictEqual(request('/api/events?run=test').json().events.length, 0, 'reset clears events');
assert.strictEqual(request('/../../server.js').status, 404);
for (let i = 0; i < 499; i++) request('/api/reset?run=r' + i, 'POST');
assert.strictEqual(request('/api/reset?run=overflow', 'POST').status, 429);
assert.strictEqual(request('/ad-probe?run=overflow').status, 429);
console.log('PASS in-process fixture handlers: routes, two-origin shared counters, positive baseline, reset, run isolation, validation, capacity bound, download archive, fixture scripts parse');
