#!/usr/bin/env node
/**
 * Phase 4.2.3 — prove the Authorize runtime survives Swagger React resetting
 * display:inline AFTER our first pin (the live Azure #18 / #QA2 failure).
 *
 * 1. Helper is defined after script eval (even on re-entry / messy runtime flag).
 * 2. Pin top-bar + modal Authorize / Close to inline !important 44px.
 * 3. Simulate Swagger re-applying display:inline, height:auto, max-height:30px.
 * 4. Fire MutationObserver / 250ms poll (do not call measure yet).
 * 5. getBoundingClientRect must still be ≥44×44 (display:inline ignores height).
 *
 * On a real page after load: window.__deedAiMeasureAuthorize()
 */
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..", "..");
const js = readFileSync(
  join(root, "src", "DeedAi.Api", "Swagger", "deedai-swagger-authorize.js"),
  "utf8"
);

if (!js.includes("POLL_MS = 250")) {
  throw new Error("runtime must poll every 250ms while on /swagger");
}
if (!js.includes("max-height") || !js.includes("none")) {
  throw new Error("runtime must force max-height:none so modal cannot clip to ~30px");
}
if (!js.includes("__deedAiAuthorizeRuntimeVersion")) {
  throw new Error("runtime must expose version for Dev self-verify");
}
if (!js.includes("__deedAiAuthorizeRuntimeVersion = \"4.2.3\"")) {
  throw new Error("runtime version must be 4.2.3");
}
if (!js.includes("deedaiAuthorizeError")) {
  throw new Error("runtime must mark document.documentElement.dataset.deedaiAuthorizeError on init failure");
}
if (js.includes("if (typeof window.__deedAiMeasureAuthorize === \"function\") return;")) {
  throw new Error("must not return before assigning __deedAiMeasureAuthorize");
}

function makeStyle() {
  const values = Object.create(null);
  const pri = Object.create(null);
  return {
    setProperty(name, value, priority) {
      values[name] = value;
      pri[name] = priority || "";
    },
    getPropertyValue(name) {
      return values[name] || "";
    },
    getPropertyPriority(name) {
      return pri[name] || "";
    }
  };
}

function rectFor(el) {
  const display = el.style.getPropertyValue("display");
  const displayImp = el.style.getPropertyPriority("display");
  const height = el.style.getPropertyValue("height");
  const maxH = el.style.getPropertyValue("max-height");
  // Real CSS: height / min-height / max-height do not apply to display:inline.
  if (display === "inline" || display === "") {
    return { width: 34, height: 34, top: 0, left: 0, right: 34, bottom: 34 };
  }
  if (maxH && maxH !== "none" && parseFloat(maxH) > 0 && parseFloat(maxH) < 44) {
    return { width: 96, height: parseFloat(maxH), top: 0, left: 0, right: 96, bottom: parseFloat(maxH) };
  }
  const pinned = display === "inline-flex"
    && displayImp === "important"
    && height === "44px";
  return {
    width: pinned ? 96 : 34,
    height: pinned ? 44 : 34,
    top: 0,
    left: 0,
    right: pinned ? 96 : 34,
    bottom: pinned ? 44 : 34
  };
}

function makeEl(tag, className, text) {
  const attrs = Object.create(null);
  const children = [];
  const el = {
    nodeType: 1,
    tagName: tag.toUpperCase(),
    className,
    textContent: text,
    style: makeStyle(),
    parentNode: null,
    children,
    id: "",
    getAttribute(name) {
      return Object.prototype.hasOwnProperty.call(attrs, name) ? attrs[name] : null;
    },
    setAttribute(name, value) {
      attrs[name] = String(value);
      if (name === "id") el.id = String(value);
    },
    cloneNode() {
      const copy = makeEl(tag, className, text);
      copy.style = makeStyle();
      return copy;
    },
    querySelectorAll(sel) {
      if (sel === "span") return children.filter((c) => c.tagName === "SPAN");
      if (sel === "svg") return children.filter((c) => c.tagName === "SVG");
      return [];
    },
    getBoundingClientRect() {
      return rectFor(el);
    }
  };
  return el;
}

