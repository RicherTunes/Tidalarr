#!/usr/bin/env bash
set -euo pipefail

# assert-host-guardrails.sh — Validate extracted Lidarr host assemblies
# Usage: bash scripts/assert-host-guardrails.sh <assemblies-dir>
#
# Checks:
#   1. .NET 8 runtime (via Lidarr.runtimeconfig.json)
#   2. FluentValidation 9.5.4.* (host-boundary package)

ASSEMBLIES_DIR="${1:-}"

if [[ -z "$ASSEMBLIES_DIR" ]]; then
  echo "Usage: assert-host-guardrails.sh <assemblies-dir>" >&2
  exit 2
fi

if [[ ! -d "$ASSEMBLIES_DIR" ]]; then
  echo "::error::Assemblies directory not found: $ASSEMBLIES_DIR" >&2
  exit 1
fi

ERRORS=0

# --- .NET 8 Runtime ---
RC="$ASSEMBLIES_DIR/Lidarr.runtimeconfig.json"
if [[ -f "$RC" ]]; then
  if grep -qE '"version":\s*"8\.' "$RC"; then
    echo "[guardrail] OK: Lidarr runtime targets .NET 8"
  else
    echo "::error::Lidarr runtime does NOT target .NET 8. The Docker image is likely a .NET 6 build." >&2
    cat "$RC" >&2
    ERRORS=$((ERRORS + 1))
  fi
else
  echo "[guardrail] WARNING: Lidarr.runtimeconfig.json not found; skipping .NET version check"
fi

# --- FluentValidation 9.5.4 ---
FV_DLL="$ASSEMBLIES_DIR/FluentValidation.dll"
if [[ -f "$FV_DLL" ]]; then
  # ProductVersion is embedded as ASCII in the .NET assembly PE metadata
  FV_VER=$(strings "$FV_DLL" 2>/dev/null | grep -oE '^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$' | head -1 || true)
  if [[ -z "$FV_VER" ]]; then
    echo "[guardrail] WARNING: Could not read FluentValidation version from DLL metadata"
  elif [[ "$FV_VER" == 9.5.4.* ]]; then
    echo "[guardrail] OK: FluentValidation version $FV_VER (matches host 9.5.4)"
  else
    echo "::error::FluentValidation version $FV_VER does not match host expectation 9.5.4.*" >&2
    ERRORS=$((ERRORS + 1))
  fi
else
  echo "[guardrail] WARNING: FluentValidation.dll not found; skipping FV version check"
fi

if [[ $ERRORS -gt 0 ]]; then
  echo "::error::$ERRORS guardrail(s) failed" >&2
  exit 1
fi

echo "[guardrail] All host assembly checks passed"
exit 0
