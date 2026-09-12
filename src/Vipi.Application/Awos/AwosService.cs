using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Application.Weather;
using Vipi.Domain;

namespace Vipi.Application.Awos;

/// <summary>
/// Compone il quadro vAWOS di uno scalo: anagrafica (piste, soglie, TA/TL) + METAR + pista in uso.
///
/// <para>⚠️ <b>Non aggiunge niente all'archivio</b>: è una vista su dati che esistono già. Le tre cose che il
/// prototipo si portava dietro cablate — rotte di pista, transition level, parser METAR — qui vengono
/// dall'anagrafica IVAO, dalla tabella dell'AIP dello scalo e dal decoder di casa.</para>
/// </summary>
public interface IAwosService
{
    /// <param name="perEditor">
    /// Chi guarda può aprire anche uno scalo non pubblicato. Arriva come <b>parametro</b> e non si chiede qui
    /// dentro: questo servizio lo usa anche un endpoint fuori da ogni pagina, e un livello letto dal contesto
    /// sbaglierebbe proprio lì.
    /// </param>
    /// <param name="metarDiProva">
    /// Bollettino iniettato a mano al posto di quello vero (il «Test METAR» del quadro).
    /// <para>⚠️ Chi chiama deve già aver verificato che sia staff: qui non c'è nessuna guardia, e non deve
    /// essercene una seconda — la prima sta alla porta, dove si sa chi bussa.</para>
    /// </param>
    Task<AwosResult> BuildAsync(string icao, bool perEditor, string? metarDiProva = null, CancellationToken ct = default);
}

/// <inheritdoc cref="IAwosService"/>
public sealed class AwosService : IAwosService
{
    private readonly IAirportEditingService _scali;
    private readonly IDocumentAdminService _documenti;
    private readonly IWeatherProvider _meteo;

    public AwosService(IAirportEditingService scali, IDocumentAdminService documenti, IWeatherProvider meteo)
    {
        _scali = scali;
        _documenti = documenti;
        _meteo = meteo;
    }

    public async Task<AwosResult> BuildAsync(string icao, bool perEditor, string? metarDiProva = null,
                                             CancellationToken ct = default)
    {
        var id = (icao ?? "").Trim().ToUpperInvariant();
        if (id.Length != 4) return AwosResult.Ignoto;

        var scalo = await _scali.LoadForViewAsync(id, ct);
        if (scalo is null) return AwosResult.Ignoto;

        var (vipi, vsop) = await DocumentiPubblicatiAsync(id, ct);
        if (!vipi && !vsop && !perEditor) return AwosResult.NonPubblicato;

        // Il METAR di prova NON passa dal provider: deve poter descrivere un tempo che non c'è, ed è tutto il
        // motivo per cui esiste. La provenienza in quel caso non si scrive: non viene da nessuna sorgente.
        WeatherReport? bollettino = null;
        string? raw = metarDiProva?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            bollettino = await _meteo.GetAsync(id, ct);
            raw = bollettino.Metar;
        }

        var metar = string.IsNullOrWhiteSpace(raw) ? null : MetarParser.ParseMetar(raw!);

        var piste = AwosComposition.Strisce(scalo.Runways);
        var identificativi = scalo.Runways
            .Select(r => (r.Ident ?? "").Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var attiva = AwosComposition.PistaAttiva(scalo.Rules, identificativi, metar);

        return new AwosResult(new AwosView(
            Icao: id,
            Nome: scalo.Name,
            AccCode: scalo.AccCode,
            HaVipi: vipi,
            HaVsop: vsop,
            MetarRaw: raw,
            MetarSource: metarDiProva is null ? bollettino?.MetarSource : null,
            MetarAsOf: metarDiProva is null ? bollettino?.AsOf : null,
            Metar: metar,
            TransitionAltitudeFt: scalo.TransitionAltitudeFt,
            TransitionLevel: AwosComposition.TransitionLevel(scalo.TransitionLevels, metar?.QnhHpa),
            Piste: piste,
            Attiva: attiva,
            AsOf: DateTimeOffset.UtcNow), AwosOutcome.Ok);
    }

    /// <summary>
    /// I due documenti dello scalo, filtrati col cancello di <b>ogni</b> elenco pubblico: release AIRAC
    /// effettiva e documento non nascosto (doc 10 §3f).
    ///
    /// <para>⚠️ Il cancello guarda i <b>documenti</b>, non la categoria dello scalo: cambiare categoria non
    /// tocca i documenti (carta 2026-09-11-categorie-aeroporto.md), e un vSOP pubblicato su un campo
    /// diventato «civile» resta leggibile finché qualcuno non lo nasconde. Aggiungere qui un filtro per
    /// categoria renderebbe il quadro irraggiungibile su uno scalo il cui documento invece si apre.</para>
    /// </summary>
    private async Task<(bool Vipi, bool Vsop)> DocumentiPubblicatiAsync(string icao, CancellationToken ct)
    {
        var docs = await _documenti.ListAsync(ct);
        bool Pubblicato(ReleaseTargetType tipo) => docs.Any(m =>
            m.Kind == tipo && m.HasEffectiveRelease && !m.IsHidden
            && string.Equals(m.Scope, icao, StringComparison.OrdinalIgnoreCase));

        return (Pubblicato(ReleaseTargetType.Airport), Pubblicato(ReleaseTargetType.AirportMil));
    }
}
