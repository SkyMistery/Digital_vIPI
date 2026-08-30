using System.Text.RegularExpressions;
using Vipi.Application.Abstractions;
using Vipi.Application.Airspace;
using Vipi.Application.Aor;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <param name="Applied">Torri che hanno ricevuto l'ATZ.</param>
/// <param name="Ambiguous">ICAO con <b>più di un</b> ATZ nel file: saltati apposta, vedi sotto.</param>
/// <param name="StillWithout">Torri bersaglio per cui il file non ha nessun ATZ: le prende il cerchio.</param>
public sealed record AtzTowerShapeResult(int Applied, IReadOnlyList<string> Ambiguous, int StillWithout)
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
/// <para>⚠️ <b>Un ICAO con più di un ATZ si SALTA, e lo si dice.</b> La colonna della shape tiene <b>un
/// anello</b>: di Guidonia (<c>LIRG</c>, due zone) e di Torino Aeritalia (<c>LIMA</c>, tre settori) prenderne
/// uno vorrebbe dire disegnare una torre con metà della sua zona di traffico, senza un errore da nessuna
/// parte. È lo stesso motivo per cui l'aggancio a mano non passa da questa colonna (carta §6-bis) — e quei
/// due campi, se servono, si agganciano proprio così.</para>
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
    private readonly ShapeFallbackScope _scope;

    private static readonly Regex QuattroLettere =
        new(@"\b[A-Z]{4}\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public AtzTowerShapeService(
        IAirportSectorRepository repo, IAirspaceCatalog catalogo, ShapeFallbackScope? scope = null)
    {
        _repo = repo;
        _catalogo = catalogo;
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
        var ambigui = new List<string>();
        var senza = 0;

        foreach (var t in bersagli)
        {
            if (!perIcao.TryGetValue(t.AirportIcao.ToUpperInvariant(), out var volumi)) { senza++; continue; }

            if (volumi.Count > 1)
            {
                // Più di un ATZ: si salta e si dice quale. Mezza zona di traffico è peggio di nessuna.
                if (!ambigui.Contains(t.AirportIcao)) ambigui.Add(t.AirportIcao);
                senza++;
                continue;
            }

            var json = volumi[0].PolygonJson;
            if (AorPolygonProjector.Project(json) is null) { senza++; continue; }   // anello degenere: non si disegna

            await _repo.SetAipShapeAsync(t.SectorId, json, ct);
            applicate++;
        }

        return new AtzTowerShapeResult(applicate, ambigui, senza);
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
