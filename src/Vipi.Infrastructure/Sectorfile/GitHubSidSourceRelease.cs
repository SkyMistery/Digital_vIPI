using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Sectorfile;

/// <summary>
/// <b>Che cosa dice di sé il sectorfile Aurora</b>: il ciclo AIRAC che dichiara, e quando è cambiato
/// l'ultima volta. Adattatore di <see cref="ISidSourceRelease"/>; carta
/// <c>docs/feature/2026-09-02-il-ciclo-entrante.md</c> §AW2.
///
/// <para><b>Il ciclo dichiarato.</b> Il repo tiene <c>CHANGELOG/&lt;ciclo&gt;.txt</c>, uno per AIRAC, e il
/// nome più alto <i>è</i> il ciclo del contenuto pubblicato. Misurato il 2 settembre 2026: il più alto era
/// <c>2608.txt</c>, che si apre con «AIRAC A2608 IN VIGORE DAL 06/08/2026» — la stessa data che calcola
/// <c>AiracService</c> — e <b>2609.txt non c'era</b>. Cioè: quel giorno la sorgente non aveva ancora i dati
/// del ciclo entrante, ed è la risposta alla domanda «perché il sito non pubblica le SID del 2609».</para>
///
/// <para><b>Il confronto è fra DATE, non fra stringhe.</b> «2701» viene dopo «2613», ma non in ordine
/// alfabetico: il file più alto si sceglie sulla data efficace del ciclo che nomina.</para>
///
/// <para><b>La data di cambiamento</b> resta come ripiego, dalla API dei commit — una chiamata:</para>
/// <code>GET /repos/{owner}/{repo}/commits?path={dir}&amp;per_page=1  →  [0].commit.committer.date</code>
/// <para>⚠️ È per <b>cartella</b>: chiedere per ICAO costerebbe una richiesta per scalo — decine — contro
/// una quota anonima di sessanta all'ora, per una risposta che a quel punto serve solo come ripiego.
/// ⚠️ E non si passa dagli header: misurato, <c>raw.githubusercontent.com</c> manda <c>ETag</c> ma
/// <b>non</b> <c>Last-Modified</c>.</para>
///
/// <para>⚠️ <b>Non solleva mai.</b> Rete giù, quota esaurita, formato diverso: torna
/// <see cref="SidSourceRelease.Muta"/> e l'import prosegue coi ripieghi di <c>SidStampCycle</c>. Un import
/// che cade perché una API di contorno ha dato 403 sarebbe un danno molto più grande della domanda a cui non
/// si è saputo rispondere.</para>
/// </summary>
public sealed class GitHubSidSourceRelease : ISidSourceRelease
{
    private readonly HttpClient _http;
    private readonly SectorfileOptions _opt;
    private readonly SectorfileCache _cache;
    private readonly Vipi.Domain.Services.IAiracService _airac;
    private readonly ILogger<GitHubSidSourceRelease> _log;

    public GitHubSidSourceRelease(HttpClient http, IOptions<SectorfileOptions> opt, SectorfileCache cache,
        Vipi.Domain.Services.IAiracService airac, ILogger<GitHubSidSourceRelease> log)
    {
        _http = http;
        _opt = opt.Value;
        _cache = cache;
        _airac = airac;
        _log = log;
    }

    public Task<SidSourceRelease> ReadAsync(CancellationToken ct = default) =>
        _cache.GetSidSourceReleaseAsync(LeggiAsync, ct);

    private async Task<SidSourceRelease> LeggiAsync(CancellationToken ct)
    {
        var ciclo = await CicloDichiaratoAsync(ct);
        var cambiata = ciclo is null ? await UltimoCambiamentoAsync(ct) : null;   // il ripiego si chiede solo se serve
        var esito = new SidSourceRelease(ciclo, cambiata);

        if (ciclo is not null)
            _log.LogInformation("Sorgente SID: ciclo dichiarato {Ciclo}.", ciclo);
        else if (cambiata is not null)
            _log.LogWarning("Sorgente SID: ciclo NON dichiarato; si usa l'ultimo cambiamento " +
                "({Data:yyyy-MM-dd HH:mm}Z) più un ciclo di attesa.", cambiata);
        else
            _log.LogWarning("Sorgente SID muta: né ciclo dichiarato né data di cambiamento. " +
                "Il ciclo d'entrata scende all'ultimo giro riuscito.");

        return esito;
    }

