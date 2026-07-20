#!/usr/bin/env python3
"""Safe process wrapper for YoutubeDownloader Agent."""

from __future__ import annotations

import os
import shutil
import subprocess
import sys
from pathlib import Path


def resolve_binary() -> str:
    override = os.environ.get("YOUTUBE_DOWNLOADER_AGENT_BIN")
    if override:
        candidate = Path(override).expanduser().resolve()
        if candidate.is_file():
            return str(candidate)
        raise SystemExit(f"Configured YOUTUBE_DOWNLOADER_AGENT_BIN does not exist: {candidate}")

    install_root = Path.home() / ".local" / "share" / "youtube-downloader-agent"
    names = ["youtube-downloader-agent.exe", "youtube-downloader-agent"] if os.name == "nt" else ["youtube-downloader-agent", "youtube-downloader-agent.exe"]
    for name in names:
        candidate = install_root / name
        if candidate.is_file():
            return str(candidate)

    on_path = shutil.which("youtube-downloader-agent")
    if on_path:
        return on_path

    raise SystemExit(
        "YoutubeDownloader Agent binary not found. Run scripts/install-local.sh "
        "from the repository or set YOUTUBE_DOWNLOADER_AGENT_BIN."
    )


def main() -> int:
    if len(sys.argv) < 2:
        print("Usage: youtube_downloader_agent.py <probe|download|self-test> [options]", file=sys.stderr)
        return 2

    command = sys.argv[1]
    if command not in {"probe", "download", "self-test"}:
        print(f"Unsupported command: {command}", file=sys.stderr)
        return 2

    args = sys.argv[2:]
    if command == "download" and "--confirm-rights" not in args:
        print(
            '{"ok":false,"error":{"code":"rights_confirmation_required",'
            '"message":"Explicit user authorization is required."}}'
        )
        return 3

    process = subprocess.run([resolve_binary(), command, *args], check=False)
    return process.returncode


if __name__ == "__main__":
    raise SystemExit(main())
