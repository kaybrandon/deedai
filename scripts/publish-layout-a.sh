#!/usr/bin/env bash
# Layout A: single Windows App Service host (appdeedai) serves the API + SPA wwwroot.
# Also drops the OCR worker in as a continuous WebJob so the same B1 plan can
# drain the ocr-jobs Azure Storage Queue (long-poll, not a 1s loop).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="${ROOT}/artifacts/layout-a"
APP_OUT="${OUT}/app"
ZIP="${OUT}/deedai-win-x64.zip"
CONFIG="${BUILD_CONFIGURATION:-Release}"
RID="${PUBLISH_RID:-win-x64}"

export PATH="${DOTNET_ROOT:-$HOME/.dotnet}:$PATH"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"

echo "==> Building SPA into API wwwroot"
if [[ ! -d "${ROOT}/spa/node_modules" ]]; then
  (cd "${ROOT}/spa" && npm ci --no-audit --no-fund)
else
  (cd "${ROOT}/spa" && npm install --no-audit --no-fund)
fi
(cd "${ROOT}/spa" && npm run build)

echo "==> Publishing API (${RID})"
rm -rf "${OUT}"
mkdir -p "${APP_OUT}"
dotnet publish "${ROOT}/src/DeedAi.Api/DeedAi.Api.csproj" \
  -c "${CONFIG}" \
  -r "${RID}" \
  --self-contained false \
  -o "${APP_OUT}"

echo "==> Publishing OCR worker WebJob (${RID})"
JOB_DIR="${APP_OUT}/App_Data/jobs/continuous/ocr-worker"
mkdir -p "${JOB_DIR}"
dotnet publish "${ROOT}/src/DeedAi.Worker/DeedAi.Worker.csproj" \
  -c "${CONFIG}" \
  -r "${RID}" \
  --self-contained false \
  -o "${JOB_DIR}"
cat > "${JOB_DIR}/run.cmd" << 'EOF'
@echo off
DeedAi.Worker.exe
EOF
printf '{ "is_singleton": true }\n' > "${JOB_DIR}/settings.job"

echo "==> Zipping Windows win-x64 package"
rm -f "${ZIP}"
# Prefer zip(1); fall back to .NET so the script works without extra packages.
if command -v zip >/dev/null 2>&1; then
  (cd "${APP_OUT}" && zip -qry "${ZIP}" .)
else
  dotnet exec -e "
using System.IO.Compression;
if (File.Exists(\"${ZIP}\")) File.Delete(\"${ZIP}\");
ZipFile.CreateFromDirectory(\"${APP_OUT}\", \"${ZIP}\");
" 2>/dev/null || python3 - << PY
import shutil
from pathlib import Path
app = Path("${APP_OUT}")
zip_path = Path("${ZIP}")
if zip_path.exists():
    zip_path.unlink()
shutil.make_archive(str(zip_path.with_suffix("")), "zip", app)
print("wrote", zip_path)
PY
fi

echo "Published ${ZIP}"
echo "Deploy to Windows App Service appdeedai (plan asp-bis-deed-ai B1, RG rg-bis-deed-ai, South Central US)."
echo "Set stack to .NET 10. Apply settings from .env.example (no secrets in source)."
