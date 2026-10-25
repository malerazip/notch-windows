using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Notch.App.Services;

internal sealed record LyricLine(TimeSpan Time, string Text);

internal sealed record Lyrics(IReadOnlyList<LyricLine> Synced, string Plain, bool Instrumental)
{
    public static readonly Lyrics None = new([], "", false);
    public bool IsEmpty => Synced.Count == 0 && Plain.Length == 0 && !Instrumental;
}

/// <summary>Letras sincronizadas do LRCLIB (lrclib.net), gratuito e sem chave de API.</summary>
internal sealed partial class LyricsService(HttpClient http)
{
    private readonly Dictionary<string, Lyrics> _cache = new();

    public async Task<Lyrics> FindAsync(string title, string artist, string album, TimeSpan duration, CancellationToken ct)
    {
        if (title.Length == 0) return Lyrics.None;
        var key = $"{artist}\n{title}";
        if (_cache.TryGetValue(key, out var cached)) return cached;

        var cleanTitle = CleanTitle(title);
        var result = Lyrics.None;
        try
        {
            // 1) busca exata (artista + título + álbum + duração)
            if (duration > TimeSpan.Zero)
            {
                var url = "https://lrclib.net/api/get?track_name=" + Uri.EscapeDataString(cleanTitle)
                    + "&artist_name=" + Uri.EscapeDataString(artist)
                    + "&album_name=" + Uri.EscapeDataString(album)
                    + "&duration=" + ((int)duration.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                using var resp = await http.GetAsync(url, ct);
                if (resp.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
                    result = Parse(doc.RootElement);
                }
            }

            // 2) busca aproximada
            if (result.IsEmpty)
            {
                var url = "https://lrclib.net/api/search?track_name=" + Uri.EscapeDataString(cleanTitle)
                    + "&artist_name=" + Uri.EscapeDataString(artist);
                var json = await http.GetStringAsync(url, ct);
                using var doc = JsonDocument.Parse(json);
                JsonElement? best = null;
                var bestScore = double.MaxValue;
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    var hasSynced = item.TryGetProperty("syncedLyrics", out var s) && s.ValueKind == JsonValueKind.String;
                    var d = item.TryGetProperty("duration", out var dd) && dd.ValueKind == JsonValueKind.Number ? dd.GetDouble() : 0;
                    var score = (hasSynced ? 0 : 1000) + (duration > TimeSpan.Zero ? Math.Abs(d - duration.TotalSeconds) : 0);
                    if (score < bestScore) { bestScore = score; best = item.Clone(); }
                }
                if (best is { } b) result = Parse(b);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException or InvalidOperationException)
        {
            if (ct.IsCancellationRequested) return Lyrics.None;
            Core.Paths.WriteLog("LRCLIB: " + ex.Message);
            return Lyrics.None; // sem cache, para tentar de novo depois
        }

        _cache[key] = result;
        return result;
    }

    private static Lyrics Parse(JsonElement e)
    {
        var instrumental = e.TryGetProperty("instrumental", out var ins) && ins.ValueKind == JsonValueKind.True;
        var plain = e.TryGetProperty("plainLyrics", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : "";
        var synced = new List<LyricLine>();
        if (e.TryGetProperty("syncedLyrics", out var s) && s.ValueKind == JsonValueKind.String)
        {
            foreach (var raw in (s.GetString() ?? "").Split('\n'))
            {
                var matches = TimeTag().Matches(raw);
                if (matches.Count == 0) continue;
                var text = TimeTag().Replace(raw, "").Trim();
                foreach (Match m in matches)
                {
                    var min = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                    var sec = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                    synced.Add(new LyricLine(TimeSpan.FromSeconds(min * 60 + sec), text));
                }
            }
            synced.Sort((a, b) => a.Time.CompareTo(b.Time));
        }
        return new Lyrics(synced, plain, instrumental);
    }

    /// <summary>Remove sufixos como "- Remastered 2011" e "(feat. X)" que atrapalham a busca.</summary>
    private static string CleanTitle(string title)
    {
        var t = Regex.Replace(title, @"\s*[\(\[](feat\.?|ft\.?|with|remaster|live|ao vivo|official|video|clipe|lyric)[^\)\]]*[\)\]]", "", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"\s+-\s+(\d{4}\s+)?(remaster(ed)?|live|ao vivo|radio edit|single version).*$", "", RegexOptions.IgnoreCase);
        return t.Trim();
    }

    [GeneratedRegex(@"\[(\d+):(\d+(?:\.\d+)?)\]")]
    private static partial Regex TimeTag();
}
