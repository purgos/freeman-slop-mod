#!/usr/bin/env bash
# Build Freeman Slop Mod and install it into the game.
#   ./install.sh            dev:     plugin -> BepInEx/scripts/ (ScriptEngine hot-reloads it while the game runs)
#   ./install.sh --release  release: plugin -> BepInEx/plugins/ (normal BepInEx loading, no ScriptEngine needed)
# The preloader patcher always goes to BepInEx/patchers/ and only takes effect on a game restart.
set -euo pipefail
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_ROLL_FORWARD=Major
GAME="$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/Freeman Guerrilla Warfare/Freeman Guerrilla Warfare"
BEP="$GAME/BepInEx"
MODE=dev
[ "${1:-}" = "--release" ] && MODE=release
cd "$(dirname "$0")"

dotnet build FreemanFixes/FreemanFixes.csproj -c Release --nologo -v q
dotnet build FreemanFixes.Patcher/FreemanFixes.Patcher.csproj -c Release --nologo -v q

# Guard: the game's old Mono (CLR 2.0) can't run these; the compiler emits them silently.
for dll in FreemanFixes/bin/Release/FreemanFixes.dll FreemanFixes.Patcher/bin/Release/FreemanFixes.Patcher.dll; do
  bad=$(ilspycmd -il "$dll" 2>/dev/null | grep -cE 'Array::Empty|<>O|System\.(Func|Action)' || true)
  if [ "$bad" != "0" ]; then
    echo "ERROR: $dll uses APIs the game's runtime lacks (Array.Empty / <>O / Func/Action): $bad hits" >&2
    exit 1
  fi
done

mkdir -p "$BEP/plugins" "$BEP/patchers" "$BEP/scripts" "$BEP/config"
cp FreemanFixes.Patcher/bin/Release/FreemanFixes.Patcher.dll "$BEP/patchers/"

if [ "$MODE" = dev ]; then
  # ScriptEngine (BepInEx.Debug r11.1, unpacked in vendor/): load scripts at startup + reload when files change.
  cp vendor/scriptengine/BepInEx/plugins/ScriptEngine.dll "$BEP/plugins/"
  SE_CFG="$BEP/config/com.bepis.bepinex.scriptengine.cfg"
  if [ ! -f "$SE_CFG" ]; then
    printf '[General]\nLoadOnStart = true\n\n[AutoReload]\nEnableFileSystemWatcher = true\n' > "$SE_CFG"
  fi
  rm -f "$BEP/plugins/FreemanFixes.dll"   # never load it twice (plugins/ + scripts/)
  cp FreemanFixes/bin/Release/FreemanFixes.dll "$BEP/scripts/"
  echo "Installed (dev): plugin -> BepInEx/scripts/ — hot-reloads in a running game (or press F6)"
else
  rm -f "$BEP/scripts/FreemanFixes.dll"
  cp FreemanFixes/bin/Release/FreemanFixes.dll "$BEP/plugins/"
  echo "Installed (release): plugin -> BepInEx/plugins/"
fi
