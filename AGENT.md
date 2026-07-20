# YoutubeDownloader Agent

AI agent가 **소유하거나 사용 허가를 받은 YouTube 영상**을 예측 가능한 JSON 인터페이스로 조회·다운로드하기 위한 CLI다. 원본 Avalonia GUI와 Core는 그대로 유지하고 `YoutubeDownloader.Agent` 프로젝트만 추가한다.

## 안전 계약

- 다운로드는 `--confirm-rights` 없이는 실행되지 않는다.
- 결과 파일은 `--output-root` 내부에만 생성된다.
- 계정 쿠키, 브라우저 프로필, 로그인 자동화는 받거나 읽지 않는다.
- 기본 최대 다운로드 수는 1개다. 재생목록/채널은 `--limit`으로 명시한다.
- 상태와 오류는 JSON Lines 형식으로 출력한다.

## 사용법

```powershell
# 메타데이터 확인(다운로드 없음)
dotnet run --project YoutubeDownloader.Agent -- probe --url "https://youtu.be/VIDEO_ID"

# 권한이 확인된 단일 영상 다운로드
dotnet run --project YoutubeDownloader.Agent -- download `
  --url "https://youtu.be/VIDEO_ID" `
  --confirm-rights `
  --output-root "C:\PrestigeMedia" `
  --output "C:\PrestigeMedia\incoming" `
  --quality 1080p `
  --format mp4

# 로컬 안전성 테스트(네트워크·다운로드 없음)
dotnet run --project YoutubeDownloader.Agent -- self-test
```

## Agent 출력 예시

```json
{"ok":true,"event":"progress","videoId":"...","percent":40}
{"ok":true,"event":"completed","videoId":"...","path":"C:\\PrestigeMedia\\incoming\\video.mp4","bytes":123456}
{"ok":true,"command":"download","count":1,"files":[...]}
```

오류는 성공 출력과 같은 stdout 채널에 다음 형태로 기록되며, 프로세스 종료 코드는 0이 아니다.

```json
{"ok":false,"error":{"code":"rights_confirmation_required","message":"..."}}
```

## 빌드

.NET SDK 10과 FFmpeg가 필요하다.

```powershell
dotnet restore YoutubeDownloader.slnx
dotnet build YoutubeDownloader.slnx -c Release
dotnet publish YoutubeDownloader.Agent/YoutubeDownloader.Agent.csproj -c Release -r win-x64 --self-contained true
```

## Upstream

이 저장소는 [Tyrrrz/YoutubeDownloader](https://github.com/Tyrrrz/YoutubeDownloader)의 MIT 라이선스 포크다. 원저작자 고지와 `License.txt`를 보존한다.
