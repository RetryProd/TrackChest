using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace ChestCounter;

/// <summary>Figures one player shares with their duo partner (never saved in the history).</summary>
public sealed record DuoStats(
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("last")] int Last,
    [property: JsonPropertyName("hasLast")] bool HasLast,
    [property: JsonPropertyName("session")] int Session,
    [property: JsonPropertyName("sessionMatches")] int SessionMatches,
    [property: JsonPropertyName("treasures")] int Treasures,
    /// <summary>Opened chests counted with the boost (pirate token estimate).</summary>
    [property: JsonPropertyName("pirate")] double Pirate,
    [property: JsonPropertyName("live")] bool Live)
{
    public static DuoStats From(TrackerData d, bool inProgress)
    {
        var last = d.Matches.OrderByDescending(m => m.MatchStartUtc ?? m.AnalyzedAtUtc).FirstOrDefault();
        var session = d.Matches.Where(ReportWriter.IsThisSession).ToList();
        return new DuoStats(
            d.Matches.Sum(m => m.ChestsTotal),
            last?.ChestsTotal ?? 0,
            last != null,
            session.Sum(m => m.ChestsTotal),
            session.Count,
            d.Matches.Sum(m => m.TreasureFinds.Count),
            Math.Round(d.Matches.Sum(m => m.ChestsOpened) * Boosts.Current, 2),
            inProgress);
    }
}

/// <summary>The partner as last received from the relay.</summary>
public sealed record DuoPartner(string Name, DuoStats Stats, DateTime ReceivedUtc);

