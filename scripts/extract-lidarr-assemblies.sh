#!/usr/bin/env bash
set -euo pipefail
# Tidalarr shim — delegates to the shared superset in Lidarr.Plugin.Common.
# Default output dir matches tidalarr CI: ext/Lidarr/_output/net8.0
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exec bash "$SCRIPT_DIR/../ext/Lidarr.Plugin.Common/scripts/extract-lidarr-assemblies.sh" \
  --output-dir "ext/Lidarr/_output/net8.0" \
  --plugin-name "Tidalarr" \
  "$@"
