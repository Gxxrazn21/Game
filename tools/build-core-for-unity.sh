#!/usr/bin/env bash
# Compila Dorsal.Core (Release, netstandard2.1) y copia el DLL al proyecto Unity.
# Newtonsoft.Json NO se copia: Unity lo aporta con el paquete com.unity.nuget.newtonsoft-json.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/unity/Dorsal/Assets/Plugins/Dorsal"
dotnet build "$ROOT/src/Dorsal.Core/Dorsal.Core.csproj" -c Release --nologo -v quiet
mkdir -p "$OUT"
cp "$ROOT/src/Dorsal.Core/bin/Release/netstandard2.1/Dorsal.Core.dll" "$OUT/"
cp "$ROOT/src/Dorsal.Core/bin/Release/netstandard2.1/Dorsal.Core.pdb" "$OUT/" 2>/dev/null || true
echo "Dorsal.Core.dll copiado a $OUT"
