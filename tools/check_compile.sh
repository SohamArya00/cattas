#!/bin/bash
# Compile-checks the game scripts against Unity's reference libraries (UnityEngine.Modules from NuGet).
# Usage: bash tools/check_compile.sh      (downloads the reference DLLs the first time)
# Needs: mcs (apt-get install mono-mcs)
set -e
D="${TMPDIR:-/tmp}/cattas_unityref"
if [ ! -d "$D/x" ]; then
  mkdir -p "$D"
  curl -sL -o "$D/m.nupkg" https://api.nuget.org/v3-flatcontainer/unityengine.modules/2021.3.33/unityengine.modules.2021.3.33.nupkg
  python3 -c "import zipfile,sys; zipfile.ZipFile(sys.argv[1]).extractall(sys.argv[2])" "$D/m.nupkg" "$D/x"
fi
REFS=$(find "$D/x" -name "UnityEngine*.dll" | grep -v "/UnityEngine.dll$" | sed 's/^/-r:/' | tr '\n' ' ')
cd "$(dirname "$0")/.."
mcs -target:library -nowarn:0618,0414,0169,0219 -out:"$D/cattas.dll" $REFS Assets/Scripts/*.cs && echo "COMPILE OK"
