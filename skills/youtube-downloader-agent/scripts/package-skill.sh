#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
SKILL_DIR="$(cd -- "$SCRIPT_DIR/.." && pwd)"
REPO_DIR="$(cd -- "$SKILL_DIR/../.." && pwd)"
ARTIFACT_DIR="$REPO_DIR/artifacts"
OUTPUT="$ARTIFACT_DIR/youtube-downloader-agent-hermes-skill.zip"

mkdir -p "$ARTIFACT_DIR"
rm -f "$OUTPUT"
python3 - "$SKILL_DIR" "$OUTPUT" <<'PY'
from pathlib import Path
import sys
import zipfile

skill_dir = Path(sys.argv[1]).resolve()
output = Path(sys.argv[2]).resolve()
with zipfile.ZipFile(output, "w", compression=zipfile.ZIP_DEFLATED) as archive:
    for path in sorted(skill_dir.rglob("*")):
        if not path.is_file() or "__pycache__" in path.parts or path.suffix == ".pyc":
            continue
        archive.write(path, Path(skill_dir.name) / path.relative_to(skill_dir))
with zipfile.ZipFile(output) as archive:
    bad = archive.testzip()
    if bad:
        raise SystemExit(f"Corrupt ZIP entry: {bad}")
print(output)
PY
