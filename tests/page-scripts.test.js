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
    location: { hostname: 'watch.example.org', href: 'https://watch.example.org/' }, getComputedStyle: el => el.style };
  const d = { elementsFromPoint: () => stack, elementFromPoint: () => stack[0] };
  vm.runInNewContext(click, { window: w, document: d, innerWidth: 1920, innerHeight: 1080, MouseEvent: Mouse, URL });
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
// Heuristic refinements: a transparent click-away backdrop at an ordinary z-index is not a trap; an empty
// full-viewport link to another site is, whatever its z-index.
const backdrop = layer('DIV', { style: { position: 'fixed', zIndex: '1300' } }), beneathBackdrop = layer('DIV', { getBoundingClientRect: () => ({ width: 100, height: 100 }) });
clickWith([backdrop, beneathBackdrop]);
assert.strictEqual(backdrop.events.length, 5, 'A transparent backdrop at an ordinary z-index still receives the click');
assert.strictEqual(beneathBackdrop.events.length, 0);
const adLink = layer('A', { href: 'https://ads.example.net/x', style: { position: 'fixed', zIndex: '100' } }), beneathLink = layer('DIV', { getBoundingClientRect: () => ({ width: 100, height: 100 }) });
clickWith([adLink, beneathLink]);
assert.strictEqual(adLink.events.length, 0, 'An empty full-viewport link to another site is skipped');
assert.strictEqual(beneathLink.events.length, 5);
const ownLink = layer('A', { href: 'https://watch.example.org/next', style: { position: 'fixed', zIndex: '100' } });
clickWith([ownLink, beneathLink]);
assert.strictEqual(ownLink.events.length, 5, 'An empty full-viewport link to the same site keeps the click');
const loneTrap = layer('A', { style: { position: 'fixed', zIndex: '2147483647' } });
clickWith([loneTrap]);
assert.strictEqual(loneTrap.events.length, 5, 'When nothing is under a trap, the original topmost element is used');
const broken = layer('DIV', {}); broken.getBoundingClientRect = () => { throw new Error('detached'); };
clickWith([broken, player]); assert.strictEqual(broken.events.length, 5, 'Inspection failure means ordinary click');
// Overlay sweep: a viewport-sized max-z layer (also inside a shadow root) is removed; ordinary layers are kept.
function sweepRun(children, on = true, fullscreen = null) {
  let observer = null;
  const ctx = { window: {}, innerWidth: 1920, innerHeight: 1080, location: { hostname: 'watch.example.org', href: 'https://watch.example.org/' },
    URL, getComputedStyle: el => el.cs, MutationObserver: class { constructor(fn) { observer = fn; } observe() {} },
    document: { body: { children }, fullscreenElement: fullscreen } };
  const body = script('SweepBody');
  vm.runInNewContext('(()=>{window.__iskoSweepOn=' + on + ';' + body + '})()', ctx);
  return { ctx, observer };
}
function box(over = {}) {
  const el = Object.assign({ nodeType: 1, shadowRoot: null, removed: false, hidden: false,
    cs: { position: 'fixed', zIndex: '2147483647' }, getBoundingClientRect: () => ({ width: 1920, height: 1080 }),
    querySelector: () => null, querySelectorAll: () => [],
    style: { setProperty(name, value, priority) { el.hidden = name === 'display' && value === 'none' && priority === 'important'; } },
    remove() { el.removed = true; } }, over);
  return el;
}
const modalInShadow = box({ cs: { position: 'static', zIndex: 'auto' }, getBoundingClientRect: () => ({ width: 1920, height: 0 }) });
modalInShadow.shadowRoot = { children: [box()] };
const trapDiv = box(), cookieBar = box({ cs: { position: 'fixed', zIndex: '9999' } });
const formModal = box({ querySelector: () => ({}) });
const ownVideo = box({ querySelectorAll: () => [{ currentSrc: 'https://cdn.watch.example.org/live.m3u8' }] });
const foreignVideo = box({ querySelectorAll: () => [{ currentSrc: 'https://ads.example.net/clip.mp4' }] });
const smallLayer = box({ getBoundingClientRect: () => ({ width: 300, height: 200 }) });
const normal = box({ cs: { position: 'static', zIndex: 'auto' } });
sweepRun([modalInShadow, trapDiv, cookieBar, formModal, ownVideo, foreignVideo, smallLayer, normal]);
assert(modalInShadow.removed && modalInShadow.hidden, 'A max-z modal inside a shadow root is hidden and removed');
assert(trapDiv.removed, 'A viewport-sized max-z layer is removed');
assert(foreignVideo.removed, 'A layer with a third-party ad video is removed');
for (const keep of [cookieBar, formModal, ownVideo, smallLayer, normal]) assert(!keep.removed, 'Ordinary, form, first-party or small layers are kept');
const mseOwn = box({ querySelectorAll: () => [{ currentSrc: 'blob:https://watch.example.org/4f2c-uuid' }] });
const mseForeign = box({ querySelectorAll: () => [{ currentSrc: 'blob:https://ads.example.net/9a1b-uuid' }] });
sweepRun([mseOwn, mseForeign]);
assert(!mseOwn.removed, 'A first-party player using a blob: (MSE) source is kept');
assert(mseForeign.removed, 'A blob: source created by another site does not protect a layer');
const off = box(); sweepRun([off], false); assert(!off.removed, 'Disabled protection leaves layers alone');
const inFs = box(); sweepRun([inFs], true, {}); assert(!inFs.removed, 'No sweeping while real fullscreen is active');
const late = [], lateEl = box(); const run = sweepRun(late);
late.push(lateEl); run.observer([{ addedNodes: [lateEl] }]); assert(lateEl.removed, 'A layer added later is removed by the observer');
{
  // The observer examines only the nodes it is told were added, not every child of body again.
  const kept = box(), added = box(), kids = [kept]; const r2 = sweepRun(kids, false);
  kept.removed = false; r2.ctx.window.__iskoSweepOn = true; kids.push(added);
  r2.observer([{ addedNodes: [added] }]);
  assert(added.removed && !kept.removed, 'Existing children are not re-scanned on every mutation');
}
assert.strictEqual(run.ctx.window.__iskoSwept, 1, 'Swept layers are counted');
// Download links: a[download] is never followed, programmatically or by click.
{
  const listeners = [], proto = { clicked: 0, click() { this.clicked++; } };
  class Anchor { constructor(dl) { this.dl = dl; } hasAttribute(n) { return n === 'download' && this.dl; } }
  Anchor.prototype.click = proto.click;
  const ctx = { window: {}, HTMLAnchorElement: Anchor, document: { addEventListener: (n, fn) => listeners.push([n, fn]) } };
  vm.runInNewContext(script('BlockDownloadLinks'), ctx); vm.runInNewContext(script('BlockDownloadLinks'), ctx);
  assert.strictEqual(listeners.length, 1, 'Installed once');
  const dl = new Anchor(true), plain = new Anchor(false); dl.clicked = 0; plain.clicked = 0;
  Anchor.prototype.click.call(dl); Anchor.prototype.click.call(plain);
  assert.strictEqual(dl.clicked, 0, 'a[download].click() is swallowed'); assert.strictEqual(plain.clicked, 1, 'ordinary link click() works');
  let prevented = 0, stopped = 0;
  const ev = (a) => ({ target: { closest: s => (s === 'a[download]' && a ? a : null) }, preventDefault() { prevented++; }, stopImmediatePropagation() { stopped++; } });
  listeners[0][1](ev(true)); assert.strictEqual(prevented, 1); assert.strictEqual(stopped, 1);
  listeners[0][1](ev(false)); assert.strictEqual(prevented, 1, 'Ordinary clicks untouched');
}
const wheel = [];
const childWin = { innerWidth: 320, innerHeight: 180, getComputedStyle: () => ({ overflowY: 'visible', overflowX: 'visible' }) };
const childDoc = { defaultView: childWin, elementFromPoint: () => ({ tagName: 'DIV' }),
  scrollingElement: { scrollBy: (x, y) => wheel.push([x, y]) } };
doc.elementFromPoint = () => ({ tagName: 'IFRAME', contentDocument: childDoc,
  getBoundingClientRect: () => ({ left: 0, top: 0 }) });
vm.runInNewContext(script('Scroll').replace(/FX|FY/g, '0.1').replace(/DX/g, '0').replace(/DY/g, '0.1'), inputContext);
assert.deepStrictEqual(wheel, [[0, 18]], 'Nested frames scroll by their own viewport');
console.log('PASS bundled page scripts: passive media observation, active-player metrics, hit coordinates, click-trap skipping, overlay sweep, download-link guard, disabled controls, frame boundary and frame scrolling (DOM doubles only)');