/// <summary>
/// Duo mode: two players linked by a 6-character code add up their chests on the overlay while the link lasts.
/// The link is temporary (it ends when the app closes or with « Délier ») and the partner's figures are never
/// written to the history: each player's own counts stay exact.
/// </summary>
public static class Duo
{
    private static readonly HttpClient Http = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(10),
        DefaultRequestHeaders = { { "User-Agent", "TrackChest-Duo" } },
    };

    /// <summary>Own figures are re-sent at least this often so the link does not expire on the relay.</summary>
    private static readonly TimeSpan KeepAlive = TimeSpan.FromMinutes(20);

    private static string? _endpoint;
    private static string _player = "";
    private static DuoStats? _sentStats;
    private static string? _sentName;
    private static DateTime _sentAtUtc;

    public static string? Code { get; private set; }
    public static bool Linked => Code != null;
    /// <summary>The partner has joined the code.</summary>
    public static bool PartnerJoined { get; private set; }
    public static DuoPartner? Partner { get; private set; }
    public static DateTime? LastSyncUtc { get; private set; }
    public static string? LastError { get; private set; }

    /// <summary>Partner figures to add on the overlay, null when not linked or nothing received yet.</summary>
    public static DuoStats? PartnerStats => Linked ? Partner?.Stats : null;

    /// <summary>The relay lives next to the add-on catalog (same site): https://site/api/duo.</summary>
    public static bool Configure(Config config)
    {
        var catalog = AddonCatalog.ResolveUrl(config);
        if (!Uri.TryCreate(catalog, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            _endpoint = null;
            return false;
        }
        _endpoint = new Uri(uri, "/api/duo").ToString();
        return true;
    }

    public static async Task<string> Create(Config config)
    {
        Reset();
        _player = NewPlayerId();
        var result = await Send(config, new { action = "create", player = _player });
        Code = result.Code ?? throw new DuoException(T("Le serveur n’a pas renvoyé de code.", "The server sent no code."));
        return Code;
    }

    public static async Task Join(Config config, string code)
    {
        code = new string(code.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (code.Length != 6) throw new DuoException(T("Le code fait 6 caractères.", "The code is 6 characters long."));
        Reset();
        _player = NewPlayerId();
        await Send(config, new { action = "join", player = _player, code });
        Code = code;
        PartnerJoined = true;
    }

    /// <summary>Sends the own figures when they changed (or to keep the link alive) and fetches the partner's.</summary>
    public static async Task<bool> Sync(Config config, DuoStats mine, string? name, bool force = false)
    {
        if (Code == null) return false;
        var changed = force || mine != _sentStats || name != _sentName || DateTime.UtcNow - _sentAtUtc > KeepAlive;
        try
        {
            var result = await Send(config, changed
                ? new { action = "sync", player = _player, code = Code, name = name ?? "", stats = (DuoStats?)mine }
                : new { action = "sync", player = _player, code = Code, name = "", stats = (DuoStats?)null });
            if (changed) { _sentStats = mine; _sentName = name; _sentAtUtc = DateTime.UtcNow; }
            var before = Partner;
            PartnerJoined = result.Joined;
            // A partner whose app closed without leaving stops counting once their figures are no longer kept alive.
            var fresh = result.Partner != null
                && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - result.Partner.At < (long)(KeepAlive * 2.5).TotalMilliseconds;
            if (result.Partner?.Stats != null && fresh)
                Partner = new DuoPartner(string.IsNullOrWhiteSpace(result.Partner.Name) ? T("Partenaire", "Partner") : result.Partner.Name!, result.Partner.Stats, DateTime.UtcNow);
            else Partner = null;
            LastSyncUtc = DateTime.UtcNow;
            LastError = null;
            return before?.Stats != Partner?.Stats || before?.Name != Partner?.Name;
        }
        catch (DuoException ex) when (ex.NotLinked)
        {
            // The code expired or the partner left everything: the link ends here.
            Reset();
            LastError = ex.Message;
            return true;
        }
        catch (DuoException ex)
        {
            LastError = ex.Message;
            return false;
        }
    }

    public static async Task Leave(Config config)
    {
        var code = Code;
        var player = _player;
        Reset();
        if (code == null) return;
        try { await Send(config, new { action = "leave", player, code }); }
        catch (DuoException) { /* the relay forgets the link after 6 hours anyway */ }
    }

    private static void Reset()
    {
        Code = null;
        Partner = null;
        PartnerJoined = false;
        LastSyncUtc = null;
        LastError = null;
        _sentStats = null;
        _sentName = null;
        _sentAtUtc = default;
    }

    private static string NewPlayerId() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    private sealed record Reply(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("code")] string? Code,
        [property: JsonPropertyName("joined")] bool Joined,
        [property: JsonPropertyName("partner")] ReplyPartner? Partner,
        [property: JsonPropertyName("error")] string? Error);

    private sealed record ReplyPartner(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("stats")] DuoStats? Stats,
        [property: JsonPropertyName("at")] long At);

    private static async Task<Reply> Send(Config config, object body)
    {
        if (_endpoint == null && !Configure(config))
            throw new DuoException(T("Aucun serveur configuré pour le mode Duo.", "No server set up for Duo mode."));
        HttpResponseMessage response;
        try { response = await Http.PostAsJsonAsync(_endpoint, body); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new DuoException(T("Serveur Duo injoignable. Vérifie ta connexion.", "Duo server unreachable. Check your connection."));
        }
        using (response)
        {
            Reply? reply = null;
            try { reply = await response.Content.ReadFromJsonAsync<Reply>(); }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or NotSupportedException or HttpRequestException) { }
            if (response.IsSuccessStatusCode && reply is { Ok: true }) return reply;
            throw (reply?.Error, response.StatusCode) switch
            {
                ("unknown_code", _) => new DuoException(T("Code inconnu ou expiré.", "Unknown or expired code.")),
                ("full", _) => new DuoException(T("Ce code est déjà utilisé par deux joueurs.", "This code is already used by two players.")),
                ("not_linked", _) => new DuoException(T("La liaison a expiré.", "The link expired."), notLinked: true),
                (_, HttpStatusCode.TooManyRequests) => new DuoException(T("Trop d’essais. Réessaie dans quelques minutes.", "Too many tries. Try again in a few minutes.")),
                _ => new DuoException(T($"Le serveur Duo a répondu {(int)response.StatusCode}.", $"The Duo server answered {(int)response.StatusCode}.")),
            };
        }
    }

    /// <summary>French or English text (the other languages get the English one).</summary>
    public static string T(string fr, string en) => L.Code == "fr" ? fr : en;
}

public sealed class DuoException(string message, bool notLinked = false) : Exception(message)
{
    public bool NotLinked { get; } = notLinked;
}
