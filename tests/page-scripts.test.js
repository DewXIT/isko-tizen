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
// Click traps: the viewport-sized empty layer is skipped, ordinary and content-bearing layers are not.
function layer(tag, over) {
  const style = Object.assign({ position: 'static', backgroundColor: 'rgba(0, 0, 0, 0)', backgroundImage: 'none', opacity: '1', zIndex: 'auto' }, over.style);
  const events = [];
  return Object.assign({ tagName: tag, textContent: '', children: [], closest: () => null, focus() {},
    getElementsByTagName: () => [], querySelector: () => null,
    getBoundingClientRect: () => ({ width: 1920, height: 1080 }), events,
    dispatchEvent(e) { events.push(e.type); return true; } }, over, { style });
}
function clickWith(stack) {
  const w = { innerWidth: 1920, innerHeight: 1080, MouseEvent: Mouse, PointerEvent: Mouse,
    getComputedStyle: el => el.style };
  const d = { elementsFromPoint: () => stack, elementFromPoint: () => stack[0] };
  vm.runInNewContext(click, { window: w, document: d, innerWidth: 1920, innerHeight: 1080, MouseEvent: Mouse });
}
const player = layer('DIV', { getBoundingClientRect: () => ({ width: 997, height: 561 }) });
let trapLayer = layer('A', { style: { position: 'fixed', zIndex: '2147483647' } });
clickWith([trapLayer, player]);
assert.strictEqual(trapLayer.events.length, 0, 'An empty viewport-sized fixed layer must not receive the click');
assert.strictEqual(player.events.length, 5, 'The element under the trap receives the click');
for (const variant of [
  { style: { position: 'fixed', backgroundColor: 'rgba(0, 0, 0, 0.7)', zIndex: '10' }, textContent: 'Accept cookies' },
  { style: { position: 'fixed', backgroundColor: 'rgb(0, 0, 0)', zIndex: '10' } },
  { style: { position: 'fixed', zIndex: '10' }, querySelector: () => ({}) },
  { style: { position: 'static' } },
  { style: { position: 'fixed' }, getBoundingClientRect: () => ({ width: 300, height: 200 }) },
]) {
  const other = layer('DIV', {}), below = layer('DIV', { getBoundingClientRect: () => ({ width: 100, height: 100 }) });
  Object.assign(other, variant, { style: Object.assign({ position: 'static', backgroundColor: 'rgba(0, 0, 0, 0)', backgroundImage: 'none', opacity: '1', zIndex: 'auto' }, variant.style) });
  clickWith([other, below]);
  assert.strictEqual(other.events.length, 5, 'A real modal, opaque layer or small element still receives the click');
  assert.strictEqual(below.events.length, 0);
}
const ghostLayer = layer('DIV', { style: { position: 'fixed', zIndex: '2147483647', opacity: '0' }, getBoundingClientRect: () => ({ width: 339, height: 672 }) });
const underGhost = layer('DIV', { getBoundingClientRect: () => ({ width: 100, height: 100 }) });
clickWith([ghostLayer, underGhost]);
assert.strictEqual(ghostLayer.events.length, 0, 'An invisible extreme-z layer over part of the page is skipped');
assert.strictEqual(underGhost.events.length, 5);
const faintSmall = layer('DIV', { style: { position: 'fixed', zIndex: '50', opacity: '0' }, getBoundingClientRect: () => ({ width: 339, height: 672 }) });
clickWith([faintSmall, underGhost]);
assert.strictEqual(faintSmall.events.length, 5, 'A partial invisible layer at an ordinary z-index is left alone');
const loneTrap = layer('A', { style: { position: 'fixed', zIndex: '2147483647' } });
clickWith([loneTrap]);
assert.strictEqual(loneTrap.events.length, 5, 'When nothing is under a trap, the original topmost element is used');
const broken = layer('DIV', {}); broken.getBoundingClientRect = () => { throw new Error('detached'); };
clickWith([broken, player]); assert.strictEqual(broken.events.length, 5, 'Inspection failure means ordinary click');
const wheel = [];
const childWin = { innerWidth: 320, innerHeight: 180, getComputedStyle: () => ({ overflowY: 'visible', overflowX: 'visible' }) };
const childDoc = { defaultView: childWin, elementFromPoint: () => ({ tagName: 'DIV' }),
  scrollingElement: { scrollBy: (x, y) => wheel.push([x, y]) } };
doc.elementFromPoint = () => ({ tagName: 'IFRAME', contentDocument: childDoc,
  getBoundingClientRect: () => ({ left: 0, top: 0 }) });
vm.runInNewContext(script('Scroll').replace(/FX|FY/g, '0.1').replace(/DX/g, '0').replace(/DY/g, '0.1'), inputContext);
assert.deepStrictEqual(wheel, [[0, 18]], 'Nested frames scroll by their own viewport');
console.log('PASS bundled page scripts: passive media observation, active-player metrics, hit coordinates, click-trap skipping, disabled controls, frame boundary and frame scrolling (DOM doubles only)');
