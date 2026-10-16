#!/usr/bin/env bash
# Build FreemanFixes (plugin + preloader patcher) and copy them into the game's BepInEx folders.
set -euo pipefail
export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"
GAME="$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/Freeman Guerrilla Warfare/Freeman Guerrilla Warfare"
cd "$(dirname "$0")"
dotnet build FreemanFixes/FreemanFixes.csproj -c Release --nologo -v q
dotnet build FreemanFixes.Patcher/FreemanFixes.Patcher.csproj -c Release --nologo -v q
# Guard: the game's old Mono (CLR 2.0) can't run these; the compiler emits them silently.
export DOTNET_ROLL_FORWARD=Major PATH="$HOME/.dotnet/tools:$PATH"
for dll in FreemanFixes/bin/Release/FreemanFixes.dll FreemanFixes.Patcher/bin/Release/FreemanFixes.Patcher.dll; do
  bad=$(ilspycmd -il "$dll" 2>/dev/null | grep -cE 'Array::Empty|<>O|System\.(Func|Action)' || true)
  if [ "$bad" != "0" ]; then
    echo "ERROR: $dll uses APIs the game's runtime lacks (Array.Empty / <>O / Func/Action): $bad hits" >&2
    exit 1
  fi
done
mkdir -p "$GAME/BepInEx/plugins" "$GAME/BepInEx/patchers"
cp FreemanFixes/bin/Release/FreemanFixes.dll "$GAME/BepInEx/plugins/"
cp FreemanFixes.Patcher/bin/Release/FreemanFixes.Patcher.dll "$GAME/BepInEx/patchers/"
echo "Installed to $GAME/BepInEx/{plugins,patchers}/"
