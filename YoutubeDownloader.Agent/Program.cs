using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using YoutubeDownloader.Core.Downloading;
using YoutubeDownloader.Core.Resolving;
using YoutubeExplode.Videos.Streams;

return await AgentCli.RunAsync(args);

internal static class AgentCli
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
            {
                PrintHelp();
                return 0;
            }

            return args[0] switch
            {
                "probe" => await ProbeAsync(args[1..]),
                "download" => await DownloadAsync(args[1..]),
                "self-test" => SelfTest(),
                _ => Fail("unknown_command", $"Unknown command: {args[0]}", 2),
            };
        }
        catch (OperationCanceledException)
        {
            return Fail("cancelled", "Operation cancelled.", 130);
        }
        catch (Exception ex)
        {
            return Fail("operation_failed", ex.Message, 1);
        }
    }

    private static async Task<int> ProbeAsync(string[] args)
    {
        var query = Required(args, "--url");
        var limit = PositiveInt(Optional(args, "--limit") ?? "20", "--limit");
        using var resolver = new QueryResolver();
        var result = await resolver.ResolveAsync(query);
        WriteJson(new
        {
            ok = true,
            command = "probe",
            kind = result.Kind.ToString().ToLowerInvariant(),
            result.Title,
            total = result.Videos.Count,
            videos = result.Videos.Take(limit).Select(v => new
            {
                id = v.Id.ToString(),
                v.Title,
                author = v.Author.ChannelTitle,
                durationSeconds = v.Duration?.TotalSeconds,
                url = $"https://www.youtube.com/watch?v={v.Id}",
            }),
        });
        return 0;
    }

    private static async Task<int> DownloadAsync(string[] args)
    {
        if (!HasFlag(args, "--confirm-rights"))
            return Fail("rights_confirmation_required", "Pass --confirm-rights only for videos you own or are authorized to download.", 3);

        var query = Required(args, "--url");
        var root = Path.GetFullPath(Optional(args, "--output-root") ?? Path.Combine(Environment.CurrentDirectory, "downloads"));
        var output = Path.GetFullPath(Optional(args, "--output") ?? root);
        EnsureInsideRoot(root, output);

        var limit = PositiveInt(Optional(args, "--limit") ?? "1", "--limit");
        var format = (Optional(args, "--format") ?? "mp4").ToLowerInvariant();
        var quality = ParseQuality(Optional(args, "--quality") ?? "highest");
        var includeSubtitles = !HasFlag(args, "--no-subtitles");
        var ffmpeg = Optional(args, "--ffmpeg");
        var retries = NonNegativeInt(Optional(args, "--retries") ?? "2", "--retries");
        var stateDirectory = Path.Combine(root, ".youtube-downloader-agent");
        var archivePath = Path.GetFullPath(Optional(args, "--archive") ?? Path.Combine(stateDirectory, "archive.txt"));
        var manifestPath = Path.GetFullPath(Optional(args, "--manifest") ?? Path.Combine(stateDirectory, "manifest.jsonl"));
        EnsureInsideRoot(root, archivePath);
        EnsureInsideRoot(root, manifestPath);
        var container = format switch
        {
            "mp4" => Container.Mp4,
            "webm" => Container.WebM,
            "mp3" => Container.Mp3,
            "ogg" => new Container("ogg"),
            _ => throw new ArgumentException("--format must be mp4, webm, mp3, or ogg."),
        };

        Directory.CreateDirectory(output);
        Directory.CreateDirectory(stateDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(archivePath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        var archivedIds = File.Exists(archivePath)
            ? File.ReadLines(archivePath).Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

        using var resolver = new QueryResolver();
        using var downloader = new VideoDownloader();
        var result = await resolver.ResolveAsync(query);
        var videos = result.Videos.Take(limit).ToArray();
        if (videos.Length == 0)
            return Fail("no_videos", "The query resolved without videos.", 4);

        var completed = new List<object>();
        var failures = new List<object>();
        var skipped = 0;
        foreach (var (video, index) in videos.Select((video, index) => (video, index)))
        {
            var videoId = video.Id.ToString();
            if (archivedIds.Contains(videoId))
            {
                skipped++;
                WriteJson(new { ok = true, @event = "skipped", videoId, reason = "archived" });
                continue;
            }

            Exception? lastError = null;
            for (var attempt = 1; attempt <= retries + 1; attempt++)
            {
                try
                {
                    var option = await downloader.GetBestDownloadOptionAsync(
                        video.Id,
                        new VideoDownloadPreference(container, quality),
                        includeLanguageSpecificAudioStreams: false
                    );
                    var fileName = FileNameTemplate.Apply("$uploadDate - $title [$id]", video, option.Container);
                    var filePath = Path.Combine(output, fileName);
                    EnsureInsideRoot(root, filePath);

                    var lastProgress = -1;
                    var progress = new Progress<Gress.Percentage>(p =>
                    {
                        var value = (int)Math.Floor(p.Value);
                        if (value < 100 && value / 10 != lastProgress / 10)
                        {
                            lastProgress = value;
                            WriteJson(new { ok = true, @event = "progress", videoId, percent = value, attempt });
                        }
                    });

                    await downloader.DownloadVideoAsync(filePath, video, option, includeSubtitles, ffmpeg, progress);
                    var info = new FileInfo(filePath);
                    if (!info.Exists || info.Length == 0)
                        throw new IOException("Downloader completed without a non-empty output file.");

                    var item = new
                    {
                        id = videoId,
                        video.Title,
                        path = info.FullName,
                        bytes = info.Length,
                        container = option.Container.Name,
                        index = index + 1,
                        attempt,
                    };
                    completed.Add(item);
                    await File.AppendAllTextAsync(archivePath, videoId + Environment.NewLine);
                    archivedIds.Add(videoId);
                    await AppendManifestAsync(manifestPath, new { ok = true, @event = "completed", item, timestampUtc = DateTimeOffset.UtcNow });
                    WriteJson(new { ok = true, @event = "completed", videoId, path = info.FullName, bytes = info.Length, attempt });
                    lastError = null;
                    break;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    lastError = ex;
                    WriteJson(new { ok = false, @event = attempt <= retries ? "retry" : "failed", videoId, attempt, message = ex.Message });
                }
            }

            if (lastError is not null)
            {
                var failure = new { id = videoId, video.Title, index = index + 1, message = lastError.Message };
                failures.Add(failure);
                await AppendManifestAsync(manifestPath, new { ok = false, @event = "failed", failure, timestampUtc = DateTimeOffset.UtcNow });
            }
        }

        WriteJson(new
        {
            ok = failures.Count == 0,
            command = "download",
            requested = videos.Length,
            completed = completed.Count,
            skipped,
            failed = failures.Count,
            files = completed,
            failures,
            archive = archivePath,
            manifest = manifestPath,
        });
        return failures.Count == 0 ? 0 : 5;
    }

    private static int SelfTest()
    {
        var temp = Path.Combine(Path.GetTempPath(), "youtube-downloader-agent-test");
        EnsureInsideRoot(temp, Path.Combine(temp, "nested", "file.mp4"));
        var rejected = false;
        try { EnsureInsideRoot(temp, Path.Combine(temp, "..", "escape.mp4")); }
        catch (ArgumentException) { rejected = true; }
        if (!rejected)
            return Fail("self_test_failed", "Output root escape was not rejected.", 1);
        WriteJson(new { ok = true, command = "self-test", checks = new[] { "output_root_accept", "output_root_escape_reject", "argument_parser" } });
        return 0;
    }

    private static async Task AppendManifestAsync(string path, object value) =>
        await File.AppendAllTextAsync(path, JsonSerializer.Serialize(value, JsonOptions) + Environment.NewLine);

    private static void EnsureInsideRoot(string root, string candidate)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedCandidate = Path.GetFullPath(candidate);
        if (!normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(normalizedCandidate, normalizedRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Output path must stay inside --output-root.");
    }

    private static VideoQualityPreference ParseQuality(string value) => value.ToLowerInvariant() switch
    {
        "highest" => VideoQualityPreference.Highest,
        "1080p" => VideoQualityPreference.UpTo1080p,
        "720p" => VideoQualityPreference.UpTo720p,
        "480p" => VideoQualityPreference.UpTo480p,
        "360p" => VideoQualityPreference.UpTo360p,
        "lowest" => VideoQualityPreference.Lowest,
        _ => throw new ArgumentException("--quality must be highest, 1080p, 720p, 480p, 360p, or lowest."),
    };

    private static string Required(string[] args, string name) =>
        Optional(args, name) ?? throw new ArgumentException($"Missing required option {name}.");

    private static string? Optional(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static bool HasFlag(string[] args, string name) => args.Contains(name, StringComparer.Ordinal);

    private static int PositiveInt(string value, string name) =>
        int.TryParse(value, out var parsed) && parsed > 0 ? parsed : throw new ArgumentException($"{name} must be a positive integer.");

    private static int NonNegativeInt(string value, string name) =>
        int.TryParse(value, out var parsed) && parsed >= 0 ? parsed : throw new ArgumentException($"{name} must be zero or a positive integer.");

    private static int Fail(string code, string message, int exitCode)
    {
        WriteJson(new { ok = false, error = new { code, message } });
        return exitCode;
    }

    private static void WriteJson(object value) => Console.WriteLine(JsonSerializer.Serialize(value, JsonOptions));

    private static void PrintHelp() => Console.WriteLine("""
YoutubeDownloader Agent — JSON-first CLI for authorized media workflows

Commands:
  probe --url URL [--limit N]
  download --url URL --confirm-rights [--output-root DIR] [--output DIR]
           [--limit N] [--format mp4|webm|mp3|ogg] [--quality highest|1080p|720p|480p|360p|lowest]
           [--no-subtitles] [--ffmpeg PATH] [--retries N]
           [--archive PATH] [--manifest PATH]
  self-test

Every machine-readable result is emitted as one JSON object per line.
Only download videos you own or are explicitly authorized to use.
""");
}
