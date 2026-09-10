# Phase 6 — AI extract

Visual SoT: Mask F Review (`04-review.png`) — 3-col queue | PDF | fields. AI fills locked fields. Human edit stays.

## Must

1. **AI PDF → locked Review fields** after upload / queue / worker: `documentNumber` · `volume` · `page` · `deedType` · `pid` · `mailingStreet` · `mailingCity` · `mailingState` · `mailingZip` · `grantors[]` · `grantees[]`. Never `docNo` / `vol` / County / CAMA.
2. **Human edit loop** on Review. Save persists locked names. Legacy `Grantor` / `Grantee` / `ParcelId` stay in sync with the first party / PID.
3. **Re-extract ConfirmSheet** before single or batch re-extract. AI overwrites locked fields. Controls ≥44px.
4. **One path (hard Must):** after AI cutover, **remove OCR/DI field-fill completely**. No dual path. No feature flag that leaves both. Human edit / ConfirmSheet loop stays. Fail if `IDocumentIntelligenceClient`, DI `AnalyzeAsync` field mapping, or a flag that keeps both remains.
5. **Brandon model lock:** Azure OpenAI in the **same Azure subscription** as `appdeedai`. Cheapest capable chat model (`gpt-4o-mini` class). KV-only keys via App Setting references: `AzureOpenAIEndpoint` · `AzureOpenAIKey` · `AzureOpenAIDeployment` · optional `AzureOpenAIModel` (default `gpt-4o-mini`).
6. **Fail closed** if unconfigured. No silent mock in Azure. `AzureOpenAI:Mode=Mock` is explicit (tests / local only).
7. **Do not raise quotas.** Escalate spend to Chief of Staff before any spend bump or pricier model. Code rejects gpt-4o / o1 / o3-class **model or deployment** names unless CoS sets `AzureOpenAI:AllowPricierModel=true`.
8. **Carry:** Mask F Review · Client/Software · no CAMA · Designer-first `20260910040000_Phase6AiExtract` with null-safe `AiRawBlobPath` / `ExtractConfidenceJson` · health 200.

## Should

- Confidence chips (High / Med / Low) on locked fields; **Edited** after a human change.
- Batch re-extract from Documents (selected + Retry Failed) with ConfirmSheet.
- Raw AI blob audit at `ai-raw/{id}.json`. Admin `GET /api/documents/{id}/extract-raw`.

## Won’t

Azure deploy · County/CAMA · Super Admin · quota increase · dual DI field-fill · leftover DI extract clients.

## Fail if

- DI still fills Review fields · dual extract paths · feature flag leaving both · unconfigured extract succeeds · invented field aliases · CAMA / County · Ready chip + OCR-failed banner · secrets on Review / health · no ConfirmSheet on re-extract · HTTP 500.30 after migration · pricier model or deployment without CoS override · quota increase.
