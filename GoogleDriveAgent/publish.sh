#!/usr/bin/env bash
# Builds a standalone, self-contained executable.
# The output does not require .NET to be installed to run.
#
# Usage:
#   ./publish.sh              # builds for this machine's OS/architecture
#   ./publish.sh osx-arm64    # cross-compiles for another platform (e.g. to
#                              # hand the binary to a different device)
set -euo pipefail
cd "$(dirname "$0")"

if [ $# -ge 1 ]; then
  RID="$1"
else
  case "$(uname -s)-$(uname -m)" in
    Darwin-x86_64)  RID=osx-x64 ;;
    Darwin-arm64)   RID=osx-arm64 ;;
    Linux-x86_64)   RID=linux-x64 ;;
    Linux-aarch64)  RID=linux-arm64 ;;
    *) echo "Unrecognized platform: $(uname -s)-$(uname -m). Pass a RID manually, e.g. ./publish.sh osx-arm64" >&2; exit 1 ;;
  esac
fi

OUT="./publish-$RID"
echo "Publishing for $RID..."
dotnet publish -c Release -r "$RID" --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -o "$OUT"

echo
echo "Done. Executable: $OUT/GoogleDriveAgent"
echo "Copy credentials.json next to it (or pass --credentials /path/to/credentials.json) before running."
