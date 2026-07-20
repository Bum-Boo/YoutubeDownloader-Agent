---
name: youtube-downloader-agent
description: "Probe and download user-owned or explicitly authorized YouTube videos, playlists, and channels through the bounded YoutubeDownloader Agent JSON CLI. Use when the user asks to inspect or download YouTube media into a specified local archive."
version: 1.0.0
author: Bum-Boo
license: MIT
platforms: [linux, windows, wsl]
metadata:
  hermes:
    tags: [youtube, downloader, media, archive, json-cli]
    homepage: https://github.com/Bum-Boo/YoutubeDownloader-Agent
---

# YoutubeDownloader Agent

Use this skill only for media the user owns or is explicitly authorized to download.

## Hard gates

1. Never infer authorization from a public URL alone. The user must directly ask to download the specific video/channel/playlist or affirm rights.
2. Never add `--confirm-rights` unless that authorization is present in the current request.
3. Probe before a multi-video download and report the resolved title/count.
4. For playlist/channel jobs, require an explicit finite `--limit` and verify available disk space first.
5. Keep output inside the user-approved `--output-root`. Do not write to unrelated folders.
6. Do not use cookies, browser profiles, account credentials, private videos, DRM bypassing, or paid content.
7. Do not publish or re-upload downloaded media unless separately requested and authorized.

## Runner

Invoke the bundled wrapper rather than constructing commands around the binary directly:

```bash
python3 scripts/youtube_downloader_agent.py self-test
python3 scripts/youtube_downloader_agent.py probe --url "<URL>" --limit 20
python3 scripts/youtube_downloader_agent.py download \
  --url "<URL>" \
  --confirm-rights \
  --output-root "<APPROVED_ROOT>" \
  --output "<APPROVED_DESTINATION>" \
  --quality 1080p \
  --format mp4 \
  --limit 1
```

The binary is resolved from `YOUTUBE_DOWNLOADER_AGENT_BIN` or `~/.local/share/youtube-downloader-agent/youtube-downloader-agent` (`.exe` on Windows).

## Workflow

1. Confirm the URL and approved destination.
2. For one video, run `probe`; for a playlist/channel, run `probe --limit N` and state the resolved count.
3. Check disk space when the request includes multiple videos.
4. Run `download` with explicit quality, format, limit, root, destination, and rights flag.
5. Parse JSON Lines. A successful process must end with `{"ok":true,"command":"download",...}` and exit code 0.
6. Verify every reported path exists. For video output, inspect resolution/audio with `ffprobe` when available.
7. Report completed count, failures, destination, and any partial results. Never call a partial batch complete.

## Defaults

- Video: `mp4`, `1080p`, subtitles included
- Audio-only: `mp3`
- Single URL: `--limit 1`
- Multi-video jobs: no implicit unlimited mode

## Failure handling

- `rights_confirmation_required`: stop and request explicit authorization.
- `operation_failed` mentioning FFmpeg: install/provide FFmpeg within the approved tool area, then retry once.
- Path containment error: correct the destination; never weaken the root check.
- Network or extraction failure: preserve completed files, record failed URLs/IDs, and retry only failed items.

## Verification

```bash
python3 scripts/youtube_downloader_agent.py self-test
```

Expected: exit code 0 and JSON containing `"ok":true` and `"command":"self-test"`.
