#!/usr/bin/env bash
set -euo pipefail

show_help() {
  cat <<'EOF'
🔨 Tidalarr Build Script (bash)

Usage:
  ./build.sh [options]

Configurations:
  --configuration Debug|Release   Build configuration (default: Debug)

Options:
  -d, --deploy                    Automatically deploy to a Lidarr instance
  --deploy-path PATH              Custom deployment path
  -c, --clean                     Run dotnet clean first
  -r, --restore                   Force dotnet restore
  -n, --no-build                  Skip the build step
  -v, --verbose                   Use normal verbosity (default: minimal)
  --use-prebuilt-assemblies       Build against pre-built Lidarr binaries
  --lidarr-version VERSION        Override Lidarr assembly version (default: 2.13.2.4685)
  -h, --help                      Show this help text

Examples:
  ./build.sh                                 # Debug build
  ./build.sh --configuration Release         # Release build
  ./build.sh -d                              # Debug build + deploy
  ./build.sh -c -r                           # Clean, restore, build
  ./build.sh --deploy-path /custom/path      # Deploy to custom path
  ./build.sh --use-prebuilt-assemblies       # Use CI-style prebuilt assemblies

Default deploy path:
  X:/lidarr-hotio-test2/plugins/RicherTunes/Tidalarr
EOF
}

CONFIGURATION="Debug"
DEPLOY=false
DEPLOY_PATH=""
CLEAN=false
RESTORE=false
NO_BUILD=false
VERBOSE=false
USE_PREBUILT=false
LIDARR_VERSION="2.13.2.4685"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --configuration)
      CONFIGURATION="$2"; shift 2 ;;
    -d|--deploy)
      DEPLOY=true; shift ;;
    --deploy-path)
      DEPLOY_PATH="$2"; shift 2 ;;
    -c|--clean)
      CLEAN=true; shift ;;
    -r|--restore)
      RESTORE=true; shift ;;
    -n|--no-build)
      NO_BUILD=true; shift ;;
    -v|--verbose)
      VERBOSE=true; shift ;;
    --use-prebuilt-assemblies)
      USE_PREBUILT=true; shift ;;
    --lidarr-version)
      LIDARR_VERSION="$2"; shift 2 ;;
    -h|--help)
      show_help; exit 0 ;;
    *)
      echo "Unknown option: $1" >&2
      show_help
      exit 1 ;;
  esac
done

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"
cd "$SCRIPT_DIR"

if [[ ! -f "Tidalarr.sln" ]]; then
  echo "❌ Error: run this script from the Tidalarr repository root" >&2
  echo "   Current directory: $(pwd)" >&2
  exit 1
fi

PLUGIN_PROJECT="src/Tidalarr/Tidalarr.csproj"
DEFAULT_DEPLOY_PATH="X:/lidarr-hotio-test2/plugins/RicherTunes/Tidalarr"

echo "🔨 Building Tidalarr Plugin"
echo "Configuration: $CONFIGURATION"

if $CLEAN; then
  echo ""
  echo "🧹 Cleaning solution..."
  if ! dotnet clean Tidalarr.sln --configuration "$CONFIGURATION" --verbosity minimal; then
    echo "⚠️ Clean failed" >&2
  fi
fi

if $RESTORE || [[ ! -f "packages.lock.json" ]]; then
  echo ""
  echo "📦 Restoring packages..."
  dotnet restore Tidalarr.sln --verbosity minimal
fi

if ! $NO_BUILD; then
  echo ""
  echo "🔧 Preparing build"

  BUILD_ARGS=("$PLUGIN_PROJECT" "--configuration" "$CONFIGURATION" "--no-restore" "-p:RunAnalyzersDuringBuild=false" "-p:EnableNETAnalyzers=false" "-p:TreatWarningsAsErrors=false")

  if ! $USE_PREBUILT && [[ -f "ext/Lidarr-source/src/Directory.Build.props" ]]; then
    echo "🔧 Lidarr sources detected. Target assembly version: $LIDARR_VERSION"
    BUILD_ARGS+=("-p:LidarrAssemblyVersion=$LIDARR_VERSION")
  elif $USE_PREBUILT; then
    echo "📦 Using pre-built Lidarr assemblies"
  fi

  if $DEPLOY; then
    TARGET_PATH="$DEPLOY_PATH"
    if [[ -z "$TARGET_PATH" ]]; then
      TARGET_PATH="$DEFAULT_DEPLOY_PATH"
    fi
    BUILD_ARGS+=("-p:EnablePluginDeployment=true" "-p:LidarrPluginDeployPath=$TARGET_PATH")
    echo "🚀 Plugin deployment enabled"
    echo "📁 Deploy path: $TARGET_PATH"
  fi

  if $VERBOSE; then
    BUILD_ARGS+=("--verbosity" "normal")
  else
    BUILD_ARGS+=("--verbosity" "minimal")
  fi

  echo ""
  echo "🔨 Building..."
  if ! dotnet build "${BUILD_ARGS[@]}"; then
    echo "❌ Build failed" >&2
    echo "💡 Try rerunning with --verbose" >&2
    exit 1
  fi

  echo ""
  echo "✅ Build successful"
  echo "📍 Output: src/Tidalarr/bin/$CONFIGURATION"
  if $DEPLOY; then
    echo "🚀 Plugin deployed; restart Lidarr to load the update"
  fi
else
  echo "⚙️ Build skipped (--no-build)"
fi

echo ""
echo "🎉 Build script completed"
if ! $DEPLOY && ! $NO_BUILD; then
  echo ""
  echo "💡 Next steps:"
  echo "  To deploy automatically: ./build.sh --configuration $CONFIGURATION --deploy"
  echo "  Plugin binaries: src/Tidalarr/bin/$CONFIGURATION"
  echo "  Manual deploy: copy the output to your Lidarr plugins folder"
fi\n