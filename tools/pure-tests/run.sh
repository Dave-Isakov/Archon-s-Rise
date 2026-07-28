#!/usr/bin/env bash
# Compile and run pure (Unity-free) classes + their NUnit tests from the CLI.
#
# Why this exists: the Unity editor holds a lock that makes batch-mode -runTests
# unreliable while the editor is open, and the legacy .NET Framework csc is C# 5
# (it rejects expression-bodied members), so we use Unity's bundled Mono mcs.
#
# Every path below is RELATIVE to the repo root on purpose: mcs.bat is a Windows
# batch file that mangles arguments containing spaces, and this project lives
# under a path with spaces in it.
#
# Usage, from the repo root:
#   tools/pure-tests/run.sh Assets/Scripts/Foo/Bar.cs Assets/Tests/EditMode/BarTests.cs
set -uo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/../.."

UNITY="$(ls -d /c/Program\ Files/Unity/Hub/Editor/*/Editor/Data/MonoBleedingEdge/bin | tail -1)"
NUNIT="$(ls -d Library/PackageCache/com.unity.ext.nunit@*/net472/unity-custom/nunit.framework.dll | head -1)"

mkdir -p Temp/pure-tests
cp -f "$NUNIT" Temp/pure-tests/

# -langversion:latest is required: mcs defaults to C# 7.0, which rejects
# `readonly struct` (C# 7.2) — a form this codebase already uses.
"$UNITY/mcs.bat" -target:exe -langversion:latest -out:Temp/pure-tests/t.exe -r:"$NUNIT" \
  "$@" tools/pure-tests/Runner.cs || exit 1

"$UNITY/cli.bat" Temp/pure-tests/t.exe
