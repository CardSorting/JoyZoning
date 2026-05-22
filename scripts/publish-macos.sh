#!/usr/bin/env bash
# Build JoyZoning for macOS (arm64). Run from repo root.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
# shellcheck source=dotnet-env.sh
source "$ROOT/scripts/dotnet-env.sh"

RID="${RID:-osx-arm64}"
CONFIG="${CONFIG:-Release}"
OUT="$ROOT/dist"

echo "Publishing JoyZoning ($CONFIG, $RID)…"

dotnet publish src/JoyZoning.ControlPlane/JoyZoning.ControlPlane.csproj \
  -c "$CONFIG" -r "$RID" --self-contained false \
  -o "$OUT/JoyZoning.ControlPlane"

dotnet publish src/JoyZoning.App/JoyZoning.App.csproj \
  -c "$CONFIG" -r "$RID" --self-contained false \
  -o "$OUT/JoyZoning.App"

cat > "$OUT/run-joyzoning.sh" <<'EOF'
#!/usr/bin/env bash
DIR="$(cd "$(dirname "$0")" && pwd)"
"$DIR/JoyZoning.ControlPlane/JoyZoning.ControlPlane" &
CP_PID=$!
sleep 2
"$DIR/JoyZoning.App/JoyZoning.App"
kill $CP_PID 2>/dev/null || true
EOF
chmod +x "$OUT/run-joyzoning.sh"

echo "Done. Launch with: $OUT/run-joyzoning.sh"
