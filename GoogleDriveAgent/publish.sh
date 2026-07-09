#!/usr/bin/env bash
# Builds a standalone, self-contained executable for this machine's platform.
# The output does not require .NET to be installed to run.
set -euo pipefail
cd "$(dirname "$0")"

case "$(uname -s)-$(uname -m)" in
  Darwin-x86_64)  RID=osx-x64 ;;
  Darwin-arm64)   RID=osx-arm64 ;;
  Linux-x86_64)   RID=linux-x64 ;;
  Linux-aarch64)  RID=linux-arm64 ;;
  *) echo "Unrecognized platform: $(uname -s)-$(uname -m). Pass a RID manually: dotnet publish -r <RID> ..." >&2; exit 1 ;;
esac

echo "Publishing for $RID..."
dotnet publish -c Release -r "$RID" --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -o ./publish

echo
echo "Done. Executable: ./publish/GoogleDriveAgent"
echo "Copy credentials.json next to it (or pass --credentials /path/to/credentials.json) before running."