    /// <summary>Il ciclo nominato dal file di changelog più recente, o null.</summary>
    private async Task<string?> CicloDichiaratoAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opt.SidChangelogUrl)) return null;
        try
        {
            using var resp = await _http.GetAsync(_opt.SidChangelogUrl, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("Changelog della sorgente SID non letto: {Status} da {Url}.",
                    (int)resp.StatusCode, _opt.SidChangelogUrl);
                return null;
            }
            using var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct);
            return PiuRecente(doc);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Changelog della sorgente SID non letto.");
            return null;
        }
    }

    /// <summary>
    /// Il ciclo più avanti fra i nomi dei file di changelog. ⚠️ La radice può essere un <b>oggetto</b> e non
    /// un array — è la forma degli errori di GitHub (<c>{"message": "API rate limit exceeded"}</c>) — e
    /// <c>EnumerateArray</c> su un oggetto solleva <c>InvalidOperationException</c>, non <c>JsonException</c>.
    /// Si controlla il tipo prima.
    /// </summary>
    private string? PiuRecente(JsonDocument? doc)
    {
        if (doc is null || doc.RootElement.ValueKind != JsonValueKind.Array) return null;

        string? migliore = null;
        DateTime quando = DateTime.MinValue;
        foreach (var voce in doc.RootElement.EnumerateArray())
        {
            if (voce.ValueKind != JsonValueKind.Object) continue;
            if (!voce.TryGetProperty("name", out var n) || n.ValueKind != JsonValueKind.String) continue;

            var ciclo = CicloDalNome(n.GetString());
            if (ciclo is null) continue;
            // Il confronto è sulle DATE: "2701" viene dopo "2613", ma non in ordine alfabetico.
            DateTime efficace;
            try { efficace = _airac.EffectiveUtcForCycle(ciclo); }
            catch (ArgumentException) { continue; }
            if (efficace <= quando) continue;
            quando = efficace;
            migliore = ciclo;
        }
        return migliore;
    }

    /// <summary>
    /// «2608.txt» → «2608». ⚠️ Nella cartella vera ci sono anche nomi come <c>2304_1.txt</c> — una revisione
    /// intermedia dello stesso ciclo: le quattro cifre iniziali sono il ciclo, e il resto non cambia da quando
    /// vale il contenuto.
    /// </summary>
    private static string? CicloDalNome(string? nome)
    {
        var m = Regex.Match(nome ?? "", @"^(\d{4})(?:[_\-.].*)?\.txt$", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : null;
    }

    private async Task<DateTime?> UltimoCambiamentoAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opt.SidCommitsUrl)) return null;
        try
        {
            using var resp = await _http.GetAsync(_opt.SidCommitsUrl, ct);
            if (!resp.IsSuccessStatusCode) return null;
            using var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct);
            return PrimaData(doc);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Data dell'ultimo cambiamento della sorgente SID non ottenuta.");
            return null;
        }
    }

    /// <summary>
    /// La data del commit più recente. ⚠️ Si legge <c>committer</c> e non <c>author</c>: su un contributo
    /// ripreso da altrove la data d'autore può essere di settimane prima, e quella che conta qui è quando il
    /// file è arrivato <b>nel repo che scarichiamo</b>.
    /// </summary>
    private static DateTime? PrimaData(JsonDocument? doc)
    {
        if (doc is null || doc.RootElement.ValueKind != JsonValueKind.Array) return null;
        foreach (var commit in doc.RootElement.EnumerateArray())
        {
            if (commit.ValueKind != JsonValueKind.Object) continue;
            if (!commit.TryGetProperty("commit", out var c) || c.ValueKind != JsonValueKind.Object) continue;
            if (!c.TryGetProperty("committer", out var who) || who.ValueKind != JsonValueKind.Object) continue;
            if (!who.TryGetProperty("date", out var d) || d.ValueKind != JsonValueKind.String) continue;
            if (d.TryGetDateTime(out var quando)) return quando.ToUniversalTime();
        }
        return null;
    }
}
