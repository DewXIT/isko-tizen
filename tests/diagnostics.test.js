'use strict';
// Exercise missing/denied TV APIs and browser Back without a TV or DOM dependency.
const assert = require('assert');
const fs = require('fs');
const path = require('path');
const vm = require('vm');
const source = fs.readFileSync(path.join(__dirname, '../src/Isko.Diagnostics/probe.js'), 'utf8');
function probe({ protocol = 'http:', userAgent = 'Desktop browser', tizen, webapis, loadError = false } = {}) {
  const listeners = {};
  const result = {};
  let focused = false, scripts = 0;
  const button = { addEventListener(name, fn) { listeners[name] = fn; }, focus() { focused = true; } };
  const document = {
    hidden: false,
    getElementById(id) { return id === 'result' ? result : button; },
    addEventListener(name, fn) { listeners[name] = fn; },
    createElement() { return {}; },
    head: { appendChild(script) { scripts++; assert.strictEqual(script.src, '$WEBAPIS/webapis/webapis.js'); (loadError ? script.onerror : script.onload)(); } }
  };
  vm.runInNewContext(source, { location: { protocol }, navigator: { userAgent }, screen: { width: 1920, height: 1080 }, document, tizen, webapis });
  return { read: () => JSON.parse(result.textContent), listeners, focused, scripts };
}
const browser = probe({ userAgent: 'Mozilla/5.0 (SMART-TV; LINUX; Tizen 9.0) AppleWebKit/537.36' });
assert.strictEqual(browser.read().browserReportedTizenVersion, '9.0');
assert.strictEqual(browser.read().platformVersion, 'unavailable');
assert.strictEqual(browser.read().model, 'unavailable');
assert.strictEqual(browser.read().requestInterception, 'not tested');
assert.strictEqual(browser.scripts, 0);
assert(browser.focused);
browser.listeners.keydown({ keyCode: 10009, preventDefault() { assert.fail('Browser Back must remain available'); } });
assert.strictEqual(probe().read().browserReportedTizenVersion, 'unavailable');
assert.strictEqual(probe({ protocol: 'file:', loadError: true }).read().firmware, 'unavailable');
let exited = false, prevented = false;
const packaged = probe({
  protocol: 'file:',
  tizen: {
    systeminfo: { getCapability(key) { if (key === 'http://tizen.org/feature/platform.version') return '8.0'; throw new Error('Not supported'); } },
    application: { getCurrentApplication() { return { exit() { exited = true; } }; } }
  },
  webapis: { productinfo: { getModel() { return 'fixture-model'; }, getFirmware() { throw new Error('Access denied'); } } }
});
assert.strictEqual(packaged.read().platformVersion, '8.0');
assert.strictEqual(packaged.read().model, 'fixture-model');
assert.strictEqual(packaged.read().firmware, 'unavailable');
assert.strictEqual(packaged.scripts, 1);
packaged.listeners.keydown({ keyCode: 10009, preventDefault() { prevented = true; } });
assert(exited && prevented);
console.log('PASS diagnostic fallback: missing/denied APIs, UA hint, widget API loading, browser Back, packaged exit');
