#!/usr/bin/env node
/**
 * Executes the Deed AI Authorize runtime against a mock Swagger DOM that
 * already has the after-paint CSS win (display:inline, min-height:0).
 * Asserts inline !important 44px was pinned on top-bar and modal buttons.
 * Measure approach on a real page: window.__deedAiMeasureAuthorize()
 */
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..", "..");
const js = readFileSync(
  join(root, "src", "DeedAi.Api", "Swagger", "deedai-swagger-authorize.js"),
  "utf8"
);

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
    getAttribute(name) {
      return Object.prototype.hasOwnProperty.call(attrs, name) ? attrs[name] : null;
    },
    setAttribute(name, value) {
      attrs[name] = String(value);
    },
    querySelectorAll(sel) {
      if (sel === "span") return children.filter((c) => c.tagName === "SPAN");
      if (sel === "svg") return children.filter((c) => c.tagName === "SVG");
      return [];
    },
    getBoundingClientRect() {
      const pinned = el.style.getPropertyValue("height") === "44px"
        && el.style.getPropertyPriority("min-height") === "important";
      return { width: pinned ? 96 : 34, height: pinned ? 44 : 34, top: 0, left: 0, right: pinned ? 96 : 34, bottom: pinned ? 44 : 34 };
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
const documentElement = { nodeType: 1, attrs: Object.create(null) };
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

class MutationObserver {
  constructor(cb) { this.cb = cb; }
  observe() {}
}

const window = { document, MutationObserver };
globalThis.window = window;
globalThis.document = document;
globalThis.MutationObserver = MutationObserver;

new Function("window", "document", "MutationObserver", js)(window, document, MutationObserver);

if (typeof window.__deedAiApplyAuthorizeHit !== "function") {
  throw new Error("runtime did not expose __deedAiApplyAuthorizeHit");
}
if (typeof window.__deedAiMeasureAuthorize !== "function") {
  throw new Error("runtime did not expose __deedAiMeasureAuthorize");
}

const measured = window.__deedAiMeasureAuthorize();
if (!Array.isArray(measured) || measured.length < 3) {
  throw new Error("expected top-bar + modal Authorize + Close, got " + JSON.stringify(measured));
}
for (const item of measured) {
  if (!item.ok || item.width < 44 || item.height < 44) {
    throw new Error("hit target failed: " + JSON.stringify(item));
  }
}

for (const el of [top, modalAuth, modalClose]) {
  if (el.getAttribute("data-deedai-hit") !== "44") throw new Error("missing data-deedai-hit");
  if (el.style.getPropertyValue("display") !== "inline-flex") throw new Error("display not pinned");
  if (el.style.getPropertyPriority("display") !== "important") throw new Error("display missing !important");
  if (el.style.getPropertyValue("min-height") !== "44px") throw new Error("min-height not pinned");
  if (el.style.getPropertyPriority("min-height") !== "important") throw new Error("min-height missing !important");
  if (el.style.getPropertyValue("height") !== "44px") throw new Error("height not pinned");
}

if (documentElement.attrs["data-deedai-authorize-hit"] !== "pass") {
  throw new Error("expected data-deedai-authorize-hit=pass, got " + documentElement.attrs["data-deedai-authorize-hit"]);
}

const late = created.find((n) => n.id === "deedai-swagger-authorize-late");
if (!late || !String(late.textContent).includes("min-height: 44px")) {
  throw new Error("late stylesheet was not pinned after paint");
}

console.log(JSON.stringify({ ok: true, measured, marker: documentElement.attrs["data-deedai-authorize-hit"] }, null, 2));
