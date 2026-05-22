#!/usr/bin/env bash
# Phase 27: automated dogfood validation (API + jz subprocess against live test server).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

export PATH="${HOME}/.dotnet:${PATH}"

echo "==> Building solution"
dotnet build tests/JoyZoning.Tests/JoyZoning.Tests.csproj -v q

echo "==> Running dogfood validation tests"
dotnet test tests/JoyZoning.Tests/JoyZoning.Tests.csproj \
  --filter "FullyQualifiedName~Dogfood" \
  --no-build

echo "==> Dogfood validation passed"
