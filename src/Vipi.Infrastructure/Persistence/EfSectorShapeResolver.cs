using Microsoft.EntityFrameworkCore;
using Vipi.Application.Airspace;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Services;

namespace Vipi.Infrastructure.Persistence;

/// <inheritdoc cref="ISectorShapeResolver"/>
public sealed class EfSectorShapeResolver : ISectorShapeResolver
{
    private readonly VipiDbContext _db;
    private readonly ISectorAirspaceBindings _agganci;
    private readonly ISectorShapeParts _pezzi;
    private readonly ShapeReleaseContext? _release;
    private readonly IAiracService? _airac;

    /// <param name="release">Il contesto del congelamento: fuori da esso è a vuoto e le shape del catalogo si
    /// leggono come sempre. ⚠️ Opzionale perché i test costruiscono il risolutore col solo contesto.</param>
    public EfSectorShapeResolver(
        VipiDbContext db, ISectorAirspaceBindings agganci, ISectorShapeParts pezzi,
        ShapeReleaseContext? release = null, IAiracService? airac = null)
    {
        _db = db;
        _agganci = agganci;
        _pezzi = pezzi;
        _release = release;
        _airac = airac;
    }

    public async Task<IReadOnlyDictionary<string, SectorShape>> ResolveAsync(
        IReadOnlyList<string> callsigns, CancellationToken ct = default)
    {
        var esito = new Dictionary<string, SectorShape>(StringComparer.OrdinalIgnoreCase);
        if (callsigns.Count == 0) return esito;

        var cercati = callsigns.Select(Norm).Where(c => c.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        // 1) L'aggancio all'AIP, risolto sul caricamento IN VIGORE. È una scelta umana, e per questo viene
        //    prima di tutto il resto.
        var agganci = await _agganci.ResolveAsync(cercati, ct);

        // 2) I pezzi in archivio, per le fonti che ci scrivono.
        var inArchivio = await _pezzi.ListInForceByCallsignAsync(cercati, ct);

        // 3) Le colonne del catalogo, col gate AIRAC: durante il congelamento di una release danno la
        //    geometria in vigore a quel ciclo, non l'ultima disegnata.
        var grezzi = await EfAccDerivationRepository.SectorPolygonsRawByCallsignAsync(
            _db, cercati, ct, _release, _airac);
        var limiti = await EfAccDerivationRepository.SectorLimitsByCallsignAsync(_db, cercati, ct);

        // ⚠️ Quali di quelle shape sono un CERCHIO DI RIPIEGO. Serve a mettere in fila i due ripieghi:
        // un confine vero del catalogo (sectorfile o anagrafica) sta SOPRA l'ATZ automatica — è la decisione
        // del committente, «l'AIP solo se non ce l'hai» — mentre il cerchio ci sta SOTTO.
        var sintetiche = await SinteticheAsync(cercati, ct);

        foreach (var cs in cercati)
        {
            var scoperti = agganci.TryGetValue(cs, out var a)
                ? a.Missing.Select(m => m.Key).ToList()
                : new List<string>();

            // ⚠️ L'ORDINE È LA REGOLA, e sta solo qui. Ogni gradino che non dà niente fa scendere a quello
            // sotto: un aggancio scoperto, o una fonte muta, NON cancellano l'area che il settore mostrava.
            //
            //   1. l'aggancio scelto a MANO                                        — il gesto di una persona
            //   2. una shape VERA del catalogo (sectorfile o anagrafica)           — fonte primaria
            //   3. i pezzi in archivio (l'ATZ automatica dell'AIP)                 — ripiego
            //   4. il cerchio sintetico da 5 NM                                    — ripiego dell'ultimo minuto
            var vera = !sintetiche.Contains(cs);
            var forma =
                DaAggancio(cs, a, scoperti)
                ?? (vera ? DaCatalogo(cs, grezzi, limiti, scoperti) : null)
                ?? DaArchivio(cs, inArchivio, scoperti)
                ?? DaCatalogo(cs, grezzi, limiti, scoperti);

            if (forma is not null) esito[cs] = forma;
        }

        return esito;
    }

    /// <summary>I volumi scelti a mano: N pezzi, ognuno con la <b>sua</b> banda e il datum che il file dichiara.</summary>
    private static SectorShape? DaAggancio(string cs, SectorAirspaceBindingRow? a, IReadOnlyList<string> scoperti)
    {
        if (a is null || a.Volumes.Count == 0) return null;

        var pezzi = a.Volumes
            .Select(v => new ShapePart(
                v.PolygonJson, v.BaseFeet, v.TopFeet, v.BaseDatum, v.TopDatum, v.BaseRaw, v.TopRaw, v.NaturalKey))
            .ToList();

        return new SectorShape(cs, ShapeSource.Aip, pezzi, scoperti);
    }

    private static SectorShape? DaArchivio(
        string cs, IReadOnlyDictionary<string, (ShapeSource Source, IReadOnlyList<ShapePart> Parts)> archivio,
        IReadOnlyList<string> scoperti) =>
        archivio.TryGetValue(cs, out var trovato) && trovato.Parts.Count > 0
            ? new SectorShape(cs, trovato.Source, trovato.Parts, scoperti)
            : null;

    /// <summary>
    /// La forma di IVAO: <b>un</b> anello e le due quote sciolte del catalogo. ⚠️ Le quote qui non hanno un
    /// datum a schema — l'unità non è tracciata — e valgono quel che vale l'euristica di <c>AorFlBand</c>:
    /// si dichiarano <c>Amsl</c>, che è come sono sempre state lette.
    /// </summary>
    private static SectorShape? DaCatalogo(
        string cs, IReadOnlyDictionary<string, string> grezzi,
        IReadOnlyDictionary<string, SectorFlLimits> limiti, IReadOnlyList<string> scoperti)
    {
        if (!grezzi.TryGetValue(cs, out var raw) || string.IsNullOrWhiteSpace(raw)) return null;

        limiti.TryGetValue(cs, out var l);
        var pezzo = new ShapePart(
            raw, l?.Lower, l?.Upper, AirspaceDatum.Amsl, AirspaceDatum.Amsl,
            Testo(l?.Lower, "GND"), Testo(l?.Upper, "UNL"));

        return new SectorShape(cs, ShapeSource.Source, new[] { pezzo }, scoperti);
    }

    private static string Testo(int? quota, string seAssente) =>
        quota is { } q ? q.ToString(System.Globalization.CultureInfo.InvariantCulture) : seAssente;

    /// <summary>
    /// I callsign la cui shape in colonna è un <b>cerchio di ripiego</b> (<c>IsShapeSynthetic</c>). Solo le
    /// posizioni d'aeroporto ce l'hanno: un subcenter di ACC non ha cerchi, quindi non compare mai qui.
    /// </summary>
    private async Task<HashSet<string>> SinteticheAsync(IReadOnlyList<string> callsigns, CancellationToken ct)
    {
        var righe = await _db.AirportSectors.AsNoTracking()
            .Where(x => callsigns.Contains(x.ComposePosition) && x.IsShapeSynthetic)
            .Select(x => x.ComposePosition)
            .ToListAsync(ct);

        return new HashSet<string>(righe, StringComparer.OrdinalIgnoreCase);
    }

    private static string Norm(string? s) => (s ?? "").Trim().ToUpperInvariant();
}
