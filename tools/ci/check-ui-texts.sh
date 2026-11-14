#!/usr/bin/env bash
# Glossary check of the UI texts (docs/product/ui-texts.md and its languages): finds the spellings a language's
# glossary forbids in the values of its resource files, so a later change can't bring them back unseen. AppTexts.resx
# holds the German texts, AppTexts.<language>.resx a translation. Keys, comments and the resx header are not texts and
# aren't checked. Judgement rules (Windows, technical names, "Wächter" on first mention) stay with the review.
# Usage: check-ui-texts.sh [resx files…]; without arguments every *Texts.resx and *Texts.<language>.resx under src/.
set -euo pipefail

if (( $# == 0 )); then
  cd "$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
  mapfile -t files < <(find src -name '*Texts*.resx' -not -path '*/bin/*' -not -path '*/obj/*' | sort)
else
  files=("$@")
fi

python3 -I - "${files[@]}" <<'PY'
import os, re, sys
import xml.etree.ElementTree as ET

common = [
    ("...", re.compile(r"\.\.\.")),
    (" - ", re.compile(r"(?<=\S) - (?=\S)")),
]
# spelling shown in the error → pattern, per language; a file without a language in its name is German
forbidden = {
    "de": common + [
        # "emulier" catches emuliert, emulieren, emulierte … (use "virtuell")
        ("emulier…", re.compile(r"emulier", re.IGNORECASE)),
        ("z. B.", re.compile(r"\bz\.\s?B\.")),
        ("ggf.", re.compile(r"\bggf\.", re.IGNORECASE)),
        ("Windows neu starten", re.compile(r"Windows\s+neu\s+starten", re.IGNORECASE)),
        ("neustarten", re.compile(r"neustarten", re.IGNORECASE)),
        ("straight quote, use „…“", re.compile(r'"')),
    ],
    "en": common + [
        # "emulate" as a verb: a controller is "virtual" and Arcadia "virtualizes" it; "emulation" stays
        ("emulate…, use virtual/virtualize", re.compile(r"\bemulat(e|ed|es|ing)\b", re.IGNORECASE)),
        ("e.g./i.e., use “for example”", re.compile(r"\b(e\.g|i\.e)\.", re.IGNORECASE)),
        ("restart Windows, use “restart the PC”", re.compile(r"(restart\w*\s+Windows|Windows\s+restart)", re.IGNORECASE)),
        ("Steam-Input, use “Steam Input”", re.compile(r"Steam-Input")),
        ("British spelling, use US English", re.compile(r"\b(colour|favourite|centre|behaviour|cancell(ed|ing)|virtualis|recognis|organis)\w*", re.IGNORECASE)),
        ("straight quote, use “…”", re.compile(r'"')),
        ("German quote „ or « »", re.compile(r"[„«»]")),
        ("space before …", re.compile(r"\s…")),
        ("German letter, text left untranslated?", re.compile(r"[äöüÄÖÜß]")),
    ],
    "fr": common + [
        # "émuler" as a verb: a controller is "virtuelle" and Arcadia "virtualise"; "émulation" stays
        ("émuler…, use virtuel/virtualiser", re.compile(r"\bémul(er|é|ée|és|ées|e|es|ent|ant)\b", re.IGNORECASE)),
        ("p. ex./c.-à-d., use “par exemple”", re.compile(r"\b(p\.\s?ex|c\.-à-d)\.", re.IGNORECASE)),
        ("redémarrer Windows, use “redémarrer le PC”", re.compile(r"(redémarr\w*\s+(de\s+)?Windows|Windows\s+redémarr)", re.IGNORECASE)),
        ("Steam-Input, use “Steam Input”", re.compile(r"Steam-Input")),
        ("straight quote, use « … »", re.compile(r'"')),
        ("straight apostrophe, use ’", re.compile(r"'(?![^<]*>)")),
        ("German or English quote, use « … »", re.compile(r"[„“”‘]")),
        ("« without a no-break space after it", re.compile(r"«(?!\u00a0)")),
        ("» without a no-break space before it", re.compile(r"(?<!\u00a0)»")),
        ("space before : ; ! ?, use a no-break space", re.compile(r"[ \t][:;!?]")),
        ("space before …", re.compile(r"\s…")),
        ("German letter, text left untranslated?", re.compile(r"[äöüÄÖÜß]")),
    ],
}
failed = False
for path in sys.argv[1:]:
    parts = os.path.basename(path).split(".")
    language = parts[-2] if len(parts) > 2 else "de"
    if language not in forbidden:
        print(f"::error file={path}::{path}: no glossary rules for the language {language!r} (tools/ci/check-ui-texts.sh)")
        failed = True
        continue
    try:
        root = ET.parse(path).getroot()
    except (ET.ParseError, OSError) as error:
        print(f"::error file={path}::{path} can't be read as XML: {error}")
        failed = True
        continue
    for data in root.iter("data"):
        value = data.findtext("value") or ""
        for spelling, pattern in forbidden[language]:
            if pattern.search(value):
                print(f"::error file={path}::{path}: {data.get('name')}: {spelling!r} is not allowed (docs/product/ui-texts.md)")
                failed = True
if not failed:
    print("UI texts follow the glossary.")
sys.exit(1 if failed else 0)
PY
