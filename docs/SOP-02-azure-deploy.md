# SOP-02 — Deed AI Azure deploy (Phase 1)

**Who:** Ops / Dev deploying `appdeedai`

## Checklist
- [ ] RG `rg-bis-deed-ai` · SQL `deedaihost01`/`dbdeedai` · Storage `stbisdeedai` (deeds + ocr-jobs queue) · KV `kv-bis-deed-ai`
- [ ] App Settings use **Key Vault references** for the six secret names (see AZURE-PROD-NOTE)
- [ ] Publish Windows Layout A zip from `main` → zipdeploy to `appdeedai`
- [ ] If **500.30**: Resume serverless `dbdeedai` if paused; verify KV refs; restart app
- [ ] Smoke: home 200 · health 200 · Admin login · documents · dashboard

## No secrets in chat
Password / connection strings stay in KV or a private chmod-600 ops file — never paste into the repo or chat.
