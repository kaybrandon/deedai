# SOP-01 — Deed AI local / ops overview (Phase 1)

**Repo:** `kaybrandon/deedai`  
**Audience:** Dev / ops standing up or verifying Phase 1

## Purpose
Phase 1 is auth + documents + OCR on Azure (Layout A). Secrets only in Key Vault / App Settings.

## Stack
.NET 10 API + React/Vite SPA (served from API wwwroot) + queue OCR worker · Azure SQL · Blob · Document Intelligence.

## Do not
- Put secrets in git or chat
- Use County / CAMA wording (use **Client** / **Software**)
- Expect Phase 2 features (Users admin UI, Reports, Settings CRUD, Software push) until those Pass on Azure

## Escalation
Build/API → Dev · Scope → BA · Prod/client go-live → Brandon via Chief of Staff
