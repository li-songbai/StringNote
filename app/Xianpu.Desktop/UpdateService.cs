using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Windows;

namespace Xianpu.Desktop;

public sealed record UpdateCheckResult(
    bool HasUpdate,
    string LatestVersion,
    string ReleaseNotes,
    string DownloadUrl,
    string AssetName,
    string HtmlUrl);

public static class UpdateService
{
    public const string DefaultOwner = "li-songbai";
    public const string DefaultRepo = "StringNote";

    public static string CurrentVersion
    {
        get
        {
            var infoAttr = typeof(UpdateService).Assembly
                .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
                .FirstOrDefault()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(infoAttr))
            {
                var plusIdx = infoAttr.IndexOf('+');
                return plusIdx >= 0 ? infoAttr[..plusIdx] : infoAttr;
            }
            return typeof(UpdateService).Assembly.GetName().Version is { } v
                ? $"{v.Major}.{v.Minor}.{Math.Max(0, v.Build)}"
                : "0.1.0-beta.1";
        }
    }

    public static bool IsRepositoryConfigured =>
        !string.Equals(DefaultOwner, "YOUR_GITHUB_USER", StringComparison.OrdinalIgnoreCase);

    public static bool HasCustomUpdateUrl =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STRINGNOTE_UPDATE_URL"));

    public static string ReleasesPageUrl => $"https://github.com/{DefaultOwner}/{DefaultRepo}/releases";

    public static string IssuesPageUrl => $"https://github.com/{DefaultOwner}/{DefaultRepo}/issues";

    public static string LatestApiUrl =>
        Environment.GetEnvironmentVariable("STRINGNOTE_UPDATE_URL") is { Length: > 0 } envUrl
            ? envUrl
            : $"https://api.github.com/repos/{DefaultOwner}/{DefaultRepo}/releases/latest";

    public static string ReleasesApiListUrl =>
        $"https://api.github.com/repos/{DefaultOwner}/{DefaultRepo}/releases?per_page=1";

    public static async Task<UpdateCheckResult?> CheckForUpdatesAsync(
        HttpMessageHandler? handler = null,
        CancellationToken ct = default)
    {
        using var client = handler is null
            ? new HttpClient()
            : new HttpClient(handler, disposeHandler: false);

        client.Timeout = TimeSpan.FromSeconds(8);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("StringNote-Updater/1.0");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var response = await client.GetAsync(LatestApiUrl, ct).ConfigureAwait(false);
        HttpResponseMessage activeResponse = response;
        HttpResponseMessage? fallbackResponse = null;

        try
        {
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound && !HasCustomUpdateUrl)
            {
                fallbackResponse = await client.GetAsync(ReleasesApiListUrl, ct).ConfigureAwait(false);
                activeResponse = fallbackResponse;
            }

            if (!activeResponse.IsSuccessStatusCode)
                return null;

            await using var stream = await activeResponse.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                if (root.GetArrayLength() == 0) return null;
                root = root[0];
            }

            var tagName = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? "" : "";
            var latestVersionText = tagName.Trim().TrimStart('v', 'V');
            if (!TryParseVersion(latestVersionText, out var latestVersion))
                return null;

            var currentVersion = TryParseVersion(CurrentVersion, out var parsedCurrent)
                ? parsedCurrent
                : new Version(0, 1, 0, 0);

            var releaseNotes = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? "" : "";
            var htmlUrl = root.TryGetProperty("html_url", out var htmlProp) ? htmlProp.GetString() ?? ReleasesPageUrl : ReleasesPageUrl;

            var (assetName, downloadUrl) = SelectPreferredAsset(root);
            var hasUpdate = CompareVersions(latestVersionText, CurrentVersion, latestVersion, currentVersion) > 0;

            return new UpdateCheckResult(
                HasUpdate: hasUpdate,
                LatestVersion: latestVersionText,
                ReleaseNotes: releaseNotes,
                DownloadUrl: downloadUrl,
                AssetName: assetName,
                HtmlUrl: htmlUrl);
        }
        finally
        {
            fallbackResponse?.Dispose();
        }
    }

    internal static int CompareVersionStrings(string latestRaw, string currentRaw)
    {
        var latestNum = TryParseVersion(latestRaw, out var parsedLatest) ? parsedLatest : new Version(0, 0, 0, 0);
        var currentNum = TryParseVersion(currentRaw, out var parsedCurrent) ? parsedCurrent : new Version(0, 0, 0, 0);
        return CompareVersions(latestRaw, currentRaw, latestNum, currentNum);
    }

    private static int CompareVersions(string latestRaw, string currentRaw, Version latestNum, Version currentNum)
    {
        var numCmp = latestNum.CompareTo(currentNum);
        if (numCmp != 0) return numCmp;

        var latestPre = ExtractPreRelease(latestRaw);
        var currentPre = ExtractPreRelease(currentRaw);
        var hasLatestPre = latestPre.Length > 0;
        var hasCurrentPre = currentPre.Length > 0;

        if (!hasLatestPre && !hasCurrentPre) return 0;
        if (hasLatestPre != hasCurrentPre) return hasCurrentPre ? 1 : -1;

        var latestParts = latestPre.Split('.');
        var currentParts = currentPre.Split('.');
        var minLen = Math.Min(latestParts.Length, currentParts.Length);
        for (var i = 0; i < minLen; i++)
        {
            var lp = latestParts[i];
            var cp = currentParts[i];
            var lIsNum = int.TryParse(lp, out var lNum);
            var cIsNum = int.TryParse(cp, out var cNum);
            if (lIsNum && cIsNum)
            {
                var cmp = lNum.CompareTo(cNum);
                if (cmp != 0) return cmp;
            }
            else if (lIsNum != cIsNum)
            {
                // Per SemVer 2.0, numeric identifiers have lower precedence than non-numeric identifiers
                return lIsNum ? -1 : 1;
            }
            else
            {
                var cmp = string.Compare(lp, cp, StringComparison.OrdinalIgnoreCase);
                if (cmp != 0) return cmp;
            }
        }

        return latestParts.Length.CompareTo(currentParts.Length);
    }

    private static string ExtractPreRelease(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var cleaned = raw.Trim();
        var plusIdx = cleaned.IndexOf('+');
        if (plusIdx >= 0)
            cleaned = cleaned[..plusIdx];
        var dashIdx = cleaned.IndexOf('-');
        return dashIdx >= 0 && dashIdx + 1 < cleaned.Length ? cleaned[(dashIdx + 1)..] : "";
    }

    public static async Task<string> DownloadAssetAsync(
        string downloadUrl,
        string fileName,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        var safeFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
            safeFileName = "StringNote-Update.bin";

        var updateDir = Path.Combine(Path.GetTempPath(), "StringNote-Updates");
        Directory.CreateDirectory(updateDir);
        var targetPath = Path.Combine(updateDir, safeFileName);

        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("StringNote-Updater/1.0");

        using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        await using var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var destination = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

        var buffer = new byte[81920];
        long totalRead = 0;
        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), ct).ConfigureAwait(false);
            totalRead += bytesRead;
            if (totalBytes is > 0)
            {
                progress?.Report(Math.Clamp((double)totalRead / totalBytes.Value, 0.0, 1.0));
            }
        }

        progress?.Report(1.0);
        return targetPath;
    }

    public static void LaunchInstallerAndExit(string installerPath)
    {
        var ext = Path.GetExtension(installerPath);
        if (string.Equals(ext, ".exe", StringComparison.OrdinalIgnoreCase))
        {
            Process.Start(new ProcessStartInfo(installerPath, "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS")
            {
                UseShellExecute = true
            });
            Application.Current?.Shutdown();
            return;
        }

        if (string.Equals(ext, ".zip", StringComparison.OrdinalIgnoreCase))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{installerPath}\"")
            {
                UseShellExecute = true
            });
        }
    }

    private static (string AssetName, string DownloadUrl) SelectPreferredAsset(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out var assetsProp) || assetsProp.ValueKind != JsonValueKind.Array)
            return ("", "");

        var candidates = new List<(string Name, string Url)>();
        foreach (var asset in assetsProp.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
            var url = asset.TryGetProperty("browser_download_url", out var urlProp) ? urlProp.GetString() ?? "" : "";
            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(url))
                candidates.Add((name, url));
        }

        var setupExe = candidates.FirstOrDefault(a =>
            a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
            a.Name.Contains("Setup", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(setupExe.Url))
            return setupExe;

        var firstExe = candidates.FirstOrDefault(a =>
            a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(firstExe.Url))
            return firstExe;

        var firstZip = candidates.FirstOrDefault(a =>
            a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(firstZip.Url))
            return firstZip;

        return ("", "");
    }

    private static bool TryParseVersion(string? raw, out Version version)
    {
        version = new Version(0, 0, 0, 0);
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var cleaned = raw.Trim().TrimStart('v', 'V');
        var suffixIdx = cleaned.IndexOfAny(['-', '+']);
        if (suffixIdx >= 0)
            cleaned = cleaned[..suffixIdx];

        if (!Version.TryParse(cleaned, out var parsed))
            return false;

        version = new Version(
            Math.Max(0, parsed.Major),
            Math.Max(0, parsed.Minor),
            Math.Max(0, parsed.Build),
            Math.Max(0, parsed.Revision));
        return true;
    }
}
