#!/usr/bin/env bash
# Build and install the jz CLI to ~/.local/bin/jz
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="${HOME}/.local/bin"
mkdir -p "$OUT" "$ROOT/dist"

# shellcheck source=dotnet-env.sh
source "$ROOT/scripts/dotnet-env.sh"

dotnet publish "$ROOT/src/JoyZoning.Cli/JoyZoning.Cli.csproj" \
  -c Release \
  -o "$ROOT/dist/jz-publish" \
  --self-contained false

# apphost resolves jz.dll next to itself — install the full publish output
rm -rf "$OUT/joyzoning-jz"
mkdir -p "$OUT/joyzoning-jz"
cp -R "$ROOT/dist/jz-publish/." "$OUT/joyzoning-jz/"
chmod +x "$OUT/joyzoning-jz/jz"

# Wrapper script (do NOT symlink jz → joyzoning-jz/jz — writing the wrapper would clobber the binary)
cat > "$OUT/jz" <<'WRAPPER'
#!/usr/bin/env bash
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="${DOTNET_ROOT}:${PATH}"
export JOYZONING_URL="${JOYZONING_URL:-http://127.0.0.1:9470}"
exec "${HOME}/.local/bin/joyzoning-jz/jz" "$@"
WRAPPER
chmod +x "$OUT/jz"

cat > "$OUT/joyzoning" <<'WRAPPER'
#!/usr/bin/env bash
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="${DOTNET_ROOT}:${PATH}"
export JOYZONING_URL="${JOYZONING_URL:-http://127.0.0.1:9470}"
exec "${HOME}/.local/bin/joyzoning-jz/jz" "$@"
WRAPPER
chmod +x "$OUT/joyzoning"

cp "$OUT/joyzoning-jz/jz" "$ROOT/dist/jz"
chmod +x "$ROOT/dist/jz"

echo "Installed: $OUT/jz and $OUT/joyzoning"
echo "Run: joyzoning agent-context --json"
echo "Optional: export JOYZONING_URL=http://127.0.0.1:9470"
