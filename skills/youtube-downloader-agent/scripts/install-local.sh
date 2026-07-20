#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
SKILL_DIR="$(cd -- "$SCRIPT_DIR/.." && pwd)"
REPO_DIR="$(cd -- "$SKILL_DIR/../.." && pwd)"
PROFILE="${HERMES_PROFILE:-friren}"
HERMES_HOME="${HERMES_HOME:-$HOME/.hermes}"
INSTALL_ROOT="${YOUTUBE_DOWNLOADER_AGENT_HOME:-$HOME/.local/share/youtube-downloader-agent}"
SKILL_ROOT="${HERMES_SKILLS_DIR:-$HERMES_HOME/profiles/$PROFILE/skills}"
DOTNET_BIN="${DOTNET_BIN:-dotnet}"
RID="${YOUTUBE_DOWNLOADER_AGENT_RID:-linux-x64}"

if ! command -v "$DOTNET_BIN" >/dev/null 2>&1; then
  printf 'dotnet was not found. Set DOTNET_BIN or install .NET SDK 10.\n' >&2
  exit 2
fi

mkdir -p "$INSTALL_ROOT" "$SKILL_ROOT/youtube-downloader-agent"
"$DOTNET_BIN" publish "$REPO_DIR/YoutubeDownloader.Agent/YoutubeDownloader.Agent.csproj" \
  -c Release -r "$RID" --self-contained true -o "$INSTALL_ROOT"

rm -rf "$SKILL_ROOT/youtube-downloader-agent/scripts"
cp "$SKILL_DIR/SKILL.md" "$SKILL_ROOT/youtube-downloader-agent/SKILL.md"
cp -R "$SKILL_DIR/scripts" "$SKILL_ROOT/youtube-downloader-agent/scripts"
chmod +x "$SKILL_ROOT/youtube-downloader-agent/scripts/"*.sh \
  "$SKILL_ROOT/youtube-downloader-agent/scripts/"*.py \
  "$INSTALL_ROOT/youtube-downloader-agent" 2>/dev/null || true

YOUTUBE_DOWNLOADER_AGENT_BIN="$INSTALL_ROOT/youtube-downloader-agent" \
  "$SKILL_ROOT/youtube-downloader-agent/scripts/youtube_downloader_agent.py" self-test
printf 'Installed binary: %s\n' "$INSTALL_ROOT/youtube-downloader-agent"
printf 'Installed skill: %s\n' "$SKILL_ROOT/youtube-downloader-agent"
printf 'Start a new Hermes session before relying on the new skill.\n'
