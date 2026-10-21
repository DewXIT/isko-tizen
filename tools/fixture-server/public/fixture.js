(function () {
  'use strict';
  var output = document.getElementById('output'), video = document.getElementById('video'), timer, messages = [], waitingAt, errors = 0, buffering = 0;
  var query = new URLSearchParams(location.search), reporting = query.get('report') === '1';
  if (query.get('run') && /^[a-zA-Z0-9_-]{1,48}$/.test(query.get('run'))) document.getElementById('run').value = query.get('run');
  function report(name, data) { if (!reporting || !/^[a-z0-9-]{1,40}$/.test(name)) return; new Image().src = '/api/event?run=' + encodeURIComponent(document.getElementById('run').value) + '&name=' + name + '&d=' + encodeURIComponent(JSON.stringify(data || {}).slice(0, 200)) + '&n=' + Date.now(); }
  function log(name, data) { report(name, data); messages.push(JSON.stringify({ event: name, ms: Math.round(performance.now()), data: data || {} })); if (messages.length > 60) messages.shift(); output.textContent = messages.join('\n'); }
  function run() { var value = document.getElementById('run').value; if (!/^[a-zA-Z0-9_-]{1,48}$/.test(value)) throw new Error('Invalid run ID'); return value; }
  function crossOrigin() { return window.FIXTURE_CROSS_ORIGIN || location.protocol + '//' + location.hostname + ':' + window.FIXTURE_PORTS[1]; }
  function target() { return '/arrived?run=' + run(); }
  // A different host, so it is a different site: localhost versus 127.0.0.1 unless a cross origin is configured.
  function crossSite() { if (window.FIXTURE_CROSS_ORIGIN) return window.FIXTURE_CROSS_ORIGIN; return location.protocol + '//' + (location.hostname === 'localhost' ? '127.0.0.1' : 'localhost') + ':' + location.port; }
  function bind(id, fn) { document.getElementById(id).onclick = function () { try { fn(); } catch (error) { log('fixture-error', { name: error.name }); } }; }
  function request(url, method) { var xhr = new XMLHttpRequest(); xhr.open(method || 'GET', url); xhr.onload = function () { log('server-response', JSON.parse(xhr.responseText)); }; xhr.onerror = function () { log('server-unreachable'); }; xhr.send(); }
  function links() { document.getElementById('blank').href = target(); document.getElementById('normal').href = target(); document.getElementById('dl-attr').href = '/download/sample.zip?run=' + run(); document.getElementById('dl-plain').href = '/download/sample.zip?run=' + run(); }
  document.getElementById('run').onchange = links; links();
  bind('reset', function () { request('/api/reset?run=' + run(), 'POST'); });
  bind('counts', function () { request('/api/counters?run=' + run()); });
  bind('popup', function () { var child = window.open(target(), '_blank'); log('popup-attempt', { returnedWindow: !!child }); });
  bind('popup-cross', function () { var child = window.open(crossSite() + target(), '_blank'); log('cross-popup-attempt', { returnedWindow: !!child }); });
  bind('popup-delayed', function () { setTimeout(function () { var child = window.open(target(), '_blank'); log('delayed-popup-attempt', { returnedWindow: !!child }); }, 5000); log('delayed-popup-armed'); });
  bind('redirect', function () { clearTimeout(timer); var url = crossOrigin() + target(); timer = setTimeout(function () { location.href = url; }, 3000); log('redirect-armed'); });
  bind('cancel', function () { clearTimeout(timer); log('redirect-cancelled'); });
  bind('probe', function () { var img = new Image(); img.alt = 'ad request probe'; img.onload = function () { log('probe-loaded'); }; img.onerror = function () { log('probe-error-not-proof-of-block'); }; img.src = '/ad-probe?run=' + run() + '&nonce=' + Date.now(); document.getElementById('ad-slot').appendChild(img); });
  bind('arm-trap', function () {
    var old = document.getElementById('click-trap'); if (old) old.remove();
    var trap = document.createElement('a'), clicks = 0; trap.id = 'click-trap'; trap.href = target(); trap.target = '_blank';
    trap.style.cssText = 'position:fixed;top:0;left:0;width:100%;height:100%;z-index:2147483647;background:transparent';
    trap.onclick = function () { clicks++; log('trap-click', { n: clicks }); if (clicks >= 3) trap.remove(); };
    document.body.appendChild(trap); log('trap-armed');
  });
  bind('arm-interstitial', function () {
    var old = document.getElementById('fixture-interstitial'); if (old) old.remove();
    var host = document.createElement('div'); host.id = 'fixture-interstitial';
    var root = host.attachShadow({ mode: 'open' });
    root.innerHTML = '<div id="modal" style="position:fixed;top:0;left:0;width:100%;height:100%;z-index:2147483646;background:#222;color:#fff;display:flex;align-items:center;justify-content:center"><div>FIXTURE INTERSTITIAL <a id="go" style="color:#8edcff" target="_blank">Go to website</a> <button id="skip">Skip</button></div></div>';
    root.getElementById('go').href = crossSite() + target();
    root.getElementById('skip').onclick = function () { host.remove(); log('interstitial-skipped-by-click'); };
    document.body.appendChild(host); log('interstitial-armed');
  });
  bind('state', function () { log('layer-state', { interstitial: !!document.getElementById('fixture-interstitial'), trap: !!document.getElementById('click-trap') }); });
  bind('dl-script', function () { var a = document.createElement('a'); a.href = '/download/sample.zip?run=' + run(); a.setAttribute('download', 'fixture.zip'); document.body.appendChild(a); a.click(); a.remove(); log('download-script-click'); });
  document.getElementById('dl-attr').addEventListener('click', function () { log('download-attr-click'); });
  document.getElementById('dl-plain').addEventListener('click', function () { log('download-plain-click'); });
  bind('bait-start', function () {
    var left = 12;
    var timer = setInterval(function () {
      var bait = document.createElement('div'); bait.id = 'div-gpt-ad-123456789-0'; bait.style.cssText = 'width:3px;height:24px;position:fixed;bottom:0;right:0';
      document.body.appendChild(bait);
      setTimeout(function () { var hidden = bait.offsetHeight === 0 || getComputedStyle(bait).display === 'none'; log(hidden ? 'bait-hidden' : 'bait-visible'); bait.remove(); }, 500);
      if (--left <= 0) clearInterval(timer);
    }, 1000);
  });
  bind('play-stream', function () { var xhr = new XMLHttpRequest(); xhr.open('GET', '/media/hls/index.m3u8?run=' + run()); xhr.onload = function () { log('manifest-fetched', { status: xhr.status, bytes: xhr.responseText.length }); }; xhr.onerror = function () { log('manifest-error'); }; xhr.send(); log('stream-requested'); });
  bind('trap-play', function () { log('player-click'); });
  bind('hide', function () { document.getElementById('isko-test-banner').style.display = 'none'; log('cosmetic-control'); });
  bind('show', function () { document.getElementById('isko-test-banner').style.display = ''; });
  bind('frames', function () { var slot = document.getElementById('frame-slot'); slot.innerHTML = ''; [location.port, String(window.FIXTURE_PORTS[1])].forEach(function (port, index) { var frame = document.createElement('iframe'); frame.title = index ? 'Cross origin' : 'Same origin'; frame.src = (index ? crossOrigin() : location.protocol + '//' + location.hostname + ':' + port) + '/frame?run=' + run() + '&x=' + encodeURIComponent(crossSite()); slot.appendChild(frame); }); });
  function cleanup() { video.pause(); video.removeAttribute('src'); video.load(); log('video-source-released'); }
  bind('load', function () { cleanup(); video.src = '/media/sample.mp4'; video.load(); });
  bind('switch', function () { cleanup(); video.src = '/media/sample.mp4?v=2'; video.load(); log('source-switched-identical-content'); });
  bind('play', function () { if (video.paused) { var promise = video.play(); if (promise && promise.catch) promise.catch(function (error) { log('play-rejected', { name: error.name }); }); } else video.pause(); });
  function fullscreen() { var fn = video.requestFullscreen || video.webkitRequestFullscreen; if (!fn) return log('fullscreen-unavailable'); var promise = fn.call(video); if (promise && promise.catch) promise.catch(function (error) { log('fullscreen-rejected', { name: error.name }); }); }
  function exit() { var fn = document.exitFullscreen || document.webkitExitFullscreen; if (fn && (document.fullscreenElement || document.webkitFullscreenElement)) fn.call(document); }
  bind('fullscreen', fullscreen); bind('exit', exit); bind('cleanup', cleanup);
  bind('clear', function () { messages = []; output.textContent = ''; });
  document.addEventListener('keydown', function (event) { if (event.target.tagName === 'INPUT') return; if (event.keyCode === 70) fullscreen(); if (event.keyCode === 27 || event.keyCode === 10009) exit(); });
  document.addEventListener('visibilitychange', function () { log('visibility', { hidden: document.hidden, paused: video.paused }); });
  window.addEventListener('pagehide', cleanup);
  ['loadedmetadata', 'playing', 'pause', 'ended', 'emptied'].forEach(function (name) { video.addEventListener(name, function () { if (name === 'playing' && waitingAt) { buffering += performance.now() - waitingAt; waitingAt = 0; } log('video-' + name); }); });
  video.addEventListener('waiting', function () { if (!waitingAt) waitingAt = performance.now(); log('video-waiting'); });
  video.addEventListener('error', function () { errors++; log('video-error', { code: video.error && video.error.code }); });
  setInterval(function () { var quality = video.getVideoPlaybackQuality ? video.getVideoPlaybackQuality() : null; log('metrics', { errors: errors, bufferingMs: Math.round(buffering + (waitingAt ? performance.now() - waitingAt : 0)), currentTime: video.currentTime, droppedFrames: quality ? quality.droppedVideoFrames : null, totalFrames: quality ? quality.totalVideoFrames : null, jsHeapBytes: performance.memory ? performance.memory.usedJSHeapSize : null, memoryScope: 'page JS heap only, not process' }); }, 10000);
  log('fixture-ready', { navigationMs: Math.round(performance.now()) });
}());
