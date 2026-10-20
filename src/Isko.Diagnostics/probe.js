(function () {
  'use strict';
  var started = Date.now();
  var browserMode = /^https?:$/.test(location.protocol);
  function read(fn) {
    try { var value = fn(); return value === undefined || value === null ? 'unavailable' : value; }
    catch (_) { return 'unavailable'; }
  }
  function refresh() {
    var result = {
      probeVersion: '0.2.0',
      mode: browserMode ? 'TV browser (limited diagnostics)' : 'packaged Web app',
      model: read(function () { return webapis.productinfo.getModel(); }),
      firmware: read(function () { return webapis.productinfo.getFirmware(); }),
      platformVersion: read(function () { return tizen.systeminfo.getCapability('http://tizen.org/feature/platform.version'); }),
      platformName: read(function () { return tizen.systeminfo.getCapability('http://tizen.org/feature/platform.version.name'); }),
      webApiVersion: read(function () { return tizen.systeminfo.getCapability('http://tizen.org/feature/platform.web.api.version'); }),
      userAgent: navigator.userAgent,
      browserReportedTizenVersion: read(function () {
        var match = /\bTizen[ /]([0-9]+(?:\.[0-9]+)*)/i.exec(navigator.userAgent);
        return match ? match[1] : null;
      }),
      screen: screen.width + 'x' + screen.height,
      timeSinceProbeStartMs: Date.now() - started,
      nativeWebViewCompatibility: 'not tested',
      requestInterception: 'not tested'
    };
    document.getElementById('result').textContent = JSON.stringify(result, null, 2);
  }
  function start() {
    document.getElementById('refresh').addEventListener('click', refresh);
    document.addEventListener('keydown', function (event) {
      if (!browserMode && (event.keyCode === 10009 || event.key === 'Escape')) {
        try { tizen.application.getCurrentApplication().exit(); event.preventDefault(); } catch (_) {}
      }
    });
    document.addEventListener('visibilitychange', function () { if (!document.hidden) refresh(); });
    refresh();
    document.getElementById('refresh').focus();
  }
  if (browserMode) start();
  else {
    var apiScript = document.createElement('script');
    apiScript.src = '$WEBAPIS/webapis/webapis.js';
    apiScript.onload = apiScript.onerror = start;
    document.head.appendChild(apiScript);
  }
}());
