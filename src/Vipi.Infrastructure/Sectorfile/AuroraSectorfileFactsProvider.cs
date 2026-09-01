using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Sectorfile;

/// <summary>
/// Adapter GitHub dei tre file del sectorfile che descrivono posizioni, aeroporti e piste
/// (<c>OTHER/itfreq.frq</c>, <c>OTHER/itap.ap</c>, <c>OTHER/itrw.rw</c>).
///
/// <para>⚠️ <b>Non è un import.</b> Questi dati non entrano in nessuna tabella: la sorgente autoritativa
/// resta l'API IVAO (ADR-0006). Si leggono per <b>confrontarli</b> — carta
/// <c>docs/design/piano-coerenza-sectorfile.md</c>.</para>
///
/// <para>⚠️ <b>Nessuna fetta in <see cref="SectorfileCache"/></b>, a differenza di punti, poligoni e MRVA: là
/// la cache esiste perché gli stessi file li chiedono più percorsi (import, bottoni d'editor, suggerimenti).
/// Questi tre li legge <b>un chiamante solo, una volta ogni 24 ore</b>, e una copia tenuta in memoria
/// direbbe «confrontato adesso» mostrando file di ieri — cioè la sola cosa che questo giro non deve fare.</para>
/// </summary>
public sealed class AuroraSectorfileFactsProvider : ISectorfileFactsSource
{
    /// <summary>I tre file, relativi a <see cref="SectorfileOptions.RawBaseUrl"/>.</summary>
    private const string PathPositions = "OTHER/itfreq.frq";
    private const string PathAirports = "OTHER/itap.ap";
    private const string PathRunways = "OTHER/itrw.rw";

    private readonly HttpClient _http;
    private readonly SectorfileOptions _opt;
    private readonly ILogger<AuroraSectorfileFactsProvider> _log;

    public AuroraSectorfileFactsProvider(HttpClient http, IOptions<SectorfileOptions> opt,
        ILogger<AuroraSectorfileFactsProvider> log)
    {
        _http = http;
        _opt = opt.Value;
        _log = log;
    }

    public async Task<SectorfileFacts?> GetFactsAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opt.RawBaseUrl)) return null;   // sorgente non configurata

        var freq = await Testo(PathPositions, ct);
        var apt = await Testo(PathAirports, ct);
        var rwy = await Testo(PathRunways, ct);

        // ⚠️ Tre 404 insieme non sono «il sectorfile è vuoto»: sono «i file non stanno più lì». Confrontare
        // contro il vuoto aprirebbe un rilievo su OGNI posizione, aeroporto e pista che abbiamo.
        if (freq is null && apt is null && rwy is null)
        {
            _log.LogWarning("Coerenza sectorfile: nessuno dei tre file trovato sotto {Base} — confronto saltato.",
                _opt.RawBaseUrl);
            return null;
        }

        var facts = new SectorfileFacts(
            AuroraSectorfileParser.ParseAtcPositions(freq),
            AuroraSectorfileParser.ParseAirports(apt),
            AuroraSectorfileParser.ParseRunwayEnds(rwy));

        _log.LogInformation("Coerenza sectorfile: letti {Pos} posizioni, {Apt} aeroporti, {Rwy} estremità di pista.",
            facts.Positions.Count, facts.Airports.Count, facts.RunwayEnds.Count);
        return facts;
    }

    private Task<string?> Testo(string relative, CancellationToken ct) =>
        SectorfileRaw.GetTextOrNullAsync(_http, _opt.RawBaseUrl, relative, ct);
}
