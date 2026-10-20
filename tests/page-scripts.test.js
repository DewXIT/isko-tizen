'use strict';
// Execute the bundled JS in controlled DOM doubles. No browser, decoder or TV claims.
const assert = require('assert');
const fs = require('fs');
const path = require('path');
const vm = require('vm');
const source = fs.readFileSync(path.join(__dirname, '../src/Isko.Tv/PageScripts.cs'), 'utf8');
function script(name) {
  const declaration = new RegExp('(?:public|private) const string ' + name + ' = ').exec(source);
  assert(declaration, 'Missing bundled script: ' + name);
  const token = /\s*(@"(?:""|[^"])*"|\w+)\s*([+;])/gy;
  token.lastIndex = declaration.index + declaration[0].length;
  let result = '', part;
  do {
    part = token.exec(source); assert(part, 'Unsupported C# string expression');
    result += part[1].startsWith('@"') ? part[1].slice(2, -1).replace(/""/g, '"') : script(part[1]);
  } while (part[2] !== ';');
  return result;
}
class Media {
  constructor(extra = {}) {
    Object.assign(this, { paused: false, ended: false, pauses: 0, readyState: 4, networkState: 2,
      currentTime: 12, videoWidth: 1280, videoHeight: 720, error: null,
      buffered: { length: 1, start: () => 10, end: () => 18 },
      getBoundingClientRect: () => ({ left: 10, top: 20, right: 650, bottom: 380, width: 640, height: 360 }),
      getVideoPlaybackQuality: () => ({ droppedVideoFrames: 2, totalVideoFrames: 100 }) }, extra);
  }
  pause() { this.pauses++; this.paused = true; }
}
const live = new Media(), preview = new Media({ paused: true });
const listeners = {};
const context = { window: {}, HTMLMediaElement: Media, innerWidth: 1920, innerHeight: 1080,
  performance: {}, getComputedStyle: () => ({ display: 'block', visibility: 'visible' }),
  document: { querySelectorAll: () => [preview, live], addEventListener(name, fn) {
    (listeners[name] ||= []).push(fn);
  } } };
function emit(name, target) { for (const fn of listeners[name] || []) fn({ target }); }
vm.runInNewContext(script('ObserveMedia'), context);
vm.runInNewContext(script('ObserveMedia'), context);
emit('play', preview);
assert.strictEqual(live.pauses, 0, 'A preview must not pause the live player');
emit('play', live);
assert.strictEqual(preview.pauses, 0, 'The observer must leave player handoffs to the site');
emit('waiting', live); emit('stalled', live); emit('error', live); emit('error', {});
assert.deepStrictEqual(JSON.parse(JSON.stringify(context.window.__iskoMedia)), { errors: 1, waiting: 1, stalls: 1 });
const report = JSON.parse(vm.runInNewContext(script('ReadMedia'), context));
assert.strictEqual(report.videoCount, 2); assert.strictEqual(report.activeVideos, 1);
assert.strictEqual(report.currentTime, 12); assert.strictEqual(report.bufferAhead, 6);
assert.strictEqual(report.readyState, 4); assert.strictEqual(report.boxWidth, 640);
assert.strictEqual(report.totalFrames, 100); assert.strictEqual(report.mediaError, null);
context.document.querySelectorAll = () => [];
assert.strictEqual(JSON.parse(vm.runInNewContext(script('ReadMedia'), context)).currentTime, null);

class Mouse { constructor(type, options) { this.type = type; Object.assign(this, options); } }
const events = [];
const target = { tagName: 'BUTTON', closest: () => null, focus() {},
  dispatchEvent(e) { events.push(e); return true; }, click() { assert.fail('click() loses hit coordinates'); } };
const win = { innerWidth: 1920, innerHeight: 1080, MouseEvent: Mouse, PointerEvent: Mouse };
const doc = { elementFromPoint: () => target };
const inputContext = { window: win, document: doc, innerWidth: 1920, innerHeight: 1080, MouseEvent: Mouse };
const click = script('Click').replace(/FX/g, '0.25').replace(/FY/g, '0.5');
vm.runInNewContext(click, inputContext);
assert.deepStrictEqual(events.map(e => e.type), ['pointerdown', 'mousedown', 'pointerup', 'mouseup', 'click']);
assert.strictEqual(events[4].clientX, 480); assert.strictEqual(events[4].clientY, 540);
assert.strictEqual(events[4].buttons, 0); assert.strictEqual(events[4].detail, 1);
target.closest = () => ({}); events.length = 0;
vm.runInNewContext(click, inputContext); assert.strictEqual(events.length, 0, 'Disabled/inert controls cannot be clicked');
doc.elementFromPoint = () => ({ tagName: 'IFRAME', contentDocument: null });
vm.runInNewContext(click, inputContext); assert.strictEqual(events.length, 0, 'Cross-origin frames are not injected');
const wheel = [];
const childWin = { innerWidth: 320, innerHeight: 180, getComputedStyle: () => ({ overflowY: 'visible', overflowX: 'visible' }) };
const childDoc = { defaultView: childWin, elementFromPoint: () => ({ tagName: 'DIV' }),
  scrollingElement: { scrollBy: (x, y) => wheel.push([x, y]) } };
doc.elementFromPoint = () => ({ tagName: 'IFRAME', contentDocument: childDoc,
  getBoundingClientRect: () => ({ left: 0, top: 0 }) });
vm.runInNewContext(script('Scroll').replace(/FX|FY/g, '0.1').replace(/DX/g, '0').replace(/DY/g, '0.1'), inputContext);
assert.deepStrictEqual(wheel, [[0, 18]], 'Nested frames scroll by their own viewport');
console.log('PASS bundled page scripts: passive media observation, active-player metrics, hit coordinates, disabled controls, frame boundary and frame scrolling (DOM doubles only)');
