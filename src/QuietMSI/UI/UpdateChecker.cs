using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace QuietMSI.UI;

internal sealed record UpdateRelease(Version Version, Uri Page);

internal static class UpdateChecker
{
    internal const string Repository = "boudywho/QuietMSI";
    internal const string ReleasesPage = "https://github.com/" + Repository + "/releases";
    private const int ResponseLimit = 1024 * 1024;
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(15) };
    internal static Version CurrentVersion => Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);

    internal static async Task<UpdateRelease?> CheckAsync(CancellationToken cancellation)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/" + Repository + "/releases?per_page=20");
        request.Headers.UserAgent.ParseAdd("QuietMSI/" + CurrentVersion.ToString(3));
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
        if (response.StatusCode == HttpStatusCode.NotFound) throw new IOException("The GitHub repository or releases are not available yet.");
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests) throw new IOException("GitHub temporarily limited update checks. Try again later.");
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > ResponseLimit) throw new IOException("GitHub returned an unexpectedly large response.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(chunk, deadline.Token)) != 0)
        {
            if (buffer.Length + count > ResponseLimit) throw new IOException("GitHub returned an unexpectedly large response.");
            buffer.Write(chunk, 0, count);
        }
        return Parse(buffer.ToArray(), CurrentVersion);
    }

    internal static UpdateRelease? Parse(byte[] json, Version current)
    {
        if (json.Length > ResponseLimit) throw new IOException("Release response exceeds the size limit.");
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array) throw new IOException("GitHub returned invalid release information.");
        UpdateRelease? newest = null;
        foreach (var release in document.RootElement.EnumerateArray())
        {
            if (!release.TryGetProperty("draft", out var draft) || draft.ValueKind != JsonValueKind.False) continue;
            if (!release.TryGetProperty("tag_name", out var tag) || tag.ValueKind != JsonValueKind.String) continue;
            if (!Version.TryParse(tag.GetString()?.TrimStart('v'), out var version) || version.Build < 0 || Normalize(version) <= Normalize(current)) continue;
            if (!release.TryGetProperty("html_url", out var page) || page.ValueKind != JsonValueKind.String
                || !Uri.TryCreate(page.GetString(), UriKind.Absolute, out var uri)
                || !uri.AbsoluteUri.StartsWith(ReleasesPage + "/tag/", StringComparison.Ordinal)) continue;
            // Offer only complete releases with both product distributions attached.
            if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) continue;
            var names = assets.EnumerateArray().Where(a => a.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String)
                .Select(a => a.GetProperty("name").GetString()).ToHashSet(StringComparer.Ordinal);
            var label = version.ToString(3);
            if (!names.Contains($"QuietMSI-{label}-Setup-x64.exe") || !names.Contains($"QuietMSI-{label}-Portable-win-x64.zip")) continue;
            if (newest is null || Normalize(version) > Normalize(newest.Version)) newest = new(version, uri);
        }
        return newest;
    }
    private static Version Normalize(Version value) => new(value.Major, value.Minor, Math.Max(0, value.Build), Math.Max(0, value.Revision));
}
