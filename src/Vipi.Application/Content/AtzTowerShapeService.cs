using System.Text.RegularExpressions;
using Vipi.Application.Abstractions;
using Vipi.Application.Airspace;
using Vipi.Application.Aor;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <param name="Applied">Torri che hanno ricevuto l'ATZ.</param>
/// <param name="MultiZone">
/// ICAO il cui ATZ è fatto di <b>più zone</b> — Guidonia due, Torino Aeritalia tre. ⚠️ <b>Non si saltano
/// più</b>: prima la colonna della shape ne teneva una sola e prenderne una vorrebbe dire disegnare mezza
/// zona di traffico, quindi si rinunciava; dall'aggancio (carta refactor 15) si prendono <b>tutte</b>. Restano
/// in elenco perché sono i campi che vale la pena guardare a schermo.
/// </param>
/// <param name="StillWithout">Torri bersaglio per cui il file non ha nessun ATZ: le prende il cerchio.</param>
public sealed record AtzTowerShapeResult(int Applied, IReadOnlyList<string> MultiZone, int StillWithout)
{
    public static AtzTowerShapeResult Empty { get; } = new(0, Array.Empty<string>(), 0);
}

/// <inheritdoc cref="AtzTowerShapeService"/>
public interface IAtzTowerShapeService
{
    /// <summary>Applica gli ATZ dell'AIP alle torri che non hanno un'area che si disegni.</summary>
    Task<AtzTowerShapeResult> ApplyAsync(CancellationToken ct = default);
}

/// <summary>
/// Dà alle <b>TWR senza area</b> la loro <b>ATZ</b> presa dal catalogo dell'AIP, al posto del cerchio da 5 NM.
///
/// <para><b>È un ripiego, non una scelta</b> (decisione 2 del committente, 29 agosto 2026: <i>fonte secondaria,
/// solo se non la trovi nel sectorfile</i>). Gira <b>dopo</b> <see cref="IGithubTowerShapeService"/> e
/// <b>prima</b> del cerchio: riempie solo quel che il sectorfile non ha riempito, e quando IVAO manderà una
/// shape vera l'upsert dell'anagrafica riprenderà il comando per intero — <c>ShapeSource</c> torna
/// <see cref="ShapeSource.Source"/> e questa riga smette di essere sua.</para>
///
/// <para>⚠️ <b>Non scrive più la colonna della shape: scrive dei PEZZI</b> (carta
/// <c>docs/refactor/15-shape-del-settore-una-porta-sola.md</c>), con la fonte <see cref="ShapeSource.Aip"/>.
/// Tre conseguenze, tutt'e tre volute: <b>è reversibile</b> — togliere i pezzi riporta la torre al suo
/// cerchio, mentre la shape scritta in colonna cancellava per sempre quel che c'era — <b>un ICAO con più
/// zone non si salta più</b> (Guidonia due, Torino Aeritalia tre: si prendono <b>intere</b>, perché i pezzi
/// sono una lista mentre la colonna teneva un anello) e la torre si porta dietro le <b>quote</b> dell'ATZ,
/// che il cerchio non ha mai avuto.</para>
///
/// <para>⚠️ <b>Resta un ripiego, e la precedenza lo dice.</b> Il risolutore mette una shape <i>vera</i>
/// del catalogo — sectorfile o anagrafica — <b>sopra</b> questi pezzi, e li tiene sopra al solo cerchio
/// sintetico. Un aggancio scelto <b>a mano</b> vince su tutto: quello è il gesto di una persona.</para>
///
/// <para>⚠️ E quando trova una torre con un'ATZ scritta in colonna dal giro vecchio, la <b>restituisce</b>
/// (<c>ClearAipShapeAsync</c>) prima di scrivere i pezzi: se restasse sotto, togliere i pezzi non riporterebbe
/// il cerchio ma la stessa ATZ di prima, e la reversibilità sarebbe finta.</para>
///
/// <para>⚠️ <b>L'ICAO si riconosce solo fra quelli che stiamo cercando.</b> Un nome come
/// <c>MATZ CERVIA-TWR</c> contiene il gruppo di quattro lettere <c>MATZ</c>, e una regola che prendesse
/// «la prima parola di quattro lettere» ci vedrebbe un codice d'aeroporto. Misurato sul file del 15 luglio
/// 2026: <b>74 ATZ su 91</b> portano l'ICAO nel nome, e i <b>17</b> che non ce l'hanno sono quasi tutti MATZ
/// di basi militari (Amendola, Aviano, Cameri, Decimomannu…), che si agganciano a mano.</para>
/// </summary>
public sealed class AtzTowerShapeService : IAtzTowerShapeService
{
    private readonly IAirportSectorRepository _repo;
    private readonly IAirspaceCatalog _catalogo;
    private readonly ISectorShapeParts _pezzi;
    private readonly ShapeFallbackScope _scope;

