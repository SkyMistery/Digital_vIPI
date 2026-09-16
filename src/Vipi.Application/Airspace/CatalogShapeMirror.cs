using System.Globalization;
using Vipi.Domain;

namespace Vipi.Application.Airspace;

/// <summary>
/// Una riga di catalogo (<c>AccSector</c> o <c>AirportSector</c>) com'è in colonna: la forma, il gate AIRAC e
/// le quote. Read-model: niente entità qui dentro.
/// </summary>
public sealed record CatalogShapeRow(
    string? Polygon, string? InForce, string? Cycle, ShapeSource Source, bool ForcePublished,
    bool IsSynthetic, int? LowerLimit, int? UpperLimit);

/// <summary>
/// I pezzi che corrispondono a una riga di catalogo: <b>una</b> fonte, l'insieme in vigore e, se il sectorfile
/// ha già disegnato il confine del ciclo prossimo, quello in attesa.
/// </summary>
/// <param name="Pending">null = nessun insieme in attesa.</param>
/// <param name="PendingCycle">Il ciclo da cui <paramref name="Pending"/> entra in vigore.</param>
public sealed record CatalogShapeParts(
    ShapeSource Source, ShapePart InForce, ShapePart? Pending, string? PendingCycle, bool ForcePublished);

/// <summary>
/// <b>Il ponte di S11, fase A</b> (carta <c>docs/refactor/15-shape-del-settore-una-porta-sola.md</c> §4-bis):
/// quali pezzi di <c>SectorShapeParts</c> dicono la stessa cosa delle colonne di una riga di catalogo.
///
/// <para>⚠️ <b>Le tre fonti di catalogo sono ESCLUSIVE.</b> Una riga porta una forma sola — l'anagrafica
/// sovrascrive il sectorfile, il sectorfile scrive sopra il cerchio — e questa funzione ne dà <b>una</b> fonte.
/// Chi la applica toglie i pezzi delle altre due fonti di catalogo e <b>non tocca mai</b> quelli dell'AIP, che
/// nessuna colonna rappresenta.</para>
///
/// <para>⚠️ <b>Equivalenza col risolutore di oggi</b>, ed è il motivo di ogni riga qui sotto: le quote si
/// dichiarano <c>Amsl</c> col testo della cifra (come <c>DaCatalogo</c>), una forma in colonna con
/// <c>ShapeSource.Aip</c> — il ramo vecchio delle ATZ, zero righe in produzione — si legge come
/// <c>Source</c>, e l'insieme in attesa esiste solo dove <see cref="Vipi.Application.Content.ShapeAiracGate"/>
/// potrebbe differire: sectorfile, con un ciclo e una forma in vigore da mostrare al posto suo.</para>
/// </summary>
public static class CatalogShapeMirror
{
    /// <summary>Le fonti che una riga di catalogo può rappresentare: fra loro si escludono.</summary>
    public static readonly IReadOnlyList<ShapeSource> CatalogSources =
        new[] { ShapeSource.Source, ShapeSource.Sectorfile, ShapeSource.Synthetic };

    /// <summary>I pezzi della riga, o null se in colonna non c'è una forma (e allora non ce ne sono nemmeno in archivio).</summary>
    public static CatalogShapeParts? Desired(CatalogShapeRow row)
    {
        if (string.IsNullOrWhiteSpace(row.Polygon)) return null;

        var fonte = row.IsSynthetic ? ShapeSource.Synthetic
            : row.Source == ShapeSource.Sectorfile ? ShapeSource.Sectorfile
            : ShapeSource.Source;

        var differita = fonte == ShapeSource.Sectorfile
                        && !string.IsNullOrWhiteSpace(row.Cycle)
                        && !string.IsNullOrWhiteSpace(row.InForce);

        return differita
            ? new CatalogShapeParts(fonte, Pezzo(row.InForce!, row), Pezzo(row.Polygon!, row), row.Cycle, row.ForcePublished)
            : new CatalogShapeParts(fonte, Pezzo(row.Polygon!, row), null, null, false);
    }

    /// <summary>La quota come la mostrava il risolutore: la cifra, o <c>GND</c>/<c>UNL</c> se assente.</summary>
    public static ShapePart Pezzo(string polygon, CatalogShapeRow row) => new(
        polygon, row.LowerLimit, row.UpperLimit, AirspaceDatum.Amsl, AirspaceDatum.Amsl,
        Testo(row.LowerLimit, "GND"), Testo(row.UpperLimit, "UNL"));

    private static string Testo(int? quota, string seAssente) =>
        quota is { } q ? q.ToString(CultureInfo.InvariantCulture) : seAssente;
}
