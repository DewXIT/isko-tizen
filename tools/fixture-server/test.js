'use strict';
const assert = require('assert');
const http = require('http');
const { createFixtureServer } = require('./server');
function request(port, route, method = 'GET', headers = {}) {
  return new Promise((resolve, reject) => { const req = http.request({ hostname: '127.0.0.1', port, path: route, method, headers }, res => { const chunks = []; res.on('data', data => chunks.push(data)); res.on('end', () => resolve({ status: res.statusCode, headers: res.headers, body: Buffer.concat(chunks).toString() })); }); req.on('error', reject); req.end(); });
}
(async () => {
  const app = createFixtureServer({ ports: [0, 0], host: '127.0.0.1', log: false });
  try {
    const ports = await app.start(); assert.notStrictEqual(ports[0], ports[1]);
    assert.strictEqual((await request(ports[0], '/')).status, 200);
    assert.strictEqual((await request(ports[0], '/diagnostics')).headers.location, '/diagnostics/');
    assert((await request(ports[0], '/diagnostics/')).body.includes('probe.js'));
    assert.strictEqual((await request(ports[0], '/diagnostics/probe.js')).status, 200);
    assert.strictEqual((await request(ports[0], '/diagnostics/config.xml')).status, 404);
    assert.strictEqual((await request(ports[1], '/frame')).status, 200);
    assert.strictEqual((await request(ports[0], '/api/reset?run=test')).status, 405);
    assert.strictEqual((await request(ports[0], '/api/reset?run=test', 'POST')).status, 200);
    assert.strictEqual(JSON.parse((await request(ports[0], '/api/counters?run=test')).body).adRequests, 0);
    assert.strictEqual((await request(ports[0], '/ad-probe?run=test&token=DO-NOT-LOG')).status, 200);
    await request(ports[1], '/ad-probe?run=test');
    assert.strictEqual(JSON.parse((await request(ports[0], '/api/counters?run=test')).body).adRequests, 2);
    assert.strictEqual(JSON.parse((await request(ports[0], '/api/counters?run=isolated')).body).adRequests, 0);
    await request(ports[0], '/api/reset?run=test', 'POST');
    assert.strictEqual(JSON.parse((await request(ports[0], '/api/counters?run=test')).body).adRequests, 0);
    assert.strictEqual((await request(ports[0], '/ad-probe?run=bad%20run')).status, 400);
    assert.strictEqual((await request(ports[0], '/../../server.js')).status, 404);
    const video = await request(ports[0], '/media/sample.mp4', 'GET', { Range: 'bytes=0-99' });
    if (video.status === 404) console.log('SKIP media range: generate media first');
    else { assert.strictEqual(video.status, 206); assert.strictEqual(video.headers['content-length'], '100'); assert.strictEqual((await request(ports[0], '/media/sample.mp4', 'GET', { Range: 'bytes=99999999999-' })).status, 416); }
    console.log('PASS fixture HTTP tests: origins, counters, baseline/reset/isolation, validation, route allowlist, optional media range');
  } finally { await app.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
