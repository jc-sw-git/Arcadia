#!/usr/bin/env bash
# Glossary check of the UI texts (docs/product/ui-texts.md): finds the spellings the glossary forbids in the values of
# the resource files, so a later change can't bring them back unseen. Keys, comments and the resx header are not
# texts and aren't checked. Judgement rules (Windows, technical names, "Wächter" on first mention) stay with the review.
# Usage: check-ui-texts.sh [resx files…]; without arguments every *Texts.resx under src/.
set -euo pipefail

if (( $# == 0 )); then
  cd "$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
  mapfile -t files < <(find src -name '*Texts.resx' -not -path '*/bin/*' -not -path '*/obj/*' | sort)
else
  files=("$@")
fi

python3 -I - "${files[@]}" <<'PY'
import re, sys
import xml.etree.ElementTree as ET

# spelling shown in the error → pattern; "emulier" catches emuliert, emulieren, emulierte … (use "virtuell")
forbidden = [
    ("emulier…", re.compile(r"emulier", re.IGNORECASE)),
    ("z. B.", re.compile(r"\bz\.\s?B\.")),
    ("ggf.", re.compile(r"\bggf\.", re.IGNORECASE)),
    ("Windows neu starten", re.compile(r"Windows\s+neu\s+starten", re.IGNORECASE)),
    ("neustarten", re.compile(r"neustarten", re.IGNORECASE)),
    ("straight quote, use „…“", re.compile(r'"')),
    ("...", re.compile(r"\.\.\.")),
    (" - ", re.compile(r"(?<=\S) - (?=\S)")),
]
failed = False
for path in sys.argv[1:]:
    try:
        root = ET.parse(path).getroot()
    except (ET.ParseError, OSError) as error:
        print(f"::error file={path}::{path} can't be read as XML: {error}")
        failed = True
        continue
    for data in root.iter("data"):
        value = data.findtext("value") or ""
        for spelling, pattern in forbidden:
            if pattern.search(value):
                print(f"::error file={path}::{path}: {data.get('name')}: {spelling!r} is not allowed (docs/product/ui-texts.md)")
                failed = True
if not failed:
    print("UI texts follow the glossary.")
sys.exit(1 if failed else 0)
PY
