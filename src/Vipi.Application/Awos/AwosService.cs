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

    /// <summary>
    /// Gli scali per cui il quadro si apre, in ordine di ICAO: quelli con almeno un documento pubblicato.
    ///
    /// <para>⚠️ È lo <b>stesso</b> insieme del cancello, e non è un dettaglio: un selettore che elencasse uno
    /// scalo che poi rifiuta di aprirsi sarebbe un gesto che non fa niente. Anche per un Editor l'elenco
    /// resta questo — è «che cosa vede il pubblico» — e uno scalo non pubblicato lui lo apre scrivendone
    /// l'indirizzo.</para>
    /// </summary>
    Task<IReadOnlyList<AwosAirport>> ElencoAsync(CancellationToken ct = default);
}

/// <inheritdoc cref="IAwosService"/>
public sealed class AwosService : IAwosService
{
    private readonly IAirportEditingService _scali;
    private readonly IDocumentAdminService _documenti;
    private readonly IWeatherProvider _meteo;
    private readonly IOnlineAtcProvider _online;

    public AwosService(IAirportEditingService scali, IDocumentAdminService documenti, IWeatherProvider meteo,
                       IOnlineAtcProvider online)
    {
        _scali = scali;
        _documenti = documenti;
        _meteo = meteo;
        _online = online;
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

        var atis = AtisDelloScalo(id);
        var attiva = AwosComposition.PistaAttiva(scalo.Rules, identificativi, metar,
            Spezza(atis?.PistePartenza), Spezza(atis?.PisteArrivo), atis?.Callsign);

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
            Atis: atis,
            AsOf: DateTimeOffset.UtcNow), AwosOutcome.Ok);
    }

    public async Task<IReadOnlyList<AwosAirport>> ElencoAsync(CancellationToken ct = default)
    {
        var docs = (await _documenti.ListAsync(ct))
            .Where(m => m.HasEffectiveRelease && !m.IsHidden
                        && m.Kind is ReleaseTargetType.Airport or ReleaseTargetType.AirportMil
                        && m.Scope.Length == 4)
            .ToList();

        return docs
            .GroupBy(m => m.Scope.ToUpperInvariant(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new AwosAirport(
                g.Key,
                // Il nome viene dal titolo del documento: è già quello che il pubblico legge altrove, e non
                // costa una seconda interrogazione all'anagrafica per una tendina.
                NomeDalTitolo(g.OrderBy(m => m.Kind == ReleaseTargetType.Airport ? 0 : 1).First().Title, g.Key),
                g.Any(m => m.Kind == ReleaseTargetType.Airport),
                g.Any(m => m.Kind == ReleaseTargetType.AirportMil)))
            .OrderBy(a => a.Icao, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// L'ATIS in onda su questo scalo, fra le postazioni online.
    ///
    /// <para>⚠️ Si preferisce la postazione <c>_ATIS</c>, poi la torre, poi qualunque altra dello scalo che
    /// trasmetta: quando su un campo ci sono ATIS e torre insieme, quella che parla ai piloti in anticipo è
    /// la prima, e le due possono dire lettere diverse per qualche minuto dopo un cambio.</para>
    ///
    /// <para>Nessuno online, o nessuno con un ATIS leggibile: <c>null</c>. Il quadro scrive «—», che è
    /// vero — e non una lettera vecchia tenuta lì perché faceva scena.</para>
    /// </summary>
    private AwosAtis? AtisDelloScalo(string icao)
    {
        var candidati = _online.GetCurrent().Details
            .Where(a => a.Callsign.StartsWith(icao + "_", StringComparison.OrdinalIgnoreCase))
            .Where(a => a.AtisLetter is not null || a.AtisArrRunways is not null || a.AtisDepRunways is not null)
            .OrderBy(a => a.Callsign.EndsWith("_ATIS", StringComparison.OrdinalIgnoreCase) ? 0
                        : a.Callsign.EndsWith("_TWR", StringComparison.OrdinalIgnoreCase) ? 1 : 2)
            .ThenBy(a => a.Callsign, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var scelto = candidati.FirstOrDefault();
        return scelto is null ? null
            : new AwosAtis(scelto.Callsign, scelto.AtisLetter, scelto.AtisTimeRaw, scelto.AtisText,
                           scelto.AtisArrRunways, scelto.AtisDepRunways);
    }

    private static IReadOnlyList<string>? Spezza(string? csv) => csv is null ? null : csv
        .Split(new[] { '/', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Il nome dello scalo dal titolo del documento: «vIPI — LIBC Crotone» → «Crotone».
    /// <para>La tendina scrive già l'ICAO da sé, e ripeterlo due volte in una riga larga così ruba lo spazio
    /// al nome, che è la parte per cui la si legge.</para>
    /// </summary>
    private static string NomeDalTitolo(string titolo, string icao)
    {
        var t = (titolo ?? "").Trim();
        foreach (var prefisso in new[] { "vIPI", "vSOP", "vLOA" })
            if (t.StartsWith(prefisso, StringComparison.OrdinalIgnoreCase))
                t = t[prefisso.Length..].TrimStart(' ', '—', '-', '–', ':');
        if (t.StartsWith(icao, StringComparison.OrdinalIgnoreCase))
            t = t[icao.Length..].TrimStart(' ', '—', '-', '–', ':');
        return t.Length == 0 ? icao : t;
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
