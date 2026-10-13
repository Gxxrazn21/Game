# Compila Dorsal.Core (Release, netstandard2.1) y copia el DLL al proyecto Unity (Windows).
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$out = Join-Path $root "unity/Dorsal/Assets/Plugins/Dorsal"
dotnet build (Join-Path $root "src/Dorsal.Core/Dorsal.Core.csproj") -c Release --nologo -v quiet
New-Item -ItemType Directory -Force -Path $out | Out-Null
Copy-Item (Join-Path $root "src/Dorsal.Core/bin/Release/netstandard2.1/Dorsal.Core.dll") $out
$pdb = Join-Path $root "src/Dorsal.Core/bin/Release/netstandard2.1/Dorsal.Core.pdb"
if (Test-Path $pdb) { Copy-Item $pdb $out }
Write-Host "Dorsal.Core.dll copiado a $out"
