#!/usr/bin/env bash
# Build JoyZoning.app bundle for macOS (arm64). Requires publish-macos.sh first.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DIST="$ROOT/dist"
APP="$DIST/JoyZoning.app"

if [[ ! -d "$DIST/JoyZoning.App" ]]; then
  echo "Run ./scripts/publish-macos.sh first."
  exit 1
fi

APP_NAME="JoyZoning"
BUNDLE_ID="${BUNDLE_ID:-com.joyzoning.operator}"

rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

cp -R "$DIST/JoyZoning.App/"* "$APP/Contents/MacOS/"
cp -R "$DIST/JoyZoning.ControlPlane/"* "$APP/Contents/MacOS/"

cat > "$APP/Contents/MacOS/launch-joyzoning.sh" <<'LAUNCH'
#!/usr/bin/env bash
DIR="$(cd "$(dirname "$0")" && pwd)"
export PATH="/usr/local/bin:/opt/homebrew/bin:$PATH"
"$DIR/JoyZoning.ControlPlane" &
sleep 2
exec "$DIR/JoyZoning.App"
LAUNCH
chmod +x "$APP/Contents/MacOS/launch-joyzoning.sh"

cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key>
  <string>en</string>
  <key>CFBundleExecutable</key>
  <string>launch-joyzoning.sh</string>
  <key>CFBundleIdentifier</key>
  <string>${BUNDLE_ID}</string>
  <key>CFBundleInfoDictionaryVersion</key>
  <string>6.0</string>
  <key>CFBundleName</key>
  <string>${APP_NAME}</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>0.1.0</string>
  <key>CFBundleVersion</key>
  <string>1</string>
  <key>LSMinimumSystemVersion</key>
  <string>12.0</string>
  <key>NSHighResolutionCapable</key>
  <true/>
</dict>
</plist>
PLIST

echo "Created $APP"
echo "Open with: open \"$APP\""
