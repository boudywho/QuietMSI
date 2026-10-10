using System.Text.Json;
using QuietMSI.UI;

if (args.Contains("--online"))
{
    var release = await UpdateChecker.CheckAsync(CancellationToken.None);
    Console.WriteLine("PASS real GitHub request, bounded body and parser: " + (release?.Version.ToString() ?? "no newer complete release"));
    return;
}

static byte[] Releases(params object[] values) => JsonSerializer.SerializeToUtf8Bytes(values);
static object Release(string version, bool draft = false, string? url = null, bool complete = true) => new {
    tag_name = "v" + version, draft, prerelease = true,
    html_url = url ?? UpdateChecker.ReleasesPage + "/tag/v" + version,
    assets = complete ? new[] { new { name = $"QuietMSI-{version}-Setup-x64.exe" }, new { name = $"QuietMSI-{version}-Portable-win-x64.zip" } } : Array.Empty<object>()
};
static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS " + message); }
var current = new Version(0, 6, 0, 0);
Check(UpdateChecker.Parse(Releases(Release("0.6.0"), Release("0.5.0")), current) is null, "same/older releases do not offer an update, including normalized fourth version component");
Check(UpdateChecker.Parse(Releases(Release("0.7.0"), Release("0.9.0"), Release("0.8.0")), current)?.Version == new Version(0, 9, 0), "choose newest complete experimental release independent of API order");
Check(UpdateChecker.Parse(Releases(Release("1.0.0", draft: true), Release("2.0.0", complete: false), Release("3.0.0", url: "https://malicious.example/update"), Release("4.0.0", url: "https://github.com.evil.example/update")), current) is null, "ignore drafts, incomplete downloads and untrusted release URLs");
Check(UpdateChecker.Parse(Releases(Release("not-a-version")), current) is null, "ignore invalid version tags");
try { UpdateChecker.Parse(JsonSerializer.SerializeToUtf8Bytes(new { message = "error" }), current); throw new Exception("Accepted invalid response"); } catch (IOException) { Console.WriteLine("PASS invalid root fails visibly"); }
try { UpdateChecker.Parse(new byte[1024 * 1024 + 1], current); throw new Exception("Accepted oversized response"); } catch (IOException) { Console.WriteLine("PASS response size is bounded"); }
