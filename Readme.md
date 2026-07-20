# YoutubeDownloader Agent

[![Upstream](https://img.shields.io/badge/upstream-Tyrrrz%2FYoutubeDownloader-blue)](https://github.com/Tyrrrz/YoutubeDownloader)
[![License](https://img.shields.io/badge/license-MIT-green)](License.txt)
[![Interface](https://img.shields.io/badge/interface-JSON%20Lines-6f42c1)](#json-contract)

**YoutubeDownloader Agent** is an agent-friendly fork of [Tyrrrz/YoutubeDownloader](https://github.com/Tyrrrz/YoutubeDownloader). It keeps the original Avalonia desktop app and download core while adding a bounded, machine-readable CLI and a ready-to-package Hermes Agent skill.

> Download only videos that you own or are explicitly authorized to use. You are responsible for complying with copyright, platform terms, and local law.

## Why this fork exists

The upstream project is optimized for interactive desktop use. AI agents need a narrower contract:

- deterministic commands and exit codes;
- JSON Lines output instead of GUI state;
- explicit rights confirmation before any download;
- output-root containment;
- bounded playlist/channel downloads;
- no browser-profile, cookie, or login automation;
- a reusable Hermes skill wrapper.

## Features

### Agent CLI

- Probe a video, playlist, channel, or search query without downloading
- Download MP4, WebM, MP3, or OGG
- Select `highest`, `1080p`, `720p`, `480p`, `360p`, or `lowest`
- Embed available subtitles by default
- Limit playlist/channel jobs with `--limit`
- Resume interrupted batches with a persistent video-ID archive
- Retry individual failures without stopping the remaining queue
- Record completed and failed items in a JSON Lines manifest
- Emit progress, completion, skip, retry, failure, and summary events as JSON Lines
- Reject output paths outside `--output-root`
- Refuse downloads unless `--confirm-rights` is present

### Original desktop application

The upstream Avalonia GUI remains available and continues to use the same `YoutubeDownloader.Core` project.

### Hermes Agent integration

The repository includes an installable skill at:

```text
skills/youtube-downloader-agent/
```

The skill wraps the CLI without exposing shell construction to the model and preserves the same rights and path-containment gates.

## CLI quick start

Requirements:

- .NET SDK 10 for source builds
- FFmpeg for audio/video merging

```bash
dotnet restore YoutubeDownloader.slnx
dotnet build YoutubeDownloader.slnx -c Release
```

Probe metadata without downloading:

```bash
dotnet run --project YoutubeDownloader.Agent -- \
  probe --url "https://www.youtube.com/watch?v=VIDEO_ID" --limit 5
```

Download one authorized video at the highest available quality as MP4:

```bash
dotnet run --project YoutubeDownloader.Agent -- \
  download \
  --url "https://www.youtube.com/watch?v=VIDEO_ID" \
  --confirm-rights \
  --output-root "/media/archive" \
  --output "/media/archive/incoming" \
  --quality highest \
  --format mp4
```

For playlists and channels, add an explicit bound:

```bash
--limit 25
```

## Commands

| Command | Purpose | Network | Writes media |
|---|---|---:|---:|
| `probe` | Resolve metadata and enumerate videos | Yes | No |
| `download` | Download and merge authorized media | Yes | Yes |
| `self-test` | Test path containment and argument handling | No | No |

Run `youtube-downloader-agent --help` for all options.

## JSON contract

Progress event:

```json
{"ok":true,"event":"progress","videoId":"VIDEO_ID","percent":40}
```

Completed file:

```json
{"ok":true,"event":"completed","videoId":"VIDEO_ID","path":"/media/archive/video.mp4","bytes":123456}
```

Final summary:

```json
{"ok":true,"command":"download","count":1,"files":[{"id":"VIDEO_ID","path":"/media/archive/video.mp4"}]}
```

Failure:

```json
{"ok":false,"error":{"code":"rights_confirmation_required","message":"..."}}
```

## Hermes skill installation

Build and install the CLI plus skill for the active Hermes profile:

```bash
bash skills/youtube-downloader-agent/scripts/install-local.sh
```

The installer:

1. publishes a self-contained binary into `~/.local/share/youtube-downloader-agent/`;
2. copies only the skill package into the active profile skill directory;
3. does not edit credentials, gateway configuration, or another profile;
4. does not restart Hermes.

Verify installation in a new shell/session:

```bash
python3 ~/.hermes/profiles/${HERMES_PROFILE:-friren}/skills/youtube-downloader-agent/scripts/youtube_downloader_agent.py self-test
hermes skills list
```

To package without installing:

```bash
bash skills/youtube-downloader-agent/scripts/package-skill.sh
```

This creates `artifacts/youtube-downloader-agent-hermes-skill.zip`.

## Safety model

- `download` always requires a human-confirmed rights gate.
- The wrapper never adds `--confirm-rights` by itself.
- Output must remain within the caller-provided root.
- Cookies, browser sessions, account login, DRM bypassing, and secret storage are out of scope.
- Agents should probe first, use an explicit `--limit`, verify free disk space for large jobs, and inspect the final JSON summary.

## Build and publish

```bash
dotnet publish YoutubeDownloader.Agent/YoutubeDownloader.Agent.csproj \
  -c Release -r win-x64 --self-contained true
```

## Project layout

```text
YoutubeDownloader/             Original Avalonia GUI
YoutubeDownloader.Core/        Shared resolving and download core
YoutubeDownloader.Agent/       JSON-first CLI
skills/youtube-downloader-agent/ Hermes skill and safe wrapper
AGENT.md                       Detailed operator notes
```

## Upstream and license

This repository is a fork of [Tyrrrz/YoutubeDownloader](https://github.com/Tyrrrz/YoutubeDownloader), created by Oleksii Holub. The original copyright notice and MIT license are preserved in [`License.txt`](License.txt).

Upstream documentation, project terms, and community links remain available in the original repository. Changes specific to this fork are maintained by Bum-Boo.