    private static readonly Regex QuattroLettere =
        new(@"\b[A-Z]{4}\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public AtzTowerShapeService(
        IAirportSectorRepository repo, IAirspaceCatalog catalogo, ISectorShapeParts pezzi,
        ShapeFallbackScope? scope = null)
    {
        _repo = repo;
        _catalogo = catalogo;
        _pezzi = pezzi;
        _scope = scope ?? new ShapeFallbackScope();
    }

    public async Task<AtzTowerShapeResult> ApplyAsync(CancellationToken ct = default)
    {
        // Bersaglio: torri della divisione senza un'area che si disegni, il cerchio di ripiego, o un'ATZ già
        // messa da noi — quest'ultima perché un file nuovo può portare un confine diverso, e una shape che
        // non si aggiorna mai è una shape che invecchia in silenzio.
        var bersagli = (await _repo.ListTwrShapesAsync(ct))
            .Where(t => _scope.IsDomestic(t.AirportIcao))
            .Where(t => t.ShapeSource == ShapeSource.Aip
                        || t.IsShapeSynthetic
                        || AorPolygonProjector.Project(t.RawPolygon) is null)
            .ToList();
        if (bersagli.Count == 0) return AtzTowerShapeResult.Empty;

        var atz = await _catalogo.ListVolumesAsync(
            new AirspaceVolumeQuery(Families: [AirspaceFamily.Atz], Take: 2000), ct);
        if (atz.Count == 0) return AtzTowerShapeResult.Empty;

        var cercati = bersagli.Select(t => t.AirportIcao.ToUpperInvariant()).ToHashSet(StringComparer.Ordinal);
        var perIcao = PerIcao(atz, cercati);

        var applicate = 0;
        var piuZone = new List<string>();
        var senza = 0;

        foreach (var t in bersagli)
        {
            if (!perIcao.TryGetValue(t.AirportIcao.ToUpperInvariant(), out var volumi)) { senza++; continue; }

            // Gli anelli degeneri si scartano uno per uno: uno rotto non porta via le altre zone del campo.
            var buoni = volumi.Where(v => AorPolygonProjector.Project(v.PolygonJson) is not null).ToList();
            if (buoni.Count == 0) { senza++; continue; }

            if (buoni.Count > 1 && !piuZone.Contains(t.AirportIcao)) piuZone.Add(t.AirportIcao);

            // ⚠️ Prima si RESTITUISCE la colonna scritta dal giro vecchio, poi si scrivono i pezzi: se la
            // vecchia ATZ restasse sotto, toglierli non riporterebbe il cerchio ma la stessa forma di prima.
            if (t.ShapeSource == ShapeSource.Aip) await _repo.ClearAipShapeAsync(t.SectorId, ct);

            await _pezzi.ReplacePartsAsync(SourceCatalog.AirportPosition, t.SectorId, t.ComposePosition,
                ShapeSource.Aip, ShapePartState.InForce,
                buoni.Select(v => new ShapePart(
                    v.PolygonJson, v.BaseFeet, v.TopFeet, v.BaseDatum, v.TopDatum, v.BaseRaw, v.TopRaw,
                    v.NaturalKey)).ToList(),
                ct: ct);
            applicate++;
        }

        return new AtzTowerShapeResult(applicate, piuZone, senza);
    }

    /// <summary>
    /// ICAO → gli ATZ che lo nominano. ⚠️ Si guardano <b>solo</b> i codici che stiamo cercando: il gruppo di
    /// quattro lettere che compare in un nome può benissimo essere <c>MATZ</c>.
    /// </summary>
    private static Dictionary<string, List<AirspaceVolumeRow>> PerIcao(
        IReadOnlyList<AirspaceVolumeRow> atz, IReadOnlySet<string> cercati)
    {
        var per = new Dictionary<string, List<AirspaceVolumeRow>>(StringComparer.Ordinal);
        foreach (var v in atz)
        {
            foreach (Match m in QuattroLettere.Matches(v.Name.ToUpperInvariant()))
            {
                if (!cercati.Contains(m.Value)) continue;
                if (!per.TryGetValue(m.Value, out var lista)) per[m.Value] = lista = new List<AirspaceVolumeRow>();
                if (!lista.Contains(v)) lista.Add(v);
            }
        }
        return per;
    }
}