const top = makeEl("button", "btn authorize unlocked", "Authorize");
top.children.push(makeEl("span", "", "Authorize"));
top.children.push(makeEl("svg", "", ""));
const modalAuth = makeEl("button", "btn modal-btn auth authorize", "Authorize");
const modalClose = makeEl("button", "btn modal-btn auth btn-done", "Close");

const bySelector = {
  ".swagger-ui .scheme-container .btn.authorize": [top],
  ".swagger-ui .scheme-container button.authorize": [top],
  ".swagger-ui .auth-wrapper .btn.authorize": [top],
  ".swagger-ui .auth-wrapper button.authorize": [top],
  ".swagger-ui .btn.authorize": [top],
  ".swagger-ui button.authorize": [top],
  ".swagger-ui .modal-ux .auth-btn-wrapper .btn": [modalAuth, modalClose],
  ".swagger-ui .dialog-ux .auth-btn-wrapper .btn": [modalAuth, modalClose],
  ".swagger-ui .auth-btn-wrapper .btn": [modalAuth, modalClose],
  ".swagger-ui .auth-btn-wrapper button": [modalAuth, modalClose],
  ".swagger-ui .modal-ux .btn.modal-btn": [modalAuth, modalClose],
  ".swagger-ui .btn.modal-btn.authorize": [modalAuth],
  ".swagger-ui .btn-done": [modalClose]
};

const created = [];
const documentElement = { nodeType: 1, attrs: Object.create(null), dataset: Object.create(null) };
documentElement.getAttribute = (n) => documentElement.attrs[n] || null;
documentElement.setAttribute = (n, v) => { documentElement.attrs[n] = String(v); };

const body = {
  nodeType: 1,
  appendChild(node) {
    created.push(node);
    node.parentNode = body;
    return node;
  }
};

const document = {
  readyState: "complete",
  documentElement,
  body,
  querySelectorAll(selector) {
    if (selector.includes(",")) {
      const seen = [];
      for (const part of selector.split(",")) {
        const list = bySelector[part.trim()] || [];
        for (const el of list) if (!seen.includes(el)) seen.push(el);
      }
      return seen;
    }
    return bySelector[selector] || [];
  },
  getElementById(id) {
    return created.find((n) => n.id === id) || null;
  },
  createElement(tag) {
    const el = { tagName: String(tag).toUpperCase(), id: "", textContent: "", type: "", parentNode: null };
    return el;
  },
  addEventListener() {}
};

const observerCallbacks = [];
class MutationObserver {
  constructor(cb) { this.cb = cb; observerCallbacks.push(cb); }
  observe() {}
  disconnect() {}
}

const intervalFns = [];
globalThis.setInterval = (fn) => {
  intervalFns.push(fn);
  return intervalFns.length;
};
globalThis.clearInterval = () => {};
globalThis.setTimeout = () => 0;
globalThis.getComputedStyle = (el) => {
  const display = el.style.getPropertyValue("display") || "inline";
  const r = rectFor(el);
  return { display, height: r.height + "px", width: r.width + "px" };
};

const window = {
  document,
  MutationObserver,
  location: { pathname: "/swagger/index.html" }
};
globalThis.window = window;
globalThis.document = document;
globalThis.MutationObserver = MutationObserver;

function evalRuntime() {
  new Function("window", "document", "MutationObserver", "setInterval", "setTimeout", "getComputedStyle", js)(
    window, document, MutationObserver, globalThis.setInterval, globalThis.setTimeout, globalThis.getComputedStyle
  );
}

evalRuntime();

const helperDefinedAfterEval = typeof window.__deedAiMeasureAuthorize === "function";
if (typeof window.__deedAiApplyAuthorizeHit !== "function") {
  throw new Error("runtime did not expose __deedAiApplyAuthorizeHit");
}
if (!helperDefinedAfterEval) {
  throw new Error("runtime did not expose __deedAiMeasureAuthorize after script eval");
}
if (window.__deedAiAuthorizeRuntimeVersion !== "4.2.3") {
  throw new Error("expected runtime version 4.2.3, got " + window.__deedAiAuthorizeRuntimeVersion);
}

