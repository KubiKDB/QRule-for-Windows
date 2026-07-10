#!/usr/bin/env bash
#
# build.sh — macOS/Linux build for QRule W.
#
# What runs here: cross-compile the WPF app, run the Core unit tests, and publish a
# self-contained win-x64 executable you can copy to a Windows machine and run.
#
# What does NOT run here: launching the app (Windows-only) and MSIX packaging
# (needs makeappx/signtool — see build.ps1). This script is the CI/compile gate.
#
# Requires the official .NET 8 SDK (the Homebrew dotnet@8 lacks the WindowsDesktop
# targets needed for WPF). If ~/.dotnet exists it is used automatically.
set -euo pipefail

cd "$(dirname "$0")"

if [ -x "$HOME/.dotnet/dotnet" ]; then
  export PATH="$HOME/.dotnet:$PATH"
  export DOTNET_ROOT="$HOME/.dotnet"
fi

RID="${1:-win-x64}"   # pass win-arm64 as the first arg to target ARM
CONFIG="Release"

echo "==> dotnet: $(command -v dotnet)  ($(dotnet --version))"

echo "==> Building solution ($CONFIG)"
dotnet build QRuleW.sln -c "$CONFIG"

echo "==> Running Core unit tests"
dotnet test tests/QRuleW.Core.Tests/QRuleW.Core.Tests.csproj -c "$CONFIG" --no-build

echo "==> Publishing self-contained $RID"
dotnet publish src/QRuleW/QRuleW.csproj \
  -c "$CONFIG" -r "$RID" --self-contained true \
  -p:PublishSingleFile=true \
  -o "dist/$RID"

echo ""
echo "==> Done. Distributable: dist/$RID/QRuleW.exe"
echo "    Copy the dist/$RID folder to a Windows 10/11 machine and run QRuleW.exe."
