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
assert.strictEqual(request('/../../server.js').status, 404);
for (let i = 0; i < 499; i++) request('/api/reset?run=r' + i, 'POST');
assert.strictEqual(request('/api/reset?run=overflow', 'POST').status, 429);
assert.strictEqual(request('/ad-probe?run=overflow').status, 429);
console.log('PASS in-process fixture handlers: routes, two-origin shared counters, positive baseline, reset, run isolation, validation, capacity bound');
