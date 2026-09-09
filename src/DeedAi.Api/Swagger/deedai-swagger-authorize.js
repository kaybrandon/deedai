/* Deed AI — Swagger Authorize hit-target runtime (Phase 4.2.1).
   QA2 measure after Swagger paints (top bar and authorize-modal):
     window.__deedAiMeasureAuthorize()
   Each item must have width >= 44 and height >= 44 (getBoundingClientRect).
   Pass marker: document.documentElement.dataset.deedaiAuthorizeHit === "pass"
   Client / Software naming only. No secrets. */
(function () {
  var MIN = 44;
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
    "html body .swagger-ui .btn.authorize,",
    "html body .swagger-ui button.authorize,",
    "html body .swagger-ui .auth-wrapper .authorize,",
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
    "  padding: 10px 16px !important;",
    "  line-height: 1.2 !important;",
    "  float: none !important;",
    "}"
  ].join("\n");

  function setImp(el, name, value) {
    el.style.setProperty(name, value, "important");
  }

  function alreadyPinned(el) {
    return el.getAttribute("data-deedai-hit") === "44"
      && el.style.getPropertyValue("min-height") === "44px"
      && el.style.getPropertyPriority("min-height") === "important"
      && el.style.getPropertyValue("display") === "inline-flex"
      && el.style.getPropertyPriority("display") === "important"
      && el.style.getPropertyValue("height") === "44px";
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

  function applyOne(el) {
    if (!el || el.nodeType !== 1) return;
    if (alreadyPinned(el)) return;
    setImp(el, "display", "inline-flex");
    setImp(el, "align-items", "center");
    setImp(el, "justify-content", "center");
    setImp(el, "box-sizing", "border-box");
    setImp(el, "flex-shrink", "0");
    setImp(el, "min-height", "44px");
    setImp(el, "min-width", "44px");
    setImp(el, "height", "44px");
    setImp(el, "padding", "10px 16px");
    setImp(el, "line-height", "1.2");
    setImp(el, "float", "none");
    el.setAttribute("data-deedai-hit", "44");
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

  function measureEl(el) {
    var r = el.getBoundingClientRect();
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
      return fn.apply(this, arguments);
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

  window.__deedAiApplyAuthorizeHit = applyAll;
  window.__deedAiMeasureAuthorize = function () {
    applyAll();
    return report();
  };

  hookSwaggerUiBundle();
  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", startObserver);
  } else {
    startObserver();
  }
  var passes = [0, 50, 200, 500, 1000, 2000, 4000];
  for (var t = 0; t < passes.length; t++) {
    setTimeout(applyAll, passes[t]);
  }
})();