delete window.__deedAiMeasureAuthorize;
delete window.__deedAiApplyAuthorizeHit;
window.__deedAiAuthorizeRuntime = true;
evalRuntime();
const helperDefinedAfterReentry = typeof window.__deedAiMeasureAuthorize === "function";
if (!helperDefinedAfterReentry) {
  throw new Error("re-entry must still assign __deedAiMeasureAuthorize");
}
if (window.__deedAiAuthorizeRuntimeVersion !== "4.2.3") {
  throw new Error("re-entry must keep runtime version 4.2.3");
}

function assertPinned(el, label) {
  if (el.getAttribute("data-deedai-hit") !== "44") throw new Error(label + ": missing data-deedai-hit");
  if (el.style.getPropertyValue("display") !== "inline-flex") throw new Error(label + ": display not pinned");
  if (el.style.getPropertyPriority("display") !== "important") throw new Error(label + ": display missing !important");
  if (el.style.getPropertyValue("min-height") !== "44px") throw new Error(label + ": min-height not pinned");
  if (el.style.getPropertyPriority("min-height") !== "important") throw new Error(label + ": min-height missing !important");
  if (el.style.getPropertyValue("height") !== "44px") throw new Error(label + ": height not pinned");
  if (el.style.getPropertyValue("max-height") !== "none") throw new Error(label + ": max-height not none");
  const r = el.getBoundingClientRect();
  if (r.width < 44 || r.height < 44) {
    throw new Error(label + ": getBoundingClientRect still " + r.width + "x" + r.height);
  }
}

function swaggerReapplyInline(el) {
  el.style.setProperty("display", "inline", "");
  el.style.setProperty("height", "auto", "important");
  el.style.setProperty("min-height", "0", "important");
  el.style.setProperty("max-height", "30px", "important");
  el.setAttribute("data-deedai-hit", "reset");
}

const first = window.__deedAiMeasureAuthorize();
if (!Array.isArray(first) || first.length < 3) {
  throw new Error("expected top-bar + modal Authorize + Close, got " + JSON.stringify(first));
}
for (const item of first) {
  if (!item.ok || item.width < 44 || item.height < 44) {
    throw new Error("initial hit target failed: " + JSON.stringify(item));
  }
}
for (const el of [top, modalAuth, modalClose]) assertPinned(el, "initial");

for (const el of [top, modalAuth, modalClose]) swaggerReapplyInline(el);

const afterReset = top.getBoundingClientRect();
if (afterReset.height >= 44) {
  throw new Error("test setup failed: display:inline should measure ~34px, got " + afterReset.height);
}

if (observerCallbacks.length === 0 && intervalFns.length === 0) {
  throw new Error("runtime did not register MutationObserver or 250ms poll");
}
for (const cb of observerCallbacks) cb();
for (const fn of intervalFns) fn();

for (const el of [top, modalAuth, modalClose]) {
  assertPinned(el, "after swagger display:inline reset + observer/poll");
}

const recovered = window.__deedAiMeasureAuthorize();
for (const item of recovered) {
  if (!item.ok || item.width < 44 || item.height < 44) {
    throw new Error("measure after reset failed: " + JSON.stringify(item));
  }
}

if (documentElement.attrs["data-deedai-authorize-hit"] !== "pass") {
  throw new Error("expected data-deedai-authorize-hit=pass, got " + documentElement.attrs["data-deedai-authorize-hit"]);
}

const late = created.find((n) => n.id === "deedai-swagger-authorize-late");
if (!late || !String(late.textContent).includes("min-height: 44px")) {
  throw new Error("late stylesheet was not pinned after paint");
}
if (!String(late.textContent).includes("max-height: none")) {
  throw new Error("late stylesheet missing max-height: none");
}

console.log(JSON.stringify({
  ok: true,
  helperDefinedAfterEval,
  helperDefinedAfterReentry,
  recoveredFromSwaggerInlineReset: true,
  measured: recovered,
  marker: documentElement.attrs["data-deedai-authorize-hit"],
  version: window.__deedAiAuthorizeRuntimeVersion,
  observers: observerCallbacks.length,
  polls: intervalFns.length
}, null, 2));
