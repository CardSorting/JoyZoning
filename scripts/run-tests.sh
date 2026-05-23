#!/usr/bin/env bash
# Tiered JoyZoning test runs — default is fast (unit only).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
# shellcheck source=dotnet-env.sh
source "$ROOT/scripts/dotnet-env.sh"

MODE="${1:-fast}"
if (($# > 1)); then EXTRA_ARGS=("${@:2}"); else EXTRA_ARGS=(); fi

CLI_PROJ="tests/JoyZoning.Cli.Tests/JoyZoning.Cli.Tests.csproj"
TESTS_PROJ="tests/JoyZoning.Tests/JoyZoning.Tests.csproj"

run_cli() {
  if ((${#EXTRA_ARGS[@]} > 0)); then
    dotnet test "$CLI_PROJ" "${EXTRA_ARGS[@]}"
  else
    dotnet test "$CLI_PROJ"
  fi
}

run_tests() {
  local filter="$1"
  shift
  if ((${#EXTRA_ARGS[@]} > 0)); then
    dotnet test "$TESTS_PROJ" --filter "$filter" "${EXTRA_ARGS[@]}" "$@"
  else
    dotnet test "$TESTS_PROJ" --filter "$filter" "$@"
  fi
}

case "$MODE" in
  fast)
    echo "==> Fast: CLI unit tests + JoyZoning unit tests (no API host, no dogfood)"
    run_cli
    run_tests "Category=Unit"
    ;;
  unit)
    echo "==> Unit: JoyZoning.Tests only"
    run_tests "Category=Unit"
    ;;
  cli)
    echo "==> CLI unit tests only"
    run_cli
    ;;
  integration|api)
    echo "==> Integration: WebApplicationFactory API tests (sequential collection)"
    dotnet build "$TESTS_PROJ" -v q "${EXTRA_ARGS[@]}"
    run_tests "Category=Integration" --no-build
    ;;
  dogfood)
    exec "$ROOT/scripts/dogfood-validate.sh"
    ;;
  medium)
    echo "==> Medium: unit + integration (no dogfood)"
    run_cli
    dotnet build "$TESTS_PROJ" -v q "${EXTRA_ARGS[@]}"
    run_tests "Category=Unit|Category=Integration" --no-build
    ;;
  all)
    echo "==> All tiers (unit ∥ CLI, then integration, then dogfood)"
    run_cli &
    cli_pid=$!
    run_tests "Category=Unit"
    wait "$cli_pid"
    dotnet build "$TESTS_PROJ" -v q "${EXTRA_ARGS[@]}"
    run_tests "Category=Integration" --no-build
    exec "$ROOT/scripts/dogfood-validate.sh"
    ;;
  help|-h|--help)
    cat <<'EOF'
Usage: ./scripts/run-tests.sh [fast|unit|cli|integration|dogfood|medium|all] [extra dotnet test args…]

  fast         (default) CLI + JoyZoning unit tests only — quick local check
  unit         JoyZoning unit tests only
  cli          JoyZoning.Cli.Tests only
  integration  API / WebApplicationFactory tests (OrchestrationApi collection)
  dogfood      Subprocess control plane + real jz binary
  medium       fast + integration
  all          everything in order (slow)

Override default filter on JoyZoning.Tests:
  dotnet test tests/JoyZoning.Tests/JoyZoning.Tests.csproj --filter "Category=Integration"
EOF
    exit 0
    ;;
  *)
    echo "Unknown mode: $MODE (try: ./scripts/run-tests.sh help)" >&2
    exit 2
    ;;
esac

echo "==> Done ($MODE)"
