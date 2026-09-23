#!/usr/bin/env bash
# Everything about Banccoon that can be checked from Linux, in one run. It does NOT replace a Windows
# build + click-through: nothing here runs XamlC, WinUI, the real UI thread, or Shell navigation.
# Usage: tools/linux-verification/verify.sh   (from anywhere; needs the .NET 10 SDK and python3)
set -u
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
fail=0

echo "== 1. Core/Infrastructure tests"
dotnet test "$ROOT/tests/Banccoon.Tests/Banccoon.Tests.csproj" 2>&1 | grep -E " error |Passed!|Failed!|^\s+Failed " || fail=1

echo "== 2. XAML and resx are well-formed XML"
python3 - "$ROOT" <<'PY' || fail=1
import glob, sys, xml.etree.ElementTree as ET
root = sys.argv[1]
bad = 0
for f in glob.glob(f'{root}/src/Banccoon.App/**/*.xaml', recursive=True) + glob.glob(f'{root}/src/Banccoon.App/Resources/Strings/*.resx'):
    try: ET.parse(f)
    except Exception as e: bad += 1; print('  MALFORMED', f, e)
print('  OK' if not bad else f'  {bad} malformed'); sys.exit(1 if bad else 0)
PY

echo "== 3. resx EN/RU parity (same keys modulo plural suffixes, same {n} placeholders)"
python3 "$HERE/scripts/resx_parity.py" | tee /dev/stderr | grep -q "^EN-only: \[\]" || fail=1

echo "== 4. App type-check (MAUI stubs) + compiled-binding and translate-key check"
(cd "$HERE/AppTypecheck" && dotnet build 2>&1 | grep -E " error |Build succeeded" | sort -u && dotnet run --no-build 2>&1 | tail -15 | tee /dev/stderr | grep -q " 0 errors") || fail=1

echo "== 5. Formatter checks (EN + RU against the real resx)"
(cd "$HERE/FormatterChecks" && dotnet run 2>&1 | grep -E " error |^FAIL|   expected|   actual|ALL PASSED|FAILED" | tee /dev/stderr | grep -q "ALL PASSED") || fail=1

echo "== 6. View-model tests (real view models + real Core services + temp SQLite)"
(cd "$HERE/ViewModelTests" && timeout 300 dotnet test 2>&1 | grep -E " error |^\s+Failed |Passed!|Failed!" | tee /dev/stderr | grep -q "Passed!") || fail=1

[ $fail -eq 0 ] && echo "== ALL CHECKS PASSED" || echo "== SOMETHING FAILED (see above)"
exit $fail
