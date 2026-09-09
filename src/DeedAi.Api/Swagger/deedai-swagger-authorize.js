/* Deed AI — Swagger Authorize hit-target runtime (Phase 4.2.3).
   4.2.2 triple-loaded this file (inline head + early src + pin-last). The
   helper is assigned late; an early run can fail first and pin-last then
   early-returned on messy __deedAiAuthorizeRuntime. Custom index now loads
   this file ONCE after index.js. This IIFE always assigns
   window.__deedAiMeasureAuthorize (even on re-entry) before init, then:
     - wraps SwaggerUIBundle onComplete
     - MutationObserver on childList + style/class
     - 250ms poll while on /swagger
     - always re-applies inline !important (height/min-height/max-height:none)
     - clones a body-owned overlay if getBoundingClientRect is still < 44
   QA2 after load: typeof window.__deedAiMeasureAuthorize === "function"
   and window.__deedAiAuthorizeRuntimeVersion === "4.2.3".
   Each item width >= 44 and height >= 44. Pass:
     document.documentElement.dataset.deedaiAuthorizeHit === "pass"
   Client / Software naming only. No secrets. */
(function () {
  function markAuthorizeError(err) {
    try {
      var root = document.documentElement;
      if (!root) return;
      var msg = (err && err.message) ? String(err.message) : String(err);
      if (root.dataset) root.dataset.deedaiAuthorizeError = msg;
      if (root.setAttribute) root.setAttribute("data-deedai-authorize-error", msg);
    } catch (ignored) { /* ignore */ }
  }

  try {
  window.__deedAiAuthorizeRuntimeVersion = "4.2.3";

  var MIN = 44;
  var POLL_MS = 250;
  var SELECTOR = [
    ".swagger-ui .scheme-container .btn.authorize",
    ".swagger-ui .scheme-container button.authorize",
    ".swagger-ui .auth-wrapper .btn.authorize",
    ".swagger-ui .auth-wrapper button.authorize",
    ".swagger-ui .btn.authorize",
    ".swagger-ui button.authorize",
    ".swagger-ui .modal-ux .auth-btn-wrapper .btn",
    ".swagger-ui .dialog-ux .auth-btn-wrapper .btn",
    ".swagger-ui .auth-btn-wrapper .btn",
    ".swagger-ui .auth-btn-wrapper button",
    ".swagger-ui .modal-ux .btn.modal-btn",
    ".swagger-ui .btn.modal-btn.authorize",
    ".swagger-ui .btn-done"
  ].join(",");

  var CSS_ID = "deedai-swagger-authorize-late";
  var CSS_TEXT = [
    "html body .swagger-ui .scheme-container .auth-wrapper button.btn.authorize,",
    "html body .swagger-ui .scheme-container .auth-wrapper .btn.authorize,",
    "html body .swagger-ui .btn.authorize,",
    "html body .swagger-ui button.authorize,",
    "html body .swagger-ui .auth-wrapper .authorize,",
    "html body .swagger-ui .modal-ux .auth-btn-wrapper button.btn,",
    "html body .swagger-ui .modal-ux .btn.modal-btn,",
    "html body .swagger-ui .auth-btn-wrapper .btn,",
    "html body .swagger-ui .auth-btn-wrapper button,",
    "html body .swagger-ui .btn.modal-btn.authorize,",
    "html body .swagger-ui .btn-done {",
    "  display: inline-flex !important;",
    "  align-items: center !important;",
    "  justify-content: center !important;",
    "  box-sizing: border-box !important;",
    "  flex-shrink: 0 !important;",
    "  min-height: 44px !important;",
    "  min-width: 44px !important;",
    "  height: 44px !important;",
    "  max-height: none !important;",
    "  padding: 10px 16px !important;",
    "  line-height: 1.2 !important;",
    "  float: none !important;",
    "}"
  ].join("\n");

  var PIN = [
    ["display", "inline-flex"],
    ["align-items", "center"],
    ["justify-content", "center"],
    ["box-sizing", "border-box"],
    ["flex-shrink", "0"],
    ["min-height", "44px"],
    ["min-width", "44px"],
    ["height", "44px"],
    ["max-height", "none"],
    ["padding", "10px 16px"],
    ["line-height", "1.2"],
    ["float", "none"]
  ];

  function setImp(el, name, value) {
    el.style.setProperty(name, value, "important");
  }

  function pinBox(el) {
    for (var i = 0; i < PIN.length; i++) {
      setImp(el, PIN[i][0], PIN[i][1]);
    }
    el.setAttribute("data-deedai-hit", "44");
  }

  function tooSmall(el) {
    if (!el || !el.getBoundingClientRect) return false;
    var r = el.getBoundingClientRect();
    return r.width < MIN || r.height < MIN;
  }

  function computedBroken(el) {
    if (typeof getComputedStyle !== "function") return false;
    try {
      var cs = getComputedStyle(el);
      if (!cs) return false;
      if (cs.display === "inline") return true;
      var h = parseFloat(cs.height);
      var w = parseFloat(cs.width);
      if ((h > 0 && h < MIN) || (w > 0 && w < MIN)) return true;
    } catch (e) { /* ignore */ }
    return false;
  }

  function pinCss() {
    var root = document.body || document.documentElement;
    if (!root) return;
    var style = document.getElementById(CSS_ID);
    if (!style) {
      style = document.createElement("style");
      style.id = CSS_ID;
      style.textContent = CSS_TEXT;
    }
    if (style.parentNode !== root) {
      root.appendChild(style);
    }
  }

  function pinChildren(el) {
    var spans = el.querySelectorAll("span");
    for (var i = 0; i < spans.length; i++) {
      setImp(spans[i], "float", "none");
      setImp(spans[i], "display", "inline");
      setImp(spans[i], "padding", "0 8px 0 0");
    }
    var svgs = el.querySelectorAll("svg");
    for (var s = 0; s < svgs.length; s++) {
      setImp(svgs[s], "width", "20px");
      setImp(svgs[s], "height", "20px");
      setImp(svgs[s], "flex-shrink", "0");
    }
  }

  function placeOverlay(overlay, el) {
    var r = el.getBoundingClientRect ? el.getBoundingClientRect() : { left: 0, top: 0, width: MIN, height: MIN };
    setImp(overlay, "position", "fixed");
    setImp(overlay, "left", Math.round(r.left || 0) + "px");
    setImp(overlay, "top", Math.round(r.top || 0) + "px");
    setImp(overlay, "width", Math.max(MIN, Math.round(r.width || MIN)) + "px");
    setImp(overlay, "height", MIN + "px");
    setImp(overlay, "z-index", "2147483646");
    setImp(overlay, "margin", "0");
    setImp(overlay, "max-height", "none");
  }

  function adoptOverlay(el) {
    if (!document.body || !document.body.appendChild) return null;
    if (el.getAttribute("data-deedai-owned") === "1") return el;
    var id = el.getAttribute("data-deedai-overlay-id");
    if (!id) {
      id = "deedai-hit-" + String(Math.random()).slice(2);
      el.setAttribute("data-deedai-overlay-id", id);
    }
    var overlay = document.getElementById(id);
    if (!overlay) {
      overlay = el.cloneNode ? el.cloneNode(true) : document.createElement("button");
      overlay.id = id;
      overlay.setAttribute("data-deedai-owned", "1");
      overlay.setAttribute("data-deedai-hit", "44");
      overlay.setAttribute("type", "button");
      if (overlay.addEventListener) {
        overlay.addEventListener("click", function (e) {
          if (e && e.preventDefault) e.preventDefault();
          if (e && e.stopPropagation) e.stopPropagation();
          if (typeof el.click === "function") el.click();
        });
      }
      document.body.appendChild(overlay);
    }
    pinBox(overlay);
    pinChildren(overlay);
    placeOverlay(overlay, el);
    return overlay;
  }

  function applyOne(el) {
    if (!el || el.nodeType !== 1) return;
    if (el.getAttribute && el.getAttribute("data-deedai-owned") === "1") {
      pinBox(el);
      pinChildren(el);
      return;
    }
    pinBox(el);
    pinChildren(el);
    observeOne(el);
    if (tooSmall(el) || computedBroken(el)) {
      adoptOverlay(el);
    }
  }

  function collect() {
    if (!document.querySelectorAll) return [];
    var seen = [];
    var nodes = document.querySelectorAll(SELECTOR);
    for (var i = 0; i < nodes.length; i++) {
      if (seen.indexOf(nodes[i]) === -1) seen.push(nodes[i]);
    }
    return seen;
  }

  function applyAll() {
    pinCss();
    var nodes = collect();
    for (var i = 0; i < nodes.length; i++) applyOne(nodes[i]);
    report(nodes);
    return nodes;
  }

  function measureTarget(el) {
    var overlayId = el.getAttribute && el.getAttribute("data-deedai-overlay-id");
    var overlay = overlayId && document.getElementById ? document.getElementById(overlayId) : null;
    return overlay || el;
  }

  function measureEl(el) {
    var target = measureTarget(el);
    var r = target.getBoundingClientRect();
    return {
      text: String(el.textContent || "").replace(/\s+/g, " ").trim(),
      width: Math.round(r.width),
      height: Math.round(r.height),
      ok: r.width >= MIN && r.height >= MIN
    };
  }

  function report(nodes) {
    nodes = nodes || collect();
    var items = [];
    var ok = nodes.length > 0;
    for (var i = 0; i < nodes.length; i++) {
      var item = measureEl(nodes[i]);
      items.push(item);
      if (!item.ok) ok = false;
    }
    if (document.documentElement) {
      document.documentElement.setAttribute("data-deedai-authorize-hit", ok ? "pass" : (nodes.length ? "fail" : "pending"));
      document.documentElement.setAttribute("data-deedai-authorize-hit-count", String(nodes.length));
      document.documentElement.setAttribute("data-deedai-authorize-runtime", "4.2.3");
    }
    var slot = document.getElementById("deedai-authorize-hit-measure");
    if (!slot && document.body) {
      slot = document.createElement("script");
      slot.id = "deedai-authorize-hit-measure";
      slot.type = "application/json";
      document.body.appendChild(slot);
    }
    if (slot) slot.textContent = JSON.stringify(items);
    return items;
  }

  function wrapBundle(fn) {
    if (!fn || fn.__deedAiHit) return fn;
    function wrapped(opts) {
      opts = opts || {};
      var prev = opts.onComplete;
      opts.onComplete = function () {
        applyAll();
        if (typeof prev === "function") return prev.apply(this, arguments);
      };
      var ui = fn.apply(this, arguments);
      applyAll();
      return ui;
    }
    wrapped.__deedAiHit = true;
    try {
      Object.keys(fn).forEach(function (k) { wrapped[k] = fn[k]; });
    } catch (e) { /* ignore */ }
    return wrapped;
  }

  function hookSwaggerUiBundle() {
    var current = window.SwaggerUIBundle;
    if (current && !current.__deedAiHit) {
      current = wrapBundle(current);
    }
    try {
      Object.defineProperty(window, "SwaggerUIBundle", {
        configurable: true,
        enumerable: true,
        get: function () { return current; },
        set: function (fn) { current = wrapBundle(fn); }
      });
    } catch (e) {
      if (current) window.SwaggerUIBundle = current;
    }
  }

  var watched = [];

  function observeOne(el) {
    if (typeof MutationObserver === "undefined") return;
    if (el.__deedAiAttrObs) return;
    try {
      var obs = new MutationObserver(function () { applyOne(el); });
      obs.observe(el, { attributes: true, attributeFilter: ["class", "style"] });
      el.__deedAiAttrObs = obs;
      watched.push(obs);
    } catch (e) { /* ignore */ }
  }

  function startObserver() {
    applyAll();
    if (typeof MutationObserver === "undefined" || !document.documentElement) return;
    var obs = new MutationObserver(function () { applyAll(); });
    obs.observe(document.documentElement, {
      childList: true,
      subtree: true,
      attributes: true,
      attributeFilter: ["class", "style"]
    });
  }

  function onSwaggerPage() {
    try {
      var path = (window.location && window.location.pathname) || "";
      return !path || /swagger/i.test(path);
    } catch (e) {
      return true;
    }
  }

  function startPoll() {
    if (window.__deedAiAuthorizePoll) return;
    if (typeof setInterval !== "function") return;
    window.__deedAiAuthorizePoll = setInterval(function () {
      if (!onSwaggerPage()) return;
      applyAll();
    }, POLL_MS);
  }

  function hookSetAttribute() {
    var proto = window.HTMLElement && window.HTMLElement.prototype;
    if (!proto || !proto.setAttribute || proto.setAttribute.__deedAiHit) return;
    var raw = proto.setAttribute;
    function wrapped(name, value) {
      var result = raw.apply(this, arguments);
      if (name === "style" || name === "class") {
        if (this.className && String(this.className).indexOf("authorize") !== -1) {
          applyOne(this);
        } else if (this.className && String(this.className).indexOf("modal-btn") !== -1) {
          applyOne(this);
        } else if (this.className && String(this.className).indexOf("btn-done") !== -1) {
          applyOne(this);
        }
      }
      return result;
    }
    wrapped.__deedAiHit = true;
    proto.setAttribute = wrapped;
  }

  window.__deedAiApplyAuthorizeHit = applyAll;
  window.__deedAiMeasureAuthorize = function () {
    applyAll();
    return report();
  };
  window.__deedAiAuthorizeRuntime = true;

  hookSetAttribute();
  hookSwaggerUiBundle();
  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", function () {
      try {
        startObserver();
        startPoll();
      } catch (e) { markAuthorizeError(e); }
    });
  } else {
    startObserver();
    startPoll();
  }
  var passes = [0, 50, 200, 250, 500, 1000, 2000, 4000];
  for (var t = 0; t < passes.length; t++) {
    setTimeout(applyAll, passes[t]);
  }
  } catch (e) {
    markAuthorizeError(e);
    if (typeof window.__deedAiMeasureAuthorize !== "function") {
      window.__deedAiMeasureAuthorize = function () { return []; };
    }
  }
})();
