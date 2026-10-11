using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Arcadia.Core.Updates;

namespace Arcadia.ControllerSupport;

public sealed record ReleaseAsset(string Name, Uri DownloadUrl, long Size, string? Sha256);

public sealed record Release(string Tag, Version? Version, IReadOnlyList<ReleaseAsset> Assets)
{
    public ReleaseAsset? FindAsset(Func<string, bool> match) => Assets.FirstOrDefault(a => match(a.Name));
}

/// <summary>Reads release information from the public GitHub API.</summary>
public sealed class GitHubReleaseClient(HttpClient http)
{
    public async Task<Release?> GetLatestAsync(string owner, string repository, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{owner}/{repository}/releases/latest");
        request.Headers.UserAgent.ParseAdd("Arcadia");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var dto = await response.Content.ReadFromJsonAsync<ReleaseDto>(cancellationToken);
        if (dto?.TagName is null)
        {
            return null;
        }

        var assets = (dto.Assets ?? [])
            .Where(a => a.Name is not null && a.BrowserDownloadUrl is not null)
            .Select(a => new ReleaseAsset(a.Name!, new Uri(a.BrowserDownloadUrl!), a.Size, ParseDigest(a.Digest)))
            .ToList();
        return new Release(dto.TagName, ParseVersion(dto.TagName), assets);
    }

    /// <summary>The most recent published releases with their notes, drafts excluded.</summary>
    public async Task<IReadOnlyList<PublishedRelease>> GetReleasesAsync(string owner, string repository, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{owner}/{repository}/releases?per_page=50");
        request.Headers.UserAgent.ParseAdd("Arcadia");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var releases = await response.Content.ReadFromJsonAsync<List<ReleaseDto>>(cancellationToken) ?? [];
        return releases
            .Where(r => r.TagName is not null && !r.Draft)
            .Select(r => new PublishedRelease(r.TagName!, r.Body ?? string.Empty, r.PublishedAt, r.Prerelease))
            .ToList();
    }

    /// <summary>Parses tags like <c>v0.6.1</c> or <c>v.0.9.8.1</c>.</summary>
    public static Version? ParseVersion(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        var text = tag.Trim().TrimStart('v', 'V').TrimStart('.');
        var dash = text.IndexOf('-');
        if (dash >= 0)
        {
            text = text[..dash];
        }

        return Version.TryParse(text, out var version) ? version : null;
    }

    private static string? ParseDigest(string? digest) =>
        digest is not null && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? digest[7..].ToLowerInvariant() : null;

    private sealed class ReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("assets")]
        public List<AssetDto>? Assets { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("published_at")]
        public DateTimeOffset? PublishedAt { get; set; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }

        [JsonPropertyName("draft")]
        public bool Draft { get; set; }
    }

    private sealed class AssetDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("digest")]
        public string? Digest { get; set; }
    }
}

public static class VerifiedDownload
{
    /// <summary>
    /// Downloads an asset to <paramref name="targetFile"/> and checks its SHA-256 against the
    /// digest GitHub publishes. Assets without a published digest are rejected.
    /// </summary>
    public static async Task DownloadAsync(HttpClient http, ReleaseAsset asset, string targetFile, CancellationToken cancellationToken = default)
    {
        if (asset.Sha256 is null)
        {
            throw new InvalidOperationException($"Für {asset.Name} ist keine Prüfsumme veröffentlicht – Download abgebrochen.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
        using (var request = new HttpRequestMessage(HttpMethod.Get, asset.DownloadUrl))
        {
            request.Headers.UserAgent.ParseAdd("Arcadia");
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var file = File.Create(targetFile);
            await response.Content.CopyToAsync(file, cancellationToken);
        }

        string actual;
        await using (var file = File.OpenRead(targetFile))
        {
            actual = Convert.ToHexStringLower(await SHA256.HashDataAsync(file, cancellationToken));
        }

        if (!string.Equals(actual, asset.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(targetFile);
            throw new InvalidOperationException($"Prüfsumme von {asset.Name} stimmt nicht – Download verworfen.");
        }
    }
}
